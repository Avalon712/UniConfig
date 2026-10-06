#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    public sealed class ConfigEditorWindow : EditorWindow
    {
        private const float TreePaneWidth = 280f;
        private const float ColWidth = 110f;
        private const float RowHeight = 20f;
        private const float HeaderRowHeight = 18f;
        private const float IndexColWidth = 40f;
        private const float TreeItemHeight = 20f;
        /// <summary>表格拉内容右侧留白，避免最大横向滚动时最后一列被垂直滚动条挡住。</summary>
        private const float GridScrollPadX = 8f;

        // 异步搜索
        private const double SearchDebounceSeconds = 0.2d;
        private const string UiStateModuleKey = "UniConfig.UI.SelectedModule";
        private const string UiStateTableKey = "UniConfig.UI.SelectedTable";
        private const string UiStateExpandedKey = "UniConfig.UI.ExpandedModules";
        private const string UiStatePageKey = "UniConfig.UI.DataPageIndex";
        private const string UiStateTreeSearchKey = "UniConfig.UI.TreeSearch";
        private const string UiStateDataSearchKey = "UniConfig.UI.DataSearch";
        private const string RenameControlName = "UniConfigTreeRename";
        private readonly HashSet<string> _columnsToDelete = new(StringComparer.Ordinal);
        private readonly HashSet<string> _expandedModules = new(StringComparer.Ordinal);
        private readonly HashSet<string> _searchColumns = new(StringComparer.Ordinal);
        private readonly HashSet<string> _visibleColumns = new(StringComparer.Ordinal);

        private GUIStyle _cellStyle;
        private int _dataPageIndex;
        private string _dataSearch = string.Empty;
        private string _deleteRowInput = string.Empty;
        private bool _dirty;
        private List<int> _displayRowIndices = new();
        private Vector2 _gridScroll;
        private GUIStyle _headerStyle;
        private bool _isSearching;
        private GUIStyle _paneTitleStyle;
        private int _readyItemCount = -1;
        private string _readySearchPattern = string.Empty;
        private string _renameBuffer;
        private bool _renameFocusPending;
        private string _renameModule;
        private string _renameSyncKey;
        private string _renameTable;
        private RenameTarget _renameTarget;
        private bool _searchColumnsInitialized;
        private bool _searchDirty;
        private double _searchDirtyAt;
        private int _searchJobId;

        private string _selectedModule;
        private string _selectedTable;
        private string _status = string.Empty;
        private bool _stylesReady;
        private GUIStyle _treeItemStyle;

        private Vector2 _treeScroll;

        private string _treeSearch = string.Empty;
        private bool _visibleColumnsInitialized;

        private Color GridLine => EditorGUIUtility.isProSkin
            ? new Color(0.12f, 0.12f, 0.12f, 1f)
            : new Color(0.65f, 0.65f, 0.65f, 1f);

        private Color HeaderBg => EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f, 1f)
            : new Color(0.82f, 0.82f, 0.82f, 1f);

        private Color RowEven => EditorGUIUtility.isProSkin
            ? new Color(0.19f, 0.19f, 0.19f, 1f)
            : new Color(0.96f, 0.96f, 0.96f, 1f);

        private Color RowOdd => EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.16f, 0.16f, 1f)
            : Color.white;

        private Color RowMatch => EditorGUIUtility.isProSkin
            ? new Color(0.28f, 0.32f, 0.18f, 1f)
            : new Color(0.90f, 0.95f, 0.75f, 1f);

        private Color SelectedItem => EditorGUIUtility.isProSkin
            ? new Color(0.17f, 0.36f, 0.53f, 1f)
            : new Color(0.24f, 0.48f, 0.90f, 0.35f);

        private Color FkErrorCell => EditorGUIUtility.isProSkin
            ? new Color(0.42f, 0.22f, 0.22f, 1f)
            : new Color(1f, 0.78f, 0.78f, 1f);

        private Color FkErrorBorder => EditorGUIUtility.isProSkin
            ? new Color(0.92f, 0.55f, 0.55f, 1f)
            : new Color(0.82f, 0.38f, 0.38f, 1f);

        private Color FkErrorText => EditorGUIUtility.isProSkin
            ? new Color(0.95f, 0.62f, 0.62f, 1f)
            : new Color(0.78f, 0.28f, 0.28f, 1f);

        private void OnEnable()
        {
            minSize = new Vector2(800, 360);
            EditorConfigMgr.EnsureLoaded();
            titleContent = Content("UniConfig", null, "ScriptableObject Icon", "d_ScriptableObject Icon");
            // 域重载后延迟恢复，确保模块列表已就绪
            EditorApplication.delayCall += RestoreUiStateAfterReload;
        }

        private void RestoreUiStateAfterReload()
        {
            // 窗口可能已关闭
            if (this == null) return;
            RestoreUiState();
            Repaint();
        }

        private void OnDisable()
        {
            PersistUiState();
            EditorApplication.delayCall -= RestoreUiStateAfterReload;
            if (UniConfigEditorSettings.instance.autoSaveOnClose && _dirty)
                try
                {
                    EditorConfigMgr.SaveAll();
                    _dirty = false;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
        }

        private void PersistUiState()
        {
            SessionState.SetString(UiStateModuleKey, _selectedModule ?? string.Empty);
            SessionState.SetString(UiStateTableKey, _selectedTable ?? string.Empty);
            SessionState.SetString(UiStateExpandedKey, string.Join("\n", _expandedModules));
            SessionState.SetInt(UiStatePageKey, _dataPageIndex);
            SessionState.SetString(UiStateTreeSearchKey, _treeSearch ?? string.Empty);
            SessionState.SetString(UiStateDataSearchKey, _dataSearch ?? string.Empty);
        }

        private void RestoreUiState()
        {
            EditorConfigMgr.EnsureLoaded();

            string expanded = SessionState.GetString(UiStateExpandedKey, string.Empty);
            _expandedModules.Clear();
            if (!string.IsNullOrEmpty(expanded))
            {
                string[] parts = expanded.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                    _expandedModules.Add(parts[i]);
            }

            _treeSearch = SessionState.GetString(UiStateTreeSearchKey, string.Empty);
            _dataSearch = SessionState.GetString(UiStateDataSearchKey, string.Empty);

            string moduleName = SessionState.GetString(UiStateModuleKey, string.Empty);
            string tableName = SessionState.GetString(UiStateTableKey, string.Empty);
            int page = SessionState.GetInt(UiStatePageKey, 0);

            if (string.IsNullOrEmpty(moduleName))
                return;

            ConfigModule module = EditorConfigMgr.GetModule(moduleName);
            if (module == null)
                return;

            _expandedModules.Add(moduleName);

            if (!string.IsNullOrEmpty(tableName) && module.FindTable(tableName) != null)
            {
                // 直接恢复选中并加载，避免 SelectTable 把页码清零后再设
                _selectedModule = moduleName;
                _selectedTable = tableName;
                _dataPageIndex = Mathf.Max(0, page);
                _searchColumnsInitialized = false;
                _visibleColumnsInitialized = false;
                ClearGridFocus();
                RequestLoadCurrentTable(module.FindTable(tableName));
                MarkSearchDirty();
            }
            else
            {
                _selectedModule = moduleName;
                _selectedTable = null;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 必须在 TextField 之前拦截，否则 Enter 会被输入框吞掉
            if (TryHandleRenameHotkeys())
                return;

            DrawMainToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawTreePane();
            DrawVerticalSplitter();
            DrawDataPane();
            EditorGUILayout.EndHorizontal();

            DrawStatusBar();
            HandleRenameClickOutside();
        }

        [MenuItem("UniConfig/Config Editor")]
        public static void Open()
        {
            ConfigEditorWindow window = GetWindow<ConfigEditorWindow>("UniConfig");
            window.minSize = new Vector2(800, 360);
            window.Show();
        }

        // 内联重命名（类似 Hierarchy）
        private enum RenameTarget
        {
            None,
            Module,
            Table
        }

        #region Toolbar

        private void DrawMainToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (ToolbarButton(UniConfigLoc.T("load"), UniConfigLoc.T("load.tip"), "Refresh", "d_Refresh"))
            {
                EditorConfigMgr.Reload();
                _status = UniConfigLoc.T("status.loaded_config");
                Repaint();
            }

            if (ToolbarButton(UniConfigLoc.T("save"), UniConfigLoc.T("save_all.tip"), "SaveAs", "d_SaveAs"))
                TryAction(() =>
                {
                    int removed = EditorConfigMgr.SaveAll();
                    _dirty = false;
                    _status = removed > 0
                        ? UniConfigLoc.F("status.saved_all_skip", removed)
                        : UniConfigLoc.T("status.saved_all");
                });

            if (ToolbarButton(UniConfigLoc.T("gen_cs"), UniConfigLoc.T("gen_cs.tip"), "cs Script Icon",
                    "d_cs Script Icon"))
                TryAction(() =>
                {
                    PersistUiState();
                    string path = EditorConfigMgr.GenerateCsharp();
                    _status = UniConfigLoc.F("status.generated", path);
                });

            if (ToolbarButton(UniConfigLoc.T("export"), UniConfigLoc.T("export.tip"), "Prefab Icon", "d_Prefab Icon"))
                TryAction(() =>
                {
                    PersistUiState();
                    EditorConfigMgr.ExportConfigs();
                    _status = UniConfigLoc.T("status.exported");
                });

            EditorGUI.BeginChangeCheck();
            bool autoSave = EditorGUILayout.ToggleLeft(
                new GUIContent(UniConfigLoc.T("auto_save"), UniConfigLoc.T("auto_save.tip")),
                UniConfigEditorSettings.instance.autoSaveOnClose,
                GUILayout.Width(UniConfigLoc.IsChinese ? 72 : 88));
            if (EditorGUI.EndChangeCheck())
            {
                UniConfigEditorSettings.instance.autoSaveOnClose = autoSave;
                UniConfigEditorSettings.instance.SaveSettings();
            }

            if (ToolbarButton(UniConfigLoc.T("settings"), UniConfigLoc.T("settings.tip"), "Settings", "d_Settings",
                    "SettingsIcon"))
                UniConfigSettingsWindow.Open();

            if (_dirty)
                GUILayout.Label(UniConfigLoc.T("unsaved"), EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            GUILayout.Label(UniConfigLoc.T("language"), EditorStyles.miniLabel,
                GUILayout.Width(UniConfigLoc.IsChinese ? 28 : 52));
            EditorGUI.BeginChangeCheck();
            int lang = EditorGUILayout.Popup(
                (int)UniConfigLoc.Language,
                new[] { "中文", "English" },
                EditorStyles.toolbarPopup,
                GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                UniConfigLoc.Language = (UniConfigLanguage)lang;
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        private static bool ToolbarButton(string text, string tooltip, params string[] icons)
        {
            GUIContent content = Content(text, tooltip, icons);
            // 不要对带大图图标的 GUIContent 做 CalcSize，否则会按贴图原始尺寸把按钮撑得极宽
            float width = EditorStyles.toolbarButton.CalcSize(new GUIContent(text)).x;
            if (content.image != null)
                width += 18f;
            width = Mathf.Max(width + 8f, 36f);
            return GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.Width(width),
                GUILayout.ExpandWidth(false));
        }

        #endregion

        #region Tree Pane

        private void DrawTreePane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(TreePaneWidth));
            DrawModuleToolbar();
            DrawTreeSearchBar();

            _treeScroll = EditorGUILayout.BeginScrollView(_treeScroll, GUILayout.ExpandHeight(true));

            foreach (ConfigModule module in EditorConfigMgr.Modules.Values.OrderBy(m => m.moduleName))
            {
                bool moduleMatch = MatchRegex(module.moduleName, _treeSearch);
                List<ConfigTable> matchedTables = GetMatchedTables(module, out bool anyTableMatch);
                if (!string.IsNullOrEmpty(_treeSearch) && !moduleMatch && !anyTableMatch)
                    continue;

                bool forceExpand = !string.IsNullOrEmpty(_treeSearch) && anyTableMatch;
                bool expanded = forceExpand || _expandedModules.Contains(module.moduleName);
                DrawModuleItem(module, expanded, forceExpand);

                if (!expanded) continue;

                IEnumerable<ConfigTable> tablesToShow = string.IsNullOrEmpty(_treeSearch) || moduleMatch
                    ? module.tables.OrderBy(t => t.tableName)
                    : matchedTables;

                foreach (ConfigTable table in tablesToShow)
                    DrawTableItem(module, table);
            }

            GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawModuleToolbar()
        {
            bool hasModule = !string.IsNullOrEmpty(_selectedModule);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (ToolbarButton(UniConfigLoc.T("new_module"), UniConfigLoc.T("new_module.tip"), "Toolbar Plus",
                    "d_Toolbar Plus"))
                CreateModuleAndRename();

            using (new EditorGUI.DisabledScope(!hasModule))
            {
                if (ToolbarButton(UniConfigLoc.T("add_table"), UniConfigLoc.T("add_table.tip"), "Toolbar Plus",
                        "d_Toolbar Plus"))
                    CreateTableAndRename(_selectedModule);

                if (ToolbarButton(UniConfigLoc.T("rename"), UniConfigLoc.T("rename_module.tip"), "editicon.sml",
                        "d_editicon.sml"))
                    BeginRenameModule(_selectedModule);

                if (ToolbarButton(UniConfigLoc.T("delete"), UniConfigLoc.T("delete_module.tip"), "TreeEditor.Trash",
                        "d_TreeEditor.Trash"))
                    DeleteSelectedModule();
            }

            EditorGUILayout.EndHorizontal();

            // 导出器始终显示；未选中模块时禁用
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(!hasModule))
            {
                GUILayout.Label(UniConfigLoc.T("exporter"), GUILayout.Width(UniConfigLoc.IsChinese ? 40 : 56));
                string[] exporterFullNames = EditorConfigMgr.ExporterTypeNames.ToArray();
                string[] exporterLabels = exporterFullNames.Length > 0
                    ? exporterFullNames.Select(GetExporterLabel).ToArray()
                    : new[] { UniConfigLoc.T("no_exporter") };

                ConfigModule module = CurrentModule();
                int exporterIndex = 0;
                if (module != null && exporterFullNames.Length > 0)
                    exporterIndex = Mathf.Max(0, Array.IndexOf(exporterFullNames, module.exporterTypeFullName));

                int newIndex = EditorGUILayout.Popup(exporterIndex, exporterLabels, EditorStyles.toolbarPopup);
                if (hasModule && module != null && exporterFullNames.Length > 0 && newIndex != exporterIndex)
                {
                    EditorConfigMgr.SetModuleExporter(module.moduleName, exporterFullNames[newIndex]);
                    _dirty = true;
                    _status = UniConfigLoc.F("status.exporter_updated", exporterLabels[newIndex]);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTreeSearchBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            // 不额外画搜索图标：toolbarSearchField 自带图标，避免与列表首项重叠
            _treeSearch = EditorGUILayout.TextField(_treeSearch ?? string.Empty, EditorStyles.toolbarSearchField);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawModuleItem(ConfigModule module, bool expanded, bool forceExpand)
        {
            Rect rect = GUILayoutUtility.GetRect(0, TreeItemHeight, GUILayout.ExpandWidth(true));
            bool selected = _selectedModule == module.moduleName && string.IsNullOrEmpty(_selectedTable);

            if (Event.current.type == EventType.Repaint)
                if (selected || (_selectedModule == module.moduleName && !string.IsNullOrEmpty(_selectedTable)))
                {
                    // 选中模块或其子表时，模块行用稍弱高亮
                    Color bg = selected ? SelectedItem : SelectedItem * new Color(1, 1, 1, 0.35f);
                    if (!selected) bg.a = 0.25f;
                    EditorGUI.DrawRect(rect,
                        selected ? SelectedItem : new Color(SelectedItem.r, SelectedItem.g, SelectedItem.b, 0.2f));
                }

            // folder icon：点击可折叠/展开
            Rect foldRect = new(rect.x + 2, rect.y + 2, 16, 16);
            Texture2D folderIcon = LoadIcon(expanded ? "FolderOpened Icon" : "Folder Icon");
            if (folderIcon != null)
                GUI.DrawTexture(foldRect, folderIcon, ScaleMode.ScaleToFit);

            Rect labelRect = new(rect.x + 22, rect.y, rect.width - 24, rect.height);

            if (_renameTarget == RenameTarget.Module && _renameModule == module.moduleName)
                DrawInlineRename(labelRect);
            else if (Event.current.type == EventType.Repaint)
                _treeItemStyle.Draw(labelRect, module.moduleName, false, false, selected, false);

            if (_renameTarget == RenameTarget.Module && _renameModule == module.moduleName)
                return;

            if (forceExpand)
                _expandedModules.Add(module.moduleName);

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                if (foldRect.Contains(Event.current.mousePosition))
                {
                    if (_expandedModules.Contains(module.moduleName))
                        _expandedModules.Remove(module.moduleName);
                    else
                        _expandedModules.Add(module.moduleName);
                    _selectedModule = module.moduleName;
                    _selectedTable = null;
                    PersistUiState();
                }
                else
                {
                    SelectModule(module.moduleName, false);
                }

                Event.current.Use();
            }
        }

        private void DrawTableItem(ConfigModule module, ConfigTable table)
        {
            Rect rect = GUILayoutUtility.GetRect(0, TreeItemHeight, GUILayout.ExpandWidth(true));
            bool selected = _selectedModule == module.moduleName && _selectedTable == table.tableName;

            if (Event.current.type == EventType.Repaint && selected)
                EditorGUI.DrawRect(rect, SelectedItem);

            Rect iconRect = new(rect.x + 22, rect.y + 2, 16, 16);
            Texture2D icon = LoadIcon("TextAsset Icon");
            if (icon != null)
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

            Rect labelRect = new(rect.x + 42, rect.y, rect.width - 44, rect.height);
            string label = table.tableName;

            if (_renameTarget == RenameTarget.Table &&
                _renameModule == module.moduleName &&
                _renameTable == table.tableName)
                DrawInlineRename(labelRect);
            else if (Event.current.type == EventType.Repaint)
                _treeItemStyle.Draw(labelRect, label, false, false, selected, false);

            if (_renameTarget == RenameTarget.Table &&
                _renameModule == module.moduleName &&
                _renameTable == table.tableName)
                return;

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                SelectTable(module.moduleName, table.tableName);
                Event.current.Use();
            }
        }

        private void CreateModuleAndRename()
        {
            TryAction(() =>
            {
                string name = EditorConfigMgr.GenerateUniqueModuleName();
                ConfigModule module = EditorConfigMgr.CreateModule(name);
                _expandedModules.Add(module.moduleName);
                SelectModule(module.moduleName, false);
                BeginRenameModule(module.moduleName);
                _status = UniConfigLoc.F("status.module_created", module.moduleName);
            });
        }

        private void CreateTableAndRename(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                EditorUtility.DisplayDialog("UniConfig", UniConfigLoc.T("select_module_first"), UniConfigLoc.T("ok"));
                return;
            }

            TryAction(() =>
            {
                string name = EditorConfigMgr.GenerateUniqueTableName(moduleName);
                ConfigTable table = EditorConfigMgr.CreateTable(moduleName, name);
                _expandedModules.Add(moduleName);
                SelectTable(moduleName, table.tableName);
                BeginRenameTable(moduleName, table.tableName);
                _status = UniConfigLoc.F("status.table_created", moduleName, table.tableName);
            });
        }

        private void DeleteSelectedModule()
        {
            if (string.IsNullOrEmpty(_selectedModule)) return;
            string moduleName = _selectedModule;
            if (!EditorUtility.DisplayDialog(UniConfigLoc.T("delete_module.title"),
                    UniConfigLoc.F("delete_module.msg", moduleName),
                    UniConfigLoc.T("delete"), UniConfigLoc.T("cancel")))
                return;

            TryAction(() =>
            {
                EditorConfigMgr.DeleteModule(moduleName);
                _expandedModules.Remove(moduleName);
                if (_selectedModule == moduleName)
                {
                    _selectedModule = null;
                    _selectedTable = null;
                }

                _status = UniConfigLoc.T("status.module_deleted");
            });
        }

        private void DeleteSelectedTable()
        {
            if (string.IsNullOrEmpty(_selectedModule) || string.IsNullOrEmpty(_selectedTable)) return;
            string moduleName = _selectedModule;
            string tableName = _selectedTable;
            if (!EditorUtility.DisplayDialog(UniConfigLoc.T("delete_table.title"),
                    UniConfigLoc.F("delete_table.msg", moduleName, tableName),
                    UniConfigLoc.T("delete"), UniConfigLoc.T("cancel")))
                return;

            TryAction(() =>
            {
                EditorConfigMgr.DeleteTable(moduleName, tableName);
                if (_selectedModule == moduleName && _selectedTable == tableName)
                    _selectedTable = null;
                _status = UniConfigLoc.T("status.table_deleted");
            });
        }

        private void BeginRenameModule(string moduleName)
        {
            _renameTarget = RenameTarget.Module;
            _renameModule = moduleName;
            _renameTable = null;
            _renameBuffer = moduleName;
            _renameFocusPending = true;
            Repaint();
        }

        private void BeginRenameTable(string moduleName, string tableName)
        {
            _renameTarget = RenameTarget.Table;
            _renameModule = moduleName;
            _renameTable = tableName;
            _renameBuffer = tableName;
            _renameFocusPending = true;
            Repaint();
        }

        private void DrawInlineRename(Rect rect)
        {
            if (_renameFocusPending && Event.current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(RenameControlName);
                _renameFocusPending = false;
            }

            GUI.SetNextControlName(RenameControlName);
            _renameBuffer = EditorGUI.TextField(rect, _renameBuffer ?? string.Empty);
        }

        /// <summary>
        /// 处理重命名快捷键。返回 true 表示已提交/取消并应结束本帧 GUI。
        /// </summary>
        private bool TryHandleRenameHotkeys()
        {
            if (_renameTarget == RenameTarget.None)
                return false;

            Event e = Event.current;
            if (e.type != EventType.KeyDown)
                return false;

            bool isEnter = e.keyCode == KeyCode.Return ||
                           e.keyCode == KeyCode.KeypadEnter ||
                           e.character == '\n' ||
                           e.character == '\r';
            bool isEscape = e.keyCode == KeyCode.Escape;

            if (!isEnter && !isEscape)
                return false;

            e.Use();
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);

            if (isEscape)
                CancelRename();
            else
                CommitRename();

            Repaint();
            return true;
        }

        private void CommitRename()
        {
            if (_renameTarget == RenameTarget.None) return;

            RenameTarget target = _renameTarget;
            string module = _renameModule;
            string table = _renameTable;
            string newName = (_renameBuffer ?? string.Empty).Trim();
            CancelRename();

            if (string.IsNullOrEmpty(newName))
            {
                _status = UniConfigLoc.T("status.name_empty");
                return;
            }

            TryAction(() =>
            {
                if (target == RenameTarget.Module)
                {
                    if (newName != module)
                    {
                        EditorConfigMgr.RenameModule(module, newName);
                        if (_expandedModules.Remove(module))
                            _expandedModules.Add(newName);
                        if (_selectedModule == module)
                            _selectedModule = newName;
                        _status = UniConfigLoc.F("status.module_renamed", newName);
                    }
                }
                else if (target == RenameTarget.Table)
                {
                    if (newName != table)
                    {
                        EditorConfigMgr.RenameTable(module, table, newName);
                        if (_selectedModule == module && _selectedTable == table)
                            _selectedTable = newName;
                        _status = UniConfigLoc.F("status.table_renamed", newName);
                    }
                }
            });
        }

        private void CancelRename()
        {
            _renameTarget = RenameTarget.None;
            _renameModule = null;
            _renameTable = null;
            _renameBuffer = null;
            _renameFocusPending = false;
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
        }

        private List<ConfigTable> GetMatchedTables(ConfigModule module, out bool anyMatch)
        {
            List<ConfigTable> list = new();
            anyMatch = false;
            if (module?.tables == null) return list;

            foreach (ConfigTable table in module.tables)
                if (MatchRegex(table.tableName, _treeSearch))
                {
                    list.Add(table);
                    anyMatch = true;
                }

            list.Sort((a, b) => string.CompareOrdinal(a.tableName, b.tableName));
            return list;
        }

        #endregion

        #region Data Pane

        private void DrawDataPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            ConfigModule module = CurrentModule();
            ConfigTable table = CurrentTable();

            if (module == null || table == null)
            {
                EditorGUILayout.HelpBox(UniConfigLoc.T("select_hint"), MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            string syncKey = module.moduleName + "/" + table.tableName;
            if (_renameSyncKey != syncKey)
            {
                _renameSyncKey = syncKey;
                ClearGridFocus();
                _dataPageIndex = 0;
                _gridScroll = Vector2.zero;
                ResetAsyncSearchState();
                ResetColumnSelections(table);
                RequestLoadCurrentTable(table);
            }

            if (!table.dataLoaded || table.isLoading)
            {
                DrawDataToolbar(module, table);
                DrawTableLoadingState(table);
                EditorGUILayout.EndVertical();
                return;
            }

            SyncColumnSelections(table);
            ConstraintEvaluator.EnsureForeignKeyState(table);
            DrawDataToolbar(module, table);
            TickAsyncSearch(table);

            if (_isSearching || (_searchDirty && !string.IsNullOrEmpty(_dataSearch)))
            {
                DrawSpreadsheetHeadersOnly(table);
                DrawSearchLoadingState();
                DrawPaginationBar(table, false);
            }
            else
            {
                DrawSpreadsheet(table);
                DrawPaginationBar(table, true);
            }

            EditorGUILayout.EndVertical();
        }

        private void RequestLoadCurrentTable(ConfigTable table)
        {
            if (table == null || table.dataLoaded || table.isLoading)
                return;

            _status = UniConfigLoc.F("status.loading", table.moduleName, table.tableName);
            EditorConfigMgr.RequestTableDataLoad(table, () =>
            {
                if (table.dataLoaded)
                {
                    ResetColumnSelections(table);
                    MarkSearchDirty();
                    _status = UniConfigLoc.F("status.loaded", table.moduleName, table.tableName, table.items.Count);
                }
                else
                {
                    _status = UniConfigLoc.F("status.load_failed", table.moduleName, table.tableName);
                }

                Repaint();
            });
        }

        private void DrawTableLoadingState(ConfigTable table)
        {
            EditorGUILayout.Space(40);
            int dots = (int)(EditorApplication.timeSinceStartup * 3d) % 4;
            string suffix = new('.', dots);
            EditorGUILayout.HelpBox(
                UniConfigLoc.F("loading_help", table.moduleName, table.tableName, suffix),
                MessageType.Info);
            if (table.isLoading || !table.dataLoaded)
                Repaint();
        }

        private void DrawSearchLoadingState()
        {
            EditorGUILayout.Space(12);
            int dots = (int)(EditorApplication.timeSinceStartup * 3d) % 4;
            string suffix = new('.', dots);
            EditorGUILayout.HelpBox(UniConfigLoc.F("searching_help", suffix), MessageType.Info);
            Repaint();
        }

        private void DrawPaginationBar(ConfigTable table, bool interactive)
        {
            int pageSize = ConfigPaths.ShardRowLimit;
            List<int> matched = _displayRowIndices ?? new List<int>();
            int total = matched.Count;
            int pageCount = total == 0
                ? 1
                : Mathf.Max(1, Mathf.CeilToInt(total / (float)pageSize));
            if (_dataPageIndex >= pageCount)
                _dataPageIndex = pageCount - 1;
            if (_dataPageIndex < 0)
                _dataPageIndex = 0;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string matchInfo = string.IsNullOrEmpty(_readySearchPattern)
                ? string.Empty
                : UniConfigLoc.F("match_count", total);
            if (_isSearching || (_searchDirty && !string.IsNullOrEmpty(_dataSearch)))
                matchInfo = UniConfigLoc.T("searching_tag");

            GUILayout.Label(
                UniConfigLoc.F("page_info", table.items.Count, matchInfo, pageSize, _dataPageIndex + 1, pageCount),
                EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!interactive || _dataPageIndex <= 0))
            {
                if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(28)))
                    SetDataPageIndex(_dataPageIndex - 1);
            }

            using (new EditorGUI.DisabledScope(!interactive))
            {
                int window = 7;
                int startPage = Mathf.Max(0, _dataPageIndex - window / 2);
                int endPage = Mathf.Min(pageCount - 1, startPage + window - 1);
                startPage = Mathf.Max(0, endPage - window + 1);

                if (startPage > 0)
                {
                    if (GUILayout.Button("1", EditorStyles.toolbarButton, GUILayout.Width(28)))
                        SetDataPageIndex(0);
                    if (startPage > 1)
                        GUILayout.Label("…", GUILayout.Width(16));
                }

                for (int p = startPage; p <= endPage; p++)
                {
                    bool on = p == _dataPageIndex;
                    if (GUILayout.Toggle(on, (p + 1).ToString(), EditorStyles.toolbarButton, GUILayout.Width(28)) &&
                        !on)
                        SetDataPageIndex(p);
                }

                if (endPage < pageCount - 1)
                {
                    if (endPage < pageCount - 2)
                        GUILayout.Label("…", GUILayout.Width(16));
                    if (GUILayout.Button(pageCount.ToString(), EditorStyles.toolbarButton, GUILayout.Width(32)))
                        SetDataPageIndex(pageCount - 1);
                }

                using (new EditorGUI.DisabledScope(_dataPageIndex >= pageCount - 1))
                {
                    if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(28)))
                        SetDataPageIndex(_dataPageIndex + 1);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void SetDataPageIndex(int pageIndex)
        {
            if (_dataPageIndex == pageIndex)
                return;
            _dataPageIndex = pageIndex;
            ClearGridFocus();
            PersistUiState();
        }

        private static void ClearGridFocus()
        {
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            GUIUtility.hotControl = 0;
        }

        private void DrawDataToolbar(ConfigModule module, ConfigTable table)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            string exportClass = string.IsNullOrEmpty(table.mapCsharpTypeFullName)
                ? UniConfigLoc.T("not_exported")
                : table.mapCsharpTypeFullName;
            string idText = table.tableId > 0 ? table.tableId.ToString() : "-";
            GUILayout.Label(
                $"{module.moduleName}/{table.tableName} (Id={idText}, {UniConfigLoc.T("export_type")}: {exportClass})",
                EditorStyles.boldLabel);

            if (ToolbarButton(UniConfigLoc.T("rename"), UniConfigLoc.T("rename_table.tip"), "editicon.sml",
                    "d_editicon.sml"))
                BeginRenameTable(module.moduleName, table.tableName);

            if (ToolbarButton(UniConfigLoc.T("delete"), UniConfigLoc.T("delete_table.tip"), "TreeEditor.Trash",
                    "d_TreeEditor.Trash"))
                DeleteSelectedTable();

            using (new EditorGUI.DisabledScope(!table.dataLoaded || table.isLoading))
            {
                if (ToolbarButton(UniConfigLoc.T("save"), UniConfigLoc.T("save_table.tip"), "SaveActive",
                        "d_SaveActive"))
                    TryAction(() =>
                    {
                        int removed = EditorConfigMgr.SaveTable(table);
                        _dirty = false;
                        _status = removed > 0
                            ? UniConfigLoc.F("status.saved_table_skip", module.moduleName, table.tableName, removed)
                            : UniConfigLoc.F("status.saved_table", module.moduleName, table.tableName);
                    });

                if (ToolbarButton(UniConfigLoc.T("add_field"), UniConfigLoc.T("add_field.tip"), "Toolbar Plus",
                        "d_Toolbar Plus"))
                    AddField(table);

                if (ToolbarButton(UniConfigLoc.T("add_row"), UniConfigLoc.T("add_row.tip"), "Toolbar Plus",
                        "d_Toolbar Plus"))
                {
                    ConfigItem item = new();
                    foreach (ConfigField field in table.fields)
                        item.SetValue(field.name, FieldUtils.GetDefaultCellString(field));
                    table.items.Add(item);
                    table.cachedRowCount = table.items.Count;
                    ConstraintEvaluator.RecalculateRowFormulas(table, table.items.Count - 1);
                    _dataPageIndex = Mathf.Max(0, (table.items.Count - 1) / ConfigPaths.ShardRowLimit);
                    _dirty = true;
                    MarkSearchDirty();
                }

                if (ToolbarButton(UniConfigLoc.T("constraint.open"), UniConfigLoc.T("constraint.open.tip"),
                        "FilterByType", "d_FilterByType"))
                    ConstraintEditorWindow.Open(table);
            }

            EditorGUILayout.EndHorizontal();

            // 左：搜索；右：删行/删列
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            _dataSearch = EditorGUILayout.TextField(_dataSearch ?? string.Empty, EditorStyles.toolbarSearchField,
                GUILayout.MinWidth(80), GUILayout.Width(140));
            bool searchTextChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            DrawColumnMaskField(UniConfigLoc.T("search_cols"), table, _searchColumns, true);
            bool searchColumnsChanged = EditorGUI.EndChangeCheck();
            DrawColumnMaskField(UniConfigLoc.T("visible_cols"), table, _visibleColumns, true);

            if (searchTextChanged || searchColumnsChanged)
                MarkSearchDirty();

            GUILayout.FlexibleSpace();

            _deleteRowInput = EditorGUILayout.TextField(_deleteRowInput ?? string.Empty, GUILayout.Width(36));
            if (ToolbarButton(UniConfigLoc.T("delete_row"), UniConfigLoc.T("delete_row.tip"), "TreeEditor.Trash",
                    "d_TreeEditor.Trash"))
                TryDeleteRowByInput(table);

            GUILayout.Space(6);
            DrawColumnMaskField(null, table, _columnsToDelete, false);
            if (ToolbarButton(UniConfigLoc.T("delete_col"), UniConfigLoc.T("delete_col.tip"), "TreeEditor.Trash",
                    "d_TreeEditor.Trash"))
                DeleteSelectedColumns(table);

            EditorGUILayout.EndHorizontal();
        }

        private void TryDeleteRowByInput(ConfigTable table)
        {
            if (string.IsNullOrWhiteSpace(_deleteRowInput))
            {
                EditorUtility.DisplayDialog(UniConfigLoc.T("delete_row.title"), UniConfigLoc.T("delete_row.need_index"),
                    UniConfigLoc.T("ok"));
                return;
            }

            if (!int.TryParse(_deleteRowInput.Trim(), out int oneBasedIndex))
            {
                EditorUtility.DisplayDialog(UniConfigLoc.T("delete_row.title"), UniConfigLoc.T("delete_row.need_int"),
                    UniConfigLoc.T("ok"));
                return;
            }

            DeleteRowByIndex(table, oneBasedIndex);
        }

        private void DeleteRowByIndex(ConfigTable table, int oneBasedIndex)
        {
            if (table.items == null || table.items.Count == 0)
            {
                EditorUtility.DisplayDialog(UniConfigLoc.T("delete_row.title"), UniConfigLoc.T("delete_row.empty"),
                    UniConfigLoc.T("ok"));
                return;
            }

            if (oneBasedIndex < 1 || oneBasedIndex > table.items.Count)
            {
                EditorUtility.DisplayDialog(UniConfigLoc.T("delete_row.title"),
                    UniConfigLoc.F("delete_row.invalid", table.items.Count), UniConfigLoc.T("ok"));
                return;
            }

            table.items.RemoveAt(oneBasedIndex - 1);
            table.cachedRowCount = table.items.Count;
            _deleteRowInput = string.Empty;
            _dirty = true;
            _status = UniConfigLoc.F("status.row_deleted", oneBasedIndex);
            GUI.FocusControl(null);
            MarkSearchDirty();

            int pageSize = ConfigPaths.ShardRowLimit;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(table.items.Count / (float)pageSize));
            if (_dataPageIndex >= pageCount)
                _dataPageIndex = pageCount - 1;
        }

        private void DeleteSelectedColumns(ConfigTable table)
        {
            if (_columnsToDelete.Count == 0)
            {
                EditorUtility.DisplayDialog(UniConfigLoc.T("delete_col.title"),
                    UniConfigLoc.T("delete_col.need_select"), UniConfigLoc.T("ok"));
                return;
            }

            List<string> names = _columnsToDelete.Where(n => table.GetFieldIndex(n) >= 0).ToList();
            if (names.Count == 0)
            {
                EditorUtility.DisplayDialog(UniConfigLoc.T("delete_col.title"), UniConfigLoc.T("delete_col.none"),
                    UniConfigLoc.T("ok"));
                return;
            }

            string preview = string.Join(", ", names);
            if (!EditorUtility.DisplayDialog(UniConfigLoc.T("delete_col.title"),
                    UniConfigLoc.F("delete_col.confirm", names.Count, preview),
                    UniConfigLoc.T("delete"), UniConfigLoc.T("cancel")))
                return;

            foreach (string name in names)
            {
                RemoveField(table, name);
                _searchColumns.Remove(name);
                _visibleColumns.Remove(name);
                _columnsToDelete.Remove(name);
            }

            _dirty = true;
            _status = UniConfigLoc.F("status.cols_deleted", names.Count);
            MarkSearchDirty();
        }

        /// <summary>
        /// 使用 Unity 原生 MaskField 做列多选。单独标签 + 无 toolbar 样式，避免下拉失效。
        /// </summary>
        private static void DrawColumnMaskField(string label, ConfigTable table, HashSet<string> selected,
            bool defaultSelectAllAsEverything)
        {
            if (!string.IsNullOrEmpty(label))
                GUILayout.Label(label, GUILayout.Width(UniConfigLoc.IsChinese ? 44 : 72));

            if (table.fields == null || table.fields.Count == 0)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.MaskField(0, new[] { UniConfigLoc.T("no_fields") }, GUILayout.Width(110));
                EditorGUI.EndDisabledGroup();
                return;
            }

            string[] columnNames = new string[table.fields.Count];
            for (int i = 0; i < table.fields.Count; i++)
                columnNames[i] = table.fields[i].name;

            int mask = BuildColumnMask(table, selected, defaultSelectAllAsEverything);
            EditorGUI.BeginChangeCheck();
            // 不用 EditorStyles.toolbarPopup：该样式会导致 MaskField 下拉异常
            int newMask = EditorGUILayout.MaskField(mask, columnNames, GUILayout.Width(110));
            if (EditorGUI.EndChangeCheck())
                ApplyColumnMask(table, selected, newMask);
        }

        private void DrawSpreadsheetHeadersOnly(ConfigTable table)
        {
            EnsureItemFields(table);
            List<int> visibleFieldIndices = GetVisibleFieldIndices(table);
            const float OpsRowHeight = 24f;
            float gridWidth = GetSpreadsheetContentWidth(visibleFieldIndices.Count);
            float gridHeight = HeaderRowHeight + RowHeight + OpsRowHeight + 8;

            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.ExpandHeight(false),
                GUILayout.Height(gridHeight + 8));
            Rect content = GUILayoutUtility.GetRect(gridWidth, gridHeight, GUILayout.ExpandWidth(false));
            DrawSpreadsheetHeaderBlock(table, visibleFieldIndices, content.x, content.y, out _, out _);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSpreadsheet(ConfigTable table)
        {
            EnsureItemFields(table);
            List<int> visibleFieldIndices = GetVisibleFieldIndices(table);
            if (visibleFieldIndices.Count == 0 && table.fields.Count > 0)
                EditorGUILayout.HelpBox(UniConfigLoc.T("no_visible_cols"), MessageType.Warning);

            const float OpsRowHeight = 24f;
            float gridWidth = GetSpreadsheetContentWidth(visibleFieldIndices.Count);
            List<int> matched = _displayRowIndices ?? new List<int>();
            int pageSize = ConfigPaths.ShardRowLimit;
            int pageCount = matched.Count == 0
                ? 1
                : Mathf.Max(1, Mathf.CeilToInt(matched.Count / (float)pageSize));
            if (_dataPageIndex >= pageCount) _dataPageIndex = pageCount - 1;
            if (_dataPageIndex < 0) _dataPageIndex = 0;
            int pageStart = _dataPageIndex * pageSize;
            int pageEnd = Mathf.Min(pageStart + pageSize, matched.Count);
            int visibleRows = Mathf.Max(0, pageEnd - pageStart);

            float gridHeight = HeaderRowHeight + RowHeight + OpsRowHeight + visibleRows * RowHeight + 8;

            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.ExpandHeight(true));
            Rect content = GUILayoutUtility.GetRect(
                gridWidth,
                Mathf.Max(gridHeight, 200),
                GUILayout.ExpandWidth(false));

            float x0 = content.x;
            float y = content.y;
            DrawSpreadsheetHeaderBlock(table, visibleFieldIndices, x0, y, out y, out bool columnOpsChanged);

            if (!columnOpsChanged)
            {
                bool highlight = !string.IsNullOrEmpty(_readySearchPattern);
                for (int i = pageStart; i < pageEnd; i++)
                {
                    int row = matched[i];
                    if (row < 0 || row >= table.items.Count)
                        continue;

                    ConfigItem item = table.items[row];
                    bool even = (i - pageStart) % 2 == 0;
                    Color rowBg = highlight ? RowMatch : even ? RowEven : RowOdd;

                    Rect indexRect = new(x0, y, IndexColWidth, RowHeight);
                    DrawCellBackground(indexRect, rowBg, true);
                    GUI.Label(indexRect, (row + 1).ToString(), _headerStyle);

                    float x = x0 + IndexColWidth;
                    foreach (int fi in visibleFieldIndices)
                    {
                        ConfigField field = table.fields[fi];
                        Rect cell = new(x, y, ColWidth, RowHeight);
                        bool fkError = table.IsFkCellViolation(row, field.name);
                        DrawCellBackground(cell, fkError ? FkErrorCell : rowBg, true);
                        Rect fieldRect = Inset(cell, 1);
                        bool formulaLocked = ConstraintEvaluator.IsFormulaField(table, field.name);
                        if (formulaLocked)
                        {
                            // 公式字段只读，由约束自动计算
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUI.TextField(fieldRect, item.GetValue(field.name), _cellStyle);
                            EditorGUI.EndDisabledGroup();
                        }
                        else if (field.IsEnum)
                        {
                            string[] enumNames;
                            try
                            {
                                enumNames = EnumTypeUtil.GetNames(field.enumTypeFullName);
                            }
                            catch (Exception ex)
                            {
                                EditorGUI.HelpBox(fieldRect, ex.Message, MessageType.Error);
                                x += ColWidth;
                                continue;
                            }

                            if (enumNames == null || enumNames.Length == 0)
                            {
                                EditorGUI.HelpBox(fieldRect, "enum empty", MessageType.Warning);
                                x += ColWidth;
                                continue;
                            }

                            string current = item.GetValue(field.name);
                            int enumIndex = IndexOfName(enumNames, current);
                            if (enumIndex < 0)
                            {
                                enumIndex = IndexOfName(enumNames, FieldUtils.GetDefaultCellString(field));
                                if (enumIndex < 0) enumIndex = 0;
                            }

                            EditorGUI.BeginChangeCheck();
                            int newEnumIndex = EditorGUI.Popup(fieldRect, enumIndex, enumNames);
                            if (EditorGUI.EndChangeCheck() && newEnumIndex >= 0 && newEnumIndex < enumNames.Length)
                            {
                                item.SetValue(field.name, enumNames[newEnumIndex]);
                                ConstraintEvaluator.RecalculateRowFormulas(table, row);
                                table.MarkFkValidationDirty();
                                ConstraintEvaluator.EnsureForeignKeyState(table);
                                _dirty = true;
                            }
                        }
                        else
                        {
                            EditorGUI.BeginChangeCheck();
                            string value = EditorGUI.TextField(fieldRect, item.GetValue(field.name), _cellStyle);
                            if (EditorGUI.EndChangeCheck())
                            {
                                item.SetValue(field.name, value);
                                ConstraintEvaluator.RecalculateRowFormulas(table, row);
                                table.MarkFkValidationDirty();
                                ConstraintEvaluator.EnsureForeignKeyState(table);
                                _dirty = true;
                            }
                        }

                        // 在输入框之上画完整红框，对齐 TextField 区域（与选中高亮一致）
                        if (fkError)
                            DrawRectBorder(fieldRect, FkErrorBorder, 2f);

                        x += ColWidth;
                    }

                    y += RowHeight;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 表格内容区宽度：列总和 + 垂直滚动条宽度，确保横向滚到尽头时最后一列完全可见。
        /// </summary>
        private static float GetSpreadsheetContentWidth(int visibleColumnCount)
        {
            float vScroll = GUI.skin != null ? GUI.skin.verticalScrollbar.fixedWidth : 0f;
            if (vScroll < 1f) vScroll = 16f;
            return IndexColWidth + visibleColumnCount * ColWidth + vScroll + GridScrollPadX;
        }

        private void DrawSpreadsheetHeaderBlock(
            ConfigTable table,
            List<int> visibleFieldIndices,
            float x0,
            float yStart,
            out float yEnd,
            out bool columnOpsChanged)
        {
            const float OpsRowHeight = 24f;
            float y = yStart;
            columnOpsChanged = false;

            DrawCellBackground(new Rect(x0, y, IndexColWidth, HeaderRowHeight), HeaderBg, true);
            GUI.Label(new Rect(x0, y, IndexColWidth, HeaderRowHeight), "#", _headerStyle);

            float x = x0 + IndexColWidth;
            foreach (int fi in visibleFieldIndices)
            {
                ConfigField field = table.fields[fi];
                Rect nameRect = new(x, y, ColWidth, HeaderRowHeight);
                DrawCellBackground(nameRect, HeaderBg, true);
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUI.TextField(Inset(nameRect, 1), field.name, _cellStyle);
                if (EditorGUI.EndChangeCheck())
                    if (newName != field.name && FieldUtils.IsValidIdentifier(newName) &&
                        table.GetFieldIndex(newName) < 0)
                    {
                        string old = field.name;
                        RenameField(table, old, newName);
                        field.name = newName;
                        RemapColumnName(old, newName);
                        _dirty = true;
                        MarkSearchDirty();
                    }

                x += ColWidth;
            }

            y += HeaderRowHeight;

            DrawCellBackground(new Rect(x0, y, IndexColWidth, RowHeight), HeaderBg, true);
            GUI.Label(new Rect(x0, y, IndexColWidth, RowHeight), UniConfigLoc.T("type"), _headerStyle);
            x = x0 + IndexColWidth;
            foreach (int fi in visibleFieldIndices)
            {
                ConfigField field = table.fields[fi];
                Rect typeRect = new(x, y, ColWidth, RowHeight);
                DrawCellBackground(typeRect, HeaderBg, true);
                Rect typeButtonRect = Inset(typeRect, 1);
                if (EditorGUI.DropdownButton(
                        typeButtonRect,
                        new GUIContent(FieldUtils.GetTypeButtonLabel(field)),
                        FocusType.Keyboard))
                {
                    ConfigField capturedField = field;
                    ConfigTable capturedTable = table;
                    FieldUtils.ShowTypeDropdownMenu(typeButtonRect, capturedField, () =>
                    {
                        string cellDefault = FieldUtils.GetDefaultCellString(capturedField);
                        if (capturedTable.items != null)
                        {
                            for (int r = 0; r < capturedTable.items.Count; r++)
                                capturedTable.items[r]?.SetValue(capturedField.name, cellDefault);
                        }

                        _dirty = true;
                        Repaint();
                    });
                }

                x += ColWidth;
            }

            y += RowHeight;

            DrawCellBackground(new Rect(x0, y, IndexColWidth, OpsRowHeight), HeaderBg, true);
            x = x0 + IndexColWidth;
            foreach (int fi in visibleFieldIndices)
            {
                Rect op = new(x, y, ColWidth, OpsRowHeight);
                DrawCellBackground(op, HeaderBg, true);
                if (DrawColumnOps(op, table, fi))
                {
                    columnOpsChanged = true;
                    break;
                }

                x += ColWidth;
            }

            y += OpsRowHeight;
            yEnd = y;
        }

        /// <summary>
        /// 在类型行下方绘制列顺序按钮（左移/右移）。
        /// 返回 true 表示结构已变化，应中断本帧后续绘制。
        /// </summary>
        private bool DrawColumnOps(Rect cell, ConfigTable table, int fieldIndex)
        {
            const float btnW = 28f;
            const float btnH = 20f;
            float totalW = btnW * 2f;
            float startX = cell.x + (cell.width - totalW) * 0.5f;
            float startY = cell.y + (cell.height - btnH) * 0.5f;

            Rect leftRect = new(startX, startY, btnW, btnH);
            Rect rightRect = new(startX + btnW, startY, btnW, btnH);

            using (new EditorGUI.DisabledScope(fieldIndex <= 0))
            {
                if (GUI.Button(leftRect, new GUIContent("<", UniConfigLoc.T("move_left")), EditorStyles.miniButtonLeft))
                {
                    SwapFields(table, fieldIndex, fieldIndex - 1);
                    _dirty = true;
                    return true;
                }
            }

            using (new EditorGUI.DisabledScope(fieldIndex >= table.fields.Count - 1))
            {
                if (GUI.Button(rightRect, new GUIContent(">", UniConfigLoc.T("move_right")),
                        EditorStyles.miniButtonRight))
                {
                    SwapFields(table, fieldIndex, fieldIndex + 1);
                    _dirty = true;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Column mask (Unity MaskField)

        private void HandleRenameClickOutside()
        {
            if (_renameTarget != RenameTarget.None &&
                Event.current.type == EventType.MouseDown &&
                GUI.GetNameOfFocusedControl() != RenameControlName)
                CommitRename();
        }

        private static int BuildColumnMask(ConfigTable table, HashSet<string> selected,
            bool mapFullSelectionToEverything)
        {
            int mask = 0;
            int count = Mathf.Min(table.fields.Count, 32);
            for (int i = 0; i < count; i++)
                if (selected.Contains(table.fields[i].name))
                    mask |= 1 << i;

            // 全选时与 MaskField 的 Everything(-1) 对齐，避免来回跳动
            // 删列下拉默认空选，不做 Everything 映射
            if (mapFullSelectionToEverything)
            {
                int allBits = count >= 32 ? -1 : (1 << count) - 1;
                if (count > 0 && mask == allBits)
                    return -1;
            }

            return mask;
        }

        private static void ApplyColumnMask(ConfigTable table, HashSet<string> selected, int mask)
        {
            selected.Clear();
            int count = Mathf.Min(table.fields.Count, 32);

            // MaskField: -1 = Everything, 0 = Nothing
            if (mask == -1)
            {
                for (int i = 0; i < count; i++)
                    selected.Add(table.fields[i].name);
                return;
            }

            for (int i = 0; i < count; i++)
                if ((mask & (1 << i)) != 0)
                    selected.Add(table.fields[i].name);
        }

        #endregion

        #region Search helpers

        private static bool MatchRegex(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void MarkSearchDirty()
        {
            _searchDirty = true;
            _searchDirtyAt = EditorApplication.timeSinceStartup;
            if (!string.IsNullOrEmpty(_dataSearch))
            {
                _searchJobId++;
                _isSearching = true;
                _displayRowIndices = new List<int>();
            }
        }

        private void ResetAsyncSearchState()
        {
            _searchJobId++;
            _searchDirty = true;
            _searchDirtyAt = 0;
            _isSearching = false;
            _readySearchPattern = string.Empty;
            _readyItemCount = -1;
            _displayRowIndices = new List<int>();
            _dataSearch = string.Empty;
        }

        private void TickAsyncSearch(ConfigTable table)
        {
            if (table?.items == null)
                return;

            if (_readyItemCount >= 0 && _readyItemCount != table.items.Count && !_searchDirty && !_isSearching)
                MarkSearchDirty();

            string pattern = _dataSearch ?? string.Empty;

            // 清空搜索：主线程快速恢复全量索引
            if (string.IsNullOrEmpty(pattern))
            {
                if (_searchDirty || _isSearching || _readyItemCount != table.items.Count ||
                    !string.IsNullOrEmpty(_readySearchPattern))
                {
                    _searchJobId++;
                    _isSearching = false;
                    _searchDirty = false;
                    FillAllRowIndices(table);
                    _readySearchPattern = string.Empty;
                    _readyItemCount = table.items.Count;
                }

                return;
            }

            if (_searchDirty)
            {
                if (EditorApplication.timeSinceStartup - _searchDirtyAt < SearchDebounceSeconds)
                {
                    Repaint();
                    return;
                }

                _searchDirty = false;
                StartAsyncSearchJob(table, pattern);
            }
        }

        private void FillAllRowIndices(ConfigTable table)
        {
            int n = table.items?.Count ?? 0;
            List<int> list = new(n);
            for (int i = 0; i < n; i++)
                list.Add(i);
            _displayRowIndices = list;
        }

        private void StartAsyncSearchJob(ConfigTable table, string pattern)
        {
            int jobId = ++_searchJobId;
            _isSearching = true;
            if (_dataPageIndex != 0)
            {
                _dataPageIndex = 0;
                ClearGridFocus();
            }

            string[] columns = _searchColumns.ToArray();
            int rowCount = table.items.Count;
            string[][] snapshot = new string[rowCount][];
            for (int i = 0; i < rowCount; i++)
            {
                ConfigItem item = table.items[i];
                string[] values = new string[columns.Length];
                for (int c = 0; c < columns.Length; c++)
                    values[c] = item.GetValue(columns[c]) ?? string.Empty;
                snapshot[i] = values;
            }

            _status = UniConfigLoc.F("status.searching", table.moduleName, table.tableName);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                List<int> result = new();
                Regex regex = null;
                bool useRegex = true;
                try
                {
                    regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
                catch (ArgumentException)
                {
                    useRegex = false;
                }

                for (int i = 0; i < snapshot.Length; i++)
                {
                    string[] values = snapshot[i];
                    bool match = false;
                    if (columns.Length == 0)
                    {
                        match = false;
                    }
                    else if (useRegex)
                    {
                        for (int c = 0; c < values.Length; c++)
                            if (regex.IsMatch(values[c]))
                            {
                                match = true;
                                break;
                            }
                    }
                    else
                    {
                        for (int c = 0; c < values.Length; c++)
                            if (values[c].IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                match = true;
                                break;
                            }
                    }

                    if (match)
                        result.Add(i);
                }

                EditorApplication.delayCall += () =>
                {
                    if (jobId != _searchJobId)
                        return;

                    _displayRowIndices = result;
                    _readySearchPattern = pattern;
                    _readyItemCount = rowCount;
                    _isSearching = false;
                    _status = UniConfigLoc.F("status.search_done", result.Count, rowCount);
                    Repaint();
                };
            });
        }

        private List<int> GetVisibleFieldIndices(ConfigTable table)
        {
            List<int> list = new();
            for (int i = 0; i < table.fields.Count; i++)
                if (_visibleColumns.Contains(table.fields[i].name))
                    list.Add(i);
            return list;
        }

        private void ResetColumnSelections(ConfigTable table)
        {
            _searchColumns.Clear();
            _visibleColumns.Clear();
            _columnsToDelete.Clear();
            foreach (ConfigField field in table.fields)
            {
                _searchColumns.Add(field.name);
                _visibleColumns.Add(field.name);
            }

            _deleteRowInput = string.Empty;
            _searchColumnsInitialized = true;
            _visibleColumnsInitialized = true;
        }

        private void SyncColumnSelections(ConfigTable table)
        {
            if (!_searchColumnsInitialized || !_visibleColumnsInitialized)
            {
                ResetColumnSelections(table);
                return;
            }

            _searchColumns.RemoveWhere(name => table.GetFieldIndex(name) < 0);
            _visibleColumns.RemoveWhere(name => table.GetFieldIndex(name) < 0);
            _columnsToDelete.RemoveWhere(name => table.GetFieldIndex(name) < 0);
        }

        private void RemapColumnName(string oldName, string newName)
        {
            if (_searchColumns.Remove(oldName)) _searchColumns.Add(newName);
            if (_visibleColumns.Remove(oldName)) _visibleColumns.Add(newName);
        }

        #endregion

        #region Shared UI

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            ConfigTable table = CurrentTable();
            bool fkBad = table != null && table.dataLoaded && table.HasFkViolations;
            string statusText = fkBad
                ? string.IsNullOrEmpty(table.fkStatusDetail)
                    ? UniConfigLoc.T("constraint.fk_invalid_status")
                    : table.fkStatusDetail
                : string.IsNullOrEmpty(_status)
                    ? UniConfigLoc.T("ready")
                    : _status;

            Color prev = GUI.contentColor;
            if (fkBad)
                GUI.contentColor = FkErrorText;
            GUILayout.Label(statusText, EditorStyles.miniLabel);
            GUI.contentColor = prev;

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_selectedModule))
                GUILayout.Label($"{UniConfigLoc.T("module")}: {_selectedModule}", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(_selectedTable))
                GUILayout.Label($"  {UniConfigLoc.T("table")}: {_selectedTable}", EditorStyles.miniLabel);
            if (UniConfigEditorSettings.instance.autoSaveOnClose)
                GUILayout.Label($"  {UniConfigLoc.T("auto_save_on")}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVerticalSplitter()
        {
            Rect r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandHeight(true), GUILayout.Width(1));
            EditorGUI.DrawRect(r, GridLine);
        }

        private void DrawCellBackground(Rect rect, Color color, bool border)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(rect, color);
            if (border)
            {
                EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), GridLine);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), GridLine);
            }
        }

        private static void DrawRectBorder(Rect rect, Color color, float thickness)
        {
            if (Event.current.type != EventType.Repaint) return;
            float t = Mathf.Max(1f, thickness);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), color);
        }

        private static Rect Inset(Rect r, float pad)
        {
            return new Rect(r.x + pad, r.y + pad, r.width - pad * 2, r.height - pad * 2);
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _cellStyle = new GUIStyle(EditorStyles.textField)
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(3, 3, 1, 1),
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };

            _headerStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };

            _treeItemStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 2, 0, 0),
                fontSize = 12
            };

            _paneTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 4, 4, 2)
            };

            _stylesReady = true;
        }

        #endregion

        #region Selection / CRUD

        private ConfigModule CurrentModule()
        {
            return string.IsNullOrEmpty(_selectedModule) ? null : EditorConfigMgr.GetModule(_selectedModule);
        }

        private ConfigTable CurrentTable()
        {
            return CurrentModule()?.FindTable(_selectedTable);
        }

        private void SelectModule(string moduleName, bool toggleExpand)
        {
            _selectedModule = moduleName;
            _selectedTable = null;

            if (toggleExpand)
            {
                if (_expandedModules.Contains(moduleName))
                    _expandedModules.Remove(moduleName);
                else
                    _expandedModules.Add(moduleName);
            }
            else
            {
                _expandedModules.Add(moduleName);
            }

            PersistUiState();
            GUI.FocusControl(null);
            Repaint();
        }

        private void SelectTable(string moduleName, string tableName)
        {
            _selectedModule = moduleName;
            _selectedTable = tableName;
            _expandedModules.Add(moduleName);
            _dataPageIndex = 0;
            ClearGridFocus();

            ConfigTable table = EditorConfigMgr.GetModule(moduleName)?.FindTable(tableName);
            RequestLoadCurrentTable(table);
            PersistUiState();
            Repaint();
        }

        private static void EnsureItemFields(ConfigTable table)
        {
            foreach (ConfigItem item in table.items)
            foreach (ConfigField field in table.fields)
                if (!item.fieldData.ContainsKey(field.name))
                    item.SetValue(field.name, FieldUtils.GetDefaultCellString(field));
        }

        private static int IndexOfName(string[] names, string value)
        {
            if (names == null || string.IsNullOrEmpty(value)) return -1;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], value, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private void AddField(ConfigTable table)
        {
            string baseName = "field";
            string name = baseName;
            int index = 1;
            while (table.GetFieldIndex(name) >= 0)
            {
                name = baseName + index;
                index++;
            }

            table.fields.Add(new ConfigField(name, SupportableFieldType.Int32));
            foreach (ConfigItem item in table.items)
                item.SetValue(name, FieldUtils.GetDefaultCellString(table.fields[table.fields.Count - 1]));
            _searchColumns.Add(name);
            _visibleColumns.Add(name);
            _dirty = true;
            MarkSearchDirty();
        }

        private static void RenameField(ConfigTable table, string oldName, string newName)
        {
            foreach (ConfigItem item in table.items)
            {
                string value = item.GetValue(oldName);
                item.fieldData.Remove(oldName);
                item.SetValue(newName, value);
            }
        }

        private static void RemoveField(ConfigTable table, string fieldName)
        {
            int index = table.GetFieldIndex(fieldName);
            if (index < 0) return;
            table.fields.RemoveAt(index);
            foreach (ConfigItem item in table.items)
                item.fieldData.Remove(fieldName);
        }

        private static void SwapFields(ConfigTable table, int a, int b)
        {
            ConfigField tmp = table.fields[a];
            table.fields[a] = table.fields[b];
            table.fields[b] = tmp;
        }

        private void TryAction(Action action)
        {
            try
            {
                action();
                Repaint();
            }
            catch (Exception ex)
            {
                _status = ex.Message;
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("UniConfig", ex.Message, UniConfigLoc.T("ok"));
            }
        }

        private static string GetExporterLabel(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;
            int dot = fullName.LastIndexOf('.');
            return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
        }

        private static readonly Dictionary<string, Texture2D> IconCache = new();
        private static MethodInfo _loadIconMethod;

        private static GUIContent Content(string text, string tooltip, params string[] icons)
        {
            Texture2D icon = LoadIcon(icons);
            return new GUIContent(text ?? string.Empty, icon, tooltip ?? string.Empty);
        }

        private static Texture2D LoadIcon(params string[] names)
        {
            if (names == null) return null;
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (IconCache.TryGetValue(name, out Texture2D cached))
                {
                    if (cached != null) return cached;
                    continue;
                }

                Texture2D tex = TryLoadIconInternal(name) ?? EditorGUIUtility.FindTexture(name);
                IconCache[name] = tex;
                if (tex != null) return tex;
            }

            return null;
        }

        private static Texture2D TryLoadIconInternal(string name)
        {
            _loadIconMethod ??= typeof(EditorGUIUtility).GetMethod(
                "LoadIcon",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);

            if (_loadIconMethod == null) return null;
            try
            {
                return _loadIconMethod.Invoke(null, new object[] { name }) as Texture2D;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}