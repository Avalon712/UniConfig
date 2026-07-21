#region

using System;

#endregion

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

        public ConfigField()
        {
        }

        public ConfigField(string name, SupportableFieldType type)
        {
            this.name = name;
            this.type = type;
        }
    }
}