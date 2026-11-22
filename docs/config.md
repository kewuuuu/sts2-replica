# 配置说明（ReplicaConfig）

配置文件路径（0.3.0 起只有这一个，读与写都认它；文件不会被自动创建，缺失即全部使用默认值）：

1. `<游戏可执行文件目录>/mods/config/sts2-replica.cfg`
   （即 `steamapps/common/Slay the Spire 2/mods/config/sts2-replica.cfg`）

内容仍是 JSON，只是扩展名从 `.json` 换成了 `.cfg`。旧名 `sts2-replica.json` **既不读也不写**：
0.3.0 不迁移它、也不再生成它，升级时请**手动删掉**，否则里面的自定义值不再生效（日志会给一行
`legacy ... is ignored` 提示）。原来的 `user://sts2-replica/config.json` 回落路径已一并取消。

加载时机：mod 初始化（`ReplicaMod.Init`）一次。手改文件后需要重启游戏；用下面「游戏内设置页」改则不需要重启，
但同样**从下一局开始生效**（见下「多久生效」）。

## 键位与默认值

| 键 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `charge_required` | int | 3 | 充能满阈值 a，钳制到 `>= 0`（负值按 0）。**0 = 每次卡牌奖励都能直接取样**，此时不再出现【制模】，充能计数也不再显示 |
| `offer_count` | int | 2 | 取样候选数量 b，钳制到 1..3。`CardSelectCmd.FromSimpleGridForRewards` 本身没有张数上限（≤3 属于 `FromChooseACardScreen`，本项目未使用），钳制理由是网格选牌 UI 在 b>3 时未经实机验证，属保守值 |
| `sample_picks` | int | 1 | 一次取样**最多**拿几张，钳制到 1..3。实际张数还受候选数与充能限制（见下「一次取样拿几张」）；每多拿一张多花 `charge_required` 层充能 |
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

## 示例（全部 13 个键）

```json
{
  "charge_required": 3,
  "offer_count": 2,
  "sample_picks": 1,
  "weight_common": 6,
  "weight_uncommon": 4,
  "weight_rare": 2,
  "weight_colorless": 1,
  "weight_curse": 3,
  "start_with_relic": false,
  "include_basic": true,
  "include_status": false,
  "include_quest": false,
  "dedupe": true
}
```

## 一次取样拿几张（0.5.0 起）

`sample_picks` 只是上限，不是保证，实际张数按下面这条式子算（充能取**取样前**的层数）：

```
实际张数 = min(sample_picks, 本次候选张数, floor(充能 / charge_required))
```

- `charge_required <= 0`（免充能模式）不做除法，实际张数 = `min(sample_picks, 候选张数)`，充能保持 0。
- 取完后扣 `charge_required × 实际张数`：拿 n 张就花 n 份充能。**余额保留**——5 层、`charge_required=3`、拿 1 张后
  剩 2 层（下次得再制模一次才够）。0.4.x 及以前固定清 0。
- 取消选择（选 0 张后确认，或直接关掉界面）→ 充能退回取样前的值。
- 任何一张入牌组失败（`success=false`）→ 充能同样退回取样前的值（沿用 0.4.x 的防御）。
- 候选张数由 `offer_count` 决定且 ≤3，所以一次最多也就 3 张。

## 容错规则

- 文件缺失、非 UTF-8、JSON 非法、根节点不是对象 → **整份默认值**。
- 单个键类型不对（例如字符串给了数字键）→ 该键用默认值，其余键照常生效。
- 负权重 → 0；`charge_required` 钳制到 `>= 0`（0 表示每次奖励都能直接取样）；`offer_count` 与 `sample_picks` 都钳制到 1..3。
- 所有 `weight_*` 全为 0 → 退化为等概率抽取（不会出现"永远抽不到"）。
- 单个档位权重为 0 时，该档位不进入候选池。

## 解析器

`src/Config/ReplicaConfig.cs` 的 `ReplicaConfig.Parse(string?)` 是纯函数（只依赖
`System.Text.Json`），可以直接单测；文件定位在 `src/Config/ReplicaConfigStore.cs`。

## 游戏内设置页（RitsuLib，可选）

装了 [RitsuLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295) 时，RitsuLib 的设置界面里会多出
「好牌多抓」一页（注册键 `mod_id=sts2-replica`、`page_id=replica-settings`），可直接改这 9 个键：

| 设置页条目 | 键 | 设置页取值范围 |
| --- | --- | --- |
| 充能阈值 a | `charge_required` | 0..20，步进 1（0 = 每次奖励都能直接取样） |
| 取样候选数 b | `offer_count` | 1..3，步进 1 |
| 一次取样张数 | `sample_picks` | 1..3，步进 1 |
| 白卡权重 c | `weight_common` | 0..100，步进 1 |
| 蓝卡权重 d | `weight_uncommon` | 0..100，步进 1 |
| 金卡权重 e | `weight_rare` | 0..100，步进 1 |
| 无色牌权重 f | `weight_colorless` | 0..100，步进 1 |
| 诅咒权重 g | `weight_curse` | 0..100，步进 1 |
| 开局自带遗物 | `start_with_relic` | 开关 |

滑块上限只是界面取值区间，解析器规则不变（`charge_required` 下限 0、`offer_count` 钳制 1..3、
权重钳制 0..1000000）。

- 写回的文件：只有 `<游戏可执行文件目录>/mods/config/sts2-replica.cfg` 一个（见开头），目录自动创建。
- 写回内容是全部 13 个键，所以设置页没暴露的 4 个过滤开关（`include_basic` / `include_status` /
  `include_quest` / `dedupe`）按当前内存值原样保留，不会因为点了一次滑块被重置。
- 多久生效：**从下一局开始生效**。参数在开局时冻结成一份本局快照，玩法只读这份快照
  （`ReplicaRuntimeConfig.ForThisRun`：有快照用快照，没有才回落 `Effective`）；`GoodCardReplica` 的
  两处判定（按钮注入、`OnSample` 取样）读的就是它，所以局内再改设置不改本局玩法，只影响之后创建的对局。
  快照来源：单人局与房主=本机 `Effective`（`src/ReplicaMod.cs` 的 `OnRunStarted` 里冻结）；
  联机客户端=房主广播来的那份（`src/Net/ReplicaConfigSync.cs` 采纳成功时冻结）。
  `start_with_relic` 同样只在开局事件里读一次。都**不需要重启游戏**，但也不会改变正在进行的那一局。
- 没有 RitsuLib 时这一页不会出现，模组照常编译、照常运行，只认 JSON 文件（构建开关见 README）。
- 写入失败（目录不可写等）只在日志里留一条 `[sts2-replica] could not write config at ...`，
  改动仍留在内存里，下一次开局生效。

## 0.3.0 变更

- **参数开局冻结**：配置文件和设置页写的是「下一局用」的值；本局参数在开局那一刻固定，局内改设置只影响之后
  创建的对局（联机局同理：房主局内改设置不影响正在进行的这一局，只影响之后创建的对局）。
- **文件名**：`sts2-replica.cfg`（内容仍是 JSON）；旧 `sts2-replica.json` 不读不写，需手动删除。
- **图标描边**：小图标的描边已经烘进 PNG，代码不再显示独立的描边层（`src/Art/RelicNodeArtPatch.cs`）；
  图鉴/查看屏的大图无描边。与本节任何键值无关。
