#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 字段约束编辑窗口（外键 / 公式）。
    /// </summary>
    public sealed class ConstraintEditorWindow : EditorWindow
    {
        private const double FormulaApplyDebounce = 0.45d;
        private static readonly string[] KindLabelsZh = { "外键引用", "公式计算" };
        private static readonly string[] KindLabelsEn = { "Foreign Key", "Formula" };
        private readonly HashSet<string> _appliedSignatures = new(StringComparer.Ordinal);
        private double _formulaApplyDueAt;
        private bool _formulaApplyPending;
        private string _formulaWarning = string.Empty;
        private Vector2 _helpScroll;
        private Vector2 _listScroll;

        private string _moduleName;
        private string _pendingApplySignature = string.Empty;
        private int _selectedIndex = -1;
        private string _status = string.Empty;
        private string _tableName;

        private void OnGUI()
        {
            ConfigTable table = CurrentTable();
            if (table == null)
            {
                EditorGUILayout.HelpBox(UniConfigLoc.T("constraint.table_missing"), MessageType.Warning);
                return;
            }

            if (!table.dataLoaded)
            {
                EditorGUILayout.HelpBox(UniConfigLoc.T("constraint.need_load"), MessageType.Info);
                if (GUILayout.Button(UniConfigLoc.T("constraint.load_now"), GUILayout.Height(28)))
                {
                    EditorConfigMgr.EnsureTableDataLoaded(table);
                    Repaint();
                }

                return;
            }

            table.constraints ??= new List<FieldConstraint>();
            ConstraintEvaluator.EnsureForeignKeyState(table);

            DrawToolbar(table);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            DrawConstraintList(table);
            DrawConstraintDetail(table);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            DrawHelp();

            string footer = table.HasFkViolations
                ? string.IsNullOrEmpty(table.fkStatusDetail)
                    ? UniConfigLoc.T("constraint.fk_invalid_status")
                    : table.fkStatusDetail
                : string.IsNullOrEmpty(_status)
                    ? UniConfigLoc.T("ready")
                    : _status;
            Color prev = GUI.contentColor;
            if (table.HasFkViolations)
                GUI.contentColor = EditorGUIUtility.isProSkin
                    ? new Color(0.95f, 0.62f, 0.62f, 1f)
                    : new Color(0.78f, 0.28f, 0.28f, 1f);
            EditorGUILayout.LabelField(footer, EditorStyles.miniLabel);
            GUI.contentColor = prev;
        }

        public static void Open(ConfigTable table)
        {
            if (table == null) return;
            ConstraintEditorWindow window = GetWindow<ConstraintEditorWindow>(true, "Constraints", true);
            window.minSize = new Vector2(640, 420);
            window._moduleName = table.moduleName;
            window._tableName = table.tableName;
            window._selectedIndex = table.constraints != null && table.constraints.Count > 0 ? 0 : -1;
            window._status = string.Empty;
            window._formulaWarning = string.Empty;
            window._appliedSignatures.Clear();
            window._formulaApplyPending = false;
            window.Show();
            window.Focus();
        }

        private ConfigTable CurrentTable()
        {
            return EditorConfigMgr.GetModule(_moduleName)?.FindTable(_tableName);
        }

        private void DrawToolbar(ConfigTable table)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"{_moduleName}/{_tableName}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(UniConfigLoc.T("constraint.add"), EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                FieldConstraint c = new()
                {
                    targetField = table.fields.Count > 0 ? table.fields[0].name : string.Empty,
                    Kind = FieldConstraintKind.Formula,
                    lookupKeyField = "id",
                    expression = string.Empty
                };
                table.constraints.Add(c);
                _selectedIndex = table.constraints.Count - 1;
                _formulaWarning = string.Empty;
                _formulaApplyPending = false;
                table.MarkFkValidationDirty();
                _status = UniConfigLoc.T("constraint.added");
            }

            using (new EditorGUI.DisabledScope(_selectedIndex < 0 || _selectedIndex >= table.constraints.Count))
            {
                if (GUILayout.Button(UniConfigLoc.T("constraint.remove"), EditorStyles.toolbarButton,
                        GUILayout.Width(90)))
                {
                    table.constraints.RemoveAt(_selectedIndex);
                    _selectedIndex = Mathf.Min(_selectedIndex, table.constraints.Count - 1);
                    _formulaWarning = string.Empty;
                    _formulaApplyPending = false;
                    table.MarkFkValidationDirty();
                    ConstraintEvaluator.EnsureForeignKeyState(table);
                    _status = UniConfigLoc.T("constraint.removed");
                }
            }

            if (GUILayout.Button(UniConfigLoc.T("constraint.validate_fk"), EditorStyles.toolbarButton,
                    GUILayout.Width(100)))
                ValidateFk(table);

            if (GUILayout.Button(UniConfigLoc.T("constraint.recalc_all"), EditorStyles.toolbarButton,
                    GUILayout.Width(100)))
                RecalcAllFormulas(table);

            if (GUILayout.Button(UniConfigLoc.T("constraint.save"), EditorStyles.toolbarButton, GUILayout.Width(70)))
                SaveTable(table);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConstraintList(ConfigTable table)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            GUILayout.Label(UniConfigLoc.T("constraint.list"), EditorStyles.boldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUI.skin.box);

            for (int i = 0; i < table.constraints.Count; i++)
            {
                FieldConstraint c = table.constraints[i];
                string kind = c.Kind == FieldConstraintKind.ForeignKey
                    ? UniConfigLoc.T("constraint.kind_fk")
                    : UniConfigLoc.T("constraint.kind_formula");
                string label = string.IsNullOrEmpty(c.targetField)
                    ? $"[{i + 1}] {kind}"
                    : $"{c.targetField} · {kind}";

                Rect rect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
                bool selected = i == _selectedIndex;
                bool fkInvalid = table.IsFkConstraintInvalid(c);
                if (Event.current.type == EventType.Repaint)
                {
                    if (fkInvalid)
                        EditorGUI.DrawRect(rect, new Color(0.65f, 0.12f, 0.12f, selected ? 0.75f : 0.55f));
                    else if (selected)
                        EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.90f, 0.35f));
                }

                Color prev = GUI.contentColor;
                if (fkInvalid)
                    GUI.contentColor = Color.white;
                if (GUI.Toggle(rect, selected, label, EditorStyles.label) && !selected)
                {
                    _selectedIndex = i;
                    _formulaWarning = string.Empty;
                    _formulaApplyPending = false;
                }

                GUI.contentColor = prev;
            }

            if (table.constraints.Count == 0)
                EditorGUILayout.LabelField(UniConfigLoc.T("constraint.empty"), EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawConstraintDetail(ConfigTable table)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(UniConfigLoc.T("constraint.detail"), EditorStyles.boldLabel);

            if (_selectedIndex < 0 || _selectedIndex >= table.constraints.Count)
            {
                EditorGUILayout.HelpBox(UniConfigLoc.T("constraint.select_one"), MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            FieldConstraint c = table.constraints[_selectedIndex];
            string[] fieldNames = table.fields.Select(f => f.name).ToArray();
            if (fieldNames.Length == 0)
            {
                EditorGUILayout.HelpBox(UniConfigLoc.T("constraint.no_fields"), MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();
            int fieldIndex = Mathf.Max(0, Array.IndexOf(fieldNames, c.targetField));
            fieldIndex = EditorGUILayout.Popup(UniConfigLoc.T("constraint.target_field"), fieldIndex, fieldNames);
            c.targetField = fieldNames[fieldIndex];

            string[] kinds = UniConfigLoc.IsChinese ? KindLabelsZh : KindLabelsEn;
            c.kind = EditorGUILayout.Popup(UniConfigLoc.T("constraint.kind"), c.kind, kinds);
            bool headerChanged = EditorGUI.EndChangeCheck();
            if (headerChanged)
            {
                table.MarkFkValidationDirty();
                ConstraintEvaluator.EnsureForeignKeyState(table);
            }

            EditorGUILayout.Space(6);
            if (c.Kind == FieldConstraintKind.ForeignKey)
            {
                _formulaWarning = string.Empty;
                DrawForeignKeyEditor(table, c);
            }
            else
            {
                DrawFormulaEditor(table, c, headerChanged);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawForeignKeyEditor(ConfigTable hostTable, FieldConstraint c)
        {
            string[] modules = EditorConfigMgr.Modules.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (modules.Length == 0)
            {
                EditorGUILayout.HelpBox(UniConfigLoc.T("constraint.no_modules"), MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            int moduleIndex = Mathf.Max(0, Array.IndexOf(modules, c.fkModule));
            moduleIndex = EditorGUILayout.Popup(UniConfigLoc.T("constraint.fk_module"), moduleIndex, modules);
            c.fkModule = modules[moduleIndex];

            ConfigModule module = EditorConfigMgr.GetModule(c.fkModule);
            string[] tables = module?.tables?.Select(t => t.tableName).OrderBy(x => x).ToArray() ??
                              Array.Empty<string>();
            if (tables.Length == 0)
            {
                EditorGUILayout.HelpBox(UniConfigLoc.T("constraint.no_tables"), MessageType.Warning);
                if (EditorGUI.EndChangeCheck())
                {
                    hostTable.MarkFkValidationDirty();
                    ConstraintEvaluator.EnsureForeignKeyState(hostTable);
                }

                return;
            }

            int tableIndex = Mathf.Max(0, Array.IndexOf(tables, c.fkTable));
            tableIndex = EditorGUILayout.Popup(UniConfigLoc.T("constraint.fk_table"), tableIndex, tables);
            c.fkTable = tables[tableIndex];

            ConfigTable refTable = module.FindTable(c.fkTable);
            string[] fields = refTable?.fields?.Select(f => f.name).ToArray() ?? Array.Empty<string>();

            if (fields.Length == 0)
            {
                EditorGUILayout.HelpBox(UniConfigLoc.T("constraint.fk_need_fields"), MessageType.Info);
                c.fkField = EditorGUILayout.TextField(UniConfigLoc.T("constraint.fk_field"), c.fkField ?? string.Empty);
            }
            else
            {
                int fieldIndex = Mathf.Max(0, Array.IndexOf(fields, c.fkField));
                fieldIndex = EditorGUILayout.Popup(UniConfigLoc.T("constraint.fk_field"), fieldIndex, fields);
                c.fkField = fields[fieldIndex];
            }

            if (EditorGUI.EndChangeCheck())
            {
                hostTable.MarkFkValidationDirty();
                ConstraintEvaluator.EnsureForeignKeyState(hostTable);
            }

            if (hostTable.IsFkConstraintInvalid(c))
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(hostTable.fkStatusDetail)
                        ? UniConfigLoc.T("constraint.fk_invalid_status")
                        : hostTable.fkStatusDetail,
                    MessageType.Error);
        }

        private void DrawFormulaEditor(ConfigTable table, FieldConstraint c, bool forceCheck)
        {
            EditorGUILayout.HelpBox(UniConfigLoc.T("constraint.formula_hint"), MessageType.Info);

            if (string.IsNullOrWhiteSpace(c.lookupKeyField))
                c.lookupKeyField = "id";

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField(UniConfigLoc.T("constraint.expression"));
            c.expression = EditorGUILayout.TextArea(c.expression ?? string.Empty, GUILayout.MinHeight(72));
            bool exprChanged = EditorGUI.EndChangeCheck() || forceCheck;

            if (exprChanged)
            {
                RefreshFormulaValidation(table, c);
                string signature = BuildFormulaSignature(c);
                if (string.IsNullOrEmpty(_formulaWarning) &&
                    !string.IsNullOrWhiteSpace(c.expression) &&
                    !_appliedSignatures.Contains(signature))
                {
                    _formulaApplyPending = true;
                    _pendingApplySignature = signature;
                    _formulaApplyDueAt = EditorApplication.timeSinceStartup + FormulaApplyDebounce;
                }
                else
                {
                    _formulaApplyPending = false;
                }
            }

            if (!string.IsNullOrEmpty(_formulaWarning))
                EditorGUILayout.HelpBox(_formulaWarning, MessageType.Warning);
            else if (!string.IsNullOrWhiteSpace(c.expression))
                EditorGUILayout.HelpBox(UniConfigLoc.T("constraint.formula_ok"), MessageType.None);

            // 停顿输入后：合法公式自动全表应用一次
            if (_formulaApplyPending &&
                EditorApplication.timeSinceStartup >= _formulaApplyDueAt &&
                string.Equals(BuildFormulaSignature(c), _pendingApplySignature, StringComparison.Ordinal))
                AutoApplyFormulaOnce(table, c, _pendingApplySignature);
            else if (_formulaApplyPending) Repaint();
        }

        private static string BuildFormulaSignature(FieldConstraint c)
        {
            return $"{c.targetField}|{c.kind}|{c.expression}|{c.lookupKeyField}";
        }

        private void RefreshFormulaValidation(ConfigTable table, FieldConstraint c)
        {
            List<string> errors = ConstraintEvaluator.ValidateFormulaConstraint(table, c);
            _formulaWarning = errors.Count == 0 ? string.Empty : string.Join("\n", errors);
        }

        private void AutoApplyFormulaOnce(ConfigTable table, FieldConstraint c, string signature)
        {
            _formulaApplyPending = false;
            if (_appliedSignatures.Contains(signature))
                return;

            List<string> warnings = new();
            int updated = ConstraintEvaluator.ApplyOneFormula(table, c, warnings);
            _appliedSignatures.Add(signature);
            if (warnings.Count > 0)
            {
                _formulaWarning = string.Join("\n", warnings);
                _status = UniConfigLoc.F("constraint.fk_fail", warnings.Count);
            }
            else
            {
                _status = UniConfigLoc.F("constraint.formula_done", updated);
            }

            Repaint();
        }

        private void DrawHelp()
        {
            _helpScroll = EditorGUILayout.BeginScrollView(_helpScroll, GUILayout.Height(100));
            EditorGUILayout.HelpBox(UniConfigLoc.T("constraint.help"), MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void ValidateFk(ConfigTable table)
        {
            try
            {
                List<string> errors = ConstraintEvaluator.ValidateForeignKeys(table);
                if (errors.Count == 0)
                {
                    _status = UniConfigLoc.T("constraint.fk_ok");
                    EditorUtility.DisplayDialog("UniConfig", UniConfigLoc.T("constraint.fk_ok"), UniConfigLoc.T("ok"));
                    return;
                }

                StringBuilder sb = new();
                int show = Math.Min(errors.Count, 30);
                for (int i = 0; i < show; i++)
                    sb.AppendLine(errors[i]);
                if (errors.Count > show)
                    sb.AppendLine($"... +{errors.Count - show}");

                _status = UniConfigLoc.F("constraint.fk_fail", errors.Count);
                EditorUtility.DisplayDialog("UniConfig", sb.ToString(), UniConfigLoc.T("ok"));
            }
            catch (Exception ex)
            {
                _status = ex.Message;
                EditorUtility.DisplayDialog("UniConfig", ex.Message, UniConfigLoc.T("ok"));
            }
        }

        private void RecalcAllFormulas(ConfigTable table)
        {
            List<string> warnings = new();
            int updated = ConstraintEvaluator.ApplyFormulas(table, warnings);
            if (warnings.Count > 0)
            {
                _formulaWarning = string.Join("\n", warnings.Take(20));
                EditorUtility.DisplayDialog("UniConfig",
                    UniConfigLoc.F("constraint.recalc_warn", updated, warnings.Count) + "\n\n" + _formulaWarning,
                    UniConfigLoc.T("ok"));
                _status = UniConfigLoc.F("constraint.recalc_warn", updated, warnings.Count);
            }
            else
            {
                _status = UniConfigLoc.F("constraint.formula_done", updated);
                EditorUtility.DisplayDialog("UniConfig", _status, UniConfigLoc.T("ok"));
            }

            foreach (FieldConstraint c in table.constraints)
                if (c?.Kind == FieldConstraintKind.Formula)
                    _appliedSignatures.Add(BuildFormulaSignature(c));
        }

        private void SaveTable(ConfigTable table)
        {
            try
            {
                List<string> formulaErrors = ConstraintEvaluator.ValidateAllFormulas(table);
                if (formulaErrors.Count > 0)
                {
                    _formulaWarning = string.Join("\n", formulaErrors);
                    EditorUtility.DisplayDialog("UniConfig",
                        UniConfigLoc.T("constraint.save_blocked") + "\n\n" + _formulaWarning,
                        UniConfigLoc.T("ok"));
                    _status = UniConfigLoc.T("constraint.save_blocked");
                    return;
                }

                // 保存前确保公式结果已写回
                ConstraintEvaluator.ApplyFormulas(table);
                EditorConfigMgr.SaveTable(table);
                table.MarkFkValidationDirty();
                ConstraintEvaluator.EnsureForeignKeyState(table);
                _status = table.HasFkViolations
                    ? string.IsNullOrEmpty(table.fkStatusDetail)
                        ? UniConfigLoc.T("constraint.fk_invalid_status")
                        : table.fkStatusDetail
                    : UniConfigLoc.F("status.saved_table", table.moduleName, table.tableName);
            }
            catch (Exception ex)
            {
                _status = ex.Message;
                EditorUtility.DisplayDialog("UniConfig", ex.Message, UniConfigLoc.T("ok"));
            }
        }
    }
}