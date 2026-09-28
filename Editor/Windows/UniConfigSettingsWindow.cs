#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    public sealed class UniConfigSettingsWindow : EditorWindow
    {
        private ReorderableList _enumReorderableList;
        private Vector2 _scroll;

        private void OnEnable()
        {
            RebuildEnumList();
        }

        private void RebuildEnumList()
        {
            UniConfigEditorSettings settings = UniConfigEditorSettings.instance;
            settings.customEnumTypeFullNames ??= new List<string>();

            _enumReorderableList = new ReorderableList(
                settings.customEnumTypeFullNames,
                typeof(string),
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true);

            _enumReorderableList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, UniConfigLoc.T("settings.enums"));
            };

            _enumReorderableList.elementHeight = EditorGUIUtility.singleLineHeight + 2f;
            _enumReorderableList.drawElementCallback = (rect, index, active, focused) =>
            {
                if (index < 0 || index >= settings.customEnumTypeFullNames.Count)
                    return;

                rect.y += 1f;
                rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.BeginChangeCheck();
                string value = EditorGUI.TextField(rect, settings.customEnumTypeFullNames[index] ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                {
                    settings.customEnumTypeFullNames[index] = value;
                    MarkEnumsDirty(settings);
                }
            };

            _enumReorderableList.onAddCallback = _ =>
            {
                settings.customEnumTypeFullNames.Add(string.Empty);
                MarkEnumsDirty(settings);
            };

            _enumReorderableList.onRemoveCallback = list =>
            {
                if (list.index < 0 || list.index >= settings.customEnumTypeFullNames.Count)
                    return;
                settings.customEnumTypeFullNames.RemoveAt(list.index);
                MarkEnumsDirty(settings);
            };

            _enumReorderableList.onReorderCallback = _ => MarkEnumsDirty(settings);
        }

        private static void MarkEnumsDirty(UniConfigEditorSettings settings)
        {
            EnumTypeUtil.ClearCache();
            settings.SaveSettings();
        }

        private void OnGUI()
        {
            UniConfigEditorSettings settings = UniConfigEditorSettings.instance;
            settings.customEnumTypeFullNames ??= new List<string>();

            // ScriptableSingleton 重载后 list 引用可能变化，需重建
            if (_enumReorderableList == null ||
                !ReferenceEquals(_enumReorderableList.list, settings.customEnumTypeFullNames))
                RebuildEnumList();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField(UniConfigLoc.T("settings.code_gen"), EditorStyles.boldLabel);
            settings.csharpExportDirectory =
                EditorGUILayout.TextField(UniConfigLoc.T("settings.cs_dir"), settings.csharpExportDirectory);
            settings.rootNamespace =
                EditorGUILayout.TextField(UniConfigLoc.T("settings.root_ns"), settings.rootNamespace);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(UniConfigLoc.T("settings.export"), EditorStyles.boldLabel);
            settings.resourcesExportDirectory = EditorGUILayout.TextField(
                UniConfigLoc.T("settings.res_dir"), settings.resourcesExportDirectory);
            settings.streamingAssetsExportDirectory = EditorGUILayout.TextField(
                UniConfigLoc.T("settings.sa_dir"), settings.streamingAssetsExportDirectory);
            EditorGUILayout.HelpBox(UniConfigLoc.T("settings.export_file_hint"), MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(UniConfigLoc.T("settings.editor"), EditorStyles.boldLabel);
            settings.autoSaveOnClose =
                EditorGUILayout.Toggle(UniConfigLoc.T("settings.auto_save_close"), settings.autoSaveOnClose);
            settings.editorLanguage = EditorGUILayout.Popup(
                UniConfigLoc.T("language"),
                settings.editorLanguage,
                new[] { "中文", "English" });

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(UniConfigLoc.T("settings.enums_hint"), MessageType.Info);
            _enumReorderableList.DoLayoutList();

            if (EditorGUI.EndChangeCheck())
            {
                NormalizeEnumTypes(settings, removeEmpty: false);
                settings.SaveSettings();
                Repaint();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(12);
            if (GUILayout.Button(UniConfigLoc.T("settings.open_editor"), GUILayout.Height(28)))
                ConfigEditorWindow.Open();
        }

        private void OnDisable()
        {
            UniConfigEditorSettings settings = UniConfigEditorSettings.instance;
            if (settings == null) return;
            NormalizeEnumTypes(settings, removeEmpty: true);
            EnumTypeUtil.ClearCache();
            settings.SaveSettings();
        }

        /// <summary>
        /// Trim + 非空去重。removeEmpty=false 时保留空槽，保证点 + 能看到新行。
        /// </summary>
        private static void NormalizeEnumTypes(UniConfigEditorSettings settings, bool removeEmpty)
        {
            settings.customEnumTypeFullNames ??= new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < settings.customEnumTypeFullNames.Count;)
            {
                string raw = settings.customEnumTypeFullNames[i] ?? string.Empty;
                string trimmed = raw.Trim();

                if (trimmed.Length == 0)
                {
                    if (removeEmpty)
                    {
                        settings.customEnumTypeFullNames.RemoveAt(i);
                        continue;
                    }

                    if (settings.customEnumTypeFullNames[i] != string.Empty)
                        settings.customEnumTypeFullNames[i] = string.Empty;
                    i++;
                    continue;
                }

                if (!seen.Add(trimmed))
                {
                    settings.customEnumTypeFullNames.RemoveAt(i);
                    continue;
                }

                if (!string.Equals(raw, trimmed, StringComparison.Ordinal))
                    settings.customEnumTypeFullNames[i] = trimmed;
                i++;
            }
        }

        [MenuItem("UniConfig/Settings")]
        public static void Open()
        {
            UniConfigSettingsWindow window = GetWindow<UniConfigSettingsWindow>("UniConfig Settings");
            window.minSize = new Vector2(460, 420);
            window.Show();
        }
    }
}
