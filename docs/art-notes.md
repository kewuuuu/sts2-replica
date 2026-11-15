# 遗物美术：资源、路径与三条可选实现路线

生成物（已入库，可直接用）：
- `assets/icons/good_card_replica_icon.png` — 128×128，游戏内小图标
- `assets/icons/good_card_replica_icon_outline.png` — 128×128，白色剪影（用于描边/发光）
- `assets/icons/good_card_replica_big.png` — 512×512，遗物详情/拿取时的大图

视觉：两张叠放的卡 + 右下金色印记环（"复制"的语义），透明背景，无文字，64px 下仍可辨认。
生成脚本语义等价于 `_cache/gen_icons.cjs`（Node 手写 PNG 编码，无第三方依赖），换图时重跑即可。

## 游戏如何找图标（本机 v0.113.1 反编译实证）

`MegaCrit.Sts2.Core.Models\RelicModel.cs`：
- `:130` `protected virtual string IconBaseName => base.Id.Entry.ToLowerInvariant();`
- `:132` `public virtual string PackedIconPath => ImageHelper.GetImagePath("atlases/relic_atlas.sprites/" + IconBaseName + ".tres")`
- `:134` `protected virtual string PackedIconOutlinePath => ImageHelper.GetImagePath("atlases/relic_outline_atlas.sprites/" + ...)`
- `:136` `protected virtual string BigIconPath => ImageHelper.GetImagePath("relics/" + IconBaseName + ".png")`
- `:144` `public Texture2D Icon => ResourceLoader.Load<Texture2D>(PackedIconPath, null, CacheMode.Reuse);` —— **非 virtual**
- `:146` `public Texture2D IconOutline => ResourceLoader.Load<Texture2D>(PackedIconOutlinePath, ...);` —— **非 virtual**
- `:148` `public Texture2D BigIcon => PreloadManager.Cache.GetTexture2D(ResolvedBigIconPath);` —— **非 virtual**
- `:150-172` `ResolvedBigIconPath`：`res://images/relics/<id>.png` → 不存在再试 `relics/beta/<id>.png` → 都没有回落到 `powers/missing_power.png`
- `MegaCrit.Sts2.Core.Helpers\ImageHelper.cs:30-38`：`GetImagePath(inner)` 前缀固定为 `"res://images/"`

结论：**只有"路径"是 virtual，"取纹理"的 getter 不是**。纯 DLL 模组没有 `res://` 里的资源，
把 `PackedIconPath` 指到 `user://` 也没用 —— Godot 4 的 `ResourceLoader.Load` 要的是已导入资源，
运行时裸 PNG 必须走 `Image` + `ImageTexture`。

## 三条路线

| 路线 | 做法 | 代价 | 结论 |
|---|---|---|---|
| **A. Harmony 后置补丁（推荐 beta）** | 对 `RelicModel.Icon` / `IconOutline` / `BigIcon` 三个 getter 打 postfix：`__instance` 是我们的遗物时才替换 `__result`；纹理由 dll 内嵌 PNG 经 `Image.LoadPngFromBuffer` + `ImageTexture.CreateFromImage` 生成并缓存 | ~60 行；依赖 0Harmony（游戏自带）；对原版遗物零影响（按类型判断） | 纯 DLL、零外部工具，beta 直接可用 |
| B. 打 .pck | `has_pck: true`；`ModManager.cs:970-981` 用 `ProjectSettings.LoadResourcePack` 把包挂进 `res://`，再 override 三个路径属性指向包内 png | 需要 Godot 4 导入/导出流水线（本机尚未确认有无 Godot）；产物多一个文件 | 需要真资源图集时再上；不是 beta 的必需项 |
| C. 沿用原版占位 | 什么也不做，回落到 `powers/missing_power.png` | 0 | 只作为"美术资源缺失时"的最后兜底，不能作为交付形态 |

## beta 采取 A，附加要求

1. 纹理缓存必须是 **每遗物实例一份或全局一份**，不要在 getter 里反复解码 PNG（`Icon` 会被 UI 频繁读取）。
2. 补丁只在 `__instance is GoodCardReplica` 时生效，其余一律放行（`__result` 不变），保证与原版遗物零竞争。
3. 若 Harmony 补丁因游戏更新失效，必须能**退化为占位图而不是崩溃** —— 补丁方法体用 try/catch 包住，失败时记录一次日志后放行原逻辑。
4. 内嵌资源方式：把三张 PNG 以 `<EmbeddedResource>` 编进 dll（csproj `ItemGroup`），运行时 `GetManifestResourceStream` 读取，避免再依赖外部文件路径。

## 已接入（路线 A 落地，v0.113.1 构建通过）

改动只有四处：`sts2-replica.csproj` + 新增 `src/Art/ReplicaArt.cs`、`src/Art/RelicIconPatch.cs` + 本段文档。

1. **打包**：csproj 新增 `ItemGroup`，三张 PNG 逐个写明 `LogicalName="Sts2Replica.Art.<文件名>"`。默认嵌入名按 `RootNamespace` 推导（会变成 `Sts2Replica.assets.icons.*`），写死后补丁侧的资源名可预测；`ReplicaArt.ResolveResourceName` 仍保留后缀匹配兜底。
2. **解码 API 的真实形态（本机 GodotSharp 实测，与"首选"写法不一致）**：`Image.LoadPngFromBuffer(byte[])` 在本机绑定是**实例方法**，像素解到接收者身上，返回值是 `Godot.Error`。
   - 写成静态调用 → `CS0120: 对象引用对于非静态的字段、方法或属性"Image.LoadPngFromBuffer(byte[])"是必需的`
   - 把返回值当 `Image` 用 → `CS0029: 无法将类型"Godot.Error"隐式转换为"Godot.Image"`
   - 正确写法：`using Image image = new Image(); if (image.LoadPngFromBuffer(png) != Error.Ok ...) return null; return ImageTexture.CreateFromImage(image);`（`ImageTexture.CreateFromImage` 是静态的，编译期已验证）
3. **缓存**：`ConcurrentDictionary<string, Texture2D>`，键 `relic.Id.Entry + "/" + variant`（`icon` / `outline` / `big`）。**只缓存成功结果** —— 失败不落缓存，下一次 getter 会重试，避免瞬时失败把占位图钉死。
4. **补丁**：三个 `[HarmonyPatch(typeof(RelicModel), nameof(...), MethodType.Getter)]` 类，`Postfix(RelicModel __instance, ref Texture2D __result)`；`__instance is not GoodCardReplica` 立即 return，其它遗物零影响；提供器返回 null 时**不改** `__result`，原 getter 结果（含 `missing_power.png` 兜底）原样保留。
5. **线程**：解码只发生在 getter 调用内（主线程），没有后台预热、没有静态构造期创建 —— `ImageTexture` 不能离开主线程创建。
6. **失败上报**：同一失败键只 `Log.Error` 一次（`Reported` 去重），不刷屏、不抛异常。
7. `src/ReplicaMod.cs` 未改：`:41` 的 `PatchAll(Assembly.GetExecutingAssembly())` 本来就会扫到新增的三个 `[HarmonyPatch]` 类。

**未验证（需要真机启动游戏）**：`NRelic.cs:145-151`（小图/大图 + 描边）、`NRelicInventoryHolder.cs:249`、`RelicReward.cs:104`、`NInspectRelicScreen.cs:400-411` 里纹理的实际显示、缩放与描边配色，以及 Harmony 的 detour 是否被调用方内联绕过（getter 体内有 `ResourceLoader.Load` 调用，按常规不会被内联）。
