#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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
        /// 行颜色标记可选颜色。只用于编辑器，不写入导出配置。
        /// </summary>
        public List<RowColorTag> rowColorTags = new();

        /// <summary>为 true 后不再把空列表补回默认 5 色，允许用户删光。</summary>
        public bool rowColorTagsInitialized;

        /// <summary>
        /// 每张表是否显示颜色标记列。没有记录时默认显示。
        /// </summary>
        public List<TableColorTagVisibility> colorTagVisibility = new();

        /// <summary>
        /// 每张表是否显示行顺序按钮。没有记录时不显示。
        /// </summary>
        public List<TableColorTagVisibility> rowReorderVisibility = new();

        /// <summary>
        /// 每张表是否显示行删除按钮。没有记录时不显示。
        /// </summary>
        public List<TableColorTagVisibility> rowDeleteVisibility = new();

        /// <summary>
        /// 每张表编辑器里一页显示多少行。没有记录时使用 <see cref="DefaultPageSize"/>。
        /// </summary>
        public List<TablePageSize> pageSizeEntries = new();

        /// <summary>
        /// 编辑器界面语言：0=中文，1=English（默认中文）
        /// </summary>
        public int editorLanguage;

        /// <summary>自动行号字段名。运行时 <c>GetConfigById</c> 的 Id 即该字段的值。</summary>
        public const string AutoRowIdFieldName = "id";

        /// <summary>未单独设置时，编辑器每页行数。</summary>
        public const int DefaultPageSize = 500;

        /// <summary>编辑器每页行数上限，避免一次画出过多行。</summary>
        public const int MaxPageSize = 10000;

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

        public void EnsureDefaultRowColorTags()
        {
            rowColorTags ??= new List<RowColorTag>();
            if (rowColorTagsInitialized)
                return;
            if (rowColorTags.Count == 0)
            {
                rowColorTags.Add(new RowColorTag { name = "红", color = new Color(0.86f, 0.30f, 0.30f) });
                rowColorTags.Add(new RowColorTag { name = "橙", color = new Color(0.90f, 0.55f, 0.22f) });
                rowColorTags.Add(new RowColorTag { name = "黄", color = new Color(0.86f, 0.76f, 0.28f) });
                rowColorTags.Add(new RowColorTag { name = "绿", color = new Color(0.32f, 0.70f, 0.40f) });
                rowColorTags.Add(new RowColorTag { name = "蓝", color = new Color(0.32f, 0.55f, 0.86f) });
            }

            rowColorTagsInitialized = true;
            SaveSettings();
        }

        public int GetPageSize(string moduleName, string tableName)
        {
            TablePageSize entry = FindPageSize(moduleName, tableName);
            if (entry == null || entry.pageSize < 1)
                return DefaultPageSize;
            return Mathf.Clamp(entry.pageSize, 1, MaxPageSize);
        }

        public void SetPageSize(string moduleName, string tableName, int pageSize)
        {
            if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(tableName))
                return;

            pageSize = Mathf.Clamp(pageSize, 1, MaxPageSize);
            pageSizeEntries ??= new List<TablePageSize>();
            TablePageSize entry = FindPageSize(moduleName, tableName);
            if (pageSize == DefaultPageSize)
            {
                if (entry == null)
                    return;
                pageSizeEntries.Remove(entry);
                SaveSettings();
                return;
            }

            if (entry == null)
            {
                entry = new TablePageSize { moduleName = moduleName, tableName = tableName };
                pageSizeEntries.Add(entry);
            }

            if (entry.pageSize == pageSize)
                return;
            entry.pageSize = pageSize;
            SaveSettings();
        }

        public void RenamePageSizeTable(string moduleName, string oldTableName, string newTableName)
        {
            TablePageSize entry = FindPageSize(moduleName, oldTableName);
            if (entry == null)
                return;
            entry.tableName = newTableName;
            SaveSettings();
        }

        public void RenamePageSizeModule(string oldModuleName, string newModuleName)
        {
            if (pageSizeEntries == null)
                return;
            bool changed = false;
            foreach (TablePageSize entry in pageSizeEntries)
            {
                if (entry == null || !string.Equals(entry.moduleName, oldModuleName, StringComparison.Ordinal))
                    continue;
                entry.moduleName = newModuleName;
                changed = true;
            }

            if (changed)
                SaveSettings();
        }

        public void RemovePageSize(string moduleName, string tableName)
        {
            if (pageSizeEntries == null)
                return;
            int removed = pageSizeEntries.RemoveAll(entry =>
                entry != null &&
                string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal) &&
                string.Equals(entry.tableName, tableName, StringComparison.Ordinal));
            if (removed > 0)
                SaveSettings();
        }

        public void RemovePageSizeForModule(string moduleName)
        {
            if (pageSizeEntries == null)
                return;
            int removed = pageSizeEntries.RemoveAll(entry =>
                entry != null && string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal));
            if (removed > 0)
                SaveSettings();
        }

        public bool GetShowRowDelete(string moduleName, string tableName)
        {
            TableColorTagVisibility entry = FindVisibility(rowDeleteVisibility, moduleName, tableName);
            return entry != null && entry.visible;
        }

        public void SetShowRowDelete(string moduleName, string tableName, bool visible)
        {
            SetVisibility(ref rowDeleteVisibility, moduleName, tableName, visible);
        }

        public void RenameRowDeleteTable(string moduleName, string oldTableName, string newTableName)
        {
            RenameVisibilityTable(rowDeleteVisibility, moduleName, oldTableName, newTableName);
        }

        public void RenameRowDeleteModule(string oldModuleName, string newModuleName)
        {
            RenameVisibilityModule(rowDeleteVisibility, oldModuleName, newModuleName);
        }

        public void RemoveRowDelete(string moduleName, string tableName)
        {
            RemoveVisibility(rowDeleteVisibility, moduleName, tableName);
        }

        public void RemoveRowDeleteForModule(string moduleName)
        {
            RemoveVisibilityForModule(rowDeleteVisibility, moduleName);
        }

        public bool GetShowRowReorder(string moduleName, string tableName)
        {
            TableColorTagVisibility entry = FindVisibility(rowReorderVisibility, moduleName, tableName);
            return entry != null && entry.visible;
        }

        public void SetShowRowReorder(string moduleName, string tableName, bool visible)
        {
            SetVisibility(ref rowReorderVisibility, moduleName, tableName, visible);
        }

        public void RenameRowReorderTable(string moduleName, string oldTableName, string newTableName)
        {
            RenameVisibilityTable(rowReorderVisibility, moduleName, oldTableName, newTableName);
        }

        public void RenameRowReorderModule(string oldModuleName, string newModuleName)
        {
            RenameVisibilityModule(rowReorderVisibility, oldModuleName, newModuleName);
        }

        public void RemoveRowReorder(string moduleName, string tableName)
        {
            RemoveVisibility(rowReorderVisibility, moduleName, tableName);
        }

        public void RemoveRowReorderForModule(string moduleName)
        {
            RemoveVisibilityForModule(rowReorderVisibility, moduleName);
        }

        public bool GetShowColorTag(string moduleName, string tableName)
        {
            TableColorTagVisibility entry = FindColorTagVisibility(moduleName, tableName);
            return entry == null || entry.visible;
        }

        public void SetShowColorTag(string moduleName, string tableName, bool visible)
        {
            if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(tableName))
                return;
            colorTagVisibility ??= new List<TableColorTagVisibility>();
            TableColorTagVisibility entry = FindColorTagVisibility(moduleName, tableName);
            if (entry == null)
            {
                entry = new TableColorTagVisibility { moduleName = moduleName, tableName = tableName };
                colorTagVisibility.Add(entry);
            }

            entry.visible = visible;
            SaveSettings();
        }

        public void RenameColorTagVisibilityTable(string moduleName, string oldTableName, string newTableName)
        {
            TableColorTagVisibility entry = FindColorTagVisibility(moduleName, oldTableName);
            if (entry == null)
                return;
            entry.tableName = newTableName;
            SaveSettings();
        }

        public void RenameColorTagVisibilityModule(string oldModuleName, string newModuleName)
        {
            if (colorTagVisibility == null)
                return;
            bool changed = false;
            foreach (TableColorTagVisibility entry in colorTagVisibility)
            {
                if (entry == null || !string.Equals(entry.moduleName, oldModuleName, StringComparison.Ordinal))
                    continue;
                entry.moduleName = newModuleName;
                changed = true;
            }

            if (changed)
                SaveSettings();
        }

        public void RemoveColorTagVisibility(string moduleName, string tableName)
        {
            if (colorTagVisibility == null)
                return;
            int removed = colorTagVisibility.RemoveAll(entry =>
                entry != null &&
                string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal) &&
                string.Equals(entry.tableName, tableName, StringComparison.Ordinal));
            if (removed > 0)
                SaveSettings();
        }

        public void RemoveColorTagVisibilityForModule(string moduleName)
        {
            if (colorTagVisibility == null)
                return;
            int removed = colorTagVisibility.RemoveAll(entry =>
                entry != null && string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal));
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

        private TablePageSize FindPageSize(string moduleName, string tableName)
        {
            if (pageSizeEntries == null || string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(tableName))
                return null;
            for (int i = 0; i < pageSizeEntries.Count; i++)
            {
                TablePageSize entry = pageSizeEntries[i];
                if (entry == null)
                    continue;
                if (string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal) &&
                    string.Equals(entry.tableName, tableName, StringComparison.Ordinal))
                    return entry;
            }

            return null;
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

        private TableColorTagVisibility FindColorTagVisibility(string moduleName, string tableName)
        {
            return FindVisibility(colorTagVisibility, moduleName, tableName);
        }

        private static TableColorTagVisibility FindVisibility(List<TableColorTagVisibility> entries, string moduleName,
            string tableName)
        {
            if (entries == null || string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(tableName))
                return null;
            for (int i = 0; i < entries.Count; i++)
            {
                TableColorTagVisibility entry = entries[i];
                if (entry == null)
                    continue;
                if (string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal) &&
                    string.Equals(entry.tableName, tableName, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private void SetVisibility(ref List<TableColorTagVisibility> entries, string moduleName, string tableName,
            bool visible)
        {
            if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(tableName))
                return;
            entries ??= new List<TableColorTagVisibility>();
            TableColorTagVisibility entry = FindVisibility(entries, moduleName, tableName);
            if (entry == null)
            {
                entry = new TableColorTagVisibility { moduleName = moduleName, tableName = tableName };
                entries.Add(entry);
            }

            entry.visible = visible;
            SaveSettings();
        }

        private void RenameVisibilityTable(List<TableColorTagVisibility> entries, string moduleName,
            string oldTableName, string newTableName)
        {
            TableColorTagVisibility entry = FindVisibility(entries, moduleName, oldTableName);
            if (entry == null)
                return;
            entry.tableName = newTableName;
            SaveSettings();
        }

        private void RenameVisibilityModule(List<TableColorTagVisibility> entries, string oldModuleName,
            string newModuleName)
        {
            if (entries == null)
                return;
            bool changed = false;
            foreach (TableColorTagVisibility entry in entries)
            {
                if (entry == null || !string.Equals(entry.moduleName, oldModuleName, StringComparison.Ordinal))
                    continue;
                entry.moduleName = newModuleName;
                changed = true;
            }

            if (changed)
                SaveSettings();
        }

        private void RemoveVisibility(List<TableColorTagVisibility> entries, string moduleName, string tableName)
        {
            if (entries == null)
                return;
            int removed = entries.RemoveAll(entry =>
                entry != null &&
                string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal) &&
                string.Equals(entry.tableName, tableName, StringComparison.Ordinal));
            if (removed > 0)
                SaveSettings();
        }

        private void RemoveVisibilityForModule(List<TableColorTagVisibility> entries, string moduleName)
        {
            if (entries == null)
                return;
            int removed = entries.RemoveAll(entry =>
                entry != null && string.Equals(entry.moduleName, moduleName, StringComparison.Ordinal));
            if (removed > 0)
                SaveSettings();
        }
    }

    [Serializable]
    public sealed class RowColorTag
    {
        public string name;
        public Color color = Color.white;
    }

    [Serializable]
    public sealed class TableColorTagVisibility
    {
        public string moduleName;
        public string tableName;
        public bool visible = true;
    }

    [Serializable]
    public sealed class TablePageSize
    {
        public string moduleName;
        public string tableName;
        public int pageSize = UniConfigEditorSettings.DefaultPageSize;
    }

    [Serializable]
    public sealed class TableVisibleColumns
    {
        public string moduleName;
        public string tableName;
        public List<string> columns = new();
    }
}