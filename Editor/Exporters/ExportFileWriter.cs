#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 写出单文件多表 configs.bytes，格式与运行时 <see cref="ConfigBinaryProtocol" /> 对齐。
    /// </summary>
    internal static class ExportFileWriter
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static void WriteBundleFile(string filePath, IReadOnlyList<ExportTableData> tables)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("filePath is null or empty", nameof(filePath));
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            // 空列表不落盘，避免把其它模块已导出的同路径文件覆盖成空文件
            if (tables.Count == 0)
            {
                Debug.Log($"[UniConfig] 跳过导出（0 张表）: {filePath}");
                return;
            }

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                ConfigPaths.EnsureDirectory(directory);

            List<ExportTableData> ordered = OrderTables(tables);
            using (FileStream stream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new(stream, Utf8, false))
            {
                if (UniConfigEditorSettings.instance.UseArraySerialize)
                {
                    int maxPayload = 0;
                    for (int t = 0; t < ordered.Count; t++)
                    {
                        int size = PayloadBytes(ordered[t]);
                        if (size > maxPayload)
                            maxPayload = size;
                    }

                    writer.Write(ConfigBinaryProtocol.HeaderMagic);
                    writer.Write(maxPayload);
                }

                for (int t = 0; t < ordered.Count; t++)
                {
                    ExportTableData table = ordered[t];
                    writer.Write(ConfigBinaryProtocol.Magic);
                    writer.Write(table.TableId);
                    if (table.Kind == ExportBlobKind.Array)
                        WriteSingle(writer, table, ConfigBinaryProtocol.RowCountArray);
                    else
                    {
                        writer.Write(table.Count);
                        for (int i = 0; i < table.Count; i++)
                        {
                            byte[] payload = table.GetBytes(i) ?? Array.Empty<byte>();
                            writer.Write(payload.Length);
                            if (payload.Length > 0)
                                writer.Write(payload);
                        }
                    }
                }
            }

            // 不调用 AssetDatabase.Refresh / ImportAsset：
            // 在写出后立即 Force 导入会导致 Infinite Import Loop，且 Resources/StreamingAssets 均可能报错。
            // Unity 会在失焦/切回编辑器时自行发现新文件；运行时 StreamingAssets 按路径读文件，无需导入。
            Debug.Log($"[UniConfig] 导出完成: {filePath}（{tables.Count} 张表）");
        }

        private static List<ExportTableData> OrderTables(IReadOnlyList<ExportTableData> tables)
        {
            List<ExportTableData> ordered = new(tables.Count);
            for (int t = 0; t < tables.Count; t++)
            {
                ExportTableData table = tables[t];
                if (table != null)
                    ordered.Add(table);
            }

            return ordered;
        }

        private static int PayloadBytes(ExportTableData table)
        {
            if (table.Kind == ExportBlobKind.PerRow)
            {
                int max = 0;
                for (int i = 0; i < table.Count; i++)
                {
                    byte[] payload = table.GetBytes(i);
                    if (payload != null && payload.Length > max)
                        max = payload.Length;
                }

                return max;
            }

            return table.SinglePayload?.Length ?? 0;
        }

        private static void WriteSingle(BinaryWriter writer, ExportTableData table, int marker)
        {
            writer.Write(marker);
            byte[] payload = table.SinglePayload ?? Array.Empty<byte>();
            writer.Write(payload.Length);
            if (payload.Length > 0)
                writer.Write(payload);
        }
    }
}