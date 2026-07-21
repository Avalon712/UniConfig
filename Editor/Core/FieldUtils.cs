#region

using System;
using System.Globalization;

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
            "double[][]"
        };

        public static readonly object[] DEFAULT_VALUES =
        {
            (short)0, 0, 0L, (ushort)0, 0u, 0ul, 0f, 0d, false, string.Empty,
            Array.Empty<short>(), Array.Empty<int>(), Array.Empty<long>(), Array.Empty<ushort>(),
            Array.Empty<uint>(), Array.Empty<ulong>(), Array.Empty<bool>(), Array.Empty<float>(), Array.Empty<double>(),
            Array.Empty<short[]>(), Array.Empty<int[]>(), Array.Empty<long[]>(), Array.Empty<ushort[]>(),
            Array.Empty<uint[]>(), Array.Empty<ulong[]>(), Array.Empty<bool[]>(), Array.Empty<float[]>(),
            Array.Empty<double[]>()
        };

        /// <summary>
        /// 获取显示名称
        /// </summary>
        public static string GetDisplayName(SupportableFieldType fieldType)
        {
            int index = (int)fieldType;
            return index >= 0 && index < DISPLAY_NAMES.Length ? DISPLAY_NAMES[index] : string.Empty;
        }

        /// <summary>
        /// 获取代码生成用的 C# 类型名
        /// </summary>
        public static string GetCsharpTypeName(SupportableFieldType fieldType)
        {
            return GetDisplayName(fieldType);
        }

        /// <summary>
        /// 获取默认值
        /// </summary>
        public static object GetDefaultValue(SupportableFieldType fieldType)
        {
            int index = (int)fieldType;
            return index >= 0 && index < DEFAULT_VALUES.Length ? DEFAULT_VALUES[index] : null;
        }

        /// <summary>
        /// 将字段值转为真实对应的对象值
        /// </summary>
        /// <param name="fieldValue">字段值（显示、编辑）</param>
        /// <param name="fieldType">字段类型</param>
        public static object ToValue(string fieldValue, SupportableFieldType fieldType)
        {
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
    }
}