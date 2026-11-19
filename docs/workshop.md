# 创意工坊发布

本条目发布到 Steam 创意工坊，appid **2868840**（Slay the Spire 2）。工具链在 `tools/`，文案在 `workshop/`。

## 0. 已发布条目

| 项 | 值 |
|---|---|
| item id | **3840979834** |
| URL | https://steamcommunity.com/sharedfiles/filedetails/?id=3840979834 |
| 当前版本 | 0.3.1-beta |
| 可见性 | public |
| 首次发布 | 2026-11-17，内容 4 files / 76365 B（`--create`） |
| 最近一次更新 | 2026-11-19，内容 4 files / 76877 B（`--update 3840979834`，实测通过） |

条目 id 同时写在 `dist/workshop-item-id.txt`（`dist/` 不入库，所以以本表为准）。

## 1. 文案与资源（改这些文件即可）

| 文件 | 用途 | 限制 |
|---|---|---|
| `workshop/title.txt` | 物品标题 | 非空、≤ 128 字符 |
| `workshop/description.bbcode` | 物品描述（Steam 支持 BBCode） | 非空、≤ 8000 字符（当前 1422） |
| `workshop/changelog.txt` | 提交时填的更新说明 | 建议一行 |
| `assets/icons/good_card_replica_big.png` | 预览图（正方形 512×512） | ≤ 1 MiB |

建议另外补 3~5 张截图（Steam 会按上传顺序展示）：

1. 卡牌奖励界面上的【制模】按钮（遗物在左上角遗物栏，充能计数在图标下方）。
2. 攒满充能后同一位置的【取样】按钮。
3. 取样给出的 2 张候选卡牌选择界面。
4. 设置页（RitsuLib 提供的「好牌多抓」设置分页）。
5. 两张遗物图标对比：带描边的小图标 vs 图鉴大图（说明这是原版分层方式）。

## 2. 一键流程

```powershell
# 1) 构建并把内容摆进 dist/workshop/
powershell -NoProfile -ExecutionPolicy Bypass -File tools/pack-workshop.ps1

# 2) 只校验、不上传（默认行为，永远先跑这一步）
tools/WorkshopUploader/bin/Release/net9.0/WorkshopUploader.exe --dry-run

# 3) 首次创建工坊条目（会写 dist/workshop-item-id.txt）
tools/WorkshopUploader/bin/Release/net9.0/WorkshopUploader.exe --create

# 4) 之后每次更新（<id> 取自 item-id.txt，或工坊页面 URL）
tools/WorkshopUploader/bin/Release/net9.0/WorkshopUploader.exe --update <id>
```

`--update` 的实测输出（与 `--create` 相比没有 `needsLegalAgreement` 那一行，也不写 item-id）：

```
[workshop] mode update
[workshop] StartItemUpdate handle=<ulong> id=3840979834
[workshop] SubmitItemUpdate call=<ulong> changelog=<改动的第一行>
[workshop] upload 0% (preparing config) / 100% (uploading content) / 0% (uploading preview)
[workshop] EResult k_EResultOK
[workshop] published 3840979834 as public
```

进度百分比会来回跳（content 到 100% 后 preview 又从 0% 起），属正常；只要最后一行是 `published … as public` 就是成功。

工具做得很少但很直白：`SteamAPI.Init()` 后走 `SteamUGC.CreateItem` / `StartItemUpdate` / `SubmitItemUpdate`，全程打印百分比。它要求 Steam 客户端已登录、且该账号拥有游戏；工作目录里的 `steam_appid.txt`（内容 2868840）与 `steam_api64.dll` 由构建自动放好。

## 3. 内容布局（为什么内容根目录下面还有一层）

内容目录 `dist/workshop/` 的结构是：

```
dist/workshop/
  sts2-replica/
    sts2-replica.dll
    sts2-replica.json
  good_card_replica_icon.png     # 图标副本，无 manifest，加载时被忽略
  good_card_replica_big.png      # 大图副本，同上
```

依据：加载器对工坊条目的安装目录做**递归**扫描（`ModManager.cs:677 ReadModsInDirRecursive(pchFolder, ModSource.SteamWorkshop, list)`），所以多一层目录与本地 `mods/sts2-replica/` 完全等价；dll 文件名必须与 manifest 的 `id` 一致（`ModManager.cs:948`）。根目录那两张 png 是 `pack-workshop.ps1` 顺手复制的预览素材，递归扫描只认 manifest，多出两个文件不影响加载，只是内容总量多 10 KB；若想内容只含模组本体，删掉脚本里复制 png 的那个 foreach 即可。

## 4. 分支支持范围

工坊条目的「支持的版本范围」由 Steamworks 的 `SetRequiredGameVersions` 决定，游戏在加载时会据此做检查（`ModManager.cs:711-725`）。**默认不设置 = 不做版本限制**（当前工具默认不调用它），需要收紧时：

```powershell
WorkshopUploader.exe --update <id> --branch-min default --branch-max default
```

## 5. 发布后自测

1. Steam 里订阅，等下载完成；`steamapps/workshop/content/2868840/<id>/sts2-replica/` 应有 dll 与 json。
2. **删掉/改名本地的 `mods/sts2-replica/`**：同 id 的本地版会让工坊版被禁用（`ModManager.cs:502-538` 的比较逻辑，本地版本更高时停用 Steam 版）。
3. 启动游戏，`%AppData%\SlayTheSpire2\logs\godot.log` 里应有 `Looking for mods to load from Steam Workshop mod <id>` 与 `[sts2-replica] initialized`。
4. 进一局：遗物小图标带描边、奖励界面出现【制模】、攒满变【取样】。

## 6. 发布前检查表

- [ ] `mod/sts2-replica.json` 的 `version` 与 `workshop/changelog.txt` 对得上。
- [ ] `affects_gameplay` 仍是 `true`（联机握手只比 id-version，见 `ModManager.cs:1133-1142`）。
- [ ] `dependencies` 仍是 `[]`：RitsuLib 只是可选的设置界面增强，不是硬依赖。
- [ ] 内容里没有 `*.pdb`、`*.zip`、`obj/`、`bin/`（工具的硬校验会拦）。
- [ ] 先用 `--dry-run` 看过一遍字段，再提交。

## 7. 已知限制（与 `docs/release.md` 同源）

- beta：双端联机的「中途加入」与「读档重放」尚未实机覆盖。
- 同一楼层重复取样会得到同一组候选（种子含 `RunState.TotalFloor`）。
- 图鉴大图没有描边，小图标有：原版只在小图标上挂描边层（`NRelic.cs:142-155`）。

## 8. 上传工具踩过的坑（别改回去）

游戏自带的 `Steamworks.NET.dll` 是 **manual dispatch** 版：

- `CallbackDispatcher.cs:34` → `SteamAPI_ManualDispatch_Init()`；`:147-208` → `ManualDispatch_RunFrame(pipe)` + `while(ManualDispatch_GetNextCallback(pipe, msg))`。
- 异步结果在 `:161-179` 处理：`msg.m_iCallback == 703` → `SteamAPICallCompleted_t` → `ManualDispatch_GetAPICallResult(pipe, m_hAsyncCall, …)` → `m_registeredCallResults[handle]` → `OnRunCallResult`。

因此**唯一**正确的取结果方式是 `CallResult<T>.Create(delegate(T param, bool bIOFailure))` + `Set(call)` + `SteamAPI.RunCallbacks()` 主循环（`CallResult.cs:8/:18/:43/:82`）。两条看似自然的替代路径都是死路，别再用：

- `SteamUtils.IsAPICallCompleted/GetAPICallResult`：对任何 handle（含伪造的 `0xFFFF…FF`）都返回 `completed=true, flaggedFailed=true, k_ESteamAPICallFailureInvalidHandle`，看起来像"通道坏了"，其实这个 API 在 manual dispatch 流程里根本没被调用。
- `Callback<CreateItemResult_t>`：`CreateItemResult_t` / `SubmitItemUpdateResult_t` 只作为 call result 存在，永远不会有广播回调。

旁证：把游戏 headless 跑起来后再跑 `--probe`，结果完全一样 ⇒ 失败与"是否由 Steam 启动"无关。反编译留档在 `D:/engine/DSH/Workspace/_cache/swdecomp/`（`SteamAPI.cs`、`CallbackDispatcher.cs`、`CallResult.cs`）。
