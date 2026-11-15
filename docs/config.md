# 配置说明（ReplicaConfig）

配置文件路径（按顺序探测，命中即用；都不会被创建，缺失即全部使用默认值）：

1. `<游戏可执行文件目录>/mods/config/sts2-replica.json`
   （即 `steamapps/common/Slay the Spire 2/mods/config/sts2-replica.json`）
2. `user://sts2-replica/config.json`
   （即 `%AppData%/Slay the Spire 2/sts2-replica/config.json`）

加载时机：mod 初始化（`ReplicaMod.Init`）一次。手改文件后需要重启游戏；用下面「游戏内设置页」改则不需要重启。

## 键位与默认值

| 键 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `charge_required` | int | 3 | 充能满阈值 a，`< 1` 时回落默认 |
| `offer_count` | int | 2 | 取样候选数量 b，钳制到 1..3。`CardSelectCmd.FromSimpleGridForRewards` 本身没有张数上限（≤3 属于 `FromChooseACardScreen`，本项目未使用），钳制理由是网格选牌 UI 在 b>3 时未经实机验证，属保守值 |
| `weight_common` | int | 6 | 白卡权重；基础牌算白卡 |
| `weight_uncommon` | int | 4 | 蓝卡权重 |
| `weight_rare` | int | 2 | 金卡权重 |
| `weight_colorless` | int | 1 | 无色牌权重（按 `ColorlessCardPool` 识别） |
| `weight_curse` | int | 3 | 诅咒权重（`CurseCardPool` 或 `CardRarity.Curse`） |
| `start_with_relic` | bool | false | 开局给每个玩家一个该遗物 |
| `include_basic` | bool | true | 基础牌是否进池 |
| `include_status` | bool | false | 状态牌是否进池（权重用 `weight_common`） |
| `include_quest` | bool | false | 任务牌是否进池（权重用 `weight_common`） |
| `dedupe` | bool | true | 同名卡牌在候选池中只保留一张 |

## 容错规则

- 文件缺失、非 UTF-8、JSON 非法、根节点不是对象 → **整份默认值**。
- 单个键类型不对（例如字符串给了数字键）→ 该键用默认值，其余键照常生效。
- 负权重 → 0；`charge_required` 下限 1；`offer_count` 钳制到 1..3。
- 所有 `weight_*` 全为 0 → 退化为等概率抽取（不会出现"永远抽不到"）。
- 单个档位权重为 0 时，该档位不进入候选池。

## 解析器

`src/Config/ReplicaConfig.cs` 的 `ReplicaConfig.Parse(string?)` 是纯函数（只依赖
`System.Text.Json`），可以直接单测；文件定位在 `src/Config/ReplicaConfigStore.cs`。

## 游戏内设置页（RitsuLib，可选）

装了 [RitsuLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295) 时，RitsuLib 的设置界面里会多出
「好牌多抓」一页（注册键 `mod_id=sts2-replica`、`page_id=replica-settings`），可直接改这 8 个键：

| 设置页条目 | 键 | 设置页取值范围 |
| --- | --- | --- |
| 充能阈值 a | `charge_required` | 1..20，步进 1 |
| 取样候选数 b | `offer_count` | 1..3，步进 1 |
| 白卡权重 c | `weight_common` | 0..100，步进 1 |
| 蓝卡权重 d | `weight_uncommon` | 0..100，步进 1 |
| 金卡权重 e | `weight_rare` | 0..100，步进 1 |
| 无色牌权重 f | `weight_colorless` | 0..100，步进 1 |
| 诅咒权重 g | `weight_curse` | 0..100，步进 1 |
| 开局自带遗物 | `start_with_relic` | 开关 |

滑块上限只是界面取值区间，解析器规则不变（`charge_required` 下限 1、`offer_count` 钳制 1..3、
权重钳制 0..1000000）。

- 写回的文件：优先覆盖**当前正在用的那个候选路径**（见开头两条）；都不存在时写
  `<游戏可执行文件目录>/mods/config/sts2-replica.json`，目录自动创建。
- 写回内容是全部 12 个键，所以设置页没暴露的 4 个过滤开关（`include_basic` / `include_status` /
  `include_quest` / `dedupe`）按当前内存值原样保留，不会因为点了一次滑块被重置。
- 多久生效：取样、权重、阈值、候选数都在**下一次判定**就生效（`GoodCardReplica` 在判定当下读
  `ReplicaConfigStore.Current`）；`start_with_relic` 只在开局事件里读一次（`src/ReplicaMod.cs` 的
  `OnRunStarted`），所以**下一次开局**生效。两者都**不需要重启游戏**。
- 没有 RitsuLib 时这一页不会出现，模组照常编译、照常运行，只认 JSON 文件（构建开关见 README）。
- 写入失败（目录不可写等）只在日志里留一条 `[sts2-replica] could not write config at ...`，
  改动仍留在内存里，本次游戏内照样生效。
