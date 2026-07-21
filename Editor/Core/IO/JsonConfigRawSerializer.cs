#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    internal sealed class JsonConfigRawSerializer : IConfigRawSerializer
    {
        public void SaveTable(ConfigTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (!table.dataLoaded)
                throw new InvalidOperationException($"表 {table.moduleName}/{table.tableName} 数据尚未加载，无法保存");

            ConfigPaths.EnsureDirectory(ConfigPaths.GetModuleDirectory(table.moduleName));

            List<ConfigItem> nonEmptyItems = new();
            if (table.items != null)
                foreach (ConfigItem item in table.items)
                    if (item != null && !item.IsEmpty(table.fields))
                        nonEmptyItems.Add(item);

            int shardCount = Math.Max(1,
                (nonEmptyItems.Count + ConfigPaths.ShardRowLimit - 1) / ConfigPaths.ShardRowLimit);
            if (nonEmptyItems.Count == 0)
                shardCount = 1;

            for (int shard = 0; shard < shardCount; shard++)
            {
                int start = shard * ConfigPaths.ShardRowLimit;
                int count = Math.Min(ConfigPaths.ShardRowLimit, nonEmptyItems.Count - start);
                if (count < 0) count = 0;

                List<ConfigItem> slice = new(count);
                for (int i = 0; i < count; i++)
                    slice.Add(nonEmptyItems[start + i]);

                TableDto dto = TableDto.FromSlice(table, slice, shard == 0);
                string path = ConfigPaths.GetTableShardPath(table.moduleName, table.tableName, shard);
                File.WriteAllText(path, JsonUtility.ToJson(dto, true), Encoding.UTF8);
            }

            // 删除多余旧切片
            foreach (string existing in ConfigPaths.GetExistingShardPaths(table.moduleName, table.tableName))
            {
                if (!ConfigPaths.TryParseTableShardFileName(Path.GetFileName(existing), out _, out int index))
                    continue;
                if (index >= shardCount && File.Exists(existing))
                    File.Delete(existing);
            }

            table.items.Clear();
            table.items.AddRange(nonEmptyItems);
            table.cachedRowCount = nonEmptyItems.Count;
            table.dataLoaded = true;
        }

        public ConfigTable LoadTable(string moduleName, string tableName)
        {
            List<string> shardPaths = ConfigPaths.GetExistingShardPaths(moduleName, tableName);
            if (shardPaths.Count == 0)
                throw new FileNotFoundException($"找不到配置表文件: {ConfigPaths.GetTablePath(moduleName, tableName)}");

            ConfigTable table = null;
            foreach (string path in shardPaths)
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                TableDto dto = JsonUtility.FromJson<TableDto>(json);
                if (dto == null)
                    throw new InvalidDataException($"无法解析配置表 Json: {path}");

                if (table == null)
                    table = dto.ToTableHeader(moduleName, tableName);
                else if (dto.fields != null && dto.fields.Count > 0 && table.fields.Count == 0)
                    foreach (FieldDto field in dto.fields)
                        table.fields.Add(new ConfigField(field.name, (SupportableFieldType)field.type));

                dto.AppendItemsTo(table);
            }

            table.dataLoaded = true;
            table.isLoading = false;
            table.cachedRowCount = table.items.Count;
            return table;
        }

        /// <summary>
        /// 仅读取首个切片的表头信息（字段/类型/本片行数），不合并全部数据。
        /// </summary>
        public void LoadTableHeader(string moduleName, string tableName, ConfigTable target)
        {
            string path = ConfigPaths.GetTablePath(moduleName, tableName);
            if (!File.Exists(path))
                return;

            string json = File.ReadAllText(path, Encoding.UTF8);
            TableDto dto = JsonUtility.FromJson<TableDto>(json);
            if (dto == null) return;

            if (!string.IsNullOrEmpty(dto.mapCsharpTypeFullName))
                target.mapCsharpTypeFullName = dto.mapCsharpTypeFullName;
            if (dto.tableId > 0)
                target.tableId = dto.tableId;

            if (dto.fields != null && dto.fields.Count > 0 && (target.fields == null || target.fields.Count == 0))
            {
                target.fields = new List<ConfigField>();
                foreach (FieldDto field in dto.fields)
                    target.fields.Add(new ConfigField(field.name, (SupportableFieldType)field.type));
            }

            if (dto.constraints != null && dto.constraints.Count > 0)
            {
                target.constraints = new List<FieldConstraint>();
                foreach (ConstraintDto c in dto.constraints)
                    target.constraints.Add(c.ToConstraint());
            }
        }

        public int CountTableRowsOnDisk(string moduleName, string tableName)
        {
            int total = 0;
            foreach (string path in ConfigPaths.GetExistingShardPaths(moduleName, tableName))
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                TableDto dto = JsonUtility.FromJson<TableDto>(json);
                if (dto?.items != null)
                    total += dto.items.Count;
            }

            return total;
        }

        /// <summary>
        /// 仅从磁盘切片中收集某一列的取值集合，不把整表加载进编辑器内存。
        /// 用于外键校验等轻量场景。
        /// </summary>
        public HashSet<string> CollectColumnValues(string moduleName, string tableName, string fieldName)
        {
            HashSet<string> values = new(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(fieldName))
                return values;

            foreach (string path in ConfigPaths.GetExistingShardPaths(moduleName, tableName))
            {
                if (!File.Exists(path)) continue;
                string json = File.ReadAllText(path, Encoding.UTF8);
                TableDto dto = JsonUtility.FromJson<TableDto>(json);
                if (dto?.items == null) continue;

                foreach (ItemDto item in dto.items)
                {
                    if (item?.entries == null) continue;
                    foreach (EntryDto entry in item.entries)
                    {
                        if (entry == null) continue;
                        if (!string.Equals(entry.key, fieldName, StringComparison.Ordinal))
                            continue;
                        if (string.IsNullOrWhiteSpace(entry.value))
                            continue;
                        values.Add(entry.value.Trim());
                        break;
                    }
                }
            }

            return values;
        }

        [Serializable]
        private sealed class TableDto
        {
            public string tableName;
            public string mapCsharpTypeFullName;
            public int tableId;
            public List<FieldDto> fields = new();
            public List<ConstraintDto> constraints = new();
            public List<ItemDto> items = new();

            public static TableDto FromSlice(ConfigTable table, List<ConfigItem> slice, bool includeFields)
            {
                TableDto dto = new()
                {
                    tableName = table.tableName,
                    mapCsharpTypeFullName = table.mapCsharpTypeFullName,
                    tableId = table.tableId
                };

                if (includeFields && table.fields != null)
                    foreach (ConfigField field in table.fields)
                        dto.fields.Add(new FieldDto
                        {
                            name = field.name,
                            type = (int)field.type
                        });

                if (includeFields && table.constraints != null)
                    foreach (FieldConstraint c in table.constraints)
                    {
                        if (c == null) continue;
                        dto.constraints.Add(ConstraintDto.FromConstraint(c));
                    }

                if (slice != null)
                    foreach (ConfigItem item in slice)
                    {
                        ItemDto itemDto = new();
                        if (table.fields != null && table.fields.Count > 0)
                            foreach (ConfigField field in table.fields)
                                itemDto.entries.Add(new EntryDto
                                {
                                    key = field.name,
                                    value = item.GetValue(field.name) ?? string.Empty
                                });
                        else if (item.fieldData != null)
                            foreach (KeyValuePair<string, string> pair in item.fieldData)
                                itemDto.entries.Add(new EntryDto
                                {
                                    key = pair.Key,
                                    value = pair.Value ?? string.Empty
                                });

                        dto.items.Add(itemDto);
                    }

                return dto;
            }

            public ConfigTable ToTableHeader(string moduleName, string fallbackTableName)
            {
                ConfigTable table = new()
                {
                    moduleName = moduleName,
                    tableName = string.IsNullOrEmpty(tableName) ? fallbackTableName : tableName,
                    mapCsharpTypeFullName = mapCsharpTypeFullName,
                    tableId = tableId
                };

                if (fields != null)
                    foreach (FieldDto field in fields)
                        table.fields.Add(new ConfigField(field.name, (SupportableFieldType)field.type));

                if (constraints != null)
                    foreach (ConstraintDto c in constraints)
                        table.constraints.Add(c.ToConstraint());

                return table;
            }

            public void AppendItemsTo(ConfigTable table)
            {
                if (items == null) return;
                foreach (ItemDto itemDto in items)
                {
                    ConfigItem item = new();
                    if (itemDto.entries != null)
                        foreach (EntryDto entry in itemDto.entries)
                            item.SetValue(entry.key, entry.value);
                    table.items.Add(item);
                }
            }
        }

        [Serializable]
        private sealed class FieldDto
        {
            public string name;
            public int type;
        }

        [Serializable]
        private sealed class ConstraintDto
        {
            public string targetField;
            public int kind;
            public string fkModule;
            public string fkTable;
            public string fkField;
            public string expression;
            public string lookupKeyField;

            public static ConstraintDto FromConstraint(FieldConstraint c)
            {
                return new ConstraintDto
                {
                    targetField = c.targetField ?? string.Empty,
                    kind = c.kind,
                    fkModule = c.fkModule ?? string.Empty,
                    fkTable = c.fkTable ?? string.Empty,
                    fkField = c.fkField ?? string.Empty,
                    expression = c.expression ?? string.Empty,
                    lookupKeyField = string.IsNullOrEmpty(c.lookupKeyField) ? "id" : c.lookupKeyField
                };
            }

            public FieldConstraint ToConstraint()
            {
                return new FieldConstraint
                {
                    targetField = targetField ?? string.Empty,
                    kind = kind,
                    fkModule = fkModule ?? string.Empty,
                    fkTable = fkTable ?? string.Empty,
                    fkField = fkField ?? string.Empty,
                    expression = expression ?? string.Empty,
                    lookupKeyField = string.IsNullOrEmpty(lookupKeyField) ? "id" : lookupKeyField
                };
            }
        }

        [Serializable]
        private sealed class ItemDto
        {
            public List<EntryDto> entries = new();
        }

        [Serializable]
        private sealed class EntryDto
        {
            public string key;
            public string value;
        }
    }
}