#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    internal static class ConfigPaths
    {
        public const string JsonExtension = ".json";
        public const string ModuleMetaFileName = "module.json";

        /// <summary>
        /// 单个切片文件最多存储的数据行数（也是编辑器每页显示行数）。
        /// </summary>
        public const int ShardRowLimit = 500;

        private static readonly Regex ShardFileRegex =
            new(@"^(?<name>.+)\.(?<index>\d+)\.json$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        public static string ConfigRoot => Path.Combine(ProjectRoot, "Config");

        public static string GetModuleDirectory(string moduleName)
        {
            return Path.Combine(ConfigRoot, moduleName);
        }

        public static string GetModuleMetaPath(string moduleName)
        {
            return Path.Combine(GetModuleDirectory(moduleName), ModuleMetaFileName);
        }

        /// <summary>
        /// shardIndex=0 → Table.json；其余 → Table.{index}.json
        /// </summary>
        public static string GetTableShardPath(string moduleName, string tableName, int shardIndex)
        {
            string dir = GetModuleDirectory(moduleName);
            if (shardIndex <= 0)
                return Path.Combine(dir, tableName + JsonExtension);
            return Path.Combine(dir, $"{tableName}.{shardIndex}{JsonExtension}");
        }

        public static string GetTablePath(string moduleName, string tableName)
        {
            return GetTableShardPath(moduleName, tableName, 0);
        }

        public static void EnsureDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        public static bool TryParseTableShardFileName(string fileName, out string tableName, out int shardIndex)
        {
            tableName = null;
            shardIndex = 0;
            if (string.IsNullOrEmpty(fileName))
                return false;

            if (fileName.Equals(ModuleMetaFileName, StringComparison.OrdinalIgnoreCase))
                return false;

            Match match = ShardFileRegex.Match(fileName);
            if (match.Success)
            {
                tableName = match.Groups["name"].Value;
                shardIndex = int.Parse(match.Groups["index"].Value);
                return !string.IsNullOrEmpty(tableName) && shardIndex > 0;
            }

            if (fileName.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
            {
                tableName = Path.GetFileNameWithoutExtension(fileName);
                shardIndex = 0;
                return !string.IsNullOrEmpty(tableName);
            }

            return false;
        }

        public static List<string> DiscoverTableNames(string moduleName)
        {
            SortedSet<string> names = new(StringComparer.Ordinal);
            string dir = GetModuleDirectory(moduleName);
            if (!Directory.Exists(dir))
                return new List<string>();

            foreach (string file in Directory.GetFiles(dir, "*" + JsonExtension))
            {
                string fileName = Path.GetFileName(file);
                if (TryParseTableShardFileName(fileName, out string tableName, out _))
                    names.Add(tableName);
            }

            return new List<string>(names);
        }

        public static List<string> GetExistingShardPaths(string moduleName, string tableName)
        {
            List<string> paths = new();
            string dir = GetModuleDirectory(moduleName);
            if (!Directory.Exists(dir))
                return paths;

            string basePath = GetTableShardPath(moduleName, tableName, 0);
            if (File.Exists(basePath))
                paths.Add(basePath);

            foreach (string file in Directory.GetFiles(dir, tableName + ".*" + JsonExtension))
            {
                string fileName = Path.GetFileName(file);
                if (!TryParseTableShardFileName(fileName, out string name, out int index))
                    continue;
                if (!string.Equals(name, tableName, StringComparison.Ordinal))
                    continue;
                if (index <= 0)
                    continue;
                paths.Add(file);
            }

            paths.Sort((a, b) =>
            {
                TryParseTableShardFileName(Path.GetFileName(a), out _, out int ia);
                TryParseTableShardFileName(Path.GetFileName(b), out _, out int ib);
                return ia.CompareTo(ib);
            });

            return paths;
        }
    }
}