#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace UniConfig
{
    public static class ConfigMgr
    {
        private static List<DeserializeCfgTableItemMethod> _methods;
        private static ResolveCfgTableIdMethod _resolveTableId;
        private static Dictionary<int, object> _configs;

        internal static bool TryDeserialize(int tableId, in ReadOnlySpan<byte> data, out IConfigTable item)
        {
            item = null;
            if (_methods == null)
                return false;
            foreach (DeserializeCfgTableItemMethod method in _methods)
            {
                item = method.Invoke(tableId, data);
                if (item != null) break;
            }

            return item != null;
        }

        internal static bool TryResolveTableId(Type mapCsharpType, out int tableId)
        {
            tableId = 0;
            if (_resolveTableId == null || mapCsharpType == null)
                return false;
            tableId = _resolveTableId.Invoke(mapCsharpType);
            return tableId > 0;
        }

        /// <summary>
        /// 注册反序列化方法（按表 Id 的泛型反序列化，避免反射）。
        /// </summary>
        public static void RegisterDeserializeMethod(DeserializeCfgTableItemMethod method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            _methods ??= new List<DeserializeCfgTableItemMethod>(1);
            _methods.Add(method);
        }

        /// <summary>
        /// 注册 Type → 表Id解析（供 <see cref="GetConfigs{T}()" /> 使用）。
        /// </summary>
        public static void RegisterGetTableIdMethod(ResolveCfgTableIdMethod method)
        {
            _resolveTableId = method ?? throw new ArgumentNullException(nameof(method));
        }

        /// <summary>
        /// 从Resources目录下一次性异步加载所有的配置文件
        /// </summary>
        public static AsyncOperation LoadAllConfigsFromResourcesAsync(string assetPath)
        {
            ResourceRequest request = Resources.LoadAsync<TextAsset>(assetPath);
            request.completed += op =>
            {
                if (op is ResourceRequest req && req.asset is TextAsset asset)
                    _configs = ResourcesLoader.ReadAllConfigs(asset.bytes);
                else
                    Debug.LogError("Resources中未找到配置！（勿带扩展名）");
            };
            return request;
        }

        /// <summary>
        /// 从Resources目录下一次性同步加载所有的配置文件
        /// </summary>
        public static void LoadAllConfigsFromResourcesSync(string assetPath)
        {
            TextAsset asset = Resources.Load<TextAsset>(assetPath);
            if (asset == null)
                throw new ArgumentException($"Resources中未找到配置: \"{assetPath}\"（勿带扩展名，例如 Assets/Resources/configs.bytes → \"configs\"）");
            _configs = ResourcesLoader.ReadAllConfigs(asset.bytes);
        }

        /// <summary>
        /// 从StreamingAssets目录下一次性异步加载所有的配置文件。
        /// 返回值须 <c>yield return</c> 等待：完成时配置已写入内部字典（晚于下载结束）。
        /// </summary>
        public static ConfigLoadOperation LoadAllConfigsFromStreamingAssetsAsync(string assetPath, int bufferSize = 1024)
        {
            var operation = new ConfigLoadOperation();
            GameObject go = new(nameof(LoadAllConfigsFromStreamingAssetsAsync));
            StreamingAssetsLoader loader = go.AddComponent<StreamingAssetsLoader>();
            Object.DontDestroyOnLoad(go);
            loader.RunLoader(assetPath, bufferSize, cfgs =>
            {
                _configs = cfgs;
                operation.MarkCompleted();
            });
            return operation;
        }

        /// <summary>
        /// 输出所有配置信息
        /// </summary>
        public static void PrintAllTables()
        {
            if (_configs == null) return;
            foreach (KeyValuePair<int, object> config in _configs)
            {
                List<object> items = (List<object>)config.Value;
                Debug.Log(
                    $"Table: Id={config.Key}, Name={(items.Count > 0 ? items[0].GetType().FullName : string.Empty)}, Count={items.Count}");
            }
        }

        /// <summary>
        /// 获取指定表的所有配置数据
        /// </summary>
        public static CfgTableIterator<T> GetConfigs<T>() where T : IConfigTable
        {
            if (_configs != null &&
                TryResolveTableId(typeof(T), out int tableId) &&
                _configs.TryGetValue(tableId, out object configs))
                return new CfgTableIterator<T>((IReadOnlyList<object>)configs);
            return new CfgTableIterator<T>(Array.Empty<object>());
        }

        /// <summary>
        /// 按表 Id 获取配置（无需解析类型）。
        /// </summary>
        public static CfgTableIterator<T> GetConfigs<T>(int tableId) where T : IConfigTable
        {
            if (_configs != null && _configs.TryGetValue(tableId, out object configs))
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
    }
}