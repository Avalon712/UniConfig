# UniConfig

面向程序员的高性能、易用的 Unity 游戏配置框架。

在编辑器中以模块/表方式管理配置数据，一键生成 C# 类型与二进制配置文件；运行时通过 `ConfigMgr` 加载并查询，内置 **Resources** 与 **StreamingAssets** 两种读取方式。

- **Unity**：2022.3+
- **版本**：1.0.0
- **仓库**：[https://github.com/Avalon712/UniConfig](https://github.com/Avalon712/UniConfig.git)

---

## 安装

UniConfig 依赖 [MemoryPack](https://github.com/Cysharp/MemoryPack)（包名 `com.cysharp.memorypack`，版本 `1.21.4`）。请**先安装 MemoryPack**，再安装 UniConfig。

### 1. 安装 MemoryPack（二选一）

#### 方式 A：Avalon712 的 PackageManager方式

通过 Package Manager 的 **Add package from git URL** 添加：

```text
https://github.com/Avalon712/MemoryPackForUnity.git#1.21.4
```

或在 `Packages/manifest.json` 的 `dependencies` 中增加：

```json
"com.cysharp.memorypack": "https://github.com/Avalon712/MemoryPackForUnity.git#1.21.4"
```

该包已内置 Core / Unity / Generator 等 DLL，无需再装 NuGetForUnity。详见 [Avalon712/MemoryPackForUnity](https://github.com/Avalon712/MemoryPackForUnity)。

#### 方式 B：MemoryPack 官方推荐方式

1. 使用 [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) 安装 NuGet 包 `MemoryPack`  
   （`NuGet` → `Manage NuGet Packages`，搜索 `MemoryPack` 并安装）
2. 再通过 Package Manager 的 **Add package from git URL** 安装 Unity 扩展：

```text
https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/MemoryPack.Unity#1.21.4
```

若出现程序集版本冲突，可在 `Edit` → `Project Settings` → `Player` → `Other Settings` 中取消勾选 **Assembly Version Validation**。

### 2. 安装 UniConfig

#### 方式一：Package Manager（Git URL）

1. 打开 Unity：`Window` → `Package Manager`
2. 左上角 `+` → `Add package from git URL...`
3. 填入（指定版本标签）：

```text
https://github.com/Avalon712/UniConfig.git#v1.0.0
```

或安装最新默认分支：

```text
https://github.com/Avalon712/UniConfig.git#v1.0.0
```

#### 方式二：manifest.json

在项目 `Packages/manifest.json` 的 `dependencies` 中增加（MemoryPack 任选一种写法）：

```json
{
  "dependencies": {
    "com.cysharp.memorypack": "https://github.com/Avalon712/MemoryPackForUnity.git#1.21.4",
    "org.avalon712.uniconfig": "https://github.com/Avalon712/UniConfig.git#v1.0.0"
  }
}
```

安装后可在菜单中找到：

- `UniConfig` → `Config Editor`：配置编辑器
- `UniConfig` → `Settings`：路径与命名空间等设置

---

## 快速开始（编辑器）

### 1. 设置

打开 `UniConfig` → `Settings`：

| 项                    | 说明                             | 默认                       |
| -------------------- | ------------------------------ | ------------------------ |
| Cfg.g.cs 导出目录        | 生成代码所在目录（须在 `Assets` 下）        | `Assets/Scripts/Config`  |
| 根命名空间                | 生成类命名空间                        | `Game.Config`            |
| Resources 导出目录       | `ResourcesExporter` 输出目录       | `Assets/Resources`       |
| StreamingAssets 导出目录 | `StreamingAssetsExporter` 输出目录 | `Assets/StreamingAssets` |

导出文件名固定为 **`configs.bytes`**。

### 2. 编辑配置

1. 打开 `UniConfig` → `Config Editor`
2. 新建**模块**、在模块下新建**表**，编辑字段与行数据  
   - 模块仅用于编辑器内管理，**导出后运行时不再有模块概念**
3. 为每个模块选择导出器（下拉框）：
   - **`ResourcesExporter`**：导出到 Resources
   - **`StreamingAssetsExporter`**：导出到 StreamingAssets（大表推荐）
4. 点击 **生成 C#**：生成 `Cfg.g.cs`（类名约定 `Cfg{表名}`，并实现 `IConfigTable`）
5. 点击 **导出配置**：写出 `configs.bytes`  
   - 若尚有表未生成/未编译对应 C# 类型，会先生成 C#，待 Unity 编译完成后自动继续导出

> 表名须全局唯一（跨模块也不能重名），因为运行时按类型全名区分表。

### 3. 字段约束（可选）

表工具栏中的 **添加约束** 支持：

- **外键**：引用另一张表某字段的取值
- **公式**：用表达式自动计算字段（可跨表引用）

---

## 运行时使用

命名空间：`UniConfig`。生成的配置类默认在设置的根命名空间下（如 `Game.Config`）。

### 加载配置

**Resources（勿带扩展名）：**

```csharp
using UniConfig;
using Game.Config;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private void Awake()
    {
        // Assets/Resources/configs.bytes  →  "configs"
        ConfigMgr.LoadAllConfigsFromResourcesSync("configs");

        // 或异步
        // var op = ConfigMgr.LoadAllConfigsFromResourcesAsync("configs");
    }
}
```

**StreamingAssets（须带文件名/扩展名，路径相对 StreamingAssets）：**

```csharp
// Assets/StreamingAssets/configs.bytes  →  "configs.bytes"
var op = ConfigMgr.LoadAllConfigsFromStreamingAssetsAsync("configs.bytes");
// 大文件可将 bufferSize 调大，加快分片读取（默认 1024）
// ConfigMgr.LoadAllConfigsFromStreamingAssetsAsync("configs.bytes", bufferSize: 64 * 1024);
```

### 查询配置

```csharp
// 遍历整表
foreach (CfgSkill skill in ConfigMgr.GetConfigs<CfgSkill>())
{
    Debug.Log(skill); // 生成类已重写 ToString
}

// 取第一条匹配
CfgSkill one = ConfigMgr.GetConfig<CfgSkill>(s => s.id == 1001);

// 取所有匹配
List<CfgSkill> list = ConfigMgr.GetConfigs<CfgSkill>(s => s.attack > 10);

// 无额外 List 分配的过滤写入
var buffer = new List<CfgSkill>();
ConfigMgr.GetConfigsNoAlloc<CfgSkill>(s => s.attack > 10, buffer);
```

生成类实现 `IConfigTable`，并生成 `public static class Cfg`：在 `BeforeSceneLoad` 自动注册反序列化与 `GetTableId`；反序列化对 **表 Id** 做 `switch`（整数 jump table，避免字符串 if 链）。未注册或 Id 未知时会 `LogError` 并跳过该行。

```csharp
int id1 = Cfg.GetTableId<CfgSkill>();
int id2 = Cfg.GetTableId(typeof(CfgSkill));
int id3 = Cfg.GetTableId("Skill");                 // 表名
int id4 = Cfg.GetTableId("Game.Config.CfgSkill");  // 全类名（与表名同一 string 重载）
```

---

## Resources vs StreamingAssets：差异与选型

两种方式最终都会把解析结果放进内存中的配置字典；**差别在「如何把文件读进内存并解析」**。

|        | **Resources**                                    | **StreamingAssets**                                   |
| ------ | ------------------------------------------------ | ----------------------------------------------------- |
| 导出器    | `ResourcesExporter`                              | `StreamingAssetsExporter`                             |
| 默认路径   | `Assets/Resources/configs.bytes`                 | `Assets/StreamingAssets/configs.bytes`                |
| 加载 API | `LoadAllConfigsFromResourcesSync` / `Async`      | `LoadAllConfigsFromStreamingAssetsAsync`              |
| 路径写法   | **不要**扩展名，如 `"configs"`                          | **要**相对路径文件名，如 `"configs.bytes"`                      |
| 读入方式   | `Resources.Load` 一次性拿到完整 `TextAsset.bytes`，再顺序解析 | `UnityWebRequest` + `DownloadHandlerScript` **边下边解析** |
| 峰值内存   | 较高：整文件字节 + 解析后的对象常同时存在                           | 更友好：按缓冲区分片到达、流式反序列化，可避免长时间持有整包原始字节                    |
| 平台注意   | 打进包体，随 Resources 管理                              | Android 等需从 jar/包内读，框架已处理 URL；适合大文件                   |

### 建议

- **小配置、编辑器里快速验证**：用 Resources，同步加载简单。
- **大配置、正式包、在意启动内存**：用 **`StreamingAssetsExporter` + `LoadAllConfigsFromStreamingAssetsAsync`**。  
  流式解析会在数据到达时按协议逐表、逐行反序列化；完整行可尽量零拷贝反序列化，跨分片时用可复用缓冲（按 2 的幂扩容），从而**降低大文件加载过程中的额外内存开销**。

同一项目中不同模块可选不同导出器：导出时会按导出器分组，各自生成一份对应目录下的 `configs.bytes`。请保证「实际加载的那份」与运行时调用的 API 一致。

---

## 二进制协议（简要）

所有表写入**同一个** `configs.bytes`，表顺序任意。每张表：

1. 魔数 `UCFG`（4 字节）
2. 表唯一 Id `int32`（生成 C# 时自增分配并持久化；已分配的 Id 不改动）
3. 行数 `int32`
4. 重复 N 次：`payload 长度 int32` + MemoryPack 行数据

空表也会导出（行数 = 0）。**协议变更后需重新生成 C# 并重新导出。**

---

## 常见问题

**Q: Resources 加载报找不到？**  
A: 路径不要带 `.bytes`。`Assets/Resources/configs.bytes` → `"configs"`。

**Q: StreamingAssets 加载报找不到？**  
A: 路径要带文件名，相对 `StreamingAssets`：`"configs.bytes"`。若放在子目录则为 `"Foo/configs.bytes"`。

**Q: 导出后提示找不到配置类型？**  
A: 先 **生成 C#** 并等待编译完成；确认生成类实现了 `IConfigTable`，再导出/运行。

**Q: 导出到 StreamingAssets/Resources 后 Console 曾出现 Import Loop？**  
A: 当前导出不再强制 `AssetDatabase.Refresh/ImportAsset`。若编辑器列表未立刻刷新，切换一下焦点或等 Unity 自动发现文件即可。

---

## 许可证

见仓库内 `LICENSE.md`。
