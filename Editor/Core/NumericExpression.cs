#region

using System;
using System.Globalization;

#endregion

namespace UniConfig.Editor
{
    internal enum NumericEvalStatus
    {
        Ok,
        Invalid,
        Overflow
    }

    /// <summary>
    /// 基础数值单元格的四则表达式。支持 + - * / % 和括号，整数结果向下取整。
    /// </summary>
    internal static class NumericExpression
    {
        public static NumericEvalStatus Evaluate(
            string text,
            SupportableFieldType type,
            out object boxed,
            out string canonical)
        {
            boxed = null;
            canonical = null;
            if (string.IsNullOrWhiteSpace(text) ||
                !ConstraintEvaluator.IsScalarNumeric(type, out bool integer))
                return NumericEvalStatus.Invalid;

            if (!TryParse(text.Trim(), out double raw))
                return NumericEvalStatus.Invalid;
            if (double.IsNaN(raw) || double.IsInfinity(raw))
                return NumericEvalStatus.Overflow;

            if (integer)
            {
                double floored = Math.Floor(raw);
                if (!TryBoxInteger(floored, type, out boxed, out canonical))
                    return NumericEvalStatus.Overflow;
                return NumericEvalStatus.Ok;
            }

            if (type == SupportableFieldType.Single)
            {
                if (raw < float.MinValue || raw > float.MaxValue)
                    return NumericEvalStatus.Overflow;
                float value = (float)raw;
                if (float.IsInfinity(value) || float.IsNaN(value))
                    return NumericEvalStatus.Overflow;
                boxed = value;
                canonical = value.ToString("G9", CultureInfo.InvariantCulture);
                return NumericEvalStatus.Ok;
            }

            boxed = raw;
            canonical = raw.ToString("G17", CultureInfo.InvariantCulture);
            return NumericEvalStatus.Ok;
        }

        /// <summary>成功且应把单元格改写成计算结果时返回 true。纯数字字面量保持原样。</summary>
        public static bool TryGetCommittedText(
            string raw,
            SupportableFieldType type,
            out string committed,
            out NumericEvalStatus status)
        {
            committed = raw;
            status = Evaluate(raw, type, out _, out string canonical);
            if (status != NumericEvalStatus.Ok || string.IsNullOrEmpty(canonical))
                return false;
            if (string.Equals(raw.Trim(), canonical, StringComparison.Ordinal))
                return false;
            if (!LooksLikeExpression(raw) && ParsesAsTargetType(raw, type))
                return false;
            committed = canonical;
            return true;
        }

        public static bool ShouldTint(string raw, SupportableFieldType type, bool focused)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            if (!LooksLikeExpression(raw) && ParsesAsTargetType(raw, type))
                return false;
            NumericEvalStatus status = Evaluate(raw, type, out _, out _);
            if (status == NumericEvalStatus.Ok)
                return false;
            if (status == NumericEvalStatus.Overflow)
                return true;
            return !focused;
        }

        /// <summary>数值本身放不进该类型（整数先向下取整再比较）。</summary>
        public static bool FitsType(double value, SupportableFieldType type)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return false;
            if (!ConstraintEvaluator.IsScalarNumeric(type, out bool integer))
                return false;
            if (!integer)
            {
                return type != SupportableFieldType.Single ||
                       (value >= float.MinValue && value <= float.MaxValue);
            }

            return TryBoxInteger(Math.Floor(value), type, out _, out _);
        }

        private static bool ParsesAsTargetType(string text, SupportableFieldType type)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            string value = text.Trim();
            switch (type)
            {
                case SupportableFieldType.Int16:
                    return short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case SupportableFieldType.Int32:
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case SupportableFieldType.Int64:
                    return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case SupportableFieldType.UInt16:
                    return ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case SupportableFieldType.UInt32:
                    return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case SupportableFieldType.UInt64:
                    return ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case SupportableFieldType.Single:
                    return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) &&
                           !float.IsNaN(f) && !float.IsInfinity(f);
                case SupportableFieldType.Double:
                    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) &&
                           !double.IsNaN(d) && !double.IsInfinity(d);
                default:
                    return false;
            }
        }

        private static bool LooksLikeExpression(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            string s = text.Trim();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '(' || c == ')' || c == '*' || c == '/' || c == '%')
                    return true;
                if ((c != '+' && c != '-') || i == 0)
                    continue;
                char prev = s[i - 1];
                if (prev == 'e' || prev == 'E')
                    continue;
                return true;
            }

            return false;
        }

        private static bool TryBoxInteger(
            double floored,
            SupportableFieldType type,
            out object boxed,
            out string canonical)
        {
            boxed = null;
            canonical = null;
            if (double.IsNaN(floored) || double.IsInfinity(floored))
                return false;

            switch (type)
            {
                case SupportableFieldType.Int16:
                    if (floored < short.MinValue || floored > short.MaxValue)
                        return false;
                    short s = (short)floored;
                    boxed = s;
                    canonical = s.ToString(CultureInfo.InvariantCulture);
                    return true;
                case SupportableFieldType.Int32:
                    if (floored < int.MinValue || floored > int.MaxValue)
                        return false;
                    int i = (int)floored;
                    boxed = i;
                    canonical = i.ToString(CultureInfo.InvariantCulture);
                    return true;
                case SupportableFieldType.Int64:
                    // 2^63 不是合法 long，double 又表示不出 long.MaxValue。
                    if (floored < long.MinValue || floored >= 9223372036854775808d)
                        return false;
                    long l = (long)floored;
                    boxed = l;
                    canonical = l.ToString(CultureInfo.InvariantCulture);
                    return true;
                case SupportableFieldType.UInt16:
                    if (floored < ushort.MinValue || floored > ushort.MaxValue)
                        return false;
                    ushort us = (ushort)floored;
                    boxed = us;
                    canonical = us.ToString(CultureInfo.InvariantCulture);
                    return true;
                case SupportableFieldType.UInt32:
                    if (floored < uint.MinValue || floored > uint.MaxValue)
                        return false;
                    uint ui = (uint)floored;
                    boxed = ui;
                    canonical = ui.ToString(CultureInfo.InvariantCulture);
                    return true;
                case SupportableFieldType.UInt64:
                    if (floored < 0d || floored >= 18446744073709551616d)
                        return false;
                    ulong ul = (ulong)floored;
                    boxed = ul;
                    canonical = ul.ToString(CultureInfo.InvariantCulture);
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParse(string text, out double value)
        {
            var parser = new Parser(Normalize(text));
            if (!parser.ParseExpr(out value))
                return false;
            parser.Skip();
            return parser.End;
        }

        /// <summary>中文输入法打出的全角括号和运算符，按对应的半角符号计算。</summary>
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                switch (chars[i])
                {
                    case '\uFF08':
                        chars[i] = '(';
                        break;
                    case '\uFF09':
                        chars[i] = ')';
                        break;
                    case '\uFF0B':
                        chars[i] = '+';
                        break;
                    case '\uFF0D':
                    case '\u2212':
                    case '\uFE63':
                        chars[i] = '-';
                        break;
                    case '\uFF0A':
                    case '\u00D7':
                        chars[i] = '*';
                        break;
                    case '\uFF0F':
                    case '\u00F7':
                        chars[i] = '/';
                        break;
                    case '\uFF05':
                        chars[i] = '%';
                        break;
                    case '\u3000':
                        chars[i] = ' ';
                        break;
                }
            }

            return new string(chars);
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string text)
            {
                _s = text ?? string.Empty;
            }

            public bool End => _i >= _s.Length;

            public bool ParseExpr(out double value)
            {
                if (!ParseTerm(out value))
                    return false;
                while (true)
                {
                    Skip();
                    if (TryOp('+'))
                    {
                        if (!ParseTerm(out double right))
                            return false;
                        value += right;
                        continue;
                    }

                    if (TryOp('-'))
                    {
                        if (!ParseTerm(out double right))
                            return false;
                        value -= right;
                        continue;
                    }

                    return true;
                }
            }

            private bool ParseTerm(out double value)
            {
                if (!ParseUnary(out value))
                    return false;
                while (true)
                {
                    Skip();
                    if (TryOp('*'))
                    {
                        if (!ParseUnary(out double right))
                            return false;
                        value *= right;
                        continue;
                    }

                    if (TryOp('/'))
                    {
                        if (!ParseUnary(out double right))
                            return false;
                        value /= right;
                        continue;
                    }

                    if (TryOp('%'))
                    {
                        if (!ParseUnary(out double right))
                            return false;
                        value %= right;
                        continue;
                    }

                    return true;
                }
            }

            private bool ParseUnary(out double value)
            {
                Skip();
                if (TryOp('+'))
                    return ParseUnary(out value);
                if (TryOp('-'))
                {
                    if (!ParseUnary(out value))
                        return false;
                    value = -value;
                    return true;
                }

                return ParsePrimary(out value);
            }

            private bool ParsePrimary(out double value)
            {
                Skip();
                if (TryOp('('))
                {
                    if (!ParseExpr(out value))
                        return false;
                    Skip();
                    return TryOp(')');
                }

                return TryReadNumber(out value);
            }

            private bool TryReadNumber(out double value)
            {
                value = 0;
                int start = _i;
                bool digits = false;
                if (_i < _s.Length && _s[_i] == '.')
                {
                    _i++;
                    int frac = _i;
                    while (_i < _s.Length && char.IsDigit(_s[_i]))
                        _i++;
                    if (_i == frac)
                        return false;
                    digits = true;
                }
                else
                {
                    while (_i < _s.Length && char.IsDigit(_s[_i]))
                    {
                        _i++;
                        digits = true;
                    }

                    if (!digits)
                        return false;
                    if (_i < _s.Length && _s[_i] == '.')
                    {
                        _i++;
                        while (_i < _s.Length && char.IsDigit(_s[_i]))
                            _i++;
                    }
                }

                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    int mark = _i;
                    _i++;
                    if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-'))
                        _i++;
                    int exp = _i;
                    while (_i < _s.Length && char.IsDigit(_s[_i]))
                        _i++;
                    if (_i == exp)
                        return false;
                    if (mark == start)
                        return false;
                }

                string token = _s.Substring(start, _i - start);
                return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }

            private bool TryOp(char op)
            {
                if (_i >= _s.Length || _s[_i] != op)
                    return false;
                _i++;
                return true;
            }

            public void Skip()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t'))
                    _i++;
            }
        }
    }
}
