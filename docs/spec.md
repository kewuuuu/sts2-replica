# 规格 v1（用户已定，勿擅改）

权威依据：D:\engine\DSH\Workspace\_cache\sts2-1131-src（本机 v0.113.1 反编译）。

## 1. 玩法

持有遗物「好牌多抓」时，卡牌奖励屏的 alternative 区出现**同一个按钮**，其文案与行为随充能状态切换：

| 状态 | 按钮 | 行为 | afterSelected |
|---|---|---|---|
| 充能 < a | 【制模】 | 放弃本次卡牌奖励，遗物充能 +1 | `EndSelectionAndCompleteReward` |
| 充能 ≥ a | 【取样】 | 从卡组按权重抽 b 张 → 选至多 p 张（p 见下）→ **按奖励路径获得同名新卡**；每张扣 a 层充能，余额保留 | `EndSelectionAndCompleteReward` |

- **取样语义（用户 m00271 澄清，勿回退成克隆）**：不是"复制卡组里那张牌"，而是**让卡牌奖励里出现一张与卡组中某张牌同名的牌**，并照常走奖励流程：
  - 候选卡 = 该牌的规范模板经 `RunState.CreateCard(template, player)` 新建的**新实例**（因此天然不继承原牌的升级与附魔）；
  - 升级照常按**卡牌奖励的升级判定**滚动（`CardFactory.RollForUpgrade`，受 `Hook.ModifyCardRewardUpgradeOdds` 影响），并允许其他遗物的改造效果作用其上；
  - 选中后走**普通卡牌奖励拿牌**的路径发放（`CardPileCmd.Add` + `RewardSynchronizer.SyncLocalObtainedCard`），确保"获得卡牌"类遗物钩子正常触发。
- 与卡牌奖励**互斥**：按钮触发即完成本次奖励，不再给出原版 3 张随机牌。
- 候选以 `CardCreationResult` 列表呈现，用 `CardSelectCmd.FromSimpleGridForRewards` 做成「选 0..p 张」（该命令内部自带
  多人选择同步；`min=0 != max=p` 强制手动确认，多选网格的原版先例是 `SeaGlass.cs:89`）。
  p = `min(sample_picks, 候选张数, floor(充能 / a))`，a = 0 时不做除法；取 n 张扣 a×n 层充能（a = 0 时充能保持 0），
  余额保留（0.4.x 及以前固定清 0）。玩家取消（选 0 张后确认或关掉界面）时充能**退还**；任一张入牌组失败同样退还。

## 2. 可配置项（默认值）

| key | 含义 | 默认 |
|---|---|---|
| `charge_required` a | 兑换所需充能（钳制 `>= 0`；0 = 每次卡牌奖励直接出现【取样】） | 3 |
| `offer_count` b | 取样给出的候选张数 | 2 |
| `sample_picks` p | 一次取样**最多**拿几张（真实张数 = min(p, 候选数, floor(充能/a))），钳制 1..3 | 1 |
| `weight_common` c | 白卡权重 | 6 |
| `weight_uncommon` d | 蓝卡权重 | 4 |
| `weight_rare` e | 金卡权重 | 2 |
| `weight_colorless` f | 无色卡权重 | 1 |
| `weight_curse` g | 诅咒权重 | 3 |
| `start_with_relic` | 开局直接获得该遗物 | 可配 |
| `include_basic` | 基础牌（打击/防御）算作白卡进池 | **true**（用户定） |
| `pool_filter` 系列 | 状态牌/任务牌/已升级牌等过滤开关 | 见下 |

- 抽取权重按**卡牌稀有度**分档：白/蓝/金/无色/诅咒；基础牌默认归入白卡档（用户 m00150 定：基础牌也算）。
- 过滤开关至少包含：基础牌是否进池、状态牌是否进池、任务牌是否进池、是否去重（同名只出现一次）。
- `offer_count` b 在 beta 钳制 1..3：`FromSimpleGridForRewards` 本身没有张数上限（`CardSelectCmd.cs:70-73` 的 ≤3 限制只属于 `FromChooseACardScreen`），钳制理由是网格选牌 UI 在 b>3 时未经实机验证，属保守值。

## 3. 投放

- 进**共享**遗物池（AllSharedRelicPools 路径）。
- 支持"开局直接给"（设置项）。

## 4. 硬约束

- 多人兼容必须考虑：所有随机必须走**多人一致的 Rng/上下文**（本实现用 `new Rng(player, relic.Id, (ulong)RunState.TotalFloor)`），不得用本地 `new Random()`；不新造**玩法**网络消息（v0.113.1 的 PaelsWing 亦无自建同步，照抄其写法即可）；唯一例外是模组参数同步的两条消息（见第 6 节），它们只承载设置，不承载任何卡牌/奖励/战斗动作。
- **拿卡路径禁止调用 `RewardSynchronizer.SyncLocalObtainedCard`**（v0.113.1 实证，本轮修正）：原版 `CardReward.OnSelect()` 拿卡只做 `CardPileCmd.Add(obtainedCard, PileType.Deck)`（CardReward.cs:279），而该同步消息的远端处理自己会 `RunState.AddCard` + `CardPileCmd.Add`（RewardSynchronizer.cs:210-211）⇒ 再发一次会让远端**重复加卡**；且它在联机战斗中会直接抛异常（RewardSynchronizer.cs:84-87）。原版唯一调用点是商人场景的 CrystalSphereCurse.cs:23。选择同步由 `CardSelectCmd.FromSimpleGridForRewards` 内部的 PlayerChoice 机制负责（:154 ReserveChoiceId / :181 SyncLocalChoice / :186 WaitForRemoteChoice）。
- alternative 按钮总数上限 2：注入前 `if (alternatives.Count >= 2) return;` 防御（Skip 已占 1 位；Driftwood 的 REROLL 会让它先到 2）；另需 `player.GetRelic<PaelsWing>() != null` 时让位，因为 `PaelsWing.cs:79` 无条件加第三项会让原版 `CardRewardAlternative.cs:74-77` 抛异常（我们自己排在它前面时）。
- **选项数量不得依赖本地配置**：充能不足或无可用模板时也注入 MOLD，永不 return false（除上面两条让位）。理由：奖励屏同步的只是索引（`CardReward.cs:256`），对端用本地配置重建列表，长度不一致会变成对端的 bad-index 错误。
- 不放独立奖励行、不新增 Reward 子类。
- 纯 DLL 模组：不要求玩家另装前置框架即可运行（设置界面方案见 docs/settings-ui.md 的结论）。

## 5. 验收点（beta）

1. 装进 `mods/` 后游戏能加载，无 Harmony 补丁冲突报错。
2. 持遗物时卡牌奖励屏出现【制模】；连点 a 次后同一位置变【取样】；取样出 b 张候选、可选至多 p 张、每张扣 a 层充能
   （余额保留）、取消或入牌组失败则充能不变；候选是新实例（起点无升级/附魔），但**奖励升级判定与遗物改造仍可作用于它**（这是与「克隆」的关键区别）。
3. 未持遗物时界面与行为与原版完全一致。
4. 同时持有 Driftwood / PaelsWing 时不抛 "More than 2 card reward alternatives"（本模组按钮让位、消失，属预期）。
5. 设置改成 a=1/b=3 等极端值不崩，权重全 0 时有安全兜底（退回等概率或取消按钮）。
6. 存读档后充能层数保持（`[SavedProperty]`）。
7. 双客户端联机：一方制模/取样，另一方界面与 run 状态一致；两端日志各有一行配置指纹（`h=` 相同），客户端设置页显示房主数值且写入不落盘。

## 6. 多人配置同步（v0.2 决策）

1. **房主权威**：生效配置 = 房主下发的覆盖 ?? 本机文件（`ReplicaRuntimeConfig.Effective`）；客户端只在内存里
   持有覆盖，不落盘。实现：`src/Config/ReplicaRuntimeConfig.cs`。
2. **客户端 JSON 不改写、设置页只读**：所有写入走 `TryWriteLocal`（联机客户端返回 false + 一条 WARN）；
   设置页读 `Effective`，因此显示房主数值。实现：`src/Config/ReplicaRuntimeConfig.cs`、
   `src/Integration/RitsuLibSettingsPage.cs`。
3. **房主配置越界 → 钳制 + WARN，不拒绝开局**：`ReplicaConfig.Normalized()` 只在网络入口使用，`Parse` 行为不变。
   实现：`src/Config/ReplicaConfig.cs`、`src/Net/ReplicaConfigSync.cs`。
4. **两端各一行配置指纹**：`Fingerprint()` 输出 `a=… b=… p=… w=…/…/…/…/… start=… basic=… status=… quest=… dedupe=… h=<FNV-1a 32>`；
   房主广播时、客户端采纳时、单人局跳过时各打一次。实现：`src/Config/ReplicaRuntimeConfig.cs`。
5. **充能不同步**：`Charges` 仍是个人 `[SavedProperty]`，同步的只有 13 个配置字段。
   实现：`src/Models/GoodCardReplica.cs`（v0.3 起改读 `ForThisRun` 的本局快照，充能逻辑不变）。

消息与接线：`src/Net/ReplicaConfigMessage.cs`（房主→客户端，13 字段，Reliable/Info）、
`src/Net/ReplicaConfigRequestMessage.cs`（客户端→房主，空载荷，Reliable/VeryDebug）、
`src/Net/ReplicaConfigSync.cs`（按 `INetGameService` 实例幂等注册；单人/回放直接跳过；房主收到请求再广播一次）。
时序假设：`RunStarted` 在读档时也触发，所以中途加入复用同一路径；客户端的 `start_with_relic` 发放被推迟到
采纳房主配置之后（`ReplicaMod.OnHostConfigAdopted`），否则两端遗物集合可能不同。

## 7. v0.3.0-beta 变更

1. **参数开局冻结**：`ReplicaRuntimeConfig` 增加本局快照（`BeginRun` / `EndRun` / `ForThisRun`）。
   单人局与房主在 `ReplicaMod.OnRunStarted` 里 `BeginRun(Effective)`；联机客户端在 `ReplicaConfigSync`
   采纳房主配置成功时（含"采纳成功后立刻生效"那条路径）`BeginRun(Effective)`。
   `ForThisRun` 有快照用快照、无快照回落 `Effective`，`Effective` 语义不变。读取点只改
   `GoodCardReplica` 的两处（按钮注入判定、`OnSample` 取样）；设置页与网络侧继续用 `Effective`。
   不新增局内广播，也没有改动任何既有 `INetMessage` 类型的名字或字段（消息 id 按类型名排序分配）。
2. **配置文件名**：`mods/config/sts2-replica.cfg`（内容仍是 JSON）。旧 `sts2-replica.json` 与
   `user://sts2-replica/config.json` 都不再读写，升级时手动删旧文件。
3. **小图标描边**：描边已烘进 PNG，`RelicNodeArtPatch` 的 Small 分支不再写 `Outline.Texture`，并把
   `Outline.Visible` 置 false（原版 `NRelic.Reload` 在 Small 分支先置 true，`NRelic.cs:146-147`）。
   Large 分支保持现状：图鉴大图无描边。
4. **配置自愈重试**：`ReplicaConfigSync.RequestConfigIfMissing` 改为「每层最多一次 + 每局累计封顶 5 次」；
   每局起点在 `AttachForRun` 里复位，跨层不清零总数，超限打一条 WARN 并沿用本机参数。
