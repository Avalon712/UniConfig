#region

using System;
using System.Collections.Generic;
using UnityEditor;

#endregion

namespace UniConfig.Editor
{
    [FilePath("ProjectSettings/UniConfigEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class UniConfigEditorSettings : ScriptableSingleton<UniConfigEditorSettings>
    {
        /// <summary>
        /// Cfg.g.cs 存放目录（必须在 Assets 目录下）
        /// </summary>
        public string csharpExportDirectory = "Assets/Scripts/Config";

        /// <summary>
        /// 生成配置类的根命名空间
        /// </summary>
        public string rootNamespace = "Game.Config";

        /// <summary>
        /// Resources 导出目录（相对项目根，须位于 Assets/Resources 下），文件名固定为 configs.bytes
        /// </summary>
        public string resourcesExportDirectory = "Assets/Resources";

        /// <summary>
        /// StreamingAssets 导出目录（相对项目根，须位于 Assets/StreamingAssets 下），文件名固定为 configs.bytes
        /// </summary>
        public string streamingAssetsExportDirectory = "Assets/StreamingAssets";

        /// <summary>
        /// 自定义枚举类型完整限定名列表（出现在字段类型下拉中）。
        /// </summary>
        public List<string> customEnumTypeFullNames = new();

        /// <summary>
        /// 窗口预设
        /// </summary>
        public string prefs;

        /// <summary>
        /// 关闭配置编辑器窗口时自动保存一次
        /// </summary>
        public bool autoSaveOnClose = true;

        /// <summary>
        /// 为每张表自动维护 int 字段 <see cref="AutoRowIdFieldName"/>，值等于行号（从 1 开始）。
        /// </summary>
        public bool autoRowIdField;

        /// <summary>
        /// 每张表的「显示列」选择。下次打开该表时恢复。
        /// </summary>
        public List<TableVisibleColumns> visibleColumnEntries = new();

        /// <summary>
        /// 编辑器界面语言：0=中文，1=English（默认中文）
        /// </summary>
        public int editorLanguage;

        /// <summary>自动行号字段名。运行时 <c>GetConfigById</c> 的 Id 即该字段的值。</summary>
        public const string AutoRowIdFieldName = "id";

        public bool IsAutoRowIdField(string fieldName)
        {
            return autoRowIdField &&
                   string.Equals(fieldName, AutoRowIdFieldName, StringComparison.Ordinal);
        }

        public bool TryGetVisibleColumns(string moduleName, string tableName, out List<string> columns)
        {
            columns = null;
            TableVisibleColumns entry = FindVisibleColumns(moduleName, tableName);
            if (entry == null)
                return false;
            columns = entry.columns ?? new List<string>();
            return true;
        }

        public void SetVisibleColumns(string moduleName, string tableName, IEnumerable<string> columns)
        {
            if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(tableName))
                return;

            visibleColumnEntries ??= new List<TableVisibleColumns>();
            TableVisibleColumns entry = FindVisibleColumns(moduleName, tableName);
            if (entry == null)
            {
                entry = new TableVisibleColumns { moduleName = moduleName, tableName = tableName };
                visibleColumnEntries.Add(entry);
            }

            entry.columns = columns == null ? new List<string>() : new List<string>(columns);
            SaveSettings();
        }

        public void RenameVisibleColumnsTable(string moduleName, string oldTableName, string newTableName)
        {
            TableVisibleColumns entry = FindVisibleColumns(moduleName, oldTableName);
            if (entry == null)
                return;
            entry.tableName = newTableName;
            SaveSettings();
        }

        public void RenameVisibleColumnsModule(string oldModuleName, string newModuleName)
        {
            if (visibleColumnEntries == null)
                return;
            bool changed = false;
            foreach (TableVisibleColumns entry in visibleColumnEntries)
            {
                if (entry == null || !string.Equals(entry.moduleName, oldModuleName, StringComparison.Ordinal))
                    continue;
                entry.moduleName = newModuleName;
                changed = true;
            }

            if (changed)
                SaveSettings();
        }

        public void RemoveVisibleColumns(string moduleName, string tableName)
        {
            if (visibleColumnEntries == null)
                return;
            int removed = visibleColumnEntries.RemoveAll(entry =>
                entry != null &&
                string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal) &&
                string.Equals(entry.tableName, tableName, StringComparison.Ordinal));
            if (removed > 0)
                SaveSettings();
        }

        public void RemoveVisibleColumnsForModule(string moduleName)
        {
            if (visibleColumnEntries == null)
                return;
            int removed = visibleColumnEntries.RemoveAll(entry =>
                entry != null && string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal));
            if (removed > 0)
                SaveSettings();
        }

        public void SaveSettings()
        {
            Save(true);
        }

        private TableVisibleColumns FindVisibleColumns(string moduleName, string tableName)
        {
            if (visibleColumnEntries == null || string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(tableName))
                return null;
            for (int i = 0; i < visibleColumnEntries.Count; i++)
            {
                TableVisibleColumns entry = visibleColumnEntries[i];
                if (entry == null)
                    continue;
                if (string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal) &&
                    string.Equals(entry.tableName, tableName, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }
    }

    [Serializable]
    public sealed class TableVisibleColumns
    {
        public string moduleName;
        public string tableName;
        public List<string> columns = new();
    }
}