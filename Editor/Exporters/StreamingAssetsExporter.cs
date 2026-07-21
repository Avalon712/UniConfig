#region

using System.Collections.Generic;
using System.IO;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 导出到 Assets/StreamingAssets/.../configs.bytes（单文件多表）。
    /// </summary>
    public sealed class StreamingAssetsExporter : ConfigExporter<StreamingAssetsExporter>
    {
        public override void Export(IReadOnlyList<ExportTableData> tables)
        {
            string dir = UniConfigEditorSettings.instance.streamingAssetsExportDirectory;
            if (string.IsNullOrWhiteSpace(dir))
                dir = "Assets/StreamingAssets";

            string path = ResolveExportFilePath(dir);
            ExportFileWriter.WriteBundleFile(path, tables);
        }

        private static string ResolveExportFilePath(string relativeDirectory)
        {
            string rel = relativeDirectory.Trim().Replace('\\', '/').Trim('/');
            string absoluteDir = Path.Combine(
                ConfigPaths.ProjectRoot,
                rel.Replace('/', Path.DirectorySeparatorChar));
            return Path.Combine(absoluteDir, ConfigBinaryProtocol.FileName);
        }
    }
}