#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 编辑器模式下的外部配置入口：从 <c>Config/</c> 下已保存的表数据构建实例，
    /// 不依赖导出的 <c>configs.bytes</c>。读取 API 对齐 <see cref="ConfigMgr"/>。
    /// 缓存由本类内部维护：Unity 打开时自动加载，配置保存后自动刷新。
    /// 写入只能往已经存在的表追加行，不会新建模块、表或字段。
    /// </summary>
    public static class ExternalConfigEditor
    {
        private static Dictionary<int, object> _configsById;
        private static Dictionary<Type, int> _typeToTableId;
        private static bool _loaded;
        private static int _saveBatchDepth;
        private static bool _refreshing;

        [InitializeOnLoadMethod]
        private static void AutoLoadOnEditorStartup()
        {
            // delayCall：避开编辑器启动早期其它 InitializeOnLoad 的顺序问题
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += RefreshCacheSafe;
                    return;
                }

                RefreshCacheSafe();
            };
        }

        /// <summary>
        /// 由 <see cref="EditorConfigMgr"/> 在写入磁盘后调用；外部无需手动加载/卸载。
        /// </summary>
        internal static void NotifyEditorDataChanged()
        {
            if (_saveBatchDepth > 0)
                return;
            RefreshCacheSafe();
        }

        internal static void BeginSaveBatch()
        {
            _saveBatchDepth++;
        }

        internal static void EndSaveBatch()
        {
            if (_saveBatchDepth > 0)
                _saveBatchDepth--;
            if (_saveBatchDepth == 0)
                RefreshCacheSafe();
        }

        /// <summary>
        /// 向已有表追加一行。单元格文本与编辑器里的写法相同。
        /// 未给出的字段使用该列默认值。自动行号和公式字段由编辑器写回，传入值会被忽略。
        /// 不会新建模块、表或字段。
        /// </summary>
        /// <returns>新行的行号，从 1 开始。</returns>
        public static int AddRow(string moduleName, string tableName, IReadOnlyDictionary<string, string> values)
        {
            AddRows(moduleName, tableName, new[] { values });
            ConfigTable table = RequireExistingTable(moduleName, tableName);
            return table.items.Count;
        }

        /// <summary>
        /// 向已有表一次追加多行。规则与 <see cref="AddRow"/> 相同。某一行不合法时，本次不会写入任何行。
        /// </summary>
        /// <returns>实际追加的行数。</returns>
        public static int AddRows(
            string moduleName,
            string tableName,
            IEnumerable<IReadOnlyDictionary<string, string>> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            ConfigTable table = RequireExistingTable(moduleName, tableName);
            if (table.fields == null || table.fields.Count == 0)
                throw new InvalidOperationException($"表 {moduleName}/{tableName} 没有字段，不能追加数据");

            List<ConfigItem> built = new();
            foreach (IReadOnlyDictionary<string, string> row in rows)
                built.Add(BuildRow(table, row));
            if (built.Count == 0)
                return 0;

            table.items ??= new List<ConfigItem>();
            int start = table.items.Count;
            table.items.AddRange(built);
            EditorConfigMgr.EnsureAutoRowId(table);
            for (int i = 0; i < built.Count; i++)
                ConstraintEvaluator.RecalculateRowFormulas(table, start + i);
            table.cachedRowCount = table.items.Count;
            table.MarkFkValidationDirty();
            return built.Count;
        }

        /// <summary>
        /// 把指定已有表写回 <c>Config/</c>。空行会被跳过。
        /// </summary>
        /// <returns>被跳过的空行数量。</returns>
        public static int Save(string moduleName, string tableName)
        {
            ConfigTable table = RequireExistingTable(moduleName, tableName);
            return EditorConfigMgr.SaveTable(table);
        }

        /// <summary>
        /// 按当前导出器导出全部配置，与编辑器里的「导出配置」相同。
        /// </summary>
        public static void Export()
        {
            EditorConfigMgr.ExportConfigs();
        }

        public static void PrintAllTables()
        {
            EnsureLoaded();
            if (_configsById == null) return;
            foreach (KeyValuePair<int, object> config in _configsById)
            {
                List<object> items = (List<object>)config.Value;
                Debug.Log(
                    $"[Editor] Table: Id={config.Key}, Name={(items.Count > 0 ? items[0].GetType().FullName : string.Empty)}, Count={items.Count}");
            }
        }

        public static CfgTableIterator<T> GetConfigs<T>() where T : IConfigTable
        {
            EnsureLoaded();
            if (_configsById == null)
                return new CfgTableIterator<T>(Array.Empty<object>());

            if (TryResolveTableId(typeof(T), out int tableId) &&
                _configsById.TryGetValue(tableId, out object configs))
                return new CfgTableIterator<T>((IReadOnlyList<object>)configs);

            return new CfgTableIterator<T>(Array.Empty<object>());
        }

        public static CfgTableIterator<T> GetConfigs<T>(int tableId) where T : IConfigTable
        {
            EnsureLoaded();
            if (_configsById != null && _configsById.TryGetValue(tableId, out object configs))
                return new CfgTableIterator<T>((IReadOnlyList<object>)configs);
            return new CfgTableIterator<T>(Array.Empty<object>());
        }

        /// <summary>
        /// 按行号取得一条配置。这里的 Id 是表的行号，从 1 开始，与编辑器中的 # 列一致，不是表的 tableId。
        /// 行序与保存顺序一致，行号减 1 即列表下标，直接按下标取出并转为 <typeparamref name="T"/>。
        /// </summary>
        public static T GetConfigById<T>(int id) where T : IConfigTable
        {
            EnsureLoaded();
            if (id <= 0 || _configsById == null || !TryResolveTableId(typeof(T), out int tableId))
                return default;
            if (!_configsById.TryGetValue(tableId, out object value) || value is not List<object> rows)
                return default;
            int index = id - 1;
            if ((uint)index >= (uint)rows.Count)
                return default;
            return (T)rows[index];
        }

        public static T GetConfig<T>(Predicate<T> condition) where T : IConfigTable
        {
            if (condition == null) return default;
            foreach (T config in GetConfigs<T>())
                if (condition.Invoke(config))
                    return config;
            return default;
        }

        public static List<T> GetConfigs<T>(Predicate<T> condition) where T : IConfigTable
        {
            List<T> configs = new();
            if (condition == null) return configs;
            foreach (T config in GetConfigs<T>())
                if (condition.Invoke(config))
                    configs.Add(config);
            return configs;
        }

        public static void GetConfigsNoAlloc<T>(Predicate<T> condition, List<T> result) where T : IConfigTable
        {
            if (condition == null || result == null) return;
            result.Clear();
            foreach (T config in GetConfigs<T>())
                if (condition.Invoke(config))
                    result.Add(config);
        }

        private static ConfigTable RequireExistingTable(string moduleName, string tableName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名不能为空", nameof(moduleName));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("表名不能为空", nameof(tableName));

            EditorConfigMgr.EnsureLoaded();
            ConfigModule module = EditorConfigMgr.GetModule(moduleName);
            if (module == null)
                throw new InvalidOperationException($"模块不存在: {moduleName}。外部接口不能新建模块。");

            ConfigTable table = module.FindTable(tableName);
            if (table == null)
                throw new InvalidOperationException(
                    $"表不存在: {moduleName}/{tableName}。外部接口只能向已有表追加数据。");

            EditorConfigMgr.EnsureTableDataLoaded(table);
            if (!table.dataLoaded)
                throw new InvalidOperationException($"表 {moduleName}/{tableName} 数据未能加载");
            return table;
        }

        private static ConfigItem BuildRow(ConfigTable table, IReadOnlyDictionary<string, string> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            HashSet<string> known = new(StringComparer.Ordinal);
            for (int i = 0; i < table.fields.Count; i++)
            {
                ConfigField field = table.fields[i];
                if (field != null && !string.IsNullOrEmpty(field.name))
                    known.Add(field.name);
            }

            foreach (string key in values.Keys)
            {
                if (string.IsNullOrEmpty(key) || !known.Contains(key))
                    throw new InvalidOperationException(
                        $"表 {table.moduleName}/{table.tableName} 不存在字段 {key}，不能新增字段");
            }

            ConfigItem item = new();
            for (int i = 0; i < table.fields.Count; i++)
            {
                ConfigField field = table.fields[i];
                if (field == null || string.IsNullOrEmpty(field.name))
                    continue;
                if (UniConfigEditorSettings.instance.IsAutoRowIdField(field.name))
                    continue;
                if (ConstraintEvaluator.IsFormulaField(table, field.name))
                    continue;

                string raw = values.TryGetValue(field.name, out string given) && given != null
                    ? given
                    : FieldUtils.GetDefaultCellString(field);
                FieldUtils.ToValue(raw, field);
                if (ConstraintEvaluator.TryGetRange(table, field, out double min, out double max) &&
                    (ConstraintEvaluator.TryParseScalar(raw, out double number) ||
                     string.IsNullOrWhiteSpace(raw)))
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        number = 0d;
                    double clamped = ConstraintEvaluator.ClampScalar(number, min, max, field.type);
                    raw = ConstraintEvaluator.FormatScalar(clamped, field.type);
                }

                item.SetValue(field.name, raw);
            }

            return item;
        }

        private static void EnsureLoaded()
        {
            if (!_loaded)
                RefreshCacheSafe();
        }

        private static void RefreshCacheSafe()
        {
            if (_refreshing)
                return;
            _refreshing = true;
            try
            {
                RefreshCache();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UniConfig] ExternalConfigEditor 刷新失败: {ex.Message}");
            }
            finally
            {
                _refreshing = false;
            }
        }

        private static void RefreshCache()
        {
            EditorConfigMgr.EnsureLoaded();

            Dictionary<int, object> byId = new();
            Dictionary<Type, int> typeMap = new();

            foreach (ConfigModule module in EditorConfigMgr.Modules.Values)
            {
                if (module?.tables == null) continue;
                foreach (ConfigTable table in module.tables)
                {
                    if (table == null) continue;
                    if (table.tableId <= 0)
                        continue;
                    if (table.fields == null || table.fields.Count == 0)
                        continue;

                    try
                    {
                        ExportTableData data = EditorConfigMgr.BuildExportTableData(table);
                        List<object> rows = new(data.Count);
                        for (int i = 0; i < data.Count; i++)
                            rows.Add(data.GetObject(i));

                        byId[data.TableId] = rows;
                        if (data.ExportCsharpType != null)
                            typeMap[data.ExportCsharpType] = data.TableId;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[UniConfig] ExternalConfigEditor 跳过表 {table.moduleName}/{table.tableName}: {ex.Message}");
                    }
                }
            }

            _configsById = byId;
            _typeToTableId = typeMap;
            _loaded = true;
        }

        private static bool TryResolveTableId(Type mapCsharpType, out int tableId)
        {
            tableId = 0;
            if (mapCsharpType == null) return false;

            if (_typeToTableId != null && _typeToTableId.TryGetValue(mapCsharpType, out tableId) && tableId > 0)
                return true;

            if (_typeToTableId == null) return false;
            string fullName = mapCsharpType.FullName;
            foreach (KeyValuePair<Type, int> kv in _typeToTableId)
            {
                if (kv.Key != null && kv.Key.FullName == fullName)
                {
                    tableId = kv.Value;
                    return tableId > 0;
                }
            }

            return false;
        }
    }
}
