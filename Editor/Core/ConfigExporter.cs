#region

using System.Collections.Generic;

#endregion

namespace UniConfig.Editor
{
    public interface IConfigExporter
    {
        /// <summary>
        /// 导出全部表到该导出器对应的单文件路径。
        /// </summary>
        void Export(IReadOnlyList<ExportTableData> tables);
    }

    public abstract class ConfigExporter<T> : IConfigExporter where T : ConfigExporter<T>, new()
    {
        public abstract void Export(IReadOnlyList<ExportTableData> tables);
    }
}