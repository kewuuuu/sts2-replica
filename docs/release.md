# 发布（最新 0.4.0-beta）

## 0.4.0-beta 变更（相对 0.3.1-beta）

1. **充能阈值最低可设为 0**：`charge_required` 的解析下限由 1 改为 0（`src/Config/ReplicaConfig.cs` 的 `Parse` 与 `Normalized`），
   设置页滑块区间同步改成 0..20（`src/Integration/RitsuLibSettingsPage.cs` 的 `ChargeRequiredMin`）。
   设成 0 时 `Charges >= 0` 恒成立，只要卡组里有可取样卡就直接注入【取样】，不再出现【制模】。
2. **0 档隐藏充能计数**：`GoodCardReplica.ShowCounter` 改为 `ForThisRun.ChargeRequired > 0`，免充能模式下不再显示一个永远为 0 的角标。
3. 遗物描述补一句「设为 0 时每次奖励都能直接取样」（`src/Localization/ReplicaLoc.cs`），工坊正文的键位表与英文段同步。

不变的部分：按钮注入仍是"恰好一个"，联机仍以房主配置为准并在开局冻结；`Charges` 仍是个人 `[SavedProperty]`。

## 0.3.0-beta 变更（相对 0.2.0-beta）

1. **参数开局冻结**：模组参数在开局那一刻固定成一份本局快照（`ReplicaRuntimeConfig.ForThisRun`）。
   局内改设置（设置页或手改文件）**不影响正在进行的那一局**，只影响之后创建的对局；联机局以房主开局时
   下发的那份为准，房主局内改设置同样不影响本局。单人局/房主在 `ReplicaMod.OnRunStarted` 冻结本机
   `Effective`，客户端在 `ReplicaConfigSync` 采纳房主配置成功时冻结。
2. **配置文件名**：`mods/config/sts2-replica.cfg`（内容仍是 JSON）。旧 `sts2-replica.json` 既不读也不写，
   升级时**手动删除**；原来的 `%AppData%\Slay the Spire 2\sts2-replica\config.json` 回落路径也取消。
3. **小图标描边**：描边已经烘进 PNG，代码不再显示独立的描边层（`RelicNodeArtPatch` 里 Small 分支把
   `Outline.Visible` 置 false 且不再写 `Outline.Texture`）；图鉴/查看屏的大图（Large）保持无描边。
4. **配置自愈重试**：`ReplicaConfigSync.RequestConfigIfMissing` 从「每层最多一次」收紧为「每层最多一次
   且每局累计封顶 5 次」，超限只打一条 WARN 并沿用本机参数。
5. **设置页文案**：页面说明补上「改动从下一局开始生效」；仍是拖动即写盘，不是只读。

## 包内容

`tools/pack-release.ps1` 会先构建，再把 `dist/sts2-replica/` 打成一个 zip：

```
dist/sts2-replica-0.3.0-beta.zip
└── sts2-replica/
    ├── sts2-replica.dll      # 模组本体（图标 PNG 已内嵌）
    └── sts2-replica.json     # manifest（id/version/min_game_version/affects_gameplay）
```

压缩包顶层就是 `sts2-replica/`，解压到 `<游戏根>\mods\` 即可，不需要再套一层目录。
包里**不含**任何前置框架的 dll：RitsuLib 只作为可选编译期引用，发布产物仍是纯 DLL 模组。

## 安装 / 卸载

1. 解压到 `<Steam>\steamapps\common\Slay the Spire 2\mods\`，得到 `mods\sts2-replica\sts2-replica.{dll,json}`。
2. 启动游戏，日志 `%AppData%\SlayTheSpire2\logs\godot.log` 应出现 `[sts2-replica] initialized` 行。
   用 `tools/check-log.ps1` 一条命令筛出来。
3. 卸载 = 删除 `mods\sts2-replica\` 目录；配置在 `mods\config\sts2-replica.cfg`，一并删掉即恢复默认。
   0.3.0 起不再读写旧名 `mods\config\sts2-replica.json`，升级时请手动删掉它（不删也不报错，只是不再生效，
   日志会给一行 `legacy ... is ignored`）。

## 验证状态（照实写）

已实机验证：

- **遗物图标**（0.1.0-beta → 0.2.0-beta 期间修复）：用户实机开局确认遗物格不再显示 `NOPE` 占位图。根因是
  `RelicModel.Icon` 等 getter 上的补丁会被同类补丁（RitsuLib 的框架资源包）覆盖，改为在 `NRelic.Reload`
  之后直接写 `TextureRect.Texture`（见下节「排障：遗物图标」）。
- manifest 被识别、dll 被加载、`[ModInitializer]` 被调用；
- 配置读取与默认值生效；`GoodCardReplica` 进入共享遗物池排队；
- 装 RitsuLib 0.6.13 时设置页注册成功（`RitsuLib settings page registered: mod_id=sts2-replica, page_id=replica-settings`）；
- 无异常/无 `[ERROR]` 指向本模组；JSON-only 构建同样正常初始化。
- `0.1.0-beta` / `0.2.0-beta` / `0.3.0-beta` 都能通过 `SemanticVersion` 解析：完整日志里**没有** `declares version ... which is not a valid
  Semantic Version` 警告。游戏内模组列表那一行显示的是 `(0.1.0)`（推测该列表只拼 Major.Minor.Patch；
  `SemanticVersion.ToString()` 本身会保留 `-beta`，见 `MegaCrit.Sts2.Core.Debug/SemanticVersion.cs:243-255`），属显示差异，不影响加载。
  0.3.0-beta 只做过程序集替换后的无头启动（模组加载、补丁 3/3、配置路径）验证，没有重跑那份模组列表截图。

**未实机验证**（首次上手请注意）：

- 卡牌奖励界面里【制模】/【取样】按钮的实际显示与点击流程；
- 取样网格的候选项、取消是否退还充能；
- 遗物图标的大图（`BigIcon`，图鉴/查看屏）观感——小图标已实机确认；0.3.0 把小图标的描边烘进 PNG
  并隐藏了独立的描边层，这一版**新增**的观感（含图鉴大图无描边）尚未实机复核；
- 双端联机实测（源码级评审已完成，见下节「多人联机」；双开验证未做）。配置同步同样只有源码级结论：
  消息是否真的到达对端、中途加入/读档是否真的重放，都要双开才能确认。
- 0.3.0 的「开局冻结」与「每层一次 / 每局 5 次」自愈重试同样只有源码级结论：单人局的开局冻结能靠无头启动
  看日志（`run settings frozen: ...`），客户端采纳后再冻结、以及跨层的重试计数，只有双开才能验。

> 排查日志提示：`godot.log` **每次启动都轮转**——旧内容会被改名成同目录的 `godot<时间戳>.log`，
> 新进程从头写 `godot.log`。所以「上次启动还在这儿、这次没了」是正常的，不是模组失灵。

## 已知问题

- 设置页是「拖动即写盘」：每次值变化都立刻落 `mods/config/sts2-replica.cfg`（0.3.0 起写盘不等于本局生效，
  见上面「0.3.0-beta 变更」第 1 条）。开发期出现过大一次无头启动后
  文件被写成 `charge_required: 2` / `start_with_relic: true`（默认 3 / false）的情况，带日志连续三次干净启动均未复现。
  写回调现在会记 `[sts2-replica] settings write <key>=<value>`，下次出现时按该行定位；文件本身只影响本机玩法，不写坏存档。
- 手改 JSON 时非法值一律回落到默认并在日志留痕（`ReplicaConfig.Parse`），不会抛异常中断启动。

## 排障：遗物图标

遗物小图标 / 描边 / 大图分别由 `RelicModel.Icon`、`IconOutline`、`BigIcon` 三个 getter 提供，
本模组用 Harmony postfix 把它们换成 dll 内嵌的三张 PNG（`src/Art/`）。看日志定位：

| 日志行 | 含义 |
| --- | --- |
| `patches applied; relic art hooks Icon=1 IconOutline=1 BigIcon=1` | 三个 getter 的补丁都挂上了（启动时就该有这一行） |
| `relic art loaded (GOOD_CARD_REPLICA/icon) 128x128` 等三行 | 贴图首次被读取，解码成功（每进程一次） |
| `relic art unavailable (<key>), keeping the vanilla icon` | 内嵌 PNG 缺失或解码失败，回落到原版占位图 |

一行都没有 ⇒ 游戏里装的是旧 dll（美术补丁加入之前）。把 `dist\sts2-replica.zip` 解压覆盖后**重启游戏**。

另有一类"看起来像缺图"的**正常**行为：图鉴里**未获得/未发现**的遗物，原版就会用
`packed/common_ui/locked_model.png` 占位（`NInspectRelicScreen.cs:386-390`、`NRelicCollectionEntry.cs:132-137`），
与补丁无关；进一局拿到该遗物后即恢复。

## 多人联机

**配置跟着房主走**（v0.2 起，v0.3 起冻结）：模组参数（`charge_required` / `offer_count` / 五档权重 /
`start_with_relic` / 三个过滤开关 / `dedupe`）在联机局里以**房主**为准，客户端只在内存里采纳房主的数值，
**不会改写客户端本机的 `sts2-replica.cfg`**。两端都在**开局那一刻**冻结这组参数（`ForThisRun`），
所以房主局内改设置不影响正在进行的那一局，只影响之后创建的对局。充能（`Charges`）仍是玩家个人的，不参与同步。
机制与日志见下面「配置同步」。

玩法路径仍与原版 PaelsWing 同构：不自造任何**玩法**网络消息，卡牌/奖励/战斗动作全靠「各端同构执行 + 索引同步」。
由此剩下三条硬要求：

1. **所有玩家装同一版本、同一份模组清单**：握手只比 `id-version` 字符串（`ModManager.cs:1133-1142` →
   `HandshakeManager.cs:119-125`），装/不装、版本不同都会被拒绝入局。另一层更硬的原因：消息类型 id 按
   **类型名排序**分配（`NetTypeCache.cs:22`），且 id 只占**一个字节**（`NetMessageBus.cs:46`）——模组清单不同会让
   同一条消息在两端落到不同 id。
2. **配置不再要求各端同一份 JSON**：客户端在 `RunStarted` 时向房主请求配置并采纳，设置页在客户端显示房主的
   数值且写入被拒绝。唯一的时序例外是 `start_with_relic`：客户端的发放被推迟到采纳房主配置之后
   （`RelicCmd.Obtain` 不发任何网络消息，`RelicCmd.cs:35-55`），否则两端遗物集合可能不同。
3. **同时持有 PaelsWing 或 Driftwood 时本模组按钮让位（消失）**：PaelsWing 无条件加第三个选项
   （`Models.Relics/PaelsWing.cs:79`），Driftwood 让 `CanReroll` 成真（`Driftwood.cs:22`），而原版
   `CardRewardAlternative.Generate` 在 3 项时抛 `InvalidOperationException`（`CardRewardAlternative.cs:74-77`）。
   这是原版既有 bug，我们只保证自己不当触发者。

已核实为安全的点：`Charges` 是 `[SavedProperty]`，进校验和与重连包（`NetFullCombatState.cs:186/486-491`、
`SerializableRelic.cs:9-33`），无需显式同步；取样 Rng 由 `RunState.Rng.Seed + 玩家槽位 + 遗物 Id + 楼层` 派生
（`Rng.cs:61-64`），不推进原版奖励链；`start_with_relic` 由房主在 `RunStarted` 发放、客户端在采纳房主配置后发放
（`RelicCmd.Obtain` 全程无网络消息，`RelicCmd.cs:35-55`；客户端推迟发放正是为了让两端遗物集合一致），
两处都在 `TotalFloor > 0`（读档）时跳过。
已知限制：同一玩家同一楼层取样两次，候选完全相同（派生 Rng 的 mixin 只含楼层）。

## 配置同步（v0.2）

两条消息（`src/Net/`）：

| 消息 | 方向 | 载荷 | Mode / LogLevel |
| --- | --- | --- | --- |
| `ReplicaConfigMessage` | 房主 → 客户端 | 12 个配置字段 | Reliable / Info |
| `ReplicaConfigRequestMessage` | 客户端 → 房主 | 无 | Reliable / VeryDebug |

- 方向语义直接来自 `INetGameService.SendMessage<T>(T)`：客户端发只到房主，房主发广播给所有客户端
  （`INetGameService.cs:62-68`）。所以不需要 `HostId`，"谁说了算"由类型决定。
- 时机是 `RunManager.RunStarted`（`RunManager.cs:316`）——**读档也会触发**（`RunManager.cs:788`、`NGame.cs:1182`），
  所以"中途加入/读档"复用同一条路径。房主广播一次；客户端注册处理器后发一次请求；房主每收到一次请求再广播
  一次，这就是中途加入者能对齐的原因。
- 两侧都可能先到：先发的那条若早于对端注册处理器，原版会丢包并记一条
  `... but no message handlers are registered for that type!`（`NetMessageBus.cs:88`）；另一方向随后仍会收敛，
  最终两端都拿到房主的配置。
- 模组消息由 `MessageTypes.Initialize()` 反射发现（`MessageTypes.cs:13-19`，在 `OneTimeInitialization.cs:84` 调用），
  id 按**类型名排序**分配（`NetTypeCache.cs:22`）。`ModManager.Initialize` 在前、`MessageTypes.Initialize` 在后，
  所以模组 `Init()` 里读 id 会抛 `InvalidOperationException`；本模组在第一次 `RunStarted` 时才在 try/catch 里读一次。
- 房主配置越界（手改 JSON、或别的模组代发）时**钳制并记 WARN**，不拒绝开局：客户端采纳前先跑
  `ReplicaConfig.Normalized()`。房主自己那份在 `ReplicaConfig.Parse` 里就已经被钳制过。

两端各打一行指纹，可以直接对账（`h` 相同即配置一致）：

```
[sts2-replica] message type ReplicaConfigMessage id=NN
[sts2-replica] message type ReplicaConfigRequestMessage id=NN
[sts2-replica] host: broadcast config a=3 b=2 w=6/4/2/1/3 start=0 basic=1 status=0 quest=0 dedupe=1 h=1a2b3c4d
[sts2-replica] adopting host config from player 7656119…: a=3 b=2 w=6/4/2/1/3 start=0 basic=1 status=0 quest=0 dedupe=1 h=1a2b3c4d
[sts2-replica] client: asked the host for its config
[sts2-replica] singleplayer: settings stay local; a=3 b=2 w=6/4/2/1/3 start=0 basic=1 status=0 quest=0 dedupe=1 h=1a2b3c4d
```

指纹格式固定：`a` = `charge_required`，`b` = `offer_count`，`w` = 白/蓝/金/无色/诅咒五档权重，
`start`/`basic`/`status`/`quest`/`dedupe` 是 0/1 开关，`h` 是这串正文的 FNV-1a 32 位（十六进制，
不用 `string.GetHashCode`：它按进程随机化，两台机器永远对不上）。

边界：

- **客户端的 `sts2-replica.cfg` 不会被改写**：所有写入都走 `ReplicaRuntimeConfig.TryWriteLocal`，联机客户端
  直接拒绝并记一条 `settings write refused`（每局一次）。
- **客户端设置页只读**：页面显示房主的数值，拖动/切换不落盘、不改显示值，只记一条
  `settings page is read-only this run`。
- **房主**：设置页照常写盘，但写盘 ≠ 本局生效：本局参数在 `RunStarted` 已冻结（日志 `run settings frozen: ...`），
  局内改的值只影响**之后创建的对局**；房主局内改设置既不改本局，也不推送给已经在局内的客户端
  （广播发生在下次 `RunStarted` 与客户端请求时）——要让一局用上新值，开局前改完即可。
- **自愈重试有上限**：客户端在卡牌奖励处发现还没拿到房主配置时会主动再请求，规则是**每层最多一次、
  每局累计最多 5 次**（每局起点在 `AttachForRun` 复位，跨层不清零总数）。用满 5 次后只打一条
  `giving up on the host config ... this run keeps the local values`，本局就一直用本机参数，不再发包。
- 充能不同步：`Charges` 仍是 `[SavedProperty]`，进校验和与重连包（`NetFullCombatState.cs:186/486-491`、
  `SerializableRelic.cs:9-33`）。

## 版本号与兼容

- 版本号是语义化版本，游戏用 `SemanticVersion.TryFromString` 解析（`MegaCrit.Sts2.Core.Debug/SemanticVersion.cs:41`），
  `0.3.0-beta` 这类 prerelease 合法；解析失败会记 `declares version ... which is not a valid Semantic Version`。
- `min_game_version` = `0.113.1`：当前 Steam 默认分支。游戏大版本更新后需要复核反编译 API 再抬版本。
- `affects_gameplay: true`（也是字段缺省值，见 `MegaCrit.Sts2.Core.Modding/ModManifest.cs:43`）：本模组改玩法，
  多人局必须人人安装同版本。
- `has_pck: false`：纯 DLL 模组，没有 `res://` 资源；本地化与图标都由 Harmony 补丁在运行时注入。

## 发工坊（需要你手动做）

游戏支持 Steam 创意工坊上传（v0.113.x 起），但上传动作要 Steam 客户端里操作，本仓库只负责产出可上传的目录：

1. 用 `tools/pack-release.ps1` 构建，或直接把 `dist\sts2-replica\` 作为上传内容根；
2. 工坊条目根目录应直接是 `sts2-replica.json` + `sts2-replica.dll`（与 `mods\sts2-replica\` 同构）；
3. 预览图、说明文字按工坊页面填，说明里请带上"多人需人人安装同版本 + 相同配置"。
