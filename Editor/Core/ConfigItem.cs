#region

using System;
using System.Collections.Generic;

#endregion

namespace UniConfig.Editor
{
    [Serializable]
    public sealed class ConfigItem
    {
        /// <summary>
        /// key=fieldName value=fieldValue
        /// </summary>
        public Dictionary<string, string> fieldData = new();

        public string GetValue(string fieldName)
        {
            return fieldData.TryGetValue(fieldName, out string value) ? value : string.Empty;
        }

        public void SetValue(string fieldName, string value)
        {
            fieldData[fieldName] = value ?? string.Empty;
        }

        /// <summary>
        /// 所有字段值均为 null / 空白时视为空行。
        /// 若传入表字段定义，则只检查当前表列（忽略已删除列残留数据）。
        /// </summary>
        public bool IsEmpty(IReadOnlyList<ConfigField> fields = null)
        {
            if (fields != null && fields.Count > 0)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    // 自动行号不算有效内容，否则空行会因为 id 永远无法被保存时跳过
                    if (UniConfigEditorSettings.instance.IsAutoRowIdField(fields[i].name))
                        continue;
                    if (!string.IsNullOrWhiteSpace(GetValue(fields[i].name)))
                        return false;
                }

                return true;
            }

            if (fieldData == null || fieldData.Count == 0)
                return true;

            foreach (KeyValuePair<string, string> pair in fieldData)
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    return false;

            return true;
        }
    }
}