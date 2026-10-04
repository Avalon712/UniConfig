#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 编辑器模式下的配置读取入口：从 <c>Config/</c> 下已保存的表数据构建实例，
    /// 不依赖导出的 <c>configs.bytes</c>。API 对齐 <see cref="ConfigMgr"/>。
    /// </summary>
    public static class ConfigMgrEditor
    {
        private static Dictionary<int, object> _configsById;
        private static Dictionary<Type, int> _typeToTableId;
        private static bool _loaded;

        public static bool IsLoaded => _loaded;

        /// <summary>
        /// 从编辑器磁盘数据加载全部表（会 EnsureLoaded 并按需拉表行）。
        /// </summary>
        public static void LoadAll()
        {
            EditorConfigMgr.EnsureLoaded();

            Dictionary<int, object> byId = new();
            Dictionary<Type, int> typeMap = new();

            foreach (ConfigModule module in EditorConfigMgr.Modules.Values)
            {
                if (module?.tables == null) continue;
                foreach (ConfigTable table in module.tables)
                {
                    if (table == null) continue;
                    if (table.tableId <= 0)
                        continue;
                    if (table.fields == null || table.fields.Count == 0)
                        continue;

                    try
                    {
                        ExportTableData data = EditorConfigMgr.BuildExportTableData(table);
                        List<object> rows = new(data.Count);
                        for (int i = 0; i < data.Count; i++)
                            rows.Add(data.GetObject(i));

                        byId[data.TableId] = rows;
                        if (data.ExportCsharpType != null)
                            typeMap[data.ExportCsharpType] = data.TableId;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[UniConfig] ConfigMgrEditor 跳过表 {table.moduleName}/{table.tableName}: {ex.Message}");
                    }
                }
            }

            _configsById = byId;
            _typeToTableId = typeMap;
            _loaded = true;
        }

        /// <summary>
        /// 重新从磁盘读取模块元数据并加载全部表。
        /// </summary>
        public static void Reload()
        {
            EditorConfigMgr.Reload();
            LoadAll();
        }

        /// <summary>
        /// 清除内存缓存（不删磁盘数据）。
        /// </summary>
        public static void Unload()
        {
            _configsById = null;
            _typeToTableId = null;
            _loaded = false;
        }

        public static void PrintAllTables()
        {
            EnsureLoaded();
            if (_configsById == null) return;
            foreach (KeyValuePair<int, object> config in _configsById)
            {
                List<object> items = (List<object>)config.Value;
                Debug.Log(
                    $"[Editor] Table: Id={config.Key}, Name={(items.Count > 0 ? items[0].GetType().FullName : string.Empty)}, Count={items.Count}");
            }
        }

        public static CfgTableIterator<T> GetConfigs<T>() where T : IConfigTable
        {
            EnsureLoaded();
            if (_configsById == null)
                return new CfgTableIterator<T>(Array.Empty<object>());

            if (TryResolveTableId(typeof(T), out int tableId) &&
                _configsById.TryGetValue(tableId, out object configs))
                return new CfgTableIterator<T>((IReadOnlyList<object>)configs);

            return new CfgTableIterator<T>(Array.Empty<object>());
        }

        public static CfgTableIterator<T> GetConfigs<T>(int tableId) where T : IConfigTable
        {
            EnsureLoaded();
            if (_configsById != null && _configsById.TryGetValue(tableId, out object configs))
                return new CfgTableIterator<T>((IReadOnlyList<object>)configs);
            return new CfgTableIterator<T>(Array.Empty<object>());
        }

        public static T GetConfig<T>(Predicate<T> condition) where T : IConfigTable
        {
            if (condition == null) return default;
            foreach (T config in GetConfigs<T>())
                if (condition.Invoke(config))
                    return config;
            return default;
        }

        public static List<T> GetConfigs<T>(Predicate<T> condition) where T : IConfigTable
        {
            List<T> configs = new();
            if (condition == null) return configs;
            foreach (T config in GetConfigs<T>())
                if (condition.Invoke(config))
                    configs.Add(config);
            return configs;
        }

        public static void GetConfigsNoAlloc<T>(Predicate<T> condition, List<T> result) where T : IConfigTable
        {
            if (condition == null || result == null) return;
            result.Clear();
            foreach (T config in GetConfigs<T>())
                if (condition.Invoke(config))
                    result.Add(config);
        }

        private static void EnsureLoaded()
        {
            if (!_loaded)
                LoadAll();
        }

        private static bool TryResolveTableId(Type mapCsharpType, out int tableId)
        {
            tableId = 0;
            if (mapCsharpType == null) return false;

            if (_typeToTableId != null && _typeToTableId.TryGetValue(mapCsharpType, out tableId) && tableId > 0)
                return true;

            // 按 FullName 回退（程序集不一致时 Type 引用不相等）
            if (_typeToTableId == null) return false;
            string fullName = mapCsharpType.FullName;
            foreach (KeyValuePair<Type, int> kv in _typeToTableId)
            {
                if (kv.Key != null && kv.Key.FullName == fullName)
                {
                    tableId = kv.Value;
                    return tableId > 0;
                }
            }

            return false;
        }
    }
}
