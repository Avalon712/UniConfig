#region

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    internal static class FieldUtils
    {
        public static readonly string[] DISPLAY_NAMES =
        {
            "short", "int", "long", "ushort", "uint", "ulong", "float", "double", "bool", "string",
            "short[]", "int[]", "long[]", "ushort[]", "uint[]", "ulong[]", "bool[]", "float[]", "double[]",
            "short[][]", "int[][]", "long[][]", "ushort[][]", "uint[][]", "ulong[][]", "bool[][]", "float[][]",
            "double[][]",
            "Vector2", "Vector3", "Vector4", "Quaternion", "Vector2Int", "Vector3Int",
            "Color", "Color32",
            "enum"
        };

        public static readonly object[] DEFAULT_VALUES =
        {
            (short)0, 0, 0L, (ushort)0, 0u, 0ul, 0f, 0d, false, string.Empty,
            Array.Empty<short>(), Array.Empty<int>(), Array.Empty<long>(), Array.Empty<ushort>(),
            Array.Empty<uint>(), Array.Empty<ulong>(), Array.Empty<bool>(), Array.Empty<float>(), Array.Empty<double>(),
            Array.Empty<short[]>(), Array.Empty<int[]>(), Array.Empty<long[]>(), Array.Empty<ushort[]>(),
            Array.Empty<uint[]>(), Array.Empty<ulong[]>(), Array.Empty<bool[]>(), Array.Empty<float[]>(),
            Array.Empty<double[]>(),
            Vector2.zero, Vector3.zero, Vector4.zero, Quaternion.identity, Vector2Int.zero, Vector3Int.zero,
            Color.clear, new Color32(0, 0, 0, 0),
            null
        };

        /// <summary>内置类型数量（不含裸 Enum 占位，自定义枚举接在其后）。</summary>
        public static int BuiltinTypeCount => (int)SupportableFieldType.Enum;

        /// <summary>
        /// 获取显示名称
        /// </summary>
        public static string GetDisplayName(SupportableFieldType fieldType)
        {
            int index = (int)fieldType;
            return index >= 0 && index < DISPLAY_NAMES.Length ? DISPLAY_NAMES[index] : string.Empty;
        }

        public static string GetDisplayName(ConfigField field)
        {
            if (field != null && field.IsEnum)
                return EnumTypeUtil.GetDisplayLabel(field.enumTypeFullName);
            return field == null ? string.Empty : GetDisplayName(field.type);
        }

        /// <summary>
        /// 获取代码生成用的 C# 类型名
        /// </summary>
        public static string GetCsharpTypeName(SupportableFieldType fieldType)
        {
            return GetDisplayName(fieldType);
        }

        public static string GetCsharpTypeName(ConfigField field)
        {
            if (field == null) return string.Empty;
            if (field.type == SupportableFieldType.Enum)
                return EnumTypeUtil.GetCsharpTypeName(field.enumTypeFullName);
            return GetDisplayName(field.type);
        }

        /// <summary>
        /// 获取默认值（对象）
        /// </summary>
        public static object GetDefaultValue(SupportableFieldType fieldType)
        {
            int index = (int)fieldType;
            return index >= 0 && index < DEFAULT_VALUES.Length ? DEFAULT_VALUES[index] : null;
        }

        public static object GetDefaultValue(ConfigField field)
        {
            if (field != null && field.type == SupportableFieldType.Enum)
                return EnumTypeUtil.ParseValue(field.enumTypeFullName, null);
            return field == null ? null : GetDefaultValue(field.type);
        }

        /// <summary>
        /// 单元格默认字符串（枚举为最小数值的成员名）。
        /// </summary>
        public static string GetDefaultCellString(ConfigField field)
        {
            if (field != null && field.type == SupportableFieldType.Enum)
                return EnumTypeUtil.GetDefaultMemberName(field.enumTypeFullName);
            return string.Empty;
        }

        private static readonly string[] EmptyStrings = Array.Empty<string>();

        private static IReadOnlyList<string> GetRegisteredEnumTypeFullNames()
        {
            return (IReadOnlyList<string>)UniConfigEditorSettings.instance.customEnumTypeFullNames
                   ?? EmptyStrings;
        }

        /// <summary>
        /// 类型按钮上显示的短名称。
        /// </summary>
        public static string GetTypeButtonLabel(ConfigField field)
        {
            if (field == null) return DISPLAY_NAMES[0];
            if (field.IsEnum)
            {
                string full = field.enumTypeFullName ?? string.Empty;
                int dot = full.LastIndexOf('.');
                string shortName = dot >= 0 ? full.Substring(dot + 1) : full;
                return shortName.Replace('+', '.');
            }

            int index = (int)field.type;
            return index >= 0 && index < BuiltinTypeCount ? DISPLAY_NAMES[index] : GetDisplayName(field.type);
        }

        /// <summary>
        /// 分级菜单选择字段类型（基础 / 数组 / Unity / 枚举）。
        /// </summary>
        public static void ShowTypeDropdownMenu(Rect activatorRect, ConfigField field, Action onSelected)
        {
            if (field == null) return;

            var menu = new GenericMenu();
            for (int i = 0; i < BuiltinTypeCount; i++)
            {
                int typeIndex = i;
                SupportableFieldType t = (SupportableFieldType)typeIndex;
                string path = GetBuiltinTypeMenuPath(t);
                bool selected = field.type == t && field.type != SupportableFieldType.Enum;
                menu.AddItem(new GUIContent(path), selected, () =>
                {
                    field.type = t;
                    field.enumTypeFullName = string.Empty;
                    onSelected?.Invoke();
                });
            }

            IReadOnlyList<string> enums = GetRegisteredEnumTypeFullNames();
            string enumCat = UniConfigLoc.T("type_cat.enum");
            if (enums.Count == 0 &&
                !(field.IsEnum && !string.IsNullOrEmpty(field.enumTypeFullName)))
            {
                menu.AddDisabledItem(new GUIContent(enumCat + "/(" + UniConfigLoc.T("settings.enums") + ")"));
            }
            else
            {
                for (int i = 0; i < enums.Count; i++)
                {
                    string fullName = enums[i];
                    if (string.IsNullOrWhiteSpace(fullName)) continue;
                    string label = EnumTypeUtil.GetDisplayLabel(fullName);
                    // 去掉 " (enum)" 后缀放进子菜单更干净
                    if (label.EndsWith(" (enum)", StringComparison.Ordinal))
                        label = label.Substring(0, label.Length - 7);
                    bool selected = field.type == SupportableFieldType.Enum &&
                                    string.Equals(field.enumTypeFullName, fullName, StringComparison.Ordinal);
                    string captured = fullName;
                    menu.AddItem(new GUIContent(enumCat + "/" + label), selected, () =>
                    {
                        field.type = SupportableFieldType.Enum;
                        field.enumTypeFullName = captured;
                        onSelected?.Invoke();
                    });
                }

                // 当前字段用了未登记的孤儿枚举，仍显示可选中项
                if (field.IsEnum && IndexOfEnum(enums, field.enumTypeFullName) < 0)
                {
                    string fullName = field.enumTypeFullName;
                    string label = EnumTypeUtil.GetDisplayLabel(fullName);
                    if (label.EndsWith(" (enum)", StringComparison.Ordinal))
                        label = label.Substring(0, label.Length - 7);
                    menu.AddItem(new GUIContent(enumCat + "/" + label), true, () => { });
                }
            }

            menu.DropDown(activatorRect);
        }

        private static string GetBuiltinTypeMenuPath(SupportableFieldType type)
        {
            int index = (int)type;
            string name = index >= 0 && index < DISPLAY_NAMES.Length ? DISPLAY_NAMES[index] : type.ToString();
            string category;
            if (index <= (int)SupportableFieldType.String)
                category = UniConfigLoc.T("type_cat.scalar");
            else if (index <= (int)SupportableFieldType.Array1D_Double)
                category = UniConfigLoc.T("type_cat.array1d");
            else if (index <= (int)SupportableFieldType.Array2D_Double)
                category = UniConfigLoc.T("type_cat.array2d");
            else
                category = UniConfigLoc.T("type_cat.unity");
            return category + "/" + name;
        }

        /// <summary>
        /// 类型行下拉选项：内置类型 + 设置中登记的自定义枚举。
        /// </summary>
        public static string[] GetTypePopupOptions(ConfigField currentField = null)
        {
            IReadOnlyList<string> enums = GetRegisteredEnumTypeFullNames();
            bool appendOrphan = currentField != null &&
                                currentField.type == SupportableFieldType.Enum &&
                                !string.IsNullOrEmpty(currentField.enumTypeFullName) &&
                                IndexOfEnum(enums, currentField.enumTypeFullName) < 0;

            var options = new string[BuiltinTypeCount + enums.Count + (appendOrphan ? 1 : 0)];
            for (int i = 0; i < BuiltinTypeCount; i++)
                options[i] = DISPLAY_NAMES[i];
            for (int i = 0; i < enums.Count; i++)
                options[BuiltinTypeCount + i] = EnumTypeUtil.GetDisplayLabel(enums[i]);
            if (appendOrphan)
                options[options.Length - 1] = EnumTypeUtil.GetDisplayLabel(currentField.enumTypeFullName);
            return options;
        }

        public static int GetTypePopupIndex(ConfigField field, string[] options = null)
        {
            if (field == null) return 0;
            if (field.type != SupportableFieldType.Enum)
                return Mathf.Clamp((int)field.type, 0, BuiltinTypeCount - 1);

            IReadOnlyList<string> enums = GetRegisteredEnumTypeFullNames();
            int idx = IndexOfEnum(enums, field.enumTypeFullName);
            if (idx >= 0)
                return BuiltinTypeCount + idx;

            // 孤儿枚举在 options 末尾
            options ??= GetTypePopupOptions(field);
            return Mathf.Max(0, options.Length - 1);
        }

        public static void ApplyTypePopupIndex(ConfigField field, int popupIndex)
        {
            if (field == null) return;
            IReadOnlyList<string> enums = GetRegisteredEnumTypeFullNames();
            if (popupIndex < BuiltinTypeCount)
            {
                field.type = (SupportableFieldType)popupIndex;
                field.enumTypeFullName = string.Empty;
                return;
            }

            int enumIndex = popupIndex - BuiltinTypeCount;
            if (enumIndex >= 0 && enumIndex < enums.Count)
            {
                field.type = SupportableFieldType.Enum;
                field.enumTypeFullName = enums[enumIndex];
                return;
            }

            // 点选孤儿项：保持不变
        }

        private static int IndexOfEnum(IReadOnlyList<string> enums, string fullName)
        {
            if (enums == null || string.IsNullOrEmpty(fullName)) return -1;
            for (int i = 0; i < enums.Count; i++)
            {
                if (string.Equals(enums[i], fullName, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 将字段值转为真实对应的对象值
        /// </summary>
        public static object ToValue(string fieldValue, ConfigField field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (field.type == SupportableFieldType.Enum)
            {
                try
                {
                    return EnumTypeUtil.ParseValue(field.enumTypeFullName, fieldValue);
                }
                catch (Exception ex)
                {
                    throw new FormatException(
                        $"无法将 \"{fieldValue}\" 解析为枚举 {field.enumTypeFullName}: {ex.Message}", ex);
                }
            }

            return ToValue(fieldValue, field.type);
        }

        /// <summary>
        /// 将字段值转为真实对应的对象值
        /// </summary>
        /// <param name="fieldValue">字段值（显示、编辑）</param>
        /// <param name="fieldType">字段类型</param>
        public static object ToValue(string fieldValue, SupportableFieldType fieldType)
        {
            if (fieldType == SupportableFieldType.Enum)
                throw new ArgumentException("枚举类型请使用 ToValue(string, ConfigField)", nameof(fieldType));

            if (string.IsNullOrWhiteSpace(fieldValue))
                return GetDefaultValue(fieldType);

            string value = fieldValue.Trim();
            try
            {
                switch (fieldType)
                {
                    case SupportableFieldType.Int16:
                        return short.Parse(value, CultureInfo.InvariantCulture);
                    case SupportableFieldType.Int32:
                        return int.Parse(value, CultureInfo.InvariantCulture);
                    case SupportableFieldType.Int64:
                        return long.Parse(value, CultureInfo.InvariantCulture);
                    case SupportableFieldType.UInt16:
                        return ushort.Parse(value, CultureInfo.InvariantCulture);
                    case SupportableFieldType.UInt32:
                        return uint.Parse(value, CultureInfo.InvariantCulture);
                    case SupportableFieldType.UInt64:
                        return ulong.Parse(value, CultureInfo.InvariantCulture);
                    case SupportableFieldType.Single:
                        return float.Parse(value, CultureInfo.InvariantCulture);
                    case SupportableFieldType.Double:
                        return double.Parse(value, CultureInfo.InvariantCulture);
                    case SupportableFieldType.Boolean:
                        return ParseBool(value);
                    case SupportableFieldType.String:
                        return fieldValue;
                    case SupportableFieldType.Array1D_Int16:
                        return Parse1D(value, s => short.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array1D_Int32:
                        return Parse1D(value, s => int.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array1D_Int64:
                        return Parse1D(value, s => long.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array1D_UInt16:
                        return Parse1D(value, s => ushort.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array1D_UInt32:
                        return Parse1D(value, s => uint.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array1D_UInt64:
                        return Parse1D(value, s => ulong.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array1D_Boolean:
                        return Parse1D(value, ParseBool);
                    case SupportableFieldType.Array1D_Single:
                        return Parse1D(value, s => float.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array1D_Double:
                        return Parse1D(value, s => double.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array2D_Int16:
                        return Parse2D(value, s => short.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array2D_Int32:
                        return Parse2D(value, s => int.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array2D_Int64:
                        return Parse2D(value, s => long.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array2D_UInt16:
                        return Parse2D(value, s => ushort.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array2D_UInt32:
                        return Parse2D(value, s => uint.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array2D_UInt64:
                        return Parse2D(value, s => ulong.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array2D_Boolean:
                        return Parse2D(value, ParseBool);
                    case SupportableFieldType.Array2D_Single:
                        return Parse2D(value, s => float.Parse(s, CultureInfo.InvariantCulture));
                    case SupportableFieldType.Array2D_Double:
                        return Parse2D(value, s => double.Parse(s, CultureInfo.InvariantCulture));

                    case SupportableFieldType.Vector2:
                    {
                        float[] c = ParseFloatComponents(value, 2);
                        return new Vector2(c[0], c[1]);
                    }
                    case SupportableFieldType.Vector3:
                    {
                        float[] c = ParseFloatComponents(value, 3);
                        return new Vector3(c[0], c[1], c[2]);
                    }
                    case SupportableFieldType.Vector4:
                    {
                        float[] c = ParseFloatComponents(value, 4);
                        return new Vector4(c[0], c[1], c[2], c[3]);
                    }
                    case SupportableFieldType.Quaternion:
                    {
                        float[] c = ParseFloatComponents(value, 4);
                        return new Quaternion(c[0], c[1], c[2], c[3]);
                    }
                    case SupportableFieldType.Vector2Int:
                    {
                        int[] c = ParseIntComponents(value, 2);
                        return new Vector2Int(c[0], c[1]);
                    }
                    case SupportableFieldType.Vector3Int:
                    {
                        int[] c = ParseIntComponents(value, 3);
                        return new Vector3Int(c[0], c[1], c[2]);
                    }
                    case SupportableFieldType.Color:
                        return ParseColor(value);
                    case SupportableFieldType.Color32:
                        return ParseColor32(value);

                    default:
                        throw new ArgumentOutOfRangeException(nameof(fieldType), fieldType, null);
                }
            }
            catch (Exception ex) when (ex is not ArgumentOutOfRangeException)
            {
                throw new FormatException($"无法将 \"{fieldValue}\" 解析为 {GetDisplayName(fieldType)}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 通过显示的字段类型名称转为真实的字段类型
        /// </summary>
        public static SupportableFieldType GetFieldType(string displayFieldType)
        {
            int index = Array.IndexOf(DISPLAY_NAMES, displayFieldType);
            if (index < 0)
                throw new InvalidCastException($"{displayFieldType} is invalid");
            return (SupportableFieldType)index;
        }

        /// <summary>
        /// 校验是否为合法 C# 标识符
        /// </summary>
        public static bool IsValidIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!(char.IsLetter(name[0]) || name[0] == '_')) return false;
            for (int i = 1; i < name.Length; i++)
                if (!(char.IsLetterOrDigit(name[i]) || name[i] == '_'))
                    return false;
            return true;
        }

        private static bool ParseBool(string value)
        {
            if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;
            throw new FormatException($"无效的 bool 值: {value}");
        }

        private static T[] Parse1D<T>(string value, Func<string, T> parse)
        {
            string[] parts = value.Split(new[] { ',' }, StringSplitOptions.None);
            T[] result = new T[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0 && parts.Length == 1)
                    return Array.Empty<T>();
                result[i] = parse(part);
            }

            return result;
        }

        private static T[][] Parse2D<T>(string value, Func<string, T> parse)
        {
            string[] rows = value.Split(new[] { ';' }, StringSplitOptions.None);
            T[][] result = new T[rows.Length][];
            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r].Trim();
                if (row.Length == 0 && rows.Length == 1)
                    return Array.Empty<T[]>();
                result[r] = Parse1D(row, parse);
            }

            return result;
        }

        /// <summary>
        /// 解析定长浮点分量。支持 <c>1,2,3</c> 或 <c>(1, 2, 3)</c>。
        /// </summary>
        private static float[] ParseFloatComponents(string value, int exactCount)
        {
            return ParseFloatComponents(value, exactCount, exactCount);
        }

        private static float[] ParseFloatComponents(string value, int minCount, int maxCount)
        {
            string[] parts = SplitComponents(value);
            if (parts.Length < minCount || parts.Length > maxCount)
            {
                string expect = minCount == maxCount
                    ? exactCountText(minCount)
                    : $"{minCount}~{maxCount} 个分量";
                throw new FormatException($"需要 {expect}，实际 {parts.Length} 个");
            }

            float[] result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
            return result;
        }

        private static int[] ParseIntComponents(string value, int exactCount)
        {
            return ParseIntComponents(value, exactCount, exactCount);
        }

        private static int[] ParseIntComponents(string value, int minCount, int maxCount)
        {
            string[] parts = SplitComponents(value);
            if (parts.Length < minCount || parts.Length > maxCount)
            {
                string expect = minCount == maxCount
                    ? exactCountText(minCount)
                    : $"{minCount}~{maxCount} 个分量";
                throw new FormatException($"需要 {expect}，实际 {parts.Length} 个");
            }

            int[] result = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
            return result;
        }

        private static string exactCountText(int count) => $"{count} 个分量";

        private static string[] SplitComponents(string value)
        {
            string text = value.Trim();
            if (text.Length >= 2 && text[0] == '(' && text[text.Length - 1] == ')')
                text = text.Substring(1, text.Length - 2).Trim();

            string[] parts = text.Split(new[] { ',' }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();
            return parts;
        }

        private static Color ParseColor(string value)
        {
            string text = value.Trim();
            if (text.Length > 0 && text[0] == '#')
            {
                Color32 c32 = ParseColor32FromHex(text);
                return (Color)c32;
            }

            float[] c = ParseFloatComponents(text, 3, 4);
            // 若出现 >1 的分量，按 0~255 解释并换算为 0~1
            bool anyOverOne = false;
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] > 1f) { anyOverOne = true; break; }
            }

            if (anyOverOne)
            {
                float a = c.Length == 3 ? 1f : Mathf.Clamp01(c[3] / 255f);
                return new Color(
                    Mathf.Clamp01(c[0] / 255f),
                    Mathf.Clamp01(c[1] / 255f),
                    Mathf.Clamp01(c[2] / 255f),
                    a);
            }

            return c.Length == 3
                ? new Color(c[0], c[1], c[2], 1f)
                : new Color(c[0], c[1], c[2], c[3]);
        }

        private static Color32 ParseColor32(string value)
        {
            string text = value.Trim();
            if (text.Length > 0 && text[0] == '#')
                return ParseColor32FromHex(text);

            int[] c = ParseIntComponents(text, 3, 4);
            byte a = (byte)(c.Length == 3 ? 255 : ClampByte(c[3]));
            return new Color32(ClampByte(c[0]), ClampByte(c[1]), ClampByte(c[2]), a);
        }

        private static Color32 ParseColor32FromHex(string hex)
        {
            string h = hex.Substring(1).Trim();
            if (h.Length == 3 || h.Length == 4)
            {
                var expanded = new char[h.Length * 2];
                for (int i = 0; i < h.Length; i++)
                {
                    expanded[i * 2] = h[i];
                    expanded[i * 2 + 1] = h[i];
                }

                h = new string(expanded);
            }

            if (h.Length != 6 && h.Length != 8)
                throw new FormatException($"无效的十六进制颜色: {hex}");

            byte r = ClampByte(Convert.ToInt32(h.Substring(0, 2), 16));
            byte g = ClampByte(Convert.ToInt32(h.Substring(2, 2), 16));
            byte b = ClampByte(Convert.ToInt32(h.Substring(4, 2), 16));
            byte a = h.Length == 6 ? (byte)255 : ClampByte(Convert.ToInt32(h.Substring(6, 2), 16));
            return new Color32(r, g, b, a);
        }

        private static byte ClampByte(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (byte)value;
        }
    }
}
