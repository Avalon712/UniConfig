#region

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
        /// 窗口预设
        /// </summary>
        public string prefs;

        /// <summary>
        /// 关闭配置编辑器窗口时自动保存一次
        /// </summary>
        public bool autoSaveOnClose = true;

        /// <summary>
        /// 编辑器界面语言：0=中文，1=English（默认中文）
        /// </summary>
        public int editorLanguage;

        public void SaveSettings()
        {
            Save(true);
        }
    }
}