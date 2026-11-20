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
        private static CreateCfgTableArrayMethod _createArray;
        private static SetCfgTableItemMethod _setItem;
        private static DeserializeCfgTableArrayMethod _deserializeArray;
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
        /// 注册逐行加载时的 new XX[length]。
        /// </summary>
        public static void RegisterCreateTableArray(CreateCfgTableArrayMethod method)
        {
            _createArray = method ?? throw new ArgumentNullException(nameof(method));
        }

        /// <summary>
        /// 注册逐行加载时按下标填入反序列化结果。
        /// </summary>
        public static void RegisterSetTableItem(SetCfgTableItemMethod method)
        {
            _setItem = method ?? throw new ArgumentNullException(nameof(method));
        }

        /// <summary>
        /// 注册整表数组的 MemoryPack 反序列化。
        /// </summary>
        public static void RegisterDeserializeTableArray(DeserializeCfgTableArrayMethod method)
        {
            _deserializeArray = method ?? throw new ArgumentNullException(nameof(method));
        }

        internal static Array CreateTableArray(int tableId, int length)
        {
            return _createArray?.Invoke(tableId, length);
        }

        internal static void SetTableItem(int tableId, Array rows, int index, ReadOnlySpan<byte> data)
        {
            if (_setItem == null)
            {
                Debug.LogError(
                    $"配置填入数组失败: tableId={tableId}。请确认已生成 C#，且 BeforeSceneLoad 已完成注册。");
                return;
            }

            _setItem(tableId, rows, index, data);
        }

        internal static Array DeserializeTableArray(int tableId, ReadOnlySpan<byte> data)
        {
            Array array = _deserializeArray?.Invoke(tableId, data);
            if (array != null)
                return array;

            Debug.LogError(
                $"整表数组反序列化失败: tableId={tableId}。请确认已生成 C#，且导出方式与当前代码一致。");
            return null;
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
                if (config.Value is not Array items)
                    continue;

                string name = string.Empty;
                for (int i = 0; i < items.Length; i++)
                {
                    object row = items.GetValue(i);
                    if (row == null)
                        continue;
                    name = row.GetType().FullName;
                    break;
                }

                Debug.Log($"Table: Id={config.Key}, Name={name}, Count={items.Length}");
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
                return IteratorOf<T>(configs);
            return new CfgTableIterator<T>(Array.Empty<object>());
        }

        /// <summary>
        /// 按表 Id 获取配置（无需解析类型）。
        /// </summary>
        public static CfgTableIterator<T> GetConfigs<T>(int tableId) where T : IConfigTable
        {
            if (_configs != null && _configs.TryGetValue(tableId, out object configs))
                return IteratorOf<T>(configs);
            return new CfgTableIterator<T>(Array.Empty<object>());
        }

        /// <summary>
        /// 按行号取得一条配置。这里的 Id 是表的行号，从 1 开始，与编辑器中的 # 列一致，不是表的 tableId。
        /// 开启「自动创建 id 字段」时，该行号会写入每条配置的 id 字段。
        /// MemoryPack 保持行序，行号减 1 即数组下标，直接按下标取出并转为 <typeparamref name="T"/>。
        /// </summary>
        public static T GetConfigById<T>(int id) where T : IConfigTable
        {
            if (id <= 0 || _configs == null || !TryResolveTableId(typeof(T), out int tableId))
                return default;
            if (!_configs.TryGetValue(tableId, out object value) || value is not Array rows)
                return default;
            int index = id - 1;
            if ((uint)index >= (uint)rows.Length)
                return default;
            return rows.GetValue(index) is T item ? item : default;
        }

        /// <summary>
        /// 取得杂项表的那一个配置对象。杂项表和普通表一样放在配置字典里，整表数组导出时这个数组只有一个元素。
        /// <see cref="GetConfigs{T}()"/>、<see cref="GetConfigById{T}"/> 同样适用。
        /// </summary>
        public static T GetMiscConfig<T>() where T : IConfigTable
        {
            if (_configs == null || !TryResolveTableId(typeof(T), out int tableId))
                return default;
            if (!_configs.TryGetValue(tableId, out object value) || value is not Array rows || rows.Length == 0)
                return default;
            return rows.GetValue(0) is T item ? item : default;
        }

        private static CfgTableIterator<T> IteratorOf<T>(object configs) where T : IConfigTable
        {
            if (configs is Array array)
                return CfgTableIterator<T>.FromArray(array);
            if (configs is IReadOnlyList<object> list)
                return new CfgTableIterator<T>(list);
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