# 好牌多抓 · Good Card Replica (STS2 mod)

《杀戮尖塔2》模组：新增遗物「好牌多抓」。用【制模】把不想要的卡牌奖励换成充能，
攒满后用【取样】从自己卡组里挑一张**同名新卡**作为本次卡牌奖励。

- 目标游戏版本：v0.113.1（Steam 默认分支，Godot 4）
- 运行时：游戏自带 mod 加载器（**不需要** BepInEx，**不需要**任何前置框架）
- 状态：`0.4.0-beta`。已实机验证：游戏能加载模组、初始化无异常、（装 RitsuLib 时）设置页注册成功、遗物小图标；
  **未实机验证**：卡牌奖励界面里的按钮、取样候选、0.3.0 的描边烘焙观感

## 玩法

持有遗物「好牌多抓」时，卡牌奖励界面的额外按钮区会出现**一个**按钮，行为随充能变化：

| 状态 | 按钮 | 效果 |
|---|---|---|
| 充能 < a | 【制模】 | 放弃本次卡牌奖励，遗物 +1 层充能（`a` 可设为 0，此时不再出现【制模】，每张奖励都直接是【取样】） |
| 充能 ≥ a | 【取样】 | 清空充能，从卡组按权重抽 b 张候选，选 1 张作为本次奖励获得 |

两种操作都会**结束本次卡牌奖励**（互斥），不会同时给出原版的 3 张随机牌。

### 「取样」到底给什么

不是「复制卡组里那张牌」，而是**让卡牌奖励里出现一张与卡组中某张牌同名的牌**，并且照常走奖励流程：

- 候选是**新实例**（由该牌的模板新建），因此起点没有升级、没有附魔；
- 但**奖励的升级判定照常生效**（含当前幕数的升级概率加成与遗物对升级概率的修正）；
- 其他遗物对卡牌奖励的改造效果（如各种蛋类遗物）也能正常作用于候选；
- 选中后走普通卡牌奖励拿卡的路径入牌堆，"获得卡牌"类遗物钩子正常触发。

### 抽取权重（默认值，可配置）

| 档位 | 白卡 | 蓝卡 | 金卡 | 无色 | 诅咒 |
|---|---|---|---|---|---|
| 权重 | 6 | 4 | 2 | 1 | 3 |

基础牌（打击/防御）算作白卡档；状态牌与任务牌默认**不进池**；同名卡默认只出现一次。

## 安装

1. 构建（产物由构建自动落到 `dist/sts2-replica/`，不需要单独打包脚本）：

   ```powershell
   dotnet build sts2-replica.csproj -c Release   # → dist/sts2-replica/{sts2-replica.dll, sts2-replica.json}
   ./install.ps1                                 # 复制进 <游戏根>\mods\sts2-replica\（会先确认）
   ```

2. 或手动把 `sts2-replica.dll` 与 `sts2-replica.json` 放进
   `<Steam>\steamapps\common\Slay the Spire 2\mods\sts2-replica\`。
3. 启动游戏，日志（`%AppData%\SlayTheSpire2\logs\godot.log`）里应出现 `[sts2-replica] initialized` 开头的行。
   跑 `./tools/check-log.ps1` 可以只筛出 `[sts2-replica]` 开头的行，加载失败时它会直接告诉你没有。

安装脚本默认目标是本机 Steam 路径，换机器请改 `-ModsDir`。

## 设置

配置文件（只有这一个；内容仍是 JSON；手改后需重启游戏）：

1. `<游戏可执行文件目录>\mods\config\sts2-replica.cfg`

0.3.0 起不再读 `sts2-replica.json`、也不再生它，原来的 `%AppData%\Slay the Spire 2\sts2-replica\config.json`
回落路径一并取消：升级时请**手动删掉旧文件**，否则里面的自定义值不再生效（日志会给一行 `legacy ... is ignored`）。

**参数在开局时冻结**：改设置只影响之后创建的对局，正在进行的那一局沿用开局时的值；联机局里房主局内改设置
同样不影响本局，只影响之后创建的对局（客户端用的是房主开局时下发的那份）。

遗物描述里的数字（`{Charges}` 充能几次可取样、`{Options}` 取样几选几）读的也是这份冻结值，
所以改完设置开下一局，悬停遗物就能看到新数字；同一局内描述不会变。

不写文件即全部使用默认值；坏值一律回落默认。全部键位、默认值与容错规则见
[`docs/config.md`](docs/config.md)：充能阈值 `charge_required`、候选数 `offer_count`、
五档权重 `weight_*`、开局自带 `start_with_relic`，以及基础牌/状态牌/任务牌/去重等过滤开关。

装了 [RitsuLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295) 时，RitsuLib 的设置界面里会多出
「好牌多抓」一页，8 个常用项（充能阈值、候选数、五档权重、开局自带）可以直接改；改完立即写回上面那份
`.cfg`，**不需要重启游戏**，但**改动从下一局开始生效**（页面说明里也写了这一句）。联机客户端里这一页是**只读**的：
显示房主的数值，改动不落盘。没装 RitsuLib 就只有配置文件，模组照常编译运行。

构建开关：默认按本机路径引用 RitsuLib（可用 `-p:RitsuLibDir=...` 覆盖、`-p:RitsuLibReferenceTarget=...` 指定
compat 版本）；`dotnet build sts2-replica.csproj -c Release -p:EnableRitsuLib=false` 产出完全不引用 RitsuLib、
不含设置页的版本，两种配置都必须 0 警告 0 错误。

## 投放

- 遗物进入**共享**遗物池，按普通遗物随机出现。
- `start_with_relic` 可在开局直接给每个玩家一个。

## 多人

- **配置以房主为准**：房主开局广播一次模组参数，客户端只在内存里采纳，**不改写**客户端本机的
  `sts2-replica.cfg`；客户端的设置页显示房主的数值且只读。两端都在开局时冻结这组参数，所以房主局内改设置
  不影响本局，只影响之后创建的对局。充能（`Charges`）是玩家个人的，不同步。
  两端各打一行配置指纹（`host: broadcast config …` / `adopting host config …`），可直接对账；机制见
  [`docs/release.md`](docs/release.md) 的「配置同步」。
- 所有玩家装**同一版本、同一份模组清单**（握手只比 `id-version`，且消息 id 按类型名排序、只占一字节）。
- 取样候选与升级判定都用从 run 状态派生的 `Rng`（`new Rng(player, relic.Id, (ulong)RunState.TotalFloor)`），各端产生同一批候选。
- 选择哪一张由 `CardSelectCmd.FromSimpleGridForRewards` 内部的选择同步负责。
- 拿卡只调 `CardPileCmd.Add`，与原版卡牌奖励完全同路径；**不**调用 `RewardSynchronizer.SyncLocalObtainedCard`
  （该消息的远端处理会自行加卡，重复发会多加一张；联机战斗中它本身还会抛异常）。
- 充能层数与原版 PaelsWing 同款做法：靠各端确定性执行同一段代码保持同步，无独立同步消息。

## 已知边界

- **未实机验证**：当前所有结论来自 v0.113.1 的反编译源码与编译期检查。
- 同时持有原版 Driftwood 或 PaelsWing 时按钮位被占满（`CardRewardAlternative.Generate` 在超过 2 个按钮时抛异常），
  本模组会**让位**：本次奖励不出现【制模】/【取样】，不会崩。
- 充能已满且卡组里没有任何可取样卡（例如把基础牌等全部过滤掉）时，按钮不出现。
- `offer_count` 钳制在 1..3（网格选牌 UI 在 b>3 时未验证）。
- 图标：小图标的描边已经烘进 PNG，代码把独立的描边层隐藏（不再叠加 `Outline.Texture`）；图鉴/查看屏的大图无描边。
- 客户端的配置自愈最多每层重试一次、每局封顶 5 次；用满后只留一条 WARN，本局沿用本机参数。

## 开发

```bash
cd /d/engine/DSH/projects/sts2-replica
dotnet build sts2-replica.csproj -c Release -v minimal
```

- `src/Core`、`src/Config` 为零引擎依赖的纯函数（可直接单测）；`src/Engine`、`src/Models` 为引擎适配。
- 逆向调查结论（API 事实与行号）在 `docs/`；进度与决策在 `PROGRESS.md`。

## 文档

| 文件 | 内容 |
|---|---|
| `docs/spec.md` | 规格（玩法/可配置项/硬约束/验收点） |
| `docs/config.md` | 配置键位与容错规则 |
| `docs/build-notes.md` | 加载器契约、构建环境、踩坑 |
| `docs/art-notes.md` | 遗物图标的三条接入路线与取舍 |
