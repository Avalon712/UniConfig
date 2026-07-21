#region

using System;

#endregion

namespace UniConfig.Editor
{
    public enum FieldConstraintKind
    {
        /// <summary>
        /// 外键：目标字段值必须出现在引用表的指定列中。
        /// </summary>
        ForeignKey = 0,

        /// <summary>
        /// 公式：目标字段值由表达式计算得到。
        /// </summary>
        Formula = 1
    }

    /// <summary>
    /// 字段约束（外键 / 公式计算）。
    /// </summary>
    [Serializable]
    public sealed class FieldConstraint
    {
        /// <summary>
        /// 施加约束的目标字段名。
        /// </summary>
        public string targetField = string.Empty;

        /// <summary>
        /// 约束类型。
        /// </summary>
        public int kind;

        /// <summary>外键：引用模块名</summary>
        public string fkModule = string.Empty;

        /// <summary>外键：引用表名</summary>
        public string fkTable = string.Empty;

        /// <summary>外键：引用字段名</summary>
        public string fkField = string.Empty;

        /// <summary>
        /// 公式表达式。
        /// 同行字段：{fieldName}
        /// 跨表/跨模块（按 lookupKeyField 对齐）：{Module/Table.fieldName}
        /// 支持 + - * / ( )
        /// </summary>
        public string expression = string.Empty;

        /// <summary>
        /// 跨表引用时的对齐键（默认 id，当前行与引用表该列值相等时取对应行）。
        /// </summary>
        public string lookupKeyField = "id";

        public FieldConstraintKind Kind
        {
            get => (FieldConstraintKind)kind;
            set => kind = (int)value;
        }

        public FieldConstraint Clone()
        {
            return new FieldConstraint
            {
                targetField = targetField,
                kind = kind,
                fkModule = fkModule,
                fkTable = fkTable,
                fkField = fkField,
                expression = expression,
                lookupKeyField = lookupKeyField
            };
        }
    }
}