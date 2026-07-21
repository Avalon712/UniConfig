#region

using UnityEditor;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    public sealed class UniConfigSettingsWindow : EditorWindow
    {
        private void OnGUI()
        {
            UniConfigEditorSettings settings = UniConfigEditorSettings.instance;
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

            if (EditorGUI.EndChangeCheck())
            {
                settings.SaveSettings();
                Repaint();
            }

            EditorGUILayout.Space(12);
            if (GUILayout.Button(UniConfigLoc.T("settings.open_editor"), GUILayout.Height(28)))
                ConfigEditorWindow.Open();
        }

        [MenuItem("UniConfig/Settings")]
        public static void Open()
        {
            UniConfigSettingsWindow window = GetWindow<UniConfigSettingsWindow>("UniConfig Settings");
            window.minSize = new Vector2(460, 280);
            window.Show();
        }
    }
}