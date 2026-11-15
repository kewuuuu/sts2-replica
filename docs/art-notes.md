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
