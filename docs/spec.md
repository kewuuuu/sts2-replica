# 规格 v1（用户已定，勿擅改）

权威依据：D:\engine\DSH\Workspace\_cache\sts2-1131-src（本机 v0.113.1 反编译）。

## 1. 玩法

持有遗物「好牌多抓」时，卡牌奖励屏的 alternative 区出现**同一个按钮**，其文案与行为随充能状态切换：

| 状态 | 按钮 | 行为 | afterSelected |
|---|---|---|---|
| 充能 < a | 【制模】 | 放弃本次卡牌奖励，遗物充能 +1 | `EndSelectionAndCompleteReward` |
| 充能 ≥ a | 【取样】 | 清空充能 → 从卡组按权重抽 b 张 → 选 1 张 → **按奖励路径获得一张同名新卡** | `EndSelectionAndCompleteReward` |

- **取样语义（用户 m00271 澄清，勿回退成克隆）**：不是"复制卡组里那张牌"，而是**让卡牌奖励里出现一张与卡组中某张牌同名的牌**，并照常走奖励流程：
  - 候选卡 = 该牌的规范模板经 `RunState.CreateCard(template, player)` 新建的**新实例**（因此天然不继承原牌的升级与附魔）；
  - 升级照常按**卡牌奖励的升级判定**滚动（`CardFactory.RollForUpgrade`，受 `Hook.ModifyCardRewardUpgradeOdds` 影响），并允许其他遗物的改造效果作用其上；
  - 选中后走**普通卡牌奖励拿牌**的路径发放（`CardPileCmd.Add` + `RewardSynchronizer.SyncLocalObtainedCard`），确保"获得卡牌"类遗物钩子正常触发。
- 与卡牌奖励**互斥**：按钮触发即完成本次奖励，不再给出原版 3 张随机牌。
- 候选以 `CardCreationResult` 列表呈现，用 `CardSelectCmd.FromSimpleGridForRewards` 选 1（该命令内部自带多人选择同步）；玩家取消时充能**退还**。

## 2. 可配置项（默认值）

| key | 含义 | 默认 |
|---|---|---|
| `charge_required` a | 兑换所需充能 | 3 |
| `offer_count` b | 取样给出的候选张数 | 2 |
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

## 3. 投放

- 进**共享**遗物池（AllSharedRelicPools 路径）。
- 支持"开局直接给"（设置项）。

## 4. 硬约束

- 多人兼容必须考虑：所有随机必须走**多人一致的 Rng/上下文**，不得用本地 `new Random()`；任何影响 run 状态的改动必须走官方已有命令通道（`CardSelectCmd` 的 PlayerChoice 同步、`RewardSynchronizer.SyncLocal*`），**不新造网络消息**（v0.113.1 的 PaelsWing 亦无自建同步，可直接照抄其写法）。
- alternative 按钮总数上限 2：注入前 `if (alternatives.Count >= 2) return;` 防御（Skip 已占 1 位；Driftwood/PaelsWing 等会再占位）。
- 不放独立奖励行、不新增 Reward 子类。
- 纯 DLL 模组：不要求玩家另装前置框架即可运行（设置界面方案见 docs/settings-ui.md 的结论）。

## 5. 验收点（beta）

1. 装进 `mods/` 后游戏能加载，无 Harmony 补丁冲突报错。
2. 持遗物时卡牌奖励屏出现【制模】；连点 a 次后同一位置变【取样】；取样出 b 张候选、只能选 1、副本无升级/附魔、充能清零。
3. 未持遗物时界面与行为与原版完全一致。
4. 同时持有 Driftwood / PaelsWing 时不抛 "More than 2 card reward alternatives"。
5. 设置改成 a=1/b=3 等极端值不崩，权重全 0 时有安全兜底（退回等概率或取消按钮）。
6. 存读档后充能层数保持（`[SavedProperty]`）。
7. 双客户端联机：一方制模/取样，另一方界面与 run 状态一致。
