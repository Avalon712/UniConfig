#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    internal static class ModuleMetaSerializer
    {
        public static void Save(ConfigModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            ConfigPaths.EnsureDirectory(ConfigPaths.GetModuleDirectory(module.moduleName));

            ModuleMetaDto dto = new()
            {
                moduleName = module.moduleName,
                exporterTypeFullName = module.exporterTypeFullName ?? string.Empty
            };

            if (module.tables != null)
                foreach (ConfigTable table in module.tables)
                {
                    TableMetaDto tableDto = new()
                    {
                        tableName = table.tableName,
                        mapCsharpTypeFullName = table.mapCsharpTypeFullName ?? string.Empty,
                        tableId = table.tableId,
                        rowCount = table.dataLoaded ? table.items?.Count ?? 0 : table.cachedRowCount
                    };

                    if (table.fields != null)
                        foreach (ConfigField field in table.fields)
                            tableDto.fields.Add(new FieldMetaDto
                            {
                                name = field.name,
                                type = (int)field.type
                            });

                    dto.tables.Add(tableDto);
                }

            string path = ConfigPaths.GetModuleMetaPath(module.moduleName);
            File.WriteAllText(path, JsonUtility.ToJson(dto, true), Encoding.UTF8);
        }

        public static ModuleMetaDto Load(string moduleName)
        {
            string path = ConfigPaths.GetModuleMetaPath(moduleName);
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path, Encoding.UTF8);
            return JsonUtility.FromJson<ModuleMetaDto>(json);
        }

        [Serializable]
        internal sealed class ModuleMetaDto
        {
            public string moduleName;
            public string exporterTypeFullName;
            public List<TableMetaDto> tables = new();
        }

        [Serializable]
        internal sealed class TableMetaDto
        {
            public string tableName;
            public string mapCsharpTypeFullName;
            public int tableId;
            public int rowCount;
            public List<FieldMetaDto> fields = new();
        }

        [Serializable]
        internal sealed class FieldMetaDto
        {
            public string name;
            public int type;
        }
    }
}