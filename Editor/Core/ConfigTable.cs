#region

using System;
using System.Collections.Generic;

#endregion

namespace UniConfig.Editor
{
    [Serializable]
    public sealed class ConfigTable
    {
        /// <summary>
        /// 所属的模块名称
        /// </summary>
        public string moduleName;

        /// <summary>
        /// 当前配置表映射的Csharp类型
        /// </summary>
        public string mapCsharpTypeFullName;

        /// <summary>
        /// 表唯一 Id（生成 C# 时自增分配，写入二进制协议；已分配后保持不变）。
        /// </summary>
        public int tableId;

        /// <summary>
        /// 表名称
        /// </summary>
        public string tableName;

        /// <summary>
        /// 表字段（有序）
        /// </summary>
        public List<ConfigField> fields = new();

        /// <summary>
        /// 所有的配置数据（懒加载；未加载时为空）
        /// </summary>
        public List<ConfigItem> items = new();

        /// <summary>
        /// 字段约束（外键 / 公式），随表头保存在首个切片文件中。
        /// </summary>
        public List<FieldConstraint> constraints = new();

        /// <summary>
        /// 未加载数据时用于树节点显示的行数缓存（来自 module.json / 磁盘扫描）。
        /// </summary>
        public int cachedRowCount;

        /// <summary>
        /// 数据是否已从磁盘加载到内存。
        /// </summary>
        [NonSerialized] public bool dataLoaded;

        /// <summary>
        /// 外键违规单元格："{rowIndex}\0{fieldName}"
        /// </summary>
        [NonSerialized] public HashSet<string> fkCellViolations = new();

        /// <summary>
        /// 外键不合法的约束目标字段名（列表项标红用）。
        /// </summary>
        [NonSerialized] public HashSet<string> fkInvalidTargetFields = new(StringComparer.Ordinal);

        /// <summary>
        /// 外键状态栏详细说明（首条问题 + 汇总）。
        /// </summary>
        [NonSerialized] public string fkStatusDetail = string.Empty;

        /// <summary>
        /// 外键校验缓存是否需要刷新。
        /// </summary>
        [NonSerialized] public bool fkValidationDirty = true;

        /// <summary>
        /// 是否正在异步加载数据。
        /// </summary>
        [NonSerialized] public bool isLoading;

        public int DisplayRowCount => dataLoaded ? items?.Count ?? 0 : cachedRowCount;

        public bool HasFkViolations =>
            (fkInvalidTargetFields != null && fkInvalidTargetFields.Count > 0) ||
            (fkCellViolations != null && fkCellViolations.Count > 0);

        public static string FkCellKey(int rowIndex, string fieldName)
        {
            return rowIndex + "\0" + fieldName;
        }

        public bool IsFkCellViolation(int rowIndex, string fieldName)
        {
            return fkCellViolations != null &&
                   fkCellViolations.Contains(FkCellKey(rowIndex, fieldName));
        }

        public bool IsFkConstraintInvalid(FieldConstraint constraint)
        {
            if (constraint == null || constraint.Kind != FieldConstraintKind.ForeignKey)
                return false;
            return fkInvalidTargetFields != null &&
                   !string.IsNullOrEmpty(constraint.targetField) &&
                   fkInvalidTargetFields.Contains(constraint.targetField);
        }

        public void MarkFkValidationDirty()
        {
            fkValidationDirty = true;
        }

        public int GetFieldIndex(string fieldName)
        {
            for (int i = 0; i < fields.Count; i++)
                if (fields[i].name == fieldName)
                    return i;
            return -1;
        }

        public bool TryGetField(string fieldName, out ConfigField field)
        {
            int index = GetFieldIndex(fieldName);
            if (index >= 0)
            {
                field = fields[index];
                return true;
            }

            field = null;
            return false;
        }
    }
}