#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

#endregion

namespace UniConfig.Editor
{
    public static class EditorConfigMgr
    {
        private const string PendingExportSessionKey = "UniConfig.PendingExportAfterReload";

        /// <summary>
        /// 所有的模块，key=moduleName
        /// </summary>
        private static readonly Dictionary<string, ConfigModule> _modules = new();

        /// <summary>
        /// 所有可用的导出器, key=typeFullName
        /// </summary>
        private static readonly Dictionary<string, IConfigExporter> _configExporters = new();

        private static bool _loaded;

        static EditorConfigMgr()
        {
            TypeCache.TypeCollection collection = TypeCache.GetTypesDerivedFrom<IConfigExporter>();
            foreach (Type type in collection)
                if (type != null && !type.IsAbstract)
                    _configExporters[type.FullName] = (IConfigExporter)Activator.CreateInstance(type);
        }

        public static IReadOnlyDictionary<string, ConfigModule> Modules
        {
            get
            {
                EnsureLoaded();
                return _modules;
            }
        }

        public static IReadOnlyDictionary<string, IConfigExporter> Exporters => _configExporters;

        public static IEnumerable<string> ExporterTypeNames => _configExporters.Keys;

        public static void EnsureLoaded()
        {
            if (!_loaded)
                LoadAll();
        }

        public static void LoadAll()
        {
            _modules.Clear();
            ConfigPaths.EnsureDirectory(ConfigPaths.ConfigRoot);

            if (!Directory.Exists(ConfigPaths.ConfigRoot))
            {
                _loaded = true;
                return;
            }

            foreach (string moduleDir in Directory.GetDirectories(ConfigPaths.ConfigRoot))
            {
                string moduleName = Path.GetFileName(moduleDir);
                if (string.IsNullOrEmpty(moduleName)) continue;

                try
                {
                    ConfigModule module = LoadModuleFromDisk(moduleName);
                    _modules[moduleName] = module;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UniConfig] 加载模块失败 {moduleName}: {ex.Message}");
                }
            }

            _loaded = true;
        }

        public static void Reload()
        {
            _loaded = false;
            LoadAll();
        }

        public static ConfigModule GetModule(string moduleName)
        {
            EnsureLoaded();
            return _modules.TryGetValue(moduleName, out ConfigModule module) ? module : null;
        }

        public static ConfigModule CreateModule(string moduleName, string exporterTypeFullName = null)
        {
            EnsureLoaded();
            ValidateIdentifier(moduleName, "模块名");

            if (_modules.ContainsKey(moduleName))
                throw new InvalidOperationException($"模块已存在: {moduleName}");

            exporterTypeFullName = ResolveExporterTypeFullName(exporterTypeFullName);

            ConfigModule module = new()
            {
                moduleName = moduleName,
                exporterTypeFullName = exporterTypeFullName
            };
            _modules[moduleName] = module;
            SaveModule(module);
            return module;
        }

        public static void DeleteModule(string moduleName)
        {
            EnsureLoaded();
            if (!_modules.Remove(moduleName))
                return;
            ConfigRawStorage.DeleteModuleDirectory(moduleName);
        }

        public static ConfigTable CreateTable(string moduleName, string tableName)
        {
            EnsureLoaded();
            ConfigModule module = RequireModule(moduleName);
            ValidateIdentifier(tableName, "表名");

            if (module.FindTable(tableName) != null)
                throw new InvalidOperationException($"表已存在: {moduleName}/{tableName}");

            ConfigTable table = new()
            {
                moduleName = moduleName,
                tableName = tableName,
                mapCsharpTypeFullName = string.Empty,
                tableId = 0,
                dataLoaded = true,
                cachedRowCount = 0
            };
            module.tables.Add(table);
            SaveModule(module);
            SaveTable(table);
            return table;
        }

        public static void DeleteTable(string moduleName, string tableName)
        {
            EnsureLoaded();
            ConfigModule module = RequireModule(moduleName);
            ConfigTable table = module.FindTable(tableName);
            if (table == null) return;

            module.tables.Remove(table);
            ConfigRawStorage.DeleteTableFile(moduleName, tableName);
            SaveModule(module);
        }

        public static void RenameTable(string moduleName, string oldName, string newName)
        {
            EnsureLoaded();
            ValidateIdentifier(newName, "表名");
            ConfigModule module = RequireModule(moduleName);
            ConfigTable table = module.FindTable(oldName)
                                ?? throw new InvalidOperationException($"表不存在: {moduleName}/{oldName}");
            if (module.FindTable(newName) != null)
                throw new InvalidOperationException($"表已存在: {moduleName}/{newName}");

            ConfigRawStorage.RenameTableFiles(moduleName, oldName, newName);
            table.tableName = newName;
            if (!string.IsNullOrEmpty(table.mapCsharpTypeFullName))
                table.mapCsharpTypeFullName = BuildTypeFullName(newName);

            if (table.dataLoaded)
                SaveTable(table);
            SaveModule(module);
        }

        public static void RenameModule(string oldName, string newName)
        {
            EnsureLoaded();
            ValidateIdentifier(newName, "模块名");
            ConfigModule module = RequireModule(oldName);
            if (string.Equals(oldName, newName, StringComparison.Ordinal))
                return;
            if (_modules.ContainsKey(newName))
                throw new InvalidOperationException($"模块已存在: {newName}");

            string oldDir = ConfigPaths.GetModuleDirectory(oldName);
            string newDir = ConfigPaths.GetModuleDirectory(newName);
            if (Directory.Exists(newDir))
                throw new InvalidOperationException($"目标模块目录已存在: {newDir}");

            _modules.Remove(oldName);
            module.moduleName = newName;
            foreach (ConfigTable table in module.tables)
                table.moduleName = newName;
            _modules[newName] = module;

            if (Directory.Exists(oldDir))
                Directory.Move(oldDir, newDir);

            SaveModule(module);
            foreach (ConfigTable table in module.tables)
                if (table.dataLoaded)
                    SaveTable(table);
        }

        public static string GenerateUniqueModuleName(string baseName = "NewModule")
        {
            EnsureLoaded();
            if (!_modules.ContainsKey(baseName))
                return baseName;
            int i = 1;
            while (_modules.ContainsKey(baseName + i))
                i++;
            return baseName + i;
        }

        public static string GenerateUniqueTableName(string moduleName, string baseName = "NewTable")
        {
            EnsureLoaded();
            ConfigModule module = RequireModule(moduleName);
            if (module.FindTable(baseName) == null)
                return baseName;
            int i = 1;
            while (module.FindTable(baseName + i) != null)
                i++;
            return baseName + i;
        }

        public static void SetModuleExporter(string moduleName, string exporterTypeFullName)
        {
            EnsureLoaded();
            ConfigModule module = RequireModule(moduleName);
            if (!_configExporters.ContainsKey(exporterTypeFullName))
                throw new InvalidOperationException($"未知导出器: {exporterTypeFullName}");
            module.exporterTypeFullName = exporterTypeFullName;
            SaveModule(module);
        }

        public static void SaveModule(ConfigModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            ModuleMetaSerializer.Save(module);
        }

        /// <returns>被跳过的空行数量</returns>
        public static int SaveTable(ConfigTable table, bool updateModuleMeta = true)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (!table.dataLoaded)
                throw new InvalidOperationException($"表 {table.moduleName}/{table.tableName} 数据尚未加载，无法保存");

            int removed = RemoveEmptyItems(table);
            ConfigRawStorage.SaveTable(table);
            table.cachedRowCount = table.items.Count;

            if (updateModuleMeta)
            {
                ConfigModule module = GetModule(table.moduleName);
                if (module != null)
                    SaveModule(module);
            }

            if (removed > 0)
                Debug.Log($"[UniConfig] 保存 {table.moduleName}/{table.tableName} 时已跳过 {removed} 行空数据");
            return removed;
        }

        /// <summary>
        /// 移除所有列均为 null / 空白的数据行，返回移除数量。
        /// </summary>
        public static int RemoveEmptyItems(ConfigTable table)
        {
            if (table?.items == null || table.items.Count == 0)
                return 0;

            int removed = 0;
            for (int i = table.items.Count - 1; i >= 0; i--)
            {
                ConfigItem item = table.items[i];
                if (item == null || item.IsEmpty(table.fields))
                {
                    table.items.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        /// <returns>所有已加载表合计跳过的空行数量</returns>
        public static int SaveAll()
        {
            EnsureLoaded();
            int removed = 0;
            foreach (ConfigModule module in _modules.Values)
            {
                foreach (ConfigTable table in module.tables)
                {
                    if (!table.dataLoaded)
                        continue;
                    removed += SaveTable(table, false);
                }

                SaveModule(module);
            }

            return removed;
        }

        /// <summary>
        /// 请求异步加载表数据。可安全在 UI 线程反复调用。
        /// </summary>
        public static void RequestTableDataLoad(ConfigTable table, Action onCompleted = null)
        {
            if (table == null)
            {
                onCompleted?.Invoke();
                return;
            }

            if (table.dataLoaded)
            {
                onCompleted?.Invoke();
                return;
            }

            if (table.isLoading)
                return;

            table.isLoading = true;
            string moduleName = table.moduleName;
            string tableName = table.tableName;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                ConfigTable loaded = null;
                Exception error = null;
                try
                {
                    if (ConfigRawStorage.TryFindTableFile(moduleName, tableName, out string _) ||
                        ConfigPaths.GetExistingShardPaths(moduleName, tableName).Count > 0)
                        loaded = ConfigRawStorage.LoadTable(moduleName, tableName);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                EditorApplication.delayCall += () =>
                {
                    ApplyLoadedTableData(moduleName, tableName, loaded, error);
                    onCompleted?.Invoke();
                };
            });
        }

        /// <summary>
        /// 同步确保表数据已加载（导出 / 生成等场景）。
        /// </summary>
        public static void EnsureTableDataLoaded(ConfigTable table)
        {
            if (table == null || table.dataLoaded)
                return;

            if (ConfigRawStorage.TryFindTableFile(table.moduleName, table.tableName, out _) ||
                ConfigPaths.GetExistingShardPaths(table.moduleName, table.tableName).Count > 0)
            {
                ConfigTable loaded = ConfigRawStorage.LoadTable(table.moduleName, table.tableName);
                ApplyLoadedTableData(table.moduleName, table.tableName, loaded, null);
            }
            else
            {
                table.items = new List<ConfigItem>();
                table.dataLoaded = true;
                table.isLoading = false;
                table.cachedRowCount = 0;
            }
        }

        public static string GenerateCsharp()
        {
            EnsureLoaded();
            UniConfigEditorSettings settings = UniConfigEditorSettings.instance;
            CsharpConfigFileGenerator generator = new(
                new List<ConfigModule>(_modules.Values),
                settings.rootNamespace,
                settings.csharpExportDirectory);
            string path = generator.Generate();
            SaveAll();
            return path;
        }

        [InitializeOnLoadMethod]
        private static void RegisterExportAfterReload()
        {
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReloadForExport;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReloadForExport;
        }

        private static void OnAfterAssemblyReloadForExport()
        {
            if (!SessionState.GetBool(PendingExportSessionKey, false))
                return;
            SessionState.EraseBool(PendingExportSessionKey);
            try
            {
                EnsureLoaded();
                ExportConfigsInternal();
                Debug.Log("[UniConfig] 编译完成，配置导出已自动继续。");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("UniConfig", ex.Message, "OK");
            }
        }

        /// <summary>
        /// 导出全部配置到各模块所选导出器对应的单文件。若存在未生成 C# 的表，先生成并等待编译后再导出。
        /// </summary>
        public static void ExportConfigs()
        {
            EnsureLoaded();
            EnsureExportTableNamesUnique();

            if (AnyTableMissingCsharpType())
            {
                GenerateCsharp();
                SessionState.SetBool(PendingExportSessionKey, true);
                Debug.Log("[UniConfig] 已生成 C#，等待 Unity 编译完成后自动继续导出…");
                return;
            }

            ExportConfigsInternal();
        }

        private static void ExportConfigsInternal()
        {
            EnsureLoaded();
            EnsureExportTableNamesUnique();

            Dictionary<string, List<ExportTableData>> groups = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ConfigModule> kv in _modules)
            {
                ConfigModule module = kv.Value;
                if (module?.tables == null) continue;

                string exporterName = ResolveExporterTypeFullName(module.exporterTypeFullName);
                if (!_configExporters.TryGetValue(exporterName, out IConfigExporter exporter))
                    throw new InvalidOperationException($"模块 {module.moduleName} 未配置有效导出器");

                if (!groups.TryGetValue(exporterName, out List<ExportTableData> list))
                {
                    list = new List<ExportTableData>();
                    groups[exporterName] = list;
                }

                foreach (ConfigTable table in module.tables)
                {
                    if (table == null) continue;
                    EnsureTableDataLoaded(table);
                    list.Add(BuildExportTableData(table));
                }
            }

            foreach (KeyValuePair<string, List<ExportTableData>> group in groups)
            {
                if (group.Value == null || group.Value.Count == 0)
                    continue;
                IConfigExporter exporter = _configExporters[group.Key];
                exporter.Export(group.Value);
            }
        }

        private static bool AnyTableMissingCsharpType()
        {
            foreach (ConfigModule module in _modules.Values)
            {
                if (module?.tables == null) continue;
                foreach (ConfigTable table in module.tables)
                {
                    if (table == null) continue;
                    if (table.fields == null || table.fields.Count == 0)
                        continue;

                    if (table.tableId <= 0)
                        return true;

                    string typeName = table.mapCsharpTypeFullName;
                    if (string.IsNullOrEmpty(typeName))
                        typeName = BuildTypeFullName(table.tableName);
                    if (FindType(typeName) == null)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 导出按 tableId 区分表；表名仍需全局唯一（C# 类名约定）。
        /// </summary>
        private static void EnsureExportTableNamesUnique()
        {
            Dictionary<string, string> nameOwner = new(StringComparer.Ordinal);
            Dictionary<int, string> idOwner = new();
            foreach (KeyValuePair<string, ConfigModule> kv in _modules)
            {
                ConfigModule module = kv.Value;
                if (module?.tables == null) continue;
                foreach (ConfigTable table in module.tables)
                {
                    if (table == null || string.IsNullOrEmpty(table.tableName)) continue;
                    if (nameOwner.TryGetValue(table.tableName, out string existingModule))
                        throw new InvalidOperationException(
                            $"导出表名冲突: 「{table.tableName}」同时存在于模块 {existingModule} 与 {kv.Key}。" +
                            "表名必须全局唯一。");

                    nameOwner[table.tableName] = kv.Key;

                    if (table.tableId <= 0) continue;
                    if (idOwner.TryGetValue(table.tableId, out string idModule))
                        throw new InvalidOperationException(
                            $"导出 tableId 冲突: {table.tableId}（{idModule} 与 {kv.Key}/{table.tableName}）。");

                    idOwner[table.tableId] = $"{kv.Key}/{table.tableName}";
                }
            }
        }

        public static ExportTableData BuildExportTableData(ConfigTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            EnsureTableDataLoaded(table);
            if (table.fields == null || table.fields.Count == 0)
                throw new InvalidOperationException($"表 {table.moduleName}/{table.tableName} 没有字段");

            Type type = ResolveConfigType(table);
            ConstructorInfo ctor = FindAllArgsConstructor(type, table);
            List<object> objects = new(table.items?.Count ?? 0);

            if (table.items != null)
                for (int row = 0; row < table.items.Count; row++)
                {
                    ConfigItem item = table.items[row];
                    if (item == null || item.IsEmpty(table.fields))
                        continue;

                    object[] args = new object[table.fields.Count];
                    for (int i = 0; i < table.fields.Count; i++)
                    {
                        ConfigField field = table.fields[i];
                        string raw = item.GetValue(field.name);
                        try
                        {
                            args[i] = FieldUtils.ToValue(raw, field.type);
                        }
                        catch (Exception ex)
                        {
                            throw new FormatException(
                                $"导出失败 {table.moduleName}/{table.tableName} 第 {row + 1} 行字段 {field.name}: {ex.Message}",
                                ex);
                        }
                    }

                    objects.Add(ctor.Invoke(args));
                }

            if (table.tableId <= 0)
                throw new InvalidOperationException(
                    $"表 {table.moduleName}/{table.tableName} 尚未分配 tableId，请先执行「生成 C#」。");

            return new ExportTableData(table.moduleName, table.tableName, table.tableId, type, objects);
        }

        private static void ApplyLoadedTableData(string moduleName, string tableName, ConfigTable loaded,
            Exception error)
        {
            ConfigModule module = GetModule(moduleName);
            ConfigTable live = module?.FindTable(tableName);
            if (live == null)
                return;

            live.isLoading = false;
            if (error != null)
            {
                Debug.LogError($"[UniConfig] 加载表失败 {moduleName}/{tableName}: {error.Message}");
                return;
            }

            if (loaded != null)
            {
                live.fields = loaded.fields ?? new List<ConfigField>();
                live.items = loaded.items ?? new List<ConfigItem>();
                live.constraints = loaded.constraints ?? new List<FieldConstraint>();
                if (!string.IsNullOrEmpty(loaded.mapCsharpTypeFullName))
                    live.mapCsharpTypeFullName = loaded.mapCsharpTypeFullName;
                if (loaded.tableId > 0)
                    live.tableId = loaded.tableId;
            }
            else
            {
                live.items ??= new List<ConfigItem>();
                live.items.Clear();
            }

            RemoveEmptyItems(live);
            live.cachedRowCount = live.items.Count;
            live.dataLoaded = true;
            live.MarkFkValidationDirty();
            ConstraintEvaluator.EnsureForeignKeyState(live);
        }

        private static ConfigModule LoadModuleFromDisk(string moduleName)
        {
            ModuleMetaSerializer.ModuleMetaDto meta = ModuleMetaSerializer.Load(moduleName);
            ConfigModule module = new()
            {
                moduleName = moduleName,
                exporterTypeFullName = meta?.exporterTypeFullName
            };

            if (meta?.tables != null && meta.tables.Count > 0)
            {
                foreach (ModuleMetaSerializer.TableMetaDto tableMeta in meta.tables)
                {
                    ConfigTable table = CreateTableStub(moduleName, tableMeta);
                    module.tables.Add(table);
                }
            }
            else
            {
                foreach (string tableName in ConfigPaths.DiscoverTableNames(moduleName))
                {
                    ModuleMetaSerializer.TableMetaDto stubMeta = new() { tableName = tableName };
                    ConfigTable table = CreateTableStub(moduleName, stubMeta);
                    module.tables.Add(table);
                }

                SaveModule(module);
            }

            module.exporterTypeFullName = ResolveExporterTypeFullName(module.exporterTypeFullName);
            return module;
        }

        private static string ResolveExporterTypeFullName(string exporterTypeFullName)
        {
            if (!string.IsNullOrEmpty(exporterTypeFullName) &&
                _configExporters.ContainsKey(exporterTypeFullName))
                return exporterTypeFullName;

            string streaming = typeof(StreamingAssetsExporter).FullName;
            if (_configExporters.ContainsKey(streaming))
                return streaming;

            string resources = typeof(ResourcesExporter).FullName;
            if (_configExporters.ContainsKey(resources))
                return resources;

            foreach (string key in _configExporters.Keys)
                return key;

            return streaming;
        }

        private static ConfigTable CreateTableStub(string moduleName, ModuleMetaSerializer.TableMetaDto tableMeta)
        {
            ConfigTable table = new()
            {
                moduleName = moduleName,
                tableName = tableMeta.tableName,
                mapCsharpTypeFullName = tableMeta.mapCsharpTypeFullName ?? string.Empty,
                tableId = tableMeta.tableId,
                cachedRowCount = tableMeta.rowCount,
                dataLoaded = false,
                isLoading = false
            };

            if (tableMeta.fields != null)
                foreach (ModuleMetaSerializer.FieldMetaDto field in tableMeta.fields)
                    table.fields.Add(new ConfigField(field.name, (SupportableFieldType)field.type));

            if (table.fields.Count == 0)
                ConfigRawStorage.LoadTableHeader(moduleName, table.tableName, table);

            return table;
        }

        private static ConfigModule RequireModule(string moduleName)
        {
            if (!_modules.TryGetValue(moduleName, out ConfigModule module))
                throw new InvalidOperationException($"模块不存在: {moduleName}");
            return module;
        }

        private static void ValidateIdentifier(string name, string label)
        {
            if (!FieldUtils.IsValidIdentifier(name))
                throw new InvalidOperationException($"{label}不是合法标识符: {name}");
        }

        private static string BuildTypeFullName(string tableName)
        {
            string ns = UniConfigEditorSettings.instance.rootNamespace;
            return $"{ns}.{CsharpConfigFileGenerator.GetClassName(tableName)}";
        }

        private static Type ResolveConfigType(ConfigTable table)
        {
            string typeName = table.mapCsharpTypeFullName;
            if (string.IsNullOrEmpty(typeName))
                typeName = BuildTypeFullName(table.tableName);

            Type type = FindType(typeName);
            if (type == null)
                throw new InvalidOperationException(
                    $"找不到配置类型 {typeName}。请先执行「生成 C#」并等待 Unity 编译完成后再导出。");
            return type;
        }

        private static Type FindType(string fullName)
        {
            Type type = Type.GetType(fullName);
            if (type != null) return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                string name;
                try
                {
                    name = assembly.GetName().Name;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(name)) continue;
                if (name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "mscorlib", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "netstandard", StringComparison.OrdinalIgnoreCase))
                    continue;

                type = assembly.GetType(fullName);
                if (type != null) return type;
            }

            return null;
        }

        private static ConstructorInfo FindAllArgsConstructor(Type type, ConfigTable table)
        {
            Type[] paramTypes = new Type[table.fields.Count];
            for (int i = 0; i < table.fields.Count; i++)
                paramTypes[i] = GetClrType(table.fields[i].type);

            ConstructorInfo ctor = type.GetConstructor(paramTypes);
            if (ctor == null)
                throw new InvalidOperationException(
                    $"类型 {type.FullName} 缺少与表字段匹配的全参构造函数");
            return ctor;
        }

        private static Type GetClrType(SupportableFieldType fieldType)
        {
            return fieldType switch
            {
                SupportableFieldType.Int16 => typeof(short),
                SupportableFieldType.Int32 => typeof(int),
                SupportableFieldType.Int64 => typeof(long),
                SupportableFieldType.UInt16 => typeof(ushort),
                SupportableFieldType.UInt32 => typeof(uint),
                SupportableFieldType.UInt64 => typeof(ulong),
                SupportableFieldType.Single => typeof(float),
                SupportableFieldType.Double => typeof(double),
                SupportableFieldType.Boolean => typeof(bool),
                SupportableFieldType.String => typeof(string),
                SupportableFieldType.Array1D_Int16 => typeof(short[]),
                SupportableFieldType.Array1D_Int32 => typeof(int[]),
                SupportableFieldType.Array1D_Int64 => typeof(long[]),
                SupportableFieldType.Array1D_UInt16 => typeof(ushort[]),
                SupportableFieldType.Array1D_UInt32 => typeof(uint[]),
                SupportableFieldType.Array1D_UInt64 => typeof(ulong[]),
                SupportableFieldType.Array1D_Boolean => typeof(bool[]),
                SupportableFieldType.Array1D_Single => typeof(float[]),
                SupportableFieldType.Array1D_Double => typeof(double[]),
                SupportableFieldType.Array2D_Int16 => typeof(short[][]),
                SupportableFieldType.Array2D_Int32 => typeof(int[][]),
                SupportableFieldType.Array2D_Int64 => typeof(long[][]),
                SupportableFieldType.Array2D_UInt16 => typeof(ushort[][]),
                SupportableFieldType.Array2D_UInt32 => typeof(uint[][]),
                SupportableFieldType.Array2D_UInt64 => typeof(ulong[][]),
                SupportableFieldType.Array2D_Boolean => typeof(bool[][]),
                SupportableFieldType.Array2D_Single => typeof(float[][]),
                SupportableFieldType.Array2D_Double => typeof(double[][]),
                _ => throw new ArgumentOutOfRangeException(nameof(fieldType), fieldType, null)
            };
        }
    }
}