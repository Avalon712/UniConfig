#region

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 枚举成员变化后，让用户把旧值手动指定成新成员。
    /// </summary>
    public sealed class EnumRepairWindow : EditorWindow
    {
        internal Action<bool> OnFinished;

        private List<EnumRepairCase> _cases = new();
        private Vector2 _scroll;
        private bool _finished;
        private bool _shown;

        private void OnEnable()
        {
            hideFlags = HideFlags.DontSave;
            titleContent = new GUIContent(UniConfigLoc.T("enum_repair.title"));
            minSize = new Vector2(720f, 420f);
        }

        internal void Setup(List<EnumRepairCase> cases)
        {
            _cases = cases ?? new List<EnumRepairCase>();
        }

        private void OnDestroy()
        {
            if (_finished || !_shown)
                return;
            _finished = true;
            EnumRepair.NotifyDeferred();
            OnFinished?.Invoke(false);
        }

        private void OnGUI()
        {
            _shown = true;
            EditorGUILayout.HelpBox(UniConfigLoc.T("enum_repair.help"), MessageType.Warning);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _cases.Count; i++)
                DrawCase(_cases[i]);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool canApply = EnumRepair.CanApply(_cases);
            EditorGUI.BeginDisabledGroup(!canApply);
            if (GUILayout.Button(UniConfigLoc.T("enum_repair.apply"), GUILayout.Width(120f), GUILayout.Height(24f)))
                ApplyAndClose();
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button(UniConfigLoc.T("enum_repair.later"), GUILayout.Width(120f), GUILayout.Height(24f)))
                Finish(false);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
        }

        private void DrawCase(EnumRepairCase repairCase)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.SelectableLabel(
                repairCase.TypeFullName,
                EditorStyles.boldLabel,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (repairCase.Current == null || repairCase.Current.Count == 0 ||
                repairCase.PopupLabels == null || repairCase.PopupLabels.Length == 0)
            {
                EditorGUILayout.HelpBox(UniConfigLoc.T("enum_repair.no_members"), MessageType.Error);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(UniConfigLoc.T("enum_repair.by_name"), EditorStyles.miniButtonLeft, GUILayout.Width(128f)))
            {
                for (int i = 0; i < repairCase.Rows.Count; i++)
                    repairCase.Rows[i].NewIndex = EnumRepair.SuggestByName(repairCase, repairCase.Rows[i]);
            }

            if (GUILayout.Button(UniConfigLoc.T("enum_repair.by_order"), EditorStyles.miniButtonRight, GUILayout.Width(140f)))
            {
                for (int i = 0; i < repairCase.Rows.Count; i++)
                    repairCase.Rows[i].NewIndex = EnumRepair.SuggestByOrder(repairCase, repairCase.Rows[i]);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(UniConfigLoc.T("enum_repair.old_value"), EditorStyles.miniBoldLabel, GUILayout.Width(72f));
            GUILayout.Label(UniConfigLoc.T("enum_repair.old_name"), EditorStyles.miniBoldLabel, GUILayout.Width(160f));
            GUILayout.Label(UniConfigLoc.T("enum_repair.status"), EditorStyles.miniBoldLabel, GUILayout.Width(148f));
            GUILayout.Label(UniConfigLoc.T("enum_repair.new_member"), EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < repairCase.Rows.Count; i++)
                DrawRow(repairCase, repairCase.Rows[i]);
        }

        private static void DrawRow(EnumRepairCase repairCase, EnumRepairRow row)
        {
            Color previous = GUI.contentColor;
            if (row.Kind != RepairRowKind.Same)
                GUI.contentColor = EditorGUIUtility.isProSkin
                    ? new Color(1f, 0.78f, 0.45f)
                    : new Color(0.7f, 0.38f, 0.05f);

            EditorGUILayout.BeginHorizontal();
            string valueText = row.HasValue
                ? row.Value.ToString(CultureInfo.InvariantCulture)
                : UniConfigLoc.T("enum_repair.none_value");
            GUILayout.Label(valueText, GUILayout.Width(72f));
            GUILayout.Label(row.OldName ?? string.Empty, GUILayout.Width(160f));
            GUILayout.Label(StatusText(row.Kind), GUILayout.Width(148f));
            int next = EditorGUILayout.Popup(row.NewIndex, repairCase.PopupLabels, GUILayout.MinWidth(180f));
            if (next >= 0 && next < repairCase.PopupLabels.Length)
                row.NewIndex = next;
            EditorGUILayout.EndHorizontal();
            GUI.contentColor = previous;
        }

        private static string StatusText(RepairRowKind kind)
        {
            switch (kind)
            {
                case RepairRowKind.ValueChanged:
                    return UniConfigLoc.T("enum_repair.status_value");
                case RepairRowKind.Missing:
                    return UniConfigLoc.T("enum_repair.status_missing");
                case RepairRowKind.Unparsed:
                    return UniConfigLoc.T("enum_repair.status_cell");
                default:
                    return UniConfigLoc.T("enum_repair.status_same");
            }
        }

        private void ApplyAndClose()
        {
            try
            {
                EnumRepair.Apply(_cases);
                Finish(true);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("UniConfig", ex.Message, UniConfigLoc.T("ok"));
            }
        }

        private void Finish(bool applied)
        {
            if (_finished)
                return;
            _finished = true;
            if (!applied)
                EnumRepair.NotifyDeferred();
            OnFinished?.Invoke(applied);
            Close();
        }
    }
}
