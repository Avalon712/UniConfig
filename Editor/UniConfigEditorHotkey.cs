#region

using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    [InitializeOnLoad]
    internal static class UniConfigEditorHotkey
    {
        private static bool _toggleQueued;
        private static double _lastToggleTime;
        private static bool _hookMissingLogged;

        static UniConfigEditorHotkey()
        {
            HookGlobalEvent();
        }

        /// <summary>
        /// 2022.3 里这个回调还不是公开 API，用反射挂上。
        /// </summary>
        private static void HookGlobalEvent()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            EditorApplication.CallbackFunction handler = OnGlobalEvent;

            EventInfo evt = typeof(EditorApplication).GetEvent("globalEventHandler", flags);
            if (evt != null)
            {
                evt.RemoveEventHandler(null, handler);
                evt.AddEventHandler(null, handler);
                return;
            }

            FieldInfo field = typeof(EditorApplication).GetField("globalEventHandler", flags);
            if (field != null && typeof(Delegate).IsAssignableFrom(field.FieldType))
            {
                Delegate current = field.GetValue(null) as Delegate;
                current = Delegate.Remove(current, handler);
                current = Delegate.Combine(current, handler);
                field.SetValue(null, current);
                return;
            }

            if (_hookMissingLogged)
                return;
            _hookMissingLogged = true;
            Debug.LogError("[UniConfig] 当前 Unity 无法监听全局快捷键。");
        }

        internal static bool IsCapturing { get; set; }

        internal static bool IsModifierKey(KeyCode key)
        {
            return key == KeyCode.LeftControl || key == KeyCode.RightControl ||
                   key == KeyCode.LeftAlt || key == KeyCode.RightAlt ||
                   key == KeyCode.LeftShift || key == KeyCode.RightShift ||
                   key == KeyCode.LeftCommand || key == KeyCode.RightCommand ||
                   key == KeyCode.LeftWindows || key == KeyCode.RightWindows;
        }

        internal static EventModifiers Significant(EventModifiers modifiers)
        {
            return modifiers & (EventModifiers.Alt | EventModifiers.Control | EventModifiers.Shift |
                                EventModifiers.Command);
        }

        internal static string Format(UniConfigEditorSettings settings)
        {
            KeyCode key = settings.ResolvedHotkeyKey();
            EventModifiers modifiers = settings.ResolvedHotkeyModifiers();
            StringBuilder text = new();
            if ((modifiers & EventModifiers.Control) != 0)
                text.Append(UniConfigLoc.IsChinese ? "左 Ctrl + " : "Left Ctrl + ");
            if ((modifiers & EventModifiers.Alt) != 0)
                text.Append("Alt + ");
            if ((modifiers & EventModifiers.Shift) != 0)
                text.Append("Shift + ");
            if ((modifiers & EventModifiers.Command) != 0)
                text.Append(Application.platform == RuntimePlatform.OSXEditor ? "Command + " : "Win + ");
            text.Append(key == KeyCode.Space
                ? UniConfigLoc.IsChinese ? "空格" : "Space"
                : key.ToString());
            return text.ToString();
        }

        private static void OnGlobalEvent()
        {
            if (IsCapturing)
                return;

            UniConfigEditorSettings settings = UniConfigEditorSettings.instance;
            if (settings == null || !settings.editorHotkeyEnabled)
                return;

            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown)
                return;
            if (current.keyCode == KeyCode.None || IsModifierKey(current.keyCode))
                return;
            if (current.keyCode != settings.ResolvedHotkeyKey())
                return;
            if (Significant(current.modifiers) != settings.ResolvedHotkeyModifiers())
                return;
            if (EditorApplication.timeSinceStartup - _lastToggleTime < 0.3d)
            {
                current.Use();
                return;
            }

            _lastToggleTime = EditorApplication.timeSinceStartup;
            current.Use();
            if (_toggleQueued)
                return;
            _toggleQueued = true;
            EditorApplication.delayCall += () =>
            {
                _toggleQueued = false;
                ConfigEditorWindow.Toggle();
            };
        }
    }
}
