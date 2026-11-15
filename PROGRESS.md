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
