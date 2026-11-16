# 好牌多抓 · Good Card Replica (STS2 mod)

《杀戮尖塔2》模组：新增遗物「好牌多抓」。用【制模】把不想要的卡牌奖励换成充能，
攒满后用【取样】从自己卡组里挑一张**同名新卡**作为本次卡牌奖励。

- 目标游戏版本：v0.113.1（Steam 默认分支，Godot 4）
- 运行时：游戏自带 mod 加载器（**不需要** BepInEx，**不需要**任何前置框架）
- 状态：`0.1.0-beta`。已实机验证：游戏能加载模组、初始化无异常、（装 RitsuLib 时）设置页注册成功；
  **未实机验证**：卡牌奖励界面里的按钮、取样候选、遗物图标观感

## 玩法

持有遗物「好牌多抓」时，卡牌奖励界面的额外按钮区会出现**一个**按钮，行为随充能变化：

| 状态 | 按钮 | 效果 |
|---|---|---|
| 充能 < a | 【制模】 | 放弃本次卡牌奖励，遗物 +1 层充能 |
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

配置文件（手改后需重启游戏）：

1. `<游戏可执行文件目录>\mods\config\sts2-replica.json`
2. `%AppData%\Slay the Spire 2\sts2-replica\config.json`

不写文件即全部使用默认值；坏值一律回落默认。全部键位、默认值与容错规则见
[`docs/config.md`](docs/config.md)：充能阈值 `charge_required`、候选数 `offer_count`、
五档权重 `weight_*`、开局自带 `start_with_relic`，以及基础牌/状态牌/任务牌/去重等过滤开关。

装了 [RitsuLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295) 时，RitsuLib 的设置界面里会多出
「好牌多抓」一页，8 个常用项（充能阈值、候选数、五档权重、开局自带）可以直接改；改完立即写回上面那份 JSON，
**不需要重启游戏**（只有 `start_with_relic` 要等下一次开局）。没装 RitsuLib 就只有 JSON 文件，模组照常编译运行。

构建开关：默认按本机路径引用 RitsuLib（可用 `-p:RitsuLibDir=...` 覆盖、`-p:RitsuLibReferenceTarget=...` 指定
compat 版本）；`dotnet build sts2-replica.csproj -c Release -p:EnableRitsuLib=false` 产出完全不引用 RitsuLib、
不含设置页的版本，两种配置都必须 0 警告 0 错误。

## 投放

- 遗物进入**共享**遗物池，按普通遗物随机出现。
- `start_with_relic` 可在开局直接给每个玩家一个。

## 多人

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
