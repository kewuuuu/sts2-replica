# 配置说明（ReplicaConfig）

配置文件路径（按顺序探测，命中即用；都不会被创建，缺失即全部使用默认值）：

1. `<游戏可执行文件目录>/mods/config/sts2-replica.json`
   （即 `steamapps/common/Slay the Spire 2/mods/config/sts2-replica.json`）
2. `user://sts2-replica/config.json`
   （即 `%AppData%/Slay the Spire 2/sts2-replica/config.json`）

加载时机：mod 初始化（`ReplicaMod.Init`）一次。改配置后需要重启游戏。

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
