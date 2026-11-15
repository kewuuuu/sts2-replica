# 构建笔记：STS2 原生 mod 加载器（游戏 v0.113.1）

本文件记录**本机实测**通过的构建方式、加载器契约与坑。所有契约结论都来自本机反编译源
`D:\engine\DSH\Workspace\_cache\sts2-1131-src`（按命名空间分目录），引用格式为 `文件:行`。

## 0. 环境

| 项 | 值 |
|---|---|
| 游戏根 | `D:\software\steam\steamapps\common\Slay the Spire 2` |
| 主程序 | `SlayTheSpire2.exe` + `SlayTheSpire2.pck`（游戏根） |
| 托管程序集目录 | `<游戏根>\data_sts2_windows_x86_64`（`sts2.dll` / `0Harmony.dll` / `GodotSharp.dll` 与另外 198 个 dll） |
| .NET SDK | 9.0.302 |
| 引擎 | Godot 4（**不是 Unity**），mod 由游戏内置加载器加载，不用 BepInEx |

## 1. TFM = net9.0（实测）

- `data_sts2_windows_x86_64\sts2.runtimeconfig.json`：`"tfm": "net9.0"`，`includedFrameworks` 为 `Microsoft.NETCore.App` / `9.0.7`。
- `sts2-replica.csproj` 用 `<TargetFramework>net9.0</TargetFramework>`，`dotnet build -c Release` 通过（0 警告 0 错误）。

## 2. 引用游戏程序集：`<Reference>` + `Private=false`

```xml
<GameDataDir Condition="'$(GameDataDir)' == ''">D:\software\steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64</GameDataDir>
...
<Reference Include="sts2"><HintPath>$(GameDataDir)\sts2.dll</HintPath><Private>false</Private></Reference>
<Reference Include="0Harmony"><HintPath>$(GameDataDir)\0Harmony.dll</HintPath><Private>false</Private></Reference>
<Reference Include="GodotSharp"><HintPath>$(GameDataDir)\GodotSharp.dll</HintPath><Private>false</Private></Reference>
```

- `Private=false` 实测有效：`bin/Release/net9.0/` 里只有 `sts2-replica.dll`、`sts2-replica.pdb`、`sts2-replica.deps.json`，**没有**任何游戏 dll 被复制。
- 换机器装游戏时不必改文件：`dotnet build -p:GameDataDir=<新路径>`。csproj 里另有 `CheckGameAssemblies` 目标，`sts2.dll` / `GodotSharp.dll` 缺失时直接报错而不是产出一个诡异产物。
- 引用版本号与游戏不一致**不会**导致加载失败：`ModManager.cs:1165-1178` 的 `HandleAssemblyResolveFailure` 在 `AssemblyResolve` 里把 `sts2,` 与 `0Harmony,` 开头的请求重定向到已加载的程序集。

## 3. mod manifest 契约

- **文件名任意**。加载器递归扫描 `<游戏根>\mods`，只按后缀判定：`ModManager.cs:550-560`（`text.EndsWith(".json")`）。实测本机 Steam Workshop 目录里的真实 manifest 名有 `mod_manifest.json`、`BaseLib.json`、`RandomForeseer.json`、`intentgraph2.json` 等。
- **manifest 所在目录即 mod 根**：`ModManager.cs:607`（`mod.path = filename.GetBaseDir()`）。
- **dll 必须命名为 `<manifest.id>.dll` 且与 manifest 同目录**：`ModManager.cs:948`（`Path.Combine(mod.path, modId + ".dll")`）；只有 `has_dll == true` 且非 TestMode 才加载（`ModManager.cs:949`）。因此本仓库 `AssemblyName` = `sts2-replica`，与 `id` 完全一致。
- `pck` 路径为 `modId + ".pck"`（`ModManager.cs:970`），本项目无美术资源，`has_pck: false`。

字段定义（`MegaCrit.Sts2.Core.Modding\ModManifest.cs:16-46`，`[JsonPropertyName]` 即 JSON 键名）：

| JSON 键 | 字段 | 类型 | 说明 |
|---|---|---|---|
| `id` | `id` | string? | 必填；缺失时该 json 被丢弃（`ModManager.cs:593-604`） |
| `name` | `name` | string? | 显示名 |
| `author` | `author` | string? | |
| `description` | `description` | string? | |
| `version` | `version` | string? | |
| `has_pck` | `hasPck` | bool | 是否有 `<id>.pck` |
| `has_dll` | `hasDll` | bool | 必须 true 才加载 dll |
| `dependencies` | `dependencies` | List\<ModDependency\>? | 元素为 `{ "id": ..., "min_version": ... }`（`ModDependency.cs:5-17`） |
| `affects_gameplay` | `affectsGameplay` | bool（默认 true） | |
| `min_game_version` | `minGameVersion` | string? | |

`ModManifest.cs:48` 的 `ReadFromStream` 走 `JsonNode.Deserialize(JsonSerializationUtility.GetTypeInfo<ModManifest>())`，未列出的键（例如真实 mod 里出现的 `pck_name`）被忽略，不会报错。

本仓库 manifest（`mod/sts2-replica.json`，构建时复制到产物目录）：

```json
{
  "id": "sts2-replica",
  "name": "STS2 Replica (skeleton)",
  "author": "kewu",
  "description": "Skeleton mod: loader entry point plus one placeholder relic in the shared relic pool.",
  "version": "0.0.1",
  "has_pck": false,
  "has_dll": true,
  "dependencies": [],
  "affects_gameplay": true,
  "min_game_version": "0.113.1"
}
```

## 4. 入口类契约

- 特性：`MegaCrit.Sts2.Core.Modding\ModInitializerAttribute.cs:10-18` —— `[AttributeUsage(AttributeTargets.Class, Inherited = false)]`，字段 `public string initializerMethod`，构造函数 `ModInitializerAttribute(string initializerMethod)`。attribute 参数就是**方法名**。
- 调用方式：`ModManager.cs:995-1005` 用 `assembly.GetTypes()` 找带该特性的类；`ModManager.cs:1085-1112`（`CallModInitializer`）用 `GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)` 取方法，成功路径是 `method.Invoke(null, null)`。
  - 必须是 **static**、**无参**、返回 void；实例方法会命中 `Log.Error("...but it's not static! Declare it to be static")` 并返回 false。
  - 方法可以是 public 或非 public（`NonPublic` 在 flag 里），本项目用 public 便于自查。
- 没有该特性的程序集走退化路径 `new Harmony(...).PatchAll(assembly)`（`ModManager.cs:1011-1013`）。

`src/ReplicaMod.cs`（`:8` 特性，`:11` 方法签名，`:13-14` 注册 + 日志）：

```csharp
1  using MegaCrit.Sts2.Core.Logging;
2  using MegaCrit.Sts2.Core.Models.RelicPools;
3  using MegaCrit.Sts2.Core.Modding;
4  using Sts2Replica.Models;
5
6  namespace Sts2Replica;
7
8  [ModInitializer("Init")]
9  public static class ReplicaMod
10 {
11     public static void Init()
12     {
13         ModHelper.AddModelToPool<SharedRelicPool, PlaceholderRelic>();
14         Log.Info("[sts2-replica] initializer invoked; PlaceholderRelic queued for SharedRelicPool");
15     }
16 }
```

产物 DLL 上用反射复核过（模拟 `CallModInitializer` 的取方法逻辑）：
`initializer=Sts2Replica.ReplicaMod method='Init' static=True params=0 returns=System.Void`。

## 5. 遗物 + 共享遗物池

`src/Models/PlaceholderRelic.cs`（`:8` 类声明，`:10` Rarity，`:12` 战斗钩子开关）：

```csharp
1  using MegaCrit.Sts2.Core.Entities.Relics;
2  using MegaCrit.Sts2.Core.Models;
3
4  namespace Sts2Replica.Models;
5
6  // Icon assets are not shipped yet: RelicModel derives icon paths from Id.Entry and
7  // ResolvedBigIconPath falls back to powers/missing_power.png when they are absent.
8  public sealed class PlaceholderRelic : RelicModel
9  {
10     public override RelicRarity Rarity => RelicRarity.Common;
11
12     public override bool ShouldReceiveCombatHooks => false;
13 }
```

需要实现的面（只有这两个抽象成员）：

- `RelicModel.cs:24` `public abstract class RelicModel : AbstractModel`；`RelicModel.cs:174` `public abstract RelicRarity Rarity { get; }`。
- `AbstractModel.cs:70` `public abstract bool ShouldReceiveCombatHooks { get; }`。
- `RelicRarity` 在 `MegaCrit.Sts2.Core.Entities.Relics\RelicRarity.cs:3-13`：None/Starter/Common/Uncommon/Rare/Shop/Event/Ancient。
- 生命周期钩子（`RelicModel.cs:578-586` 的 `AfterObtained`/`AfterRemoved` 等）都是 virtual 返回 `Task.CompletedTask`，无需实现。

图标占位为什么不会崩：`RelicModel.cs:130` `IconBaseName => Id.Entry.ToLowerInvariant()`；`:132` `PackedIconPath`、`:136` `BigIconPath` 都由此拼出；`:150-170` 的 `ResolvedBigIconPath` 在资源不存在时回退到 `:140` 的 `powers/missing_power.png`。所以「不加任何图片资源」就是安全的占位实现。

自动进 ModelDb：

- `ModelDb.cs:82` 把 `AbstractModelSubtypes.All` 与 `ReflectionHelper.GetSubtypesInMods<AbstractModel>()` 合并（`ModelDb.cs:69-89`），mod 程序集里的模型子类因此会被自动发现。
- `ModelDb.cs:427-436` `Init(Type[]? injectedModelTypes = null)` 对每个类型取 `GetId(type)` 并 `Activator.CreateInstance` 放进 `_contentById`；`ModelDb.cs:442` `Inject(Type)` 是给 mod/测试补充注入的入口。
- `ModelDb.cs:543-566`：`GetId(Type) = new ModelId(GetCategory(type), GetEntry(type))`，category 是「直到基类为 `AbstractModel` 的那一层」的类型名 slug（这里是 `RelicModel` → `RELIC`），entry 是 `StringHelper.Slugify(type.Name)`（`PlaceholderRelic` → `PLACEHOLDER_RELIC`）。实测产物得到 `RELIC.PLACEHOLDER_RELIC`。
- `AbstractModel.cs:78-89` 构造函数会用 `GetId(type)` 登记并在 id 重复时抛 `DuplicateModelException` —— 类名不能与游戏已有模型同名。

进**共享**遗物池：

- `ModelDb.cs:271` `AllRelicPools = CharacterRelicPools.Concat(AllSharedRelicPools).Distinct()`；`ModelDb.cs:281-287` 共享池四个：`DeprecatedRelicPool` / `EventRelicPool` / `FallbackRelicPool` / `SharedRelicPool`。本项目选 `SharedRelicPool`（`MegaCrit.Sts2.Core.Models.RelicPools\SharedRelicPool.cs:9`，`EnergyColorName => "colorless"`）。
- `ModHelper.cs:414` `AddModelToPool<TPoolType, TModelType>() where TPoolType : AbstractModel, IPoolModel where TModelType : AbstractModel`，转调 `:419` 的 `AddModelToPool(Type, Type)`：把类型塞进 `_moddedContentForPools[poolType].modelsToAdd`；若该池已冻结则 `:429-433` 抛 `InvalidOperationException("...it's too late! You must add content before the game is initialized.")`。
- 池消费侧：`RelicPoolModel.cs:23-34` 的 `AllRelics` 先 `GenerateAllRelics()` 缓存，然后 `ModHelper.ConcatModelsFromMods(this, _relics)`；`ModHelper.cs:443-458` 用 `poolModel.GetType()` 作键（即 `SharedRelicPool`，与注册时一致）、置 `isFrozen = true`，并把待加类型经 `ModelDb.GetById<RelicModel>(ModelDb.GetId(t))` 解析成实例追加到池尾。
- 时序保证：`OneTimeInitialization.cs:59` 在 `ExecuteVeryEarly` 里 `await ModManager.Initialize(...)`（mod initializer 在这里跑），而 `ModelDb.Init()` 在之后的 `ExecuteEssential`（`OneTimeInitialization.cs:81`）。所以在 initializer 里调 `AddModelToPool` 必然早于池冻结。
- 反射复核：`pool=MegaCrit.Sts2.Core.Models.RelicPools.SharedRelicPool frozen=False models=[Sts2Replica.Models.PlaceholderRelic]`，且 `typeof(SharedRelicPool) is IPoolModel: True`。

## 6. 产物结构与安装

`dotnet build -c Release` 之后（`StageModDirectory` 目标，AfterTargets=Build）：

```
dist/sts2-replica/
├─ sts2-replica.dll     (4608 B)
└─ sts2-replica.json    (334 B)   <- mod/sts2-replica.json 原样复制
```

整个 `dist/sts2-replica/` 目录丢进 `<游戏根>\mods\` 即可（目录名不参与加载判定，但用 id 命名便于管理）。
`install.ps1` 是**手动**脚本（不会自动执行），默认目标 `<游戏根>\mods\sts2-replica`，因为 `SupportsShouldProcess` 会先要确认；无人值守时用 `-Confirm:$false`。
卸载 = 删掉那个目录。临时禁用 = 启动参数 `-nomods`（`ModManager.cs:235-240`）或游戏内 mod 列表。

`dist/` 在 `.gitignore` 里（构建产物不入库）；`mod/sts2-replica.json` 才是源。

## 7. 游戏怎么加载 mod / 日志在哪

- 加载器扫描三个来源（`ModManager.cs:248-263`）：`<exe 所在目录>\mods`（`ModSource.ModsDirectory`）、`<exe 所在目录>\mods_STEAMTEST`（`SteamWorkshop`）、以及 Steam 初始化成功后的 `ReadSteamMods()`。
- `OS.GetExecutablePath()` 指向 `<游戏根>\SlayTheSpire2.exe`，所以**本地 mod 放 `<游戏根>\mods`**（本机该目录已存在，里面目前只有一个 `config` 子目录）。注意不是 `data_sts2_windows_x86_64\mods`。
- 日志实现：`Log.Info` → `Logger` → `ConsoleLogPrinter.Print`（`MegaCrit.Sts2.Core.Logging\ConsoleLogPrinter.cs:8-27`）→ `Godot.GD.Print`，即走 Godot 自己的日志系统。
- 实测落盘位置：`C:\Users\<用户>\AppData\Roaming\SlayTheSpire2\logs\godot.log`（同目录还有按时间戳轮转的 `godot<ISO时间>.log`）。验证本 mod 是否被调用：在该日志里搜 `[sts2-replica]`；加载成功时还会出现 `Found mod manifest file ...` 与 `--- RUNNING MODDED! --- Loaded N mods`（`ModManager.cs:283`）。

## 8. 踩坑

1. **默认编译 glob 会把临时工程收进来**：`_tmp/verify/obj/**\*.cs` 一旦存在，主工程会报 `CS0579 特性重复`（`AssemblyInfo.cs` 与 `.NETCoreApp,Version=v9.0.AssemblyAttributes.cs` 撞车）。已在 csproj 里加 `<Compile Remove="_tmp/**" />` 与 `<Compile Remove="dist/**" />`。
2. **`Log` 的静态构造依赖 Godot 原生**：`Logger` 静态构造调 `Godot.OS.GetCmdlineArgs()` → `StringName` → 原生调用；在**纯 .NET 进程**里调用入口方法会直接 `Fatal error. 0xC0000005`。所以不要试图用单元测试/控制台程序去跑 `ReplicaMod.Init()`，要验证就启动游戏看 `godot.log`。（本仓库的反射自查只读元数据、不调 `Init`。）
3. `win_bash` 不传 `workdir` 时不会停在项目目录，`git`/`dotnet` 命令必须显式带工作目录。
4. `mods\` 下任何 `.json` 都会被当 manifest 尝试一遍：内容不像 manifest 时只打 `does not look like a mod manifest; skipping it`（`ModManager.cs:593-604`），不是错误。
5. `has_dll` 忘写就静默不加载 dll（默认 false，`ModManifest.cs:36-37`）；TestMode 下也不加载（`ModManager.cs:949`）。
6. 反编译源目录名带点（`MegaCrit.Sts2.Core.Models.RelicPools`），文件名检索时按命名空间目录走，别只按类名猜路径。

## 9. 未确认清单

1. **进池后的实际表现未验证**：按约束未启动游戏，所以「奖励/商店里真的出现这个遗物」只到契约层（类型进 `_moddedContentForPools`、池未冻结），没有运行时证据。
2. `SharedRelicPool.GetUnlockedRelics` 的 Epoch 过滤对 mod 追加项的取舍未逐行核对（`SharedRelicPool.cs` 里硬编码过滤逻辑）。
3. 缺失本地化键（`relics.placeholder_relic.title` / `.description` / `.flavor`）时 UI 的具体表现未验证：`RelicModel.cs:50-69` 只用 `Id.Entry` 拼 key，缺失时是显示 key 还是报错，本机没有运行证据。
4. `min_game_version` 的比较/提示细节未读（`ReleaseInfoManager` 的版本语义未展开）。
5. Godot 文件日志是否由 project setting 保证、是否可能在别的机器上关闭：只能确认本机 `logs\godot.log` 实测存在，C# 侧无法证明。
6. 图标若要真图：需 `atlases/relic_atlas.sprites/<entry>.tres` 一类资源（`RelicModel.cs:132-136`），是否需要随 mod 打 `.pck`、以及 `.tres` 图集的制作流程，未验证。
