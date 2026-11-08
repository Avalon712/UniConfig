#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 字段约束：外键校验、公式计算与数值范围。
    /// </summary>
    public static class ConstraintEvaluator
    {
        private static readonly Regex TokenRegex = new(@"\{([^{}]+)\}", RegexOptions.Compiled);

        public static bool IsFormulaField(ConfigTable table, string fieldName)
        {
            if (table?.constraints == null || string.IsNullOrEmpty(fieldName))
                return false;
            foreach (FieldConstraint c in table.constraints)
                if (c != null &&
                    c.Kind == FieldConstraintKind.Formula &&
                    string.Equals(c.targetField, fieldName, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static List<string> CollectFormulaFieldNames(ConfigTable table)
        {
            List<string> names = new();
            if (table?.constraints == null) return names;
            foreach (FieldConstraint c in table.constraints)
                if (c != null &&
                    c.Kind == FieldConstraintKind.Formula &&
                    !string.IsNullOrEmpty(c.targetField) &&
                    !names.Contains(c.targetField))
                    names.Add(c.targetField);
            return names;
        }

        public static List<string> ValidateForeignKeys(ConfigTable table)
        {
            RefreshForeignKeyState(table);
            List<string> errors = new();
            if (table?.constraints == null || table.items == null)
                return errors;

            foreach (FieldConstraint c in table.constraints)
            {
                if (c == null || c.Kind != FieldConstraintKind.ForeignKey)
                    continue;
                if (string.IsNullOrEmpty(c.targetField) ||
                    string.IsNullOrEmpty(c.fkModule) ||
                    string.IsNullOrEmpty(c.fkTable) ||
                    string.IsNullOrEmpty(c.fkField))
                {
                    errors.Add(UniConfigLoc.F("constraint.err_fk_incomplete", c?.targetField ?? "?"));
                    continue;
                }

                ConfigModule refModule = EditorConfigMgr.GetModule(c.fkModule);
                ConfigTable refTable = refModule?.FindTable(c.fkTable);
                if (refTable == null)
                {
                    errors.Add(UniConfigLoc.F("constraint.err_fk_table", c.targetField, c.fkModule, c.fkTable));
                    continue;
                }

                HashSet<string> allowed = GetForeignKeyAllowedValues(refTable, c.fkModule, c.fkTable, c.fkField);
                for (int row = 0; row < table.items.Count; row++)
                {
                    string value = table.items[row].GetValue(c.targetField);
                    if (string.IsNullOrWhiteSpace(value))
                        continue;
                    if (!allowed.Contains(value.Trim()))
                        errors.Add(UniConfigLoc.F(
                            "constraint.err_fk_value",
                            row + 1, c.targetField, value, c.fkModule, c.fkTable, c.fkField));
                }
            }

            return errors;
        }

        /// <summary>
        /// 刷新外键校验缓存（单元格违规 + 不合法约束目标字段 + 状态栏详情）。
        /// 引用表未加载时只扫描磁盘上该列取值，不把整表灌进编辑器内存。
        /// </summary>
        public static void RefreshForeignKeyState(ConfigTable table)
        {
            if (table == null) return;

            table.fkCellViolations ??= new HashSet<string>();
            table.fkInvalidTargetFields ??= new HashSet<string>(StringComparer.Ordinal);
            table.fkCellViolations.Clear();
            table.fkInvalidTargetFields.Clear();
            table.fkStatusDetail = string.Empty;
            table.fkValidationDirty = false;

            if (table.constraints == null || table.items == null || !table.dataLoaded)
                return;

            string firstDetail = null;
            int issueCount = 0;

            foreach (FieldConstraint c in table.constraints)
            {
                if (c == null || c.Kind != FieldConstraintKind.ForeignKey)
                    continue;

                if (string.IsNullOrEmpty(c.targetField) ||
                    string.IsNullOrEmpty(c.fkModule) ||
                    string.IsNullOrEmpty(c.fkTable) ||
                    string.IsNullOrEmpty(c.fkField))
                {
                    if (!string.IsNullOrEmpty(c.targetField))
                        table.fkInvalidTargetFields.Add(c.targetField);
                    issueCount++;
                    firstDetail ??= UniConfigLoc.F(
                        "constraint.fk_detail_incomplete",
                        c.targetField ?? "?",
                        $"{c.fkModule}/{c.fkTable}.{c.fkField}");
                    continue;
                }

                ConfigModule refModule = EditorConfigMgr.GetModule(c.fkModule);
                ConfigTable refTable = refModule?.FindTable(c.fkTable);
                if (refTable == null)
                {
                    table.fkInvalidTargetFields.Add(c.targetField);
                    issueCount++;
                    firstDetail ??= UniConfigLoc.F(
                        "constraint.fk_detail_missing_table",
                        c.targetField,
                        c.fkModule,
                        c.fkTable,
                        c.fkField);
                    continue;
                }

                HashSet<string> allowed = GetForeignKeyAllowedValues(refTable, c.fkModule, c.fkTable, c.fkField);
                bool anyBad = false;
                for (int row = 0; row < table.items.Count; row++)
                {
                    string value = table.items[row].GetValue(c.targetField);
                    if (string.IsNullOrWhiteSpace(value))
                        continue;
                    if (!allowed.Contains(value.Trim()))
                    {
                        anyBad = true;
                        issueCount++;
                        table.fkCellViolations.Add(ConfigTable.FkCellKey(row, c.targetField));
                        firstDetail ??= UniConfigLoc.F(
                            "constraint.fk_detail_value",
                            row + 1,
                            c.targetField,
                            value.Trim(),
                            c.fkModule,
                            c.fkTable,
                            c.fkField);
                    }
                }

                if (anyBad)
                    table.fkInvalidTargetFields.Add(c.targetField);
            }

            if (issueCount > 0 && !string.IsNullOrEmpty(firstDetail))
                table.fkStatusDetail = issueCount <= 1
                    ? firstDetail
                    : UniConfigLoc.F("constraint.fk_detail_more", firstDetail, issueCount);
        }

        /// <summary>
        /// 获取外键允许值：引用表已在内存则用内存；否则只扫磁盘该列。
        /// </summary>
        public static HashSet<string> GetForeignKeyAllowedValues(
            ConfigTable refTable,
            string moduleName,
            string tableName,
            string fieldName)
        {
            if (refTable != null && refTable.dataLoaded && refTable.items != null)
            {
                HashSet<string> allowed = new(StringComparer.Ordinal);
                foreach (ConfigItem item in refTable.items)
                {
                    string v = item.GetValue(fieldName);
                    if (!string.IsNullOrWhiteSpace(v))
                        allowed.Add(v.Trim());
                }

                return allowed;
            }

            return ConfigRawStorage.CollectColumnValues(moduleName, tableName, fieldName);
        }

        public static void EnsureForeignKeyState(ConfigTable table)
        {
            if (table == null) return;
            if (table.fkValidationDirty || table.fkCellViolations == null || table.fkInvalidTargetFields == null)
                RefreshForeignKeyState(table);
        }

        /// <summary>
        /// 校验单条公式约束是否合法（不写数据）。
        /// </summary>
        public static List<string> ValidateFormulaConstraint(ConfigTable table, FieldConstraint c)
        {
            List<string> errors = new();
            if (table == null || c == null || c.Kind != FieldConstraintKind.Formula)
                return errors;

            if (string.IsNullOrWhiteSpace(c.targetField))
            {
                errors.Add(UniConfigLoc.T("constraint.err_no_target"));
                return errors;
            }

            if (table.GetFieldIndex(c.targetField) < 0)
            {
                errors.Add(UniConfigLoc.F("constraint.err_target_missing", c.targetField));
                return errors;
            }

            if (string.IsNullOrWhiteSpace(c.expression))
            {
                errors.Add(UniConfigLoc.F("constraint.err_empty_expr", c.targetField));
                return errors;
            }

            MatchCollection matches = TokenRegex.Matches(c.expression);
            if (matches.Count == 0)
            {
                // 允许纯数字表达式
                try
                {
                    SimpleMath.Evaluate(c.expression.Trim());
                }
                catch (Exception ex)
                {
                    errors.Add(UniConfigLoc.F("constraint.err_expr_parse", c.targetField, ex.Message));
                }

                return errors;
            }

            foreach (Match match in matches)
            {
                string token = match.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(token))
                {
                    errors.Add(UniConfigLoc.F("constraint.err_empty_token", c.targetField));
                    continue;
                }

                int slash = token.IndexOf('/');
                int dot = token.LastIndexOf('.');
                if (slash > 0 && dot > slash)
                {
                    string moduleName = token.Substring(0, slash).Trim();
                    string tableName = token.Substring(slash + 1, dot - slash - 1).Trim();
                    string fieldName = token.Substring(dot + 1).Trim();
                    ConfigModule mod = EditorConfigMgr.GetModule(moduleName);
                    ConfigTable refTable = mod?.FindTable(tableName);
                    if (refTable == null)
                    {
                        errors.Add(UniConfigLoc.F("constraint.err_cross_table", c.targetField, moduleName, tableName));
                        continue;
                    }

                    if (refTable.fields != null && refTable.fields.Count > 0 && refTable.GetFieldIndex(fieldName) < 0)
                        errors.Add(UniConfigLoc.F("constraint.err_cross_field", c.targetField, token));
                }
                else
                {
                    if (string.Equals(token, c.targetField, StringComparison.Ordinal))
                        errors.Add(UniConfigLoc.F("constraint.err_self_ref", c.targetField));
                    else if (table.GetFieldIndex(token) < 0)
                        errors.Add(UniConfigLoc.F("constraint.err_local_field", c.targetField, token));
                }
            }

            // 试算第一行（或空行）验证语法
            if (errors.Count == 0)
            {
                ConfigItem probe = table.items != null && table.items.Count > 0
                    ? table.items[0]
                    : new ConfigItem();
                EvaluateFormula(table, probe, c.expression, c.lookupKeyField, out string evalError);
                if (evalError != null)
                    errors.Add(UniConfigLoc.F("constraint.err_expr_parse", c.targetField, evalError));
            }

            return errors;
        }

        public static List<string> ValidateAllFormulas(ConfigTable table)
        {
            List<string> errors = new();
            if (table?.constraints == null) return errors;
            foreach (FieldConstraint c in table.constraints)
                if (c?.Kind == FieldConstraintKind.Formula)
                    errors.AddRange(ValidateFormulaConstraint(table, c));
            return errors;
        }

        public static bool IsScalarNumeric(SupportableFieldType type, out bool integer)
        {
            switch (type)
            {
                case SupportableFieldType.Int16:
                case SupportableFieldType.Int32:
                case SupportableFieldType.Int64:
                case SupportableFieldType.UInt16:
                case SupportableFieldType.UInt32:
                case SupportableFieldType.UInt64:
                    integer = true;
                    return true;
                case SupportableFieldType.Single:
                case SupportableFieldType.Double:
                    integer = false;
                    return true;
                default:
                    integer = false;
                    return false;
            }
        }

        public static bool TryGetRange(ConfigTable table, ConfigField field, out double min, out double max)
        {
            min = 0;
            max = 0;
            if (table?.constraints == null || field == null || !IsScalarNumeric(field.type, out _))
                return false;

            foreach (FieldConstraint c in table.constraints)
            {
                if (c == null || c.Kind != FieldConstraintKind.Range)
                    continue;
                if (!string.Equals(c.targetField, field.name, StringComparison.Ordinal))
                    continue;
                min = c.rangeMin;
                max = c.rangeMax;
                return true;
            }

            return false;
        }

        public static List<string> ValidateAllRanges(ConfigTable table)
        {
            List<string> errors = new();
            if (table?.constraints == null) return errors;
            foreach (FieldConstraint c in table.constraints)
                if (c?.Kind == FieldConstraintKind.Range)
                    errors.AddRange(ValidateRangeConstraint(table, c));
            return errors;
        }

        public static List<string> ValidateRangeConstraint(ConfigTable table, FieldConstraint c)
        {
            List<string> errors = new();
            if (c == null || c.Kind != FieldConstraintKind.Range)
                return errors;
            if (string.IsNullOrEmpty(c.targetField))
            {
                errors.Add(UniConfigLoc.T("constraint.err_no_target"));
                return errors;
            }

            ConfigField field = FindField(table, c.targetField);
            if (field == null)
            {
                errors.Add(UniConfigLoc.F("constraint.err_target_missing", c.targetField));
                return errors;
            }

            if (!IsScalarNumeric(field.type, out _))
                errors.Add(UniConfigLoc.F("constraint.err_range_type", c.targetField));
            if (c.rangeMin > c.rangeMax)
                errors.Add(UniConfigLoc.F("constraint.err_range_order", c.targetField));
            return errors;
        }

        public static bool TryParseScalar(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }

        public static double ClampScalar(double value, double rangeMin, double rangeMax, SupportableFieldType type)
        {
            if (rangeMin > rangeMax)
            {
                double swap = rangeMin;
                rangeMin = rangeMax;
                rangeMax = swap;
            }

            GetScalarLimits(type, out double typeMin, out double typeMax);
            double min = Math.Max(rangeMin, typeMin);
            double max = Math.Min(rangeMax, typeMax);
            if (min > max)
            {
                min = typeMin;
                max = typeMax;
            }

            if (double.IsNaN(value) || value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static string FormatScalar(double value, SupportableFieldType type)
        {
            if (!IsScalarNumeric(type, out bool integer))
                return value.ToString("G17", CultureInfo.InvariantCulture);

            GetScalarLimits(type, out double typeMin, out double typeMax);
            if (double.IsNaN(value) || value < typeMin) value = typeMin;
            if (value > typeMax) value = typeMax;

            if (!integer)
            {
                double rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
                return rounded.ToString("0.######", CultureInfo.InvariantCulture);
            }

            double whole = Math.Round(value, MidpointRounding.AwayFromZero);
            return whole.ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 慢速拖动大约 200 像素走过整个范围。鼠标越快，单帧位移越大，再用 1.5 次方放大，数值变化跟手速正相关。
        /// </summary>
        public static double RangeDragDelta(double rangeMin, double rangeMax, float mouseDeltaX, bool integer)
        {
            double absPixels = Math.Abs(mouseDeltaX);
            if (absPixels < 1e-6)
                return 0;

            double span = Math.Abs(rangeMax - rangeMin);
            double perPixel = span / 200.0;
            if (integer)
                perPixel = Math.Max(perPixel, 1.0 / 12.0);
            if (perPixel <= 0)
                return 0;

            double acceleratedPixels = Math.Sign(mouseDeltaX) * Math.Pow(absPixels, 1.5);
            return acceleratedPixels * perPixel;
        }

        private static ConfigField FindField(ConfigTable table, string fieldName)
        {
            if (table?.fields == null || string.IsNullOrEmpty(fieldName))
                return null;
            foreach (ConfigField field in table.fields)
                if (field != null && string.Equals(field.name, fieldName, StringComparison.Ordinal))
                    return field;
            return null;
        }

        private static void GetScalarLimits(SupportableFieldType type, out double min, out double max)
        {
            switch (type)
            {
                case SupportableFieldType.Int16:
                    min = short.MinValue;
                    max = short.MaxValue;
                    return;
                case SupportableFieldType.Int32:
                    min = int.MinValue;
                    max = int.MaxValue;
                    return;
                case SupportableFieldType.Int64:
                    min = long.MinValue;
                    max = long.MaxValue;
                    return;
                case SupportableFieldType.UInt16:
                    min = ushort.MinValue;
                    max = ushort.MaxValue;
                    return;
                case SupportableFieldType.UInt32:
                    min = uint.MinValue;
                    max = uint.MaxValue;
                    return;
                case SupportableFieldType.UInt64:
                    min = 0d;
                    max = ulong.MaxValue;
                    return;
                case SupportableFieldType.Single:
                    min = float.MinValue;
                    max = float.MaxValue;
                    return;
                case SupportableFieldType.Double:
                    min = double.MinValue;
                    max = double.MaxValue;
                    return;
                default:
                    min = double.NegativeInfinity;
                    max = double.PositiveInfinity;
                    return;
            }
        }

        /// <summary>
        /// 表达式是否引用了本表指定字段（不含跨表 token）。
        /// </summary>
        public static bool ExpressionDependsOnLocalField(string expression, string fieldName)
        {
            if (string.IsNullOrEmpty(expression) || string.IsNullOrEmpty(fieldName))
                return false;
            foreach (Match match in TokenRegex.Matches(expression))
            {
                string token = match.Groups[1].Value.Trim();
                if (token.IndexOf('/') >= 0) continue;
                if (string.Equals(token, fieldName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 对全部公式约束整表重算。非法约束跳过并记入 warnings。
        /// </summary>
        public static int ApplyFormulas(ConfigTable table, List<string> warnings = null)
        {
            if (table?.constraints == null || table.items == null)
                return 0;

            int updated = 0;
            foreach (FieldConstraint c in table.constraints)
            {
                if (c == null || c.Kind != FieldConstraintKind.Formula)
                    continue;
                List<string> errs = ValidateFormulaConstraint(table, c);
                if (errs.Count > 0)
                {
                    warnings?.AddRange(errs);
                    continue;
                }

                updated += ApplyOneFormula(table, c, warnings);
            }

            return updated;
        }

        public static int ApplyOneFormula(ConfigTable table, FieldConstraint c, List<string> warnings = null)
        {
            if (table?.items == null || c == null || c.Kind != FieldConstraintKind.Formula)
                return 0;

            int updated = 0;
            for (int row = 0; row < table.items.Count; row++)
                if (TryApplyFormulaToRow(table, row, c, out string error))
                    updated++;
                else if (error != null)
                    warnings?.Add(UniConfigLoc.F("constraint.err_row", row + 1, c.targetField, error));

            return updated;
        }

        /// <summary>
        /// 某行可编辑字段变化后，重算本行所有公式字段。
        /// </summary>
        public static int RecalculateRowFormulas(ConfigTable table, int rowIndex)
        {
            if (table?.constraints == null || table.items == null)
                return 0;
            if (rowIndex < 0 || rowIndex >= table.items.Count)
                return 0;

            int updated = 0;
            // 多轮：公式之间可能依赖（仅允许依赖非公式或已算字段时简单多遍）
            for (int pass = 0; pass < 3; pass++)
            {
                int passUpdated = 0;
                foreach (FieldConstraint c in table.constraints)
                {
                    if (c == null || c.Kind != FieldConstraintKind.Formula)
                        continue;
                    if (ValidateFormulaConstraint(table, c).Count > 0)
                        continue;
                    if (TryApplyFormulaToRow(table, rowIndex, c, out _))
                        passUpdated++;
                }

                updated += passUpdated;
                if (passUpdated == 0) break;
            }

            return updated;
        }

        private static bool TryApplyFormulaToRow(
            ConfigTable table,
            int rowIndex,
            FieldConstraint c,
            out string error)
        {
            error = null;
            ConfigItem item = table.items[rowIndex];
            string result = EvaluateFormula(table, item, c.expression, c.lookupKeyField, out error);
            if (error != null)
                return false;

            string old = item.GetValue(c.targetField);
            if (string.Equals(old, result, StringComparison.Ordinal))
                return false;
            item.SetValue(c.targetField, result);
            return true;
        }

        public static string EvaluateFormula(
            ConfigTable table,
            ConfigItem row,
            string expression,
            string lookupKeyField,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(expression))
            {
                error = "empty expression";
                return string.Empty;
            }

            string keyField = ResolveLookupKey(table, lookupKeyField);
            string keyValue = row.GetValue(keyField);

            try
            {
                string replaced = TokenRegex.Replace(expression, match =>
                {
                    string token = match.Groups[1].Value.Trim();
                    string value = ResolveToken(row, token, keyField, keyValue);
                    if (string.IsNullOrEmpty(value))
                        return "0";
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                        return num.ToString(CultureInfo.InvariantCulture);
                    return "0";
                });

                double computed = SimpleMath.Evaluate(replaced);
                return FormatNumber(computed);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return string.Empty;
            }
        }

        /// <summary>
        /// 跨表对齐：优先用约束配置的键，否则用 id，再否则用第一列。
        /// </summary>
        public static string ResolveLookupKey(ConfigTable table, string lookupKeyField)
        {
            if (!string.IsNullOrWhiteSpace(lookupKeyField) && table != null && table.GetFieldIndex(lookupKeyField) >= 0)
                return lookupKeyField.Trim();
            if (table != null && table.GetFieldIndex("id") >= 0)
                return "id";
            if (table?.fields != null && table.fields.Count > 0)
                return table.fields[0].name;
            return "id";
        }

        private static string ResolveToken(
            ConfigItem currentRow,
            string token,
            string keyField,
            string keyValue)
        {
            int slash = token.IndexOf('/');
            int dot = token.LastIndexOf('.');
            if (slash > 0 && dot > slash)
            {
                string moduleName = token.Substring(0, slash).Trim();
                string tableName = token.Substring(slash + 1, dot - slash - 1).Trim();
                string fieldName = token.Substring(dot + 1).Trim();
                return LookupCrossValue(moduleName, tableName, fieldName, keyField, keyValue);
            }

            return currentRow.GetValue(token);
        }

        private static string LookupCrossValue(
            string moduleName,
            string tableName,
            string fieldName,
            string keyField,
            string keyValue)
        {
            ConfigModule module = EditorConfigMgr.GetModule(moduleName);
            ConfigTable table = module?.FindTable(tableName);
            if (table == null)
                throw new InvalidOperationException($"table not found: {moduleName}/{tableName}");

            EditorConfigMgr.EnsureTableDataLoaded(table);
            if (string.IsNullOrEmpty(keyValue))
                return string.Empty;

            // 引用表对齐键：同名优先，否则 id，否则第一列
            string refKey = table.GetFieldIndex(keyField) >= 0
                ? keyField
                : table.GetFieldIndex("id") >= 0
                    ? "id"
                    : table.fields.Count > 0
                        ? table.fields[0].name
                        : keyField;

            foreach (ConfigItem item in table.items)
                if (string.Equals(item.GetValue(refKey), keyValue, StringComparison.Ordinal))
                    return item.GetValue(fieldName);

            return string.Empty;
        }

        private static string FormatNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return "0";
            if (Math.Abs(value - Math.Round(value)) < 1e-9)
                return ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
            return value.ToString("G15", CultureInfo.InvariantCulture);
        }

        private static class SimpleMath
        {
            public static double Evaluate(string expression)
            {
                if (string.IsNullOrWhiteSpace(expression))
                    return 0;
                int index = 0;
                double value = ParseExpression(expression, ref index);
                SkipWs(expression, ref index);
                if (index < expression.Length)
                    throw new FormatException($"unexpected '{expression[index]}' in: {expression}");
                return value;
            }

            private static double ParseExpression(string s, ref int i)
            {
                double left = ParseTerm(s, ref i);
                while (true)
                {
                    SkipWs(s, ref i);
                    if (i >= s.Length) break;
                    char op = s[i];
                    if (op != '+' && op != '-') break;
                    i++;
                    double right = ParseTerm(s, ref i);
                    left = op == '+' ? left + right : left - right;
                }

                return left;
            }

            private static double ParseTerm(string s, ref int i)
            {
                double left = ParseFactor(s, ref i);
                while (true)
                {
                    SkipWs(s, ref i);
                    if (i >= s.Length) break;
                    char op = s[i];
                    if (op != '*' && op != '/') break;
                    i++;
                    double right = ParseFactor(s, ref i);
                    if (op == '*') left *= right;
                    else left = Math.Abs(right) < 1e-15 ? throw new DivideByZeroException() : left / right;
                }

                return left;
            }

            private static double ParseFactor(string s, ref int i)
            {
                SkipWs(s, ref i);
                if (i >= s.Length)
                    throw new FormatException("unexpected end of expression");

                if (s[i] == '+')
                {
                    i++;
                    return ParseFactor(s, ref i);
                }

                if (s[i] == '-')
                {
                    i++;
                    return -ParseFactor(s, ref i);
                }

                if (s[i] == '(')
                {
                    i++;
                    double value = ParseExpression(s, ref i);
                    SkipWs(s, ref i);
                    if (i >= s.Length || s[i] != ')')
                        throw new FormatException("missing ')'");
                    i++;
                    return value;
                }

                int start = i;
                if (s[i] == '.' || char.IsDigit(s[i]))
                {
                    i++;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' ||
                                            s[i] == '+' || s[i] == '-'))
                    {
                        if ((s[i] == '+' || s[i] == '-') && s[i - 1] != 'e' && s[i - 1] != 'E')
                            break;
                        i++;
                    }

                    string num = s.Substring(start, i - start);
                    if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                        throw new FormatException($"invalid number: {num}");
                    return value;
                }

                throw new FormatException($"unexpected '{s[i]}'");
            }

            private static void SkipWs(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i]))
                    i++;
            }
        }
    }
}