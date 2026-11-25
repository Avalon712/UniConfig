#region

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 记录自定义枚举的（值, 名称）快照。改名、删除，或同名但顺序/数值变化时，
    /// 让用户把旧数据手动对应到新成员，再写回单元格。
    /// </summary>
    internal static class EnumRepair
    {
        private static bool _scheduled;
        private static bool _deferred;
        private static bool _open;

        /// <summary>域重载或打开编辑器后检查一次。点过「稍后处理」则本轮不再自动弹出。</summary>
        public static void SchedulePrompt()
        {
            if (_deferred || _scheduled || _open)
                return;
            _scheduled = true;
            EditorApplication.delayCall += RunScheduled;
        }

        /// <summary>导出前必须处理完。用户选择稍后则中断导出。</summary>
        public static void EnsureResolvedForExport()
        {
            PromptResult result = Prompt(scanAll: true);
            if (result == PromptResult.Clear || result == PromptResult.Applied)
                return;
            throw new EnumRepairDeferredException();
        }

        internal static void NotifyDeferred()
        {
            _deferred = true;
        }

        internal static void Apply(List<EnumRepairCase> cases)
        {
            if (cases == null || cases.Count == 0)
                return;
            if (!CanApply(cases))
                throw new InvalidOperationException(UniConfigLoc.T("enum_repair.no_members"));

            var maps = new Dictionary<string, MemberMap>(StringComparer.Ordinal);
            var typeNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnumRepairCase repairCase in cases)
            {
                maps[repairCase.TypeFullName] = MemberMap.Build(repairCase);
                typeNames.Add(repairCase.TypeFullName);
            }

            int cellCount = 0;
            var dirty = new List<ConfigTable>();
            var edits = new List<CellEdit>();
            ExternalConfigEditor.BeginSaveBatch();
            try
            {
                ForEachEnumCell(typeNames, UniConfigLoc.T("enum_repair.applying"),
                    (table, item, fieldName, typeName) =>
                    {
                        if (!maps.TryGetValue(typeName, out MemberMap map))
                            return;
                        string raw = item.GetValue(fieldName);
                        string next = RemapRaw(raw, map);
                        if (string.Equals(next, raw, StringComparison.Ordinal))
                            return;
                        edits.Add(new CellEdit { Item = item, Field = fieldName, Raw = raw });
                        item.SetValue(fieldName, next);
                        if (!dirty.Contains(table))
                            dirty.Add(table);
                        cellCount++;
                    });

                for (int i = 0; i < dirty.Count; i++)
                    EditorConfigMgr.SaveTable(dirty[i]);

                UniConfigEditorSettings settings = UniConfigEditorSettings.instance;
                settings.enumMemberSnapshots ??= new List<EnumTypeSnapshot>();
                for (int i = 0; i < cases.Count; i++)
                    StoreSnapshot(settings, cases[i].TypeFullName, cases[i].Current);
                settings.SaveSettings();
            }
            catch
            {
                for (int i = 0; i < edits.Count; i++)
                    edits[i].Item.SetValue(edits[i].Field, edits[i].Raw);
                for (int i = 0; i < dirty.Count; i++)
                {
                    try
                    {
                        EditorConfigMgr.SaveTable(dirty[i]);
                    }
                    catch (Exception rollbackEx)
                    {
                        Debug.LogException(rollbackEx);
                    }
                }

                throw;
            }
            finally
            {
                try
                {
                    ExternalConfigEditor.EndSaveBatch();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                EditorUtility.ClearProgressBar();
            }

            foreach (ConfigEditorWindow window in Resources.FindObjectsOfTypeAll<ConfigEditorWindow>())
            {
                if (window != null)
                    window.Repaint();
            }

            Debug.Log("[UniConfig] " + UniConfigLoc.F("enum_repair.done", cases.Count, cellCount));
        }

        internal static bool CanApply(List<EnumRepairCase> cases)
        {
            if (cases == null || cases.Count == 0)
                return false;
            for (int i = 0; i < cases.Count; i++)
            {
                EnumRepairCase repairCase = cases[i];
                if (repairCase?.Current == null || repairCase.Current.Count == 0 || repairCase.Rows == null)
                    return false;
                for (int r = 0; r < repairCase.Rows.Count; r++)
                {
                    int index = repairCase.Rows[r].NewIndex;
                    if (index < 0 || index >= repairCase.Current.Count)
                        return false;
                }
            }

            return true;
        }

        internal static int SuggestByName(EnumRepairCase repairCase, EnumRepairRow row)
        {
            if (repairCase?.Current == null || repairCase.Current.Count == 0)
                return -1;
            return Suggest(repairCase.Current, repairCase.OldNames, row.OldName, row.HasValue, row.Value);
        }

        internal static int SuggestByOrder(EnumRepairCase repairCase, EnumRepairRow row)
        {
            if (repairCase?.Current == null || repairCase.Current.Count == 0)
                return -1;
            if (row.OldOrdinal >= 0 && row.OldOrdinal < repairCase.Current.Count)
                return row.OldOrdinal;
            if (row.NewIndex >= 0 && row.NewIndex < repairCase.Current.Count)
                return row.NewIndex;
            return 0;
        }

        private static void RunScheduled()
        {
            _scheduled = false;
            if (_deferred || _open)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                SchedulePrompt();
                return;
            }

            try
            {
                Prompt(scanAll: false);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static PromptResult Prompt(bool scanAll)
        {
            if (_open)
                return PromptResult.Busy;
            List<EnumRepairCase> cases = Collect(scanAll);
            if (cases.Count == 0)
                return PromptResult.Clear;

            _open = true;
            try
            {
                bool applied = false;
                EnumRepairWindow window = ScriptableObject.CreateInstance<EnumRepairWindow>();
                window.Setup(cases);
                window.OnFinished = ok => applied = ok;
                window.ShowModalUtility();
                return applied ? PromptResult.Applied : PromptResult.Deferred;
            }
            finally
            {
                _open = false;
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<EnumRepairCase> Collect(bool scanAll)
        {
            UniConfigEditorSettings settings = UniConfigEditorSettings.instance;
            settings.customEnumTypeFullNames ??= new List<string>();
            settings.enumMemberSnapshots ??= new List<EnumTypeSnapshot>();

            var keep = new HashSet<string>(StringComparer.Ordinal);
            var pending = new List<PendingType>();
            for (int i = 0; i < settings.customEnumTypeFullNames.Count; i++)
            {
                string rawName = settings.customEnumTypeFullNames[i];
                if (string.IsNullOrWhiteSpace(rawName))
                    continue;
                string fullName = rawName.Trim();
                if (!keep.Add(fullName))
                    continue;
                if (!EnumTypeUtil.TryResolve(fullName, out Type enumType, out _))
                    continue;

                List<EnumMemberSnapshot> current = Capture(enumType);
                EnumTypeSnapshot stored = FindSnapshot(settings.enumMemberSnapshots, fullName);
                List<EnumMemberSnapshot> old = CopyMembers(stored);
                bool missing = stored == null;
                bool same = !missing && SameMembers(old, current);
                bool prefix = !missing && !same && IsPrefixAppend(old, current);
                pending.Add(new PendingType
                {
                    FullName = fullName,
                    Flags = EnumTypeUtil.IsFlags(fullName),
                    Current = current,
                    Old = old,
                    Missing = missing,
                    Structural = !missing && !same && !prefix,
                    Prefix = prefix
                });
            }

            bool removedStale = settings.enumMemberSnapshots.RemoveAll(snapshot =>
                snapshot == null ||
                string.IsNullOrEmpty(snapshot.typeFullName) ||
                !keep.Contains(snapshot.typeFullName)) > 0;

            bool needScan = scanAll;
            if (!needScan)
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    if (pending[i].Missing || pending[i].Structural)
                    {
                        needScan = true;
                        break;
                    }
                }
            }

            if (needScan)
            {
                try
                {
                    ScanOrphans(pending, scanAll);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            var cases = new List<EnumRepairCase>();
            bool dirty = removedStale;
            for (int i = 0; i < pending.Count; i++)
            {
                PendingType type = pending[i];
                bool hasOrphans = type.Orphans != null && type.Orphans.Count > 0;
                bool hasOld = type.Old != null && type.Old.Count > 0;
                if ((type.Structural && hasOld) || hasOrphans)
                {
                    cases.Add(BuildCase(type, type.Structural && hasOld));
                    continue;
                }

                if (type.Missing || type.Prefix || type.Structural)
                {
                    StoreSnapshot(settings, type.FullName, type.Current);
                    dirty = true;
                }
            }

            if (dirty)
                settings.SaveSettings();
            return cases;
        }

        private static void ScanOrphans(List<PendingType> pending, bool scanAll)
        {
            var map = new Dictionary<string, PendingType>(StringComparer.Ordinal);
            for (int i = 0; i < pending.Count; i++)
            {
                PendingType type = pending[i];
                if (!(scanAll || type.Missing || type.Structural))
                    continue;
                type.EnsureKnown();
                type.OrphanSet = new HashSet<string>(StringComparer.Ordinal);
                map[type.FullName] = type;
            }

            if (map.Count == 0)
                return;

            ForEachEnumCell(new HashSet<string>(map.Keys, StringComparer.Ordinal),
                UniConfigLoc.T("enum_repair.scanning"),
                (_, item, fieldName, typeName) =>
                {
                    if (!map.TryGetValue(typeName, out PendingType type))
                        return;
                    string raw = item.GetValue(fieldName);
                    if (string.IsNullOrWhiteSpace(raw))
                        return;

                    if (type.Flags && raw.IndexOf(',') >= 0)
                    {
                        string[] parts = raw.Split(',');
                        for (int p = 0; p < parts.Length; p++)
                        {
                            string part = parts[p].Trim();
                            if (part.Length == 0 || type.IsKnownToken(part))
                                continue;
                            type.OrphanSet.Add(part);
                        }

                        return;
                    }

                    string token = raw.Trim();
                    if (!type.IsKnownToken(token))
                        type.OrphanSet.Add(token);
                });

            foreach (PendingType type in map.Values)
            {
                if (type.OrphanSet == null || type.OrphanSet.Count == 0)
                    continue;
                type.Orphans = new List<string>(type.OrphanSet);
                type.Orphans.Sort(StringComparer.Ordinal);
            }
        }

        private static EnumRepairCase BuildCase(PendingType type, bool includeOld)
        {
            var repairCase = new EnumRepairCase
            {
                TypeFullName = type.FullName,
                Flags = type.Flags,
                Current = type.Current,
                PopupLabels = BuildLabels(type.Current),
                Rows = new List<EnumRepairRow>(),
                OldNames = new HashSet<string>(StringComparer.Ordinal)
            };
            if (type.Old != null)
            {
                for (int i = 0; i < type.Old.Count; i++)
                {
                    if (!string.IsNullOrEmpty(type.Old[i].name))
                        repairCase.OldNames.Add(type.Old[i].name);
                }
            }

            if (includeOld && type.Old != null)
            {
                for (int i = 0; i < type.Old.Count; i++)
                {
                    EnumMemberSnapshot member = type.Old[i];
                    var row = new EnumRepairRow
                    {
                        HasValue = true,
                        Value = member.value,
                        OldName = member.name,
                        OldOrdinal = i,
                        NewIndex = Suggest(type.Current, repairCase.OldNames, member.name, true, member.value)
                    };
                    row.Kind = Classify(type.Current, row);
                    repairCase.Rows.Add(row);
                }
            }

            if (type.Orphans != null)
            {
                for (int i = 0; i < type.Orphans.Count; i++)
                {
                    string token = type.Orphans[i];
                    repairCase.Rows.Add(new EnumRepairRow
                    {
                        HasValue = false,
                        OldName = token,
                        OldOrdinal = -1,
                        NewIndex = Suggest(type.Current, repairCase.OldNames, token, false, 0),
                        Kind = RepairRowKind.Unparsed
                    });
                }
            }

            return repairCase;
        }

        private static void ForEachEnumCell(
            HashSet<string> typeNames,
            string progressTitle,
            Action<ConfigTable, ConfigItem, string, string> visit)
        {
            if (typeNames == null || typeNames.Count == 0)
                return;

            EditorConfigMgr.EnsureLoaded();
            var targets = new List<ConfigTable>();
            foreach (ConfigModule module in EditorConfigMgr.Modules.Values)
            {
                if (module?.tables == null)
                    continue;
                for (int i = 0; i < module.tables.Count; i++)
                {
                    ConfigTable table = module.tables[i];
                    if (table == null)
                        continue;
                    if (table.misc || TableUses(table, typeNames))
                        targets.Add(table);
                }
            }

            for (int t = 0; t < targets.Count; t++)
            {
                ConfigTable table = targets[t];
                if (!string.IsNullOrEmpty(progressTitle))
                {
                    float progress = targets.Count <= 1 ? 1f : (float)t / targets.Count;
                    EditorUtility.DisplayProgressBar(progressTitle, table.moduleName + "/" + table.tableName, progress);
                }

                try
                {
                    EditorConfigMgr.EnsureTableDataLoaded(table);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    continue;
                }

                if (table.items == null)
                    continue;

                if (table.misc)
                {
                    for (int i = 0; i < table.items.Count; i++)
                    {
                        ConfigItem item = table.items[i];
                        if (item?.fieldData == null)
                            continue;
                        if (!MiscTableSchema.TryGetEnumTypeName(item.GetValue(MiscTableSchema.TypeField), out string enumName))
                            continue;
                        if (!typeNames.Contains(enumName))
                            continue;
                        visit(table, item, MiscTableSchema.ValueField, enumName);
                    }

                    continue;
                }

                if (table.fields == null)
                    continue;
                for (int i = 0; i < table.items.Count; i++)
                {
                    ConfigItem item = table.items[i];
                    if (item?.fieldData == null)
                        continue;
                    for (int f = 0; f < table.fields.Count; f++)
                    {
                        ConfigField field = table.fields[f];
                        if (field == null || !field.IsEnum || !typeNames.Contains(field.enumTypeFullName))
                            continue;
                        visit(table, item, field.name, field.enumTypeFullName);
                    }
                }
            }
        }

        private static bool TableUses(ConfigTable table, HashSet<string> typeNames)
        {
            if (table?.fields == null)
                return false;
            for (int i = 0; i < table.fields.Count; i++)
            {
                ConfigField field = table.fields[i];
                if (field != null && field.IsEnum && typeNames.Contains(field.enumTypeFullName))
                    return true;
            }

            return false;
        }

        private static string RemapRaw(string raw, MemberMap map)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            string text = raw.Trim();
            if (map.Case.Flags && text.IndexOf(',') >= 0)
                return RemapList(raw, text, map);

            if (TryMapToken(text, map, out string mapped))
                return string.Equals(mapped, text, StringComparison.Ordinal) ? raw : mapped;

            if (map.Case.Flags &&
                TryParseInteger(text, out long bits) &&
                TryDecompose(bits, map, out string combined))
                return combined;

            return raw;
        }

        private static string RemapList(string raw, string text, MemberMap map)
        {
            string[] parts = text.Split(',');
            bool changed = false;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0)
                    continue;
                if (!TryMapToken(part, map, out string mapped))
                    continue;
                if (string.Equals(mapped, part, StringComparison.Ordinal))
                    continue;
                parts[i] = mapped;
                changed = true;
            }

            if (!changed)
                return raw;
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();
            return string.Join(", ", parts);
        }

        private static bool TryMapToken(string token, MemberMap map, out string mapped)
        {
            if (map.Names.TryGetValue(token, out mapped))
                return true;
            if (map.NamesIgnore.TryGetValue(token, out mapped))
                return true;
            if (TryParseInteger(token, out long number) && map.Values.TryGetValue(number, out mapped))
                return true;
            mapped = null;
            return false;
        }

        private static bool TryDecompose(long bits, MemberMap map, out string text)
        {
            text = null;
            if (bits <= 0)
                return false;

            var candidates = new List<EnumRepairRow>();
            for (int i = 0; i < map.Case.Rows.Count; i++)
            {
                EnumRepairRow row = map.Case.Rows[i];
                if (row.HasValue && row.Value > 0)
                    candidates.Add(row);
            }

            candidates.Sort((a, b) => b.Value.CompareTo(a.Value));
            long remain = bits;
            var picked = new List<(long value, string name)>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                EnumRepairRow row = candidates[i];
                long value = row.Value;
                if ((remain & value) != value)
                    continue;
                remain &= ~value;
                int newIndex = row.NewIndex;
                if (newIndex < 0 || newIndex >= map.Case.Current.Count)
                    return false;
                string name = map.Case.Current[newIndex].name;
                if (used.Add(name))
                    picked.Add((value, name));
                if (remain == 0)
                    break;
            }

            if (remain != 0 || picked.Count == 0)
                return false;
            picked.Sort((a, b) => a.value.CompareTo(b.value));
            var names = new string[picked.Count];
            for (int i = 0; i < picked.Count; i++)
                names[i] = picked[i].name;
            text = string.Join(", ", names);
            return true;
        }

        /// <summary>
        /// GetNames 按底层数值排序，不是源码声明顺序。
        /// 只改声明顺序、显式数值不变时快照相同；隐式枚举调换成员会同时改变顺序和数值。
        /// </summary>
        private static List<EnumMemberSnapshot> Capture(Type enumType)
        {
            string[] names = Enum.GetNames(enumType);
            var list = new List<EnumMemberSnapshot>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                list.Add(new EnumMemberSnapshot
                {
                    name = names[i],
                    value = ToSnapshotValue(enumType, names[i])
                });
            }

            return list;
        }

        private static long ToSnapshotValue(Type enumType, string name)
        {
            object value = Enum.Parse(enumType, name);
            Type underlying = Enum.GetUnderlyingType(enumType);
            if (underlying == typeof(ulong))
                return unchecked((long)Convert.ToUInt64(value, CultureInfo.InvariantCulture));
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static List<EnumMemberSnapshot> CopyMembers(EnumTypeSnapshot stored)
        {
            if (stored?.members == null)
                return stored == null ? null : new List<EnumMemberSnapshot>();
            var copy = new List<EnumMemberSnapshot>(stored.members.Count);
            for (int i = 0; i < stored.members.Count; i++)
            {
                EnumMemberSnapshot member = stored.members[i];
                if (member == null || string.IsNullOrEmpty(member.name))
                    continue;
                copy.Add(new EnumMemberSnapshot { value = member.value, name = member.name });
            }

            return copy;
        }

        private static bool SameMembers(List<EnumMemberSnapshot> oldMembers, List<EnumMemberSnapshot> current)
        {
            if (oldMembers == null || current == null || oldMembers.Count != current.Count)
                return false;
            for (int i = 0; i < oldMembers.Count; i++)
            {
                if (oldMembers[i].value != current[i].value ||
                    !string.Equals(oldMembers[i].name, current[i].name, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        /// <summary>旧成员是新成员的前缀，只在末尾追加了成员。</summary>
        private static bool IsPrefixAppend(List<EnumMemberSnapshot> oldMembers, List<EnumMemberSnapshot> current)
        {
            if (oldMembers == null || current == null || oldMembers.Count == 0 || oldMembers.Count >= current.Count)
                return false;
            for (int i = 0; i < oldMembers.Count; i++)
            {
                if (oldMembers[i].value != current[i].value ||
                    !string.Equals(oldMembers[i].name, current[i].name, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static int Suggest(
            List<EnumMemberSnapshot> current,
            HashSet<string> oldNames,
            string oldName,
            bool hasValue,
            long value)
        {
            if (current == null || current.Count == 0)
                return -1;
            int byName = IndexOfName(current, oldName);
            if (byName >= 0)
                return byName;
            if (hasValue)
            {
                for (int i = 0; i < current.Count; i++)
                {
                    if (current[i].value != value)
                        continue;
                    // 数值被另一个仍存在的旧成员占用时，不当作改名。
                    if (oldNames != null &&
                        oldNames.Contains(current[i].name) &&
                        !string.Equals(current[i].name, oldName, StringComparison.Ordinal))
                        continue;
                    return i;
                }
            }

            return 0;
        }

        private static RepairRowKind Classify(List<EnumMemberSnapshot> current, EnumRepairRow row)
        {
            int index = IndexOfName(current, row.OldName);
            if (index < 0)
                return RepairRowKind.Missing;
            if (current[index].value != row.Value || index != row.OldOrdinal)
                return RepairRowKind.ValueChanged;
            return RepairRowKind.Same;
        }

        private static int IndexOfName(List<EnumMemberSnapshot> members, string name)
        {
            if (members == null || string.IsNullOrEmpty(name))
                return -1;
            for (int i = 0; i < members.Count; i++)
            {
                if (string.Equals(members[i].name, name, StringComparison.Ordinal))
                    return i;
            }

            for (int i = 0; i < members.Count; i++)
            {
                if (string.Equals(members[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static string[] BuildLabels(List<EnumMemberSnapshot> current)
        {
            if (current == null || current.Count == 0)
                return Array.Empty<string>();
            var labels = new string[current.Count];
            for (int i = 0; i < current.Count; i++)
            {
                labels[i] = current[i].value.ToString(CultureInfo.InvariantCulture) + "  " + current[i].name;
            }

            return labels;
        }

        private static bool TryParseInteger(string text, out long value)
        {
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool CanDecompose(long bits, HashSet<long> values)
        {
            if (bits <= 0 || values == null || values.Count == 0)
                return false;
            var ordered = new List<long>();
            foreach (long value in values)
            {
                if (value > 0)
                    ordered.Add(value);
            }

            ordered.Sort((a, b) => b.CompareTo(a));
            long remain = bits;
            for (int i = 0; i < ordered.Count; i++)
            {
                long value = ordered[i];
                if ((remain & value) != value)
                    continue;
                remain &= ~value;
                if (remain == 0)
                    return true;
            }

            return false;
        }

        private static EnumTypeSnapshot FindSnapshot(List<EnumTypeSnapshot> snapshots, string fullName)
        {
            if (snapshots == null)
                return null;
            for (int i = 0; i < snapshots.Count; i++)
            {
                EnumTypeSnapshot snapshot = snapshots[i];
                if (snapshot != null && string.Equals(snapshot.typeFullName, fullName, StringComparison.Ordinal))
                    return snapshot;
            }

            return null;
        }

        private static void StoreSnapshot(UniConfigEditorSettings settings, string fullName, List<EnumMemberSnapshot> current)
        {
            settings.enumMemberSnapshots ??= new List<EnumTypeSnapshot>();
            EnumTypeSnapshot snapshot = FindSnapshot(settings.enumMemberSnapshots, fullName);
            if (snapshot == null)
            {
                snapshot = new EnumTypeSnapshot { typeFullName = fullName };
                settings.enumMemberSnapshots.Add(snapshot);
            }

            snapshot.typeFullName = fullName;
            snapshot.members = new List<EnumMemberSnapshot>(current?.Count ?? 0);
            if (current == null)
                return;
            for (int i = 0; i < current.Count; i++)
            {
                snapshot.members.Add(new EnumMemberSnapshot
                {
                    value = current[i].value,
                    name = current[i].name
                });
            }
        }

        private enum PromptResult
        {
            Clear,
            Applied,
            Deferred,
            Busy
        }

        private sealed class PendingType
        {
            public string FullName;
            public bool Flags;
            public List<EnumMemberSnapshot> Current;
            public List<EnumMemberSnapshot> Old;
            public bool Missing;
            public bool Structural;
            public bool Prefix;
            public HashSet<string> OrphanSet;
            public List<string> Orphans;
            public HashSet<string> KnownNames;
            public HashSet<long> KnownValues;

            public void EnsureKnown()
            {
                if (KnownNames != null)
                    return;
                KnownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                KnownValues = new HashSet<long>();
                AddKnown(Current);
                AddKnown(Old);
            }

            public bool IsKnownToken(string token)
            {
                EnsureKnown();
                if (KnownNames.Contains(token))
                    return true;
                if (!TryParseInteger(token, out long number))
                    return false;
                if (KnownValues.Contains(number))
                    return true;
                return Flags && CanDecompose(number, KnownValues);
            }

            private void AddKnown(List<EnumMemberSnapshot> members)
            {
                if (members == null)
                    return;
                for (int i = 0; i < members.Count; i++)
                {
                    EnumMemberSnapshot member = members[i];
                    if (member == null || string.IsNullOrEmpty(member.name))
                        continue;
                    KnownNames.Add(member.name);
                    KnownValues.Add(member.value);
                }
            }
        }

        private struct CellEdit
        {
            public ConfigItem Item;
            public string Field;
            public string Raw;
        }

        private sealed class MemberMap
        {
            public EnumRepairCase Case;
            public Dictionary<string, string> Names;
            public Dictionary<string, string> NamesIgnore;
            public Dictionary<long, string> Values;

            public static MemberMap Build(EnumRepairCase repairCase)
            {
                var map = new MemberMap
                {
                    Case = repairCase,
                    Names = new Dictionary<string, string>(StringComparer.Ordinal),
                    NamesIgnore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    Values = new Dictionary<long, string>()
                };
                for (int i = 0; i < repairCase.Rows.Count; i++)
                {
                    EnumRepairRow row = repairCase.Rows[i];
                    string target = repairCase.Current[row.NewIndex].name;
                    if (!map.Names.ContainsKey(row.OldName))
                        map.Names.Add(row.OldName, target);
                    if (!map.NamesIgnore.ContainsKey(row.OldName))
                        map.NamesIgnore.Add(row.OldName, target);
                    if (row.HasValue && !map.Values.ContainsKey(row.Value))
                        map.Values.Add(row.Value, target);
                }

                return map;
            }
        }
    }

    internal enum RepairRowKind
    {
        Same,
        ValueChanged,
        Missing,
        Unparsed
    }

    internal sealed class EnumRepairCase
    {
        public string TypeFullName;
        public bool Flags;
        public List<EnumMemberSnapshot> Current;
        public string[] PopupLabels;
        public List<EnumRepairRow> Rows;
        public HashSet<string> OldNames;
    }

    internal sealed class EnumRepairRow
    {
        public bool HasValue;
        public long Value;
        public string OldName;
        public int OldOrdinal;
        public int NewIndex;
        public RepairRowKind Kind;
    }

    internal sealed class EnumRepairDeferredException : Exception
    {
        public EnumRepairDeferredException()
            : base(UniConfigLoc.T("enum_repair.export_blocked"))
        {
        }
    }
}
