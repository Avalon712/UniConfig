#region

using System;
using System.Collections.Generic;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 杂项表固定四列：name（字段名）、type（类型编码）、value（按该行类型编辑的值）、remark（备注，不生成属性）。
    /// </summary>
    internal static class MiscTableSchema
    {
        internal const string NameField = "name";
        internal const string TypeField = "type";
        internal const string ValueField = "value";
        internal const string RemarkField = "remark";
        internal const string ApplyMethodName = "Apply";

        private const string EnumPrefix = "enum:";

        private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
            "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
            "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
            "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static",
            "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        internal sealed class MiscEntry
        {
            public string Name;
            public ConfigField Field;
            public string RawValue;
        }

        internal static bool IsReservedWord(string name)
        {
            return !string.IsNullOrEmpty(name) && ReservedWords.Contains(name);
        }

        internal static bool IsForbiddenTableName(string name)
        {
            return IsReservedWord(name) ||
                   name == ApplyMethodName ||
                   name == "CfgMisc";
        }

        /// <summary>
        /// 把列锁成 name / type / value / remark。有变化时返回 true。
        /// </summary>
        internal static bool EnsureColumns(ConfigTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            table.fields ??= new List<ConfigField>();

            bool coreOk = table.fields.Count >= 3
                          && IsLockedStringColumn(table.fields[0], NameField)
                          && IsLockedStringColumn(table.fields[1], TypeField)
                          && IsLockedStringColumn(table.fields[2], ValueField);
            bool remarkOk = table.fields.Count == 4 && IsLockedStringColumn(table.fields[3], RemarkField);
            bool changed = false;
            if (!coreOk)
            {
                table.fields.Clear();
                table.fields.Add(new ConfigField(NameField, SupportableFieldType.String));
                table.fields.Add(new ConfigField(TypeField, SupportableFieldType.String));
                table.fields.Add(new ConfigField(ValueField, SupportableFieldType.String));
                table.fields.Add(new ConfigField(RemarkField, SupportableFieldType.String));
                changed = true;
            }
            else if (!remarkOk)
            {
                if (table.fields.Count > 3)
                    table.fields.RemoveRange(3, table.fields.Count - 3);
                table.fields.Add(new ConfigField(RemarkField, SupportableFieldType.String));
                changed = true;
            }

            if (table.constraints != null && table.constraints.Count > 0)
            {
                table.constraints.Clear();
                changed = true;
            }

            return changed;
        }

        internal static string FormatType(ConfigField field)
        {
            if (field == null)
                return SupportableFieldType.Int32.ToString();
            if (field.type == SupportableFieldType.Enum)
            {
                if (string.IsNullOrEmpty(field.enumTypeFullName))
                    return SupportableFieldType.Int32.ToString();
                return EnumPrefix + field.enumTypeFullName;
            }

            return field.type.ToString();
        }

        internal static bool TryParseType(string raw, out ConfigField field)
        {
            field = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            raw = raw.Trim();
            if (raw.StartsWith(EnumPrefix, StringComparison.Ordinal))
            {
                string fullName = raw.Substring(EnumPrefix.Length);
                if (string.IsNullOrWhiteSpace(fullName))
                    return false;

                field = new ConfigField(ValueField, SupportableFieldType.Enum, fullName.Trim());
                return true;
            }

            if (Enum.TryParse(raw, false, out SupportableFieldType type) &&
                type != SupportableFieldType.Enum &&
                Enum.IsDefined(typeof(SupportableFieldType), type))
            {
                field = new ConfigField(ValueField, type);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 按行序收集非空字段。空行跳过；名字、类型或值不合法时抛出。
        /// </summary>
        internal static List<MiscEntry> Collect(ConfigTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            EnsureColumns(table);

            List<MiscEntry> list = new();
            HashSet<string> names = new(StringComparer.Ordinal);
            if (table.items == null)
                return list;

            for (int i = 0; i < table.items.Count; i++)
            {
                ConfigItem item = table.items[i];
                if (item == null)
                    continue;

                int row = i + 1;
                string name = item.GetValue(NameField).Trim();
                string rawValue = item.GetValue(ValueField);
                if (name.Length == 0)
                {
                    if (!string.IsNullOrWhiteSpace(rawValue))
                        throw new InvalidOperationException(
                            $"杂项表 {table.moduleName}/{table.tableName} 第 {row} 行缺少字段名");
                    continue;
                }

                if (!FieldUtils.IsValidIdentifier(name) || IsReservedWord(name))
                    throw new InvalidOperationException(
                        $"杂项表 {table.moduleName}/{table.tableName} 第 {row} 行字段名不是可用的 C# 标识符: {name}");

                if (!names.Add(name))
                    throw new InvalidOperationException(
                        $"杂项表 {table.moduleName}/{table.tableName} 字段名重复: {name}");

                if (!TryParseType(item.GetValue(TypeField), out ConfigField parsed))
                    throw new InvalidOperationException(
                        $"杂项表 {table.moduleName}/{table.tableName} 第 {row} 行类型无效");

                ConfigField field = new(name, parsed.type, parsed.enumTypeFullName, item.GetValue(RemarkField));
                if (field.type == SupportableFieldType.Enum)
                    EnumTypeUtil.ResolveOrThrow(field.enumTypeFullName);

                try
                {
                    FieldUtils.ToValue(rawValue, field);
                }
                catch (Exception ex)
                {
                    throw new FormatException(
                        $"杂项表 {table.moduleName}/{table.tableName} 第 {row} 行 {name} 的值无效: {ex.Message}",
                        ex);
                }

                list.Add(new MiscEntry
                {
                    Name = name,
                    Field = field,
                    RawValue = rawValue
                });
            }

            return list;
        }

        private static bool IsLockedStringColumn(ConfigField field, string name)
        {
            return field != null &&
                   field.name == name &&
                   field.type == SupportableFieldType.String &&
                   string.IsNullOrEmpty(field.enumTypeFullName);
        }
    }
}
