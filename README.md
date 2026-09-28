# UniConfig

面向程序员的高性能、易用的 Unity 游戏配置框架。

在编辑器中以模块/表方式管理配置数据，一键生成 C# 类型与二进制配置文件；运行时通过 `ConfigMgr` 加载并查询，内置 **Resources** 与 **StreamingAssets** 两种读取方式。

- **Unity**：2022.3+
- **版本**：1.1.0
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
3. 填入：

```text
https://github.com/Avalon712/UniConfig.git
```

将跟踪仓库默认分支（`master`）最新提交。

#### 方式二：manifest.json

在项目 `Packages/manifest.json` 的 `dependencies` 中增加（MemoryPack 任选一种写法）：

```json
{
  "dependencies": {
    "com.cysharp.memorypack": "https://github.com/Avalon712/MemoryPackForUnity.git#1.21.4",
    "org.avalon712.uniconfig": "https://github.com/Avalon712/UniConfig.git"
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
| 自定义枚举类型              | 完整类型名列表（`List`），会出现在字段类型菜单中    | （空）                      |

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

> 表名须全局唯一（跨模块也不能重名）。运行时按表唯一 Id 区分表。

### 3. 字段类型与单元格写法

编辑器单元格一律填**字符串**；导出时按字段类型解析。空单元格使用该类型默认值（数值 `0`、`bool` 为 `false`、`string` 为空串、数组为空数组、Unity 向量为零向量 / `Quaternion.identity` / 透明色）。

#### 标量

| 编辑器类型                       | 生成 C#    | 写法示例                         | 说明                  |
| --------------------------- | -------- | ---------------------------- | ------------------- |
| `short` / `int` / `long`    | 同左       | `42` / `-1`                  | 十进制整数               |
| `ushort` / `uint` / `ulong` | 同左       | `0` / `100`                  | 无符号，勿写负号            |
| `float` / `double`          | 同左       | `1.5` / `-0.25`              | 使用不变区域小数点 `.`       |
| `bool`                      | `bool`   | `true` / `false` / `1` / `0` | 大小写不敏感              |
| `string`                    | `string` | `hello` / 任意文本               | 原样保存，不做 Trim 语义外的转换 |

#### 一维数组 `T[]`

用英文逗号 `,` 分隔元素；元素规则与对应标量相同。

| 编辑器类型     | 写法示例             | 结果                      |
| --------- | ---------------- | ----------------------- |
| `int[]`   | `1,2,3`          | `{ 1, 2, 3 }`           |
| `float[]` | `1.0, 2.5, -3`   | 允许空格                    |
| `bool[]`  | `true,0,1,false` | 混用 `true/false` 与 `0/1` |
| （空数组）     | 空单元格             | `Array.Empty<T>()`      |

> 单个空段且只有一个部分时视为空数组；不要在末尾随意多写逗号（会产生空元素解析失败）。

#### 二维数组 `T[][]`

行与行之间用英文分号 `;`，行内仍用逗号 `,`。

| 编辑器类型       | 写法示例           | 结果                   |
| ----------- | -------------- | -------------------- |
| `int[][]`   | `1,2;3,4,5`    | `{ {1,2}, {3,4,5} }` |
| `float[][]` | `1.0,2.0; 3.5` | 各行长度可不同              |
| （空）         | 空单元格           | `Array.Empty<T[]>()` |

#### Unity 数学类型

分量之间用逗号分隔；可选外层圆括号。MemoryPack 可直接序列化这些 unmanaged struct。

| 编辑器类型        | 分量顺序         | 写法示例                                                                      |
| ------------ | ------------ | ------------------------------------------------------------------------- |
| `Vector2`    | x, y         | `1.5,2.5` 或 `(1.5, 2.5)`                                                  |
| `Vector3`    | x, y, z      | `1,2,3`                                                                   |
| `Vector4`    | x, y, z, w   | `1,0,0,0`                                                                 |
| `Quaternion` | x, y, z, w   | `0,0,0,1`（单位四元数）                                                          |
| `Vector2Int` | x, y         | `3,4` / `(-2, 8)`                                                         |
| `Vector3Int` | x, y, z      | `1,2,3`                                                                   |
| `Color`      | r, g, b[, a] | `1,0,0,1`（0~1）；`255,128,0,255`（出现 >1 时按 0~255 换算）；`#FF0000` / `#FF0000FF` |
| `Color32`    | r, g, b[, a] | `255,0,0,255`；`#00FF00` / `#0000FFFF`（a 缺省为 255）                          |

`Color` / `Color32` 的十六进制支持 `#RGB`、`#RGBA`、`#RRGGBB`、`#RRGGBBAA`。

#### 自定义枚举

1. 打开 `UniConfig` → `Settings`，在 **自定义枚举类型** 列表中添加完整类型名（如 `Game.ESkillType`），类型必须已编译且为 `enum`；重复项会自动去除。
2. 配置表字段类型使用**分级菜单**选择（见下）；选中枚举后单元格变为**枚举成员下拉**。
3. 单元格存成员名（如 `Fire`）；默认值为底层数值**最小**的成员（不必从 0 开始）。
4. 生成的 C# 字段类型为该枚举的完整类型名；MemoryPack 可直接序列化枚举。

#### 字段类型分级菜单

类型列不再使用超长扁平下拉，改为多级菜单：

| 分类 | 内容 |
|------|------|
| 基础 | short / int / long / … / string |
| 一维数组 | `T[]` |
| 二维数组 | `T[][]` |
| Unity | Vector / Quaternion / Color 等 |
| 枚举 | Settings 中登记的自定义枚举 |

### 4. 字段约束（可选）

表工具栏中的 **添加约束** 支持：

- **外键**：引用另一张表某字段的取值
- **公式**：用表达式自动计算字段（可跨表引用）

### 5. 编辑器界面状态

**生成 C#** / **导出配置** 可能触发脚本域重载。编辑器会通过 `SessionState` 记住并自动恢复：

- 当前选中的模块 / 表
- 左侧树展开状态
- 数据页码
- 树搜索 / 表搜索关键字

重载后仍停留在原先编辑的位置，无需重新点开表。

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
// 须 yield return：完成时配置才写入完毕（不能只等下载 AsyncOperation）
IEnumerator Load()
{
    yield return ConfigMgr.LoadAllConfigsFromStreamingAssetsAsync("configs.bytes");
    // 大文件可将 bufferSize 调大：LoadAllConfigsFromStreamingAssetsAsync("configs.bytes", 64 * 1024);
}
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
