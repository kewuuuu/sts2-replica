# PROGRESS

## 2026-11-15 立项

- 项目建立，目标：STS2 v0.113.1 的「好牌多抓」遗物模组 beta。
- 规格来源：用户设计（遗物驱动、制模/取样同一按钮、充值阈值 a=3、取样出 b=2 张、权重 c-g、可配置、可开局自带、进共享遗物池）。
- 待办：可编译骨架 → 遗物实现 → 设置 → 美术 → 多人验证 → 打包。
## 2026-11-15 骨架完成

- 可编译骨架：`net9.0` class library，`<Reference>` 指向游戏 `sts2.dll` / `0Harmony.dll` / `GodotSharp.dll` 且 `Private=false`（不随产物复制）。
- 入口 `Sts2Replica.ReplicaMod.Init`（`[ModInitializer("Init")]`，static/无参/void），把 `PlaceholderRelic` 排进 `SharedRelicPool`。
- 产物 `dist/sts2-replica/{sts2-replica.dll, sts2-replica.json}`，整目录丢进 `<游戏根>\mods`；`install.ps1` 为手动脚本，不自动执行。
- 契约依据（`文件:行`）、实测 TFM/日志路径、踩坑与未确认清单见 `docs/build-notes.md`。
## 2026-11-15 核心切片 + 收口

- `GoodCardReplica` 垂直切片（提交 `5156e63`）：`[SavedProperty] int Charges`、`ShowCounter/DisplayAmount`、
  按充能状态二选一注入 `MOLD`/`SAMPLE`（`EndSelectionAndCompleteReward`），前置 `base.Owner != player` 与
  `alternatives.Count >= 2` 双防御；取样走「模板 → `RunState.CreateCard` → 复刻 `CardFactory.RollForUpgrade`
  （private，CardFactory.cs:293-311）→ `CardCreationResult` → 奖励改造钩子 → `FromSimpleGridForRewards` 选 1
  → `CardPileCmd.Add`」，取消退还充能。
- 第 2 轮收口（提交 `cbdf930`）：入牌堆 `success=false` 时退还充能并 `Log.Error`；修正 `offer_count` 钳制 1..3 的理由
  （与 `FromChooseACardScreen` 的 ≤3 限制无关）。
- 规格修正（提交 `4392f1e`）：拿卡路径禁止 `RewardSynchronizer.SyncLocalObtainedCard`（远端会自行加卡导致重复；
  该方法在联机战斗中还会抛异常，RewardSynchronizer.cs:84-87）。
- README 重写（玩法/安装/设置/多人/已知边界）、manifest 升到 0.1.0、新增 `pack.ps1`。
- 待办：图标接入 → 实机冒烟 → 打包发布。

## 2026-11-15 游戏内设置页（RitsuLib）

- 新增 `src/Integration/RitsuLibSettingsPage.cs`：`ModSettingsRegistry.Register(ModSettingsPage)` +
  `ModSettingsPageBuilder` / `ModSettingsSectionBuilder` / `ModSettingsBindings.Callback<T>`（`SaveScope.Global`）
  注册一页设置；8 个键与 `ReplicaConfig` 同名同默认值，绑定读写 `ReplicaConfigStore.Current`，
  写入回调直接落盘、框架的 Save 回调按脏标记兜底。
- `src/Config/ReplicaConfigStore.cs` 新增 `Save(ReplicaConfig)`：写回当前在用的候选路径，并用
  `ReplicaConfig.Parse` 重算 `Current`；写出的 12 个键与解析器读取的键集完全相同（实测 diff 为空）。
- `sts2-replica.csproj` 新增 `EnableRitsuLib` 开关（默认 true）：条件 Import `RitsuLib.References.props`
  （五个 `Private=False` 引用）+ `RITSULIB` 编译常量；关掉或本机找不到 RitsuLib 时
  `Compile Remove="src/Integration/**"`。两种配置均 0 警告 0 错误，`dist/sts2-replica/` 仍只有 dll + json。
- `src/ReplicaMod.cs` 在 `Load()` 之后加一行 `#if RITSULIB` 的 `Integration.RitsuLibSettingsPage.TryRegister()`：
  先探测 `STS2-RitsuLib.Settings` 程序集是否加载，注册异常只留一条
  `[sts2-replica] RitsuLib settings page not registered: ...` 警告。
- 无头启动实测：注册成功（日志 `[sts2-replica] RitsuLib settings page registered: mod_id=sts2-replica, page_id=replica-settings`）；
  手工放的 12 键 JSON 被读回（`charge_required=5, offer_count=3, start_with_relic=True`）；关掉开关的版本照常初始化、
  无设置页相关日志、无异常，退出码 0。
- 未验证：设置页在真实 UI 里的观感、滑块步进手感、拖动时写入的触发频率（无头模式无法交互）。

## 2026-11-16 release 打包（0.1.0-beta）

- manifest 版本抬到 `0.1.0-beta`：游戏用 `SemanticVersion.TryFromString` 解析（`MegaCrit.Sts2.Core.Debug/SemanticVersion.cs:41`，支持 prerelease），解析失败会记 `declares version ... which is not a valid Semantic Version`。
- 新增 `tools/pack-release.ps1`：build → 读 manifest version → 打成 `dist/sts2-replica-<version>.zip`，包内顶层是 `sts2-replica/`（解压即进 `mods/`）。
- 新增 `docs/release.md`：包结构、安装/卸载、验证状态（已验/未验照实列）、版本号与 `affects_gameplay` 依据、工坊上传步骤（需人工在 Steam 客户端做）。
- 踩坑：Windows PowerShell 5.1 的 `Get-Content -Raw` 对**无 BOM 的 UTF-8** 文件按 ANSI 代码页解码，中文描述末字节吃掉 JSON 引号 ⇒ `ConvertFrom-Json` 抛 `ArgumentException`。改用 `[System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)`。manifest 本身不加 BOM（游戏加载器按 UTF-8 读，日志里中文名正常）。
- 产物实测：`dist/sts2-replica-0.1.0-beta.zip` 28913 B，解压得 `sts2-replica/sts2-replica.dll` 52224 B（与 dist 逐字节一致）+ `sts2-replica.json` 434 B（version=0.1.0-beta）。`dist/` 在 .gitignore 内。

## 2026-11-16 多人兼容加固（三条风险收口）

- Q3 选项数量溢出：`PaelsWing` 追加自己的选项时没有任何数量判断（`Models.Relics/PaelsWing.cs:79`），
  而 `CardRewardAlternative.Generate` 在第三项存活时直接抛 `More than 2 card reward alternatives are not supported`
  （`Entities.CardRewardAlternatives/CardRewardAlternative.cs:74-77`）。谁排在监听器前面谁把它推到 3，且各端同时抛。
  处置：`GoodCardReplica` 在 `player.GetRelic<PaelsWing>() != null` 时直接不注入（让位），按钮消失属预期。
- Q8 选项索引契约：奖励屏只同步索引（`Rewards/CardReward.cs:256` `cardRewardOption[num.Value - _cards.Count]`），
  选项列表每端用本地配置重建。原实现「充能满但无可用模板就 return false」会让**选项数量**取决于本地配置。
  处置：只要轮到我们注入，就**恰好注入一个**——充能满且有可用模板注入 SAMPLE，其余一律注入 MOLD。
- Q5 读档重入：`RunStarted` 在读档时也会触发（`RunManager.cs:788`、`NGame.cs:1182`），
  `GrantStartingRelics` 增加 `if (state.TotalFloor > 0) return;`，新局从 0 层起步。
- 配置分叉预警：握手只比 id-version（`ModManager.cs:1133-1142`、`HandshakeManager.cs:119-125`），不比配置。
  `ReplicaConfigStore.Load()` 读到非默认值时记一条 Warn，提示联机各方必须用同一份 `sts2-replica.json`。
- 设置页写回调增加 `[sts2-replica] settings write <key>=<value>` 日志：此前有一次无头启动后
  `mods/config/sts2-replica.json` 被写成 `charge_required: 2` / `start_with_relic: true`（默认 3 / false），
  带日志连续三次干净启动均未复现，保留该行作为下次出现时的定位手段。
