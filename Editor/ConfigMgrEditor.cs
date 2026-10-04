#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    /// <summary>
    /// 编辑器模式下的配置读取入口：从 <c>Config/</c> 下已保存的表数据构建实例，
    /// 不依赖导出的 <c>configs.bytes</c>。API 对齐 <see cref="ConfigMgr"/>。
    /// 缓存由本类内部维护：Unity 打开时自动加载，配置保存后自动刷新。
    /// </summary>
    public static class ConfigMgrEditor
    {
        private static Dictionary<int, object> _configsById;
        private static Dictionary<Type, int> _typeToTableId;
        private static bool _loaded;
        private static int _saveBatchDepth;
        private static bool _refreshing;

        [InitializeOnLoadMethod]
        private static void AutoLoadOnEditorStartup()
        {
            // delayCall：避开编辑器启动早期其它 InitializeOnLoad 的顺序问题
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += RefreshCacheSafe;
                    return;
                }

                RefreshCacheSafe();
            };
        }

        /// <summary>
        /// 由 <see cref="EditorConfigMgr"/> 在写入磁盘后调用；外部无需手动加载/卸载。
        /// </summary>
        internal static void NotifyEditorDataChanged()
        {
            if (_saveBatchDepth > 0)
                return;
            RefreshCacheSafe();
        }

        internal static void BeginSaveBatch()
        {
            _saveBatchDepth++;
        }

        internal static void EndSaveBatch()
        {
            if (_saveBatchDepth > 0)
                _saveBatchDepth--;
            if (_saveBatchDepth == 0)
                RefreshCacheSafe();
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
                RefreshCacheSafe();
        }

        private static void RefreshCacheSafe()
        {
            if (_refreshing)
                return;
            _refreshing = true;
            try
            {
                RefreshCache();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UniConfig] ConfigMgrEditor 刷新失败: {ex.Message}");
            }
            finally
            {
                _refreshing = false;
            }
        }

        private static void RefreshCache()
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

        private static bool TryResolveTableId(Type mapCsharpType, out int tableId)
        {
            tableId = 0;
            if (mapCsharpType == null) return false;

            if (_typeToTableId != null && _typeToTableId.TryGetValue(mapCsharpType, out tableId) && tableId > 0)
                return true;

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
