#region

using System.Collections.Generic;
using System.IO;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 导出到 Assets/Resources/.../configs.bytes（单文件多表）。
    /// </summary>
    public sealed class ResourcesExporter : ConfigExporter<ResourcesExporter>
    {
        public override void Export(IReadOnlyList<ExportTableData> tables)
        {
            string dir = UniConfigEditorSettings.instance.resourcesExportDirectory;
            if (string.IsNullOrWhiteSpace(dir))
                dir = "Assets/Resources";

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