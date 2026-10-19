using System;
using System.Collections.Generic;
using System.Reflection;

namespace UniConfig.Editor
{
    /// <summary>
    /// 解析设置中登记的自定义枚举类型，并缓存名称/默认值。
    /// </summary>
    internal static class EnumTypeUtil
    {
        private static readonly Dictionary<string, Type> TypeCache = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string[]> NamesCache = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> DefaultNameCache = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool> FlagsCache = new(StringComparer.Ordinal);

        public static void ClearCache()
        {
            TypeCache.Clear();
            NamesCache.Clear();
            DefaultNameCache.Clear();
            FlagsCache.Clear();
        }

        public static bool TryResolve(string typeFullName, out Type enumType, out string error)
        {
            enumType = null;
            error = null;
            if (string.IsNullOrWhiteSpace(typeFullName))
            {
                error = "类型名为空";
                return false;
            }

            string key = typeFullName.Trim();
            if (TypeCache.TryGetValue(key, out enumType) && enumType != null)
                return true;

            enumType = FindType(key);
            if (enumType == null)
            {
                error = $"找不到类型: {key}";
                return false;
            }

            if (!enumType.IsEnum)
            {
                error = $"不是枚举类型: {key}";
                enumType = null;
                return false;
            }

            TypeCache[key] = enumType;
            return true;
        }

        public static Type ResolveOrThrow(string typeFullName)
        {
            if (!TryResolve(typeFullName, out Type enumType, out string error))
                throw new TypeLoadException(error);
            return enumType;
        }

        /// <summary>
        /// 类型是否带 <see cref="FlagsAttribute"/>。带标记的枚举在表格里用多选编辑。
        /// </summary>
        public static bool IsFlags(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName))
                return false;
            if (FlagsCache.TryGetValue(typeFullName, out bool cached))
                return cached;

            Type enumType = ResolveOrThrow(typeFullName);
            bool flags = enumType.IsDefined(typeof(FlagsAttribute), false);
            FlagsCache[typeFullName] = flags;
            return flags;
        }

        public static string[] GetNames(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName))
                return Array.Empty<string>();
            if (NamesCache.TryGetValue(typeFullName, out string[] cached))
                return cached;

            Type enumType = ResolveOrThrow(typeFullName);
            string[] names = Enum.GetNames(enumType);
            NamesCache[typeFullName] = names;
            return names;
        }

        /// <summary>
        /// 默认枚举成员名：取底层数值最小的那个（不要求从 0 开始）。
        /// </summary>
        public static string GetDefaultMemberName(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName))
                return string.Empty;
            if (DefaultNameCache.TryGetValue(typeFullName, out string cached))
                return cached;

            Type enumType = ResolveOrThrow(typeFullName);
            Array values = Enum.GetValues(enumType);
            if (values.Length == 0)
            {
                DefaultNameCache[typeFullName] = string.Empty;
                return string.Empty;
            }

            object minObj = values.GetValue(0);
            long minNum = ToInt64(minObj);
            for (int i = 1; i < values.Length; i++)
            {
                object v = values.GetValue(i);
                long n = ToInt64(v);
                if (n < minNum)
                {
                    minNum = n;
                    minObj = v;
                }
            }

            string name = Enum.GetName(enumType, minObj) ?? minObj.ToString();
            DefaultNameCache[typeFullName] = name;
            return name;
        }

        public static object ParseValue(string typeFullName, string raw)
        {
            Type enumType = ResolveOrThrow(typeFullName);
            if (string.IsNullOrWhiteSpace(raw))
                return Enum.Parse(enumType, GetDefaultMemberName(typeFullName));

            string text = raw.Trim();
            try
            {
                // Flags 组合存成 "A, B"，Enum.Parse 可直接还原
                return Enum.Parse(enumType, text, ignoreCase: true);
            }
            catch (ArgumentException)
            {
                // 允许填底层整数
                Type underlying = Enum.GetUnderlyingType(enumType);
                object number = Convert.ChangeType(text, underlying, System.Globalization.CultureInfo.InvariantCulture);
                return Enum.ToObject(enumType, number);
            }
        }

        public static string GetCsharpTypeName(string typeFullName)
        {
            Type enumType = ResolveOrThrow(typeFullName);
            return BuildCsharpTypeName(enumType);
        }

        public static string GetDisplayLabel(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName))
                return "enum";
            int dot = typeFullName.LastIndexOf('.');
            string shortName = dot >= 0 ? typeFullName.Substring(dot + 1) : typeFullName;
            // Outer+Inner → Outer.Inner 显示
            string suffix = " (enum)";
            if (TryResolve(typeFullName, out Type enumType, out _) &&
                enumType.IsDefined(typeof(FlagsAttribute), false))
                suffix = " (flags)";
            return shortName.Replace('+', '.') + suffix;
        }

        private static string BuildCsharpTypeName(Type type)
        {
            if (type.DeclaringType != null)
                return BuildCsharpTypeName(type.DeclaringType) + "." + type.Name;
            return string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name;
        }

        private static long ToInt64(object enumValue)
        {
            return Convert.ToInt64(enumValue, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Type FindType(string fullName)
        {
            Type direct = Type.GetType(fullName);
            if (direct != null) return direct;

            // Unity 模块类型（如 UnityEngine.HumanBodyBones）不在 UnityEngine.dll，
            // Type.GetType(短名) 找不到，必须扫已加载程序集。
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                Type type = TryGetType(assembly, fullName);
                if (type != null) return type;
            }

            return null;
        }

        private static Type TryGetType(Assembly assembly, string fullName)
        {
            Type type = assembly.GetType(fullName);
            if (type != null) return type;

            // 兼容嵌套枚举：C# 名 Namespace.Outer.Inner ↔ CLR FullName Namespace.Outer+Inner
            for (int i = fullName.Length - 1; i >= 0; i--)
            {
                if (fullName[i] != '.') continue;
                string candidate = fullName.Substring(0, i) + "+" + fullName.Substring(i + 1).Replace('.', '+');
                type = assembly.GetType(candidate);
                if (type != null) return type;
            }

            return null;
        }
    }
}
