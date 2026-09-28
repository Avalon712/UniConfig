using System;

namespace UniConfig.Editor
{
    /// <summary>
    /// 配置表字段定义（保持插入顺序）
    /// </summary>
    [Serializable]
    public sealed class ConfigField
    {
        public string name;
        public SupportableFieldType type;

        /// <summary>
        /// 当 <see cref="type"/> 为 <see cref="SupportableFieldType.Enum"/> 时，枚举的完整类型名。
        /// </summary>
        public string enumTypeFullName;

        public ConfigField()
        {
        }

        public ConfigField(string name, SupportableFieldType type, string enumTypeFullName = null)
        {
            this.name = name;
            this.type = type;
            this.enumTypeFullName = enumTypeFullName ?? string.Empty;
        }

        public bool IsEnum => type == SupportableFieldType.Enum && !string.IsNullOrEmpty(enumTypeFullName);
    }
}
