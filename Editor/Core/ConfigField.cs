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

        /// <summary>
        /// 备注。空文本不生成 XML 文档注释，也不参与二进制导出。
        /// </summary>
        public string remark;

        public ConfigField()
        {
        }

        public ConfigField(string name, SupportableFieldType type, string enumTypeFullName = null, string remark = null)
        {
            this.name = name;
            this.type = type;
            this.enumTypeFullName = enumTypeFullName ?? string.Empty;
            this.remark = remark ?? string.Empty;
        }

        public bool IsEnum => type == SupportableFieldType.Enum && !string.IsNullOrEmpty(enumTypeFullName);
    }
}
