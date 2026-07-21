#region

using System.Collections.Generic;

#endregion

namespace UniConfig.Editor
{
    public sealed class ConfigModule
    {
        /// <summary>
        /// 将原始数据导出为二进制数据的导出器(实现IConfigExporter接口的类)
        /// </summary>
        public string exporterTypeFullName;

        /// <summary>
        /// 模块名称
        /// </summary>
        public string moduleName;

        /// <summary>
        /// 当前模块的所有配置表
        /// </summary>
        public List<ConfigTable> tables = new();

        public ConfigTable FindTable(string tableName)
        {
            for (int i = 0; i < tables.Count; i++)
                if (tables[i].tableName == tableName)
                    return tables[i];
            return null;
        }
    }
}