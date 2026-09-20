#region

using System;
using System.Globalization;
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
            "Color", "Color32"
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
            Color.clear, new Color32(0, 0, 0, 0)
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
