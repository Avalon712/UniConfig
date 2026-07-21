#region

using System.Collections.Generic;
using System.IO;

#endregion

namespace UniConfig.Editor
{
    internal static class ConfigRawStorage
    {
        private static readonly JsonConfigRawSerializer JsonSerializer = new();

        public static void SaveTable(ConfigTable table)
        {
            JsonSerializer.SaveTable(table);
        }

        public static ConfigTable LoadTable(string moduleName, string tableName)
        {
            return JsonSerializer.LoadTable(moduleName, tableName);
        }

        public static void LoadTableHeader(string moduleName, string tableName, ConfigTable target)
        {
            JsonSerializer.LoadTableHeader(moduleName, tableName, target);
        }

        public static int CountTableRowsOnDisk(string moduleName, string tableName)
        {
            return JsonSerializer.CountTableRowsOnDisk(moduleName, tableName);
        }

        /// <summary>
        /// 从磁盘只收集指定列的取值（不加载整表到内存）。
        /// </summary>
        public static HashSet<string> CollectColumnValues(string moduleName, string tableName, string fieldName)
        {
            return JsonSerializer.CollectColumnValues(moduleName, tableName, fieldName);
        }

        public static bool TryFindTableFile(string moduleName, string tableName, out string path)
        {
            path = ConfigPaths.GetTablePath(moduleName, tableName);
            return File.Exists(path);
        }

        public static void DeleteTableFile(string moduleName, string tableName)
        {
            foreach (string shardPath in ConfigPaths.GetExistingShardPaths(moduleName, tableName))
                if (File.Exists(shardPath))
                    File.Delete(shardPath);

            // 清理历史二进制原始数据（若仍存在）
            string legacyBin = Path.Combine(ConfigPaths.GetModuleDirectory(moduleName), tableName + ".ucbin");
            if (File.Exists(legacyBin))
                File.Delete(legacyBin);
        }

        public static void RenameTableFiles(string moduleName, string oldName, string newName)
        {
            List<string> oldShards = ConfigPaths.GetExistingShardPaths(moduleName, oldName);
            foreach (string oldPath in oldShards)
            {
                if (!ConfigPaths.TryParseTableShardFileName(Path.GetFileName(oldPath), out _, out int index))
                    continue;
                string newPath = ConfigPaths.GetTableShardPath(moduleName, newName, index);
                if (File.Exists(newPath))
                    File.Delete(newPath);
                File.Move(oldPath, newPath);
            }
        }

        public static void DeleteModuleDirectory(string moduleName)
        {
            string dir = ConfigPaths.GetModuleDirectory(moduleName);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}