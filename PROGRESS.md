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

## 2026-11-16 联机参数跟随房主（v0.2.0-beta）

- 需求（m00935 / m00962）：遗物充能是**每个玩家自己的**，但模组参数在联机局里以房主为准；客户端与房主不一致时看房主。
- 网络消息：新增 `src/Net/ReplicaConfigMessage.cs`（房主→客户端，12 个字段：7 个 int + 5 个 bool，`ShouldBroadcast=true`、`Reliable`、`ShouldBuffer=true`）与 `ReplicaConfigRequestMessage.cs`（客户端→房主，空载荷）。
  类型由 `MessageTypes.Initialize` 的 `GetSubtypesInMods<INetMessage>` 自动收编（`MessageTypes.cs:13-19`），id 按**类型名排序**分配（`NetTypeCache.cs:22`）且只占一字节（`NetMessageBus.cs:46`）⇒ **类型名不可再改，改名等于两端 id 变化**。
- 权威判定：`ReplicaRuntimeConfig.IsMultiplayerClient`（`RunManager.Instance.NetService.Type == NetGameType.Client`）；房主开局广播、客户端开局请求，客户端采纳后 `Effective` 才切到房主数值，本机 JSON 不被覆盖（`TryWriteLocal` 在联机客户端只警告不落盘）。
- 开局遗物：客户端在 `OnRunStarted` 里**不**发放，等 `OnHostConfigAdopted` 后按房主数值发放；`GrantStartingRelics` 保留 `TotalFloor > 0` 读档守卫与逐玩家去重。
- 自愈：客户端的首问可能早于房主 handler 注册而被丢弃（`NetMessageBus.cs:88`），故在首个卡牌奖励判定处 `RequestConfigIfMissing()` 补发**一次**（flag 在 `AttachForRun` 复位），并记 Warn。
- 异常面：`TypeToId(Type)` 是纯字典索引器（`NetTypeCache.cs:52-55`），未注册类型抛 `KeyNotFoundException`；`MessageTypes` 未初始化则抛 `InvalidOperationException`。故 id 探测用宽 catch，只记一条 Warn，绝不让异常冒出 `RunStarted`。
- 未实测：自愈整链、中途加入/读档重放、「房主局内改设置不推送已在局内的客户端」；这三条仍是源码级结论。
- 版本：manifest 升到 0.2.0-beta。

## 2026-11-17 描边、参数开局冻结、配置改名（v0.3.0-beta）

- 需求（m01092 / m01101）：遗物小图标缺黑色描边；模组参数"游戏期间不能修改"，创建对局时记录、房主局内改动只影响之后创建的对局；配置文件名改成 sts2-replica.cfg。决策：图鉴大图保持现状（不加描边）、设置页仍可拖动写盘但只影响下一局、自愈重试每层一次且每局封顶 5 次。
- 描边：`NRelic.cs:142-155` 分两态——Small 走 `Icon.Texture = Model.Icon` + `Outline.Visible = true; Outline.Texture = Model.IconOutline`，Large 走 `BigIcon` 且 `Outline.Visible = false` ⇒ 描边层只存在于小图标，图鉴大图本就没有。原剪影 PNG 与图标同为 128×128、无外扩，若 `res://materials/ui/relic_mat.tres` 的 shader 不向外扩张就被压在图标下面，表现为"没有描边"。处置：把黑描边烘进 `assets/icons/good_card_replica_icon.png`（rim pass，深色 #14161b 外扩约 3 px，1545 B → 1955 B），`RelicNodeArtPatch.cs:54-59` 改为 `Outline.Visible = false` 且不再写 `Outline.Texture`；大图与剪影资源不动。
- 冻结：`ReplicaRuntimeConfig` 增加 `_runSnapshot` 与 `ForThisRun`（`:27`，无快照回落 `Effective`）、`BeginRun`（`:33`，日志 `run settings frozen: <fingerprint>`）、`EndRun`（`:43`）。快照时机：单人/房主在 `ReplicaMod.cs:74` 的 `OnRunStarted` 里 `EndRun` → `BeginRun(Effective)`；联机客户端在 `ReplicaConfigSync.cs:112` 采纳房主配置成功后冻结。玩法两条读取（`GoodCardReplica.cs:77`、`:114`）改用 `ForThisRun`。**不做局内广播**：局内改设置不推送，只影响之后创建的对局。
- 改名：`ReplicaConfigStore.cs:15` `FileName = "sts2-replica.cfg"`，`:88` `ResolveConfigPath()` 是唯一路径（`<exe 目录>/mods/config/`），删掉 `CandidatePaths`/`ResolveSavePath`/`user://` 回落；`:109` `WarnAboutLegacyFile()` 只对旧 `sts2-replica.json` 记一条 Warn、不读不迁移。依据：`ModManager.cs:552-558` 只把 `.json` 当 manifest 解析，`.cfg` 不再被扫，启动噪音消失。
- 自愈：`ReplicaConfigSync.cs:18` `MaxConfigRequestsPerRun = 5`；同层已试即静默返回（`:135`），计满打一次 `giving up on the host config`，每次补发记 `attempt N/5`；`AttachForRun` 复位每层标志与每局计数（跨层不清零总数）。任何 `INetMessage` 类型名/字段未动。
- 验收（我本机重跑）：`dotnet build -c Release` 0 警告 0 错误，dll 66048 B，与装入 `mods/sts2-replica/` 的副本逐字节一致；自带 dll 里的 UTF-16 字面量含 `sts2-replica.cfg`、`run settings frozen:`、`giving up on the host config`；无头启动 exit=0，日志 233 行 `legacy ...sts2-replica.json is ignored`、234 `no config file found, using defaults`、236 patches applied、237 initialized（charge_required=3/offer_count=2/start_with_relic=False）、238 fingerprint，全日志 `[ERROR]` 计数 0。
- 未实测：`.cfg` 首次真实写入（无头不进设置页）；客户端采纳后再冻结、跨层重试计数与 5 次封顶（需双开多局）；描边观感（headless 不渲染）。
- 旧文件：`mods/config/sts2-replica.json` 由玩家手动删除，本版既不读也不写。
- 版本：manifest 升到 0.3.0-beta。

## 2026-11-17 创意工坊发布（item 3840979834）

- 目标（m01173「直接推创意工坊」）：把 0.3.0-beta 发布到 Steam 创意工坊。
- 结论：条目 **3840979834** 已创建并公开提交内容（https://steamcommunity.com/sharedfiles/filedetails/?id=3840979834），页面实测可打开，标题「好牌多抓 / Good Card Replica」、76.365 KB、描述与预览图渲染正常、1 项改动说明。
- 工具链（新增）：`tools/WorkshopUploader/`（`--dry-run` 校验 / `--create` 建条目 / `--update <id>` 更新 / `--probe` 只读诊断），`tools/pack-workshop.ps1` 把构建产物摆进 `dist/workshop/`；`sts2-replica.csproj` 加一行 `<Compile Remove="tools/**" />` 防止工具源码被编进模组 dll（dll 仍是 66048 B）。
- **关键坑（manual dispatch）**：游戏目录的 `Steamworks.NET.dll` 是 manual dispatch 版——`CallbackDispatcher.cs:34` 调 `SteamAPI_ManualDispatch_Init()`，`:147-208` 用 `ManualDispatch_RunFrame/GetNextCallback` 取回调，异步结果在 `:161-179`（`m_iCallback == 703` → `SteamAPICallCompleted_t` → `ManualDispatch_GetAPICallResult`）按 handle 从 `m_registeredCallResults` 派发。
  ⇒ 必须用 `CallResult<T>.Create(delegate(T, bool bIOFailure))` + `Set(call)` + `SteamAPI.RunCallbacks()` 主循环取结果；`SteamUtils.IsAPICallCompleted/GetAPICallResult` 在这套流程里是死路（对任何 handle 都回 `completed=True, flaggedFailed=True, k_ESteamAPICallFailureInvalidHandle`，包括伪造的 handle），`Callback<CreateItemResult_t>` 也永远收不到（那两种 result 只作为 call result 存在）。
- 反编译证据留档：`D:/engine/DSH/Workspace/_cache/swdecomp/{SteamAPI.cs,CallbackDispatcher.cs,CallResult.cs}`（`ilspycmd -t <类型>` 逐类型导出）。
- 反证：把游戏 headless 跑起来（Steam 认为 app 正在运行）后重跑 `--probe`，结果不变 ⇒ 「必须由 Steam 启动」的假设被排除，问题纯属机制用错。
- 发布后核验：`dist/workshop-item-id.txt` = 3840979834；用浏览器打开条目页确认公开可见（标题/预览图/描述/大小 76.365 KB 均正常）。
- 未验证：`--update` 走「复用同一 item」的分支（`SubmitItemUpdate` 已在 `--create` 内跑通，但复用分支没跑）；条目截图仍缺（`docs/workshop.md` 列了建议的 5 张）。


## 2026-11-19 描述数据显示本局数值（v0.3.1-beta）

- 需求（m01423）：把设置里的数值写进遗物描述——制模几次可取样、取样几选几。
- 机制（反编译证据）：提示框走 `RelicModel.HoverTip` → `DynamicDescription`（`RelicModel.cs:93-111`、`:358-377`），图鉴走 `NInspectRelicScreen.cs:408` 同一个属性；正文支持 SmartFormat 占位符（`LocString.cs:85-94`），`LocString.AddObj` 对同名键是覆盖（`:161-169`）。`Description` 是私有（`:69`）、`DynamicVars` 不可覆写（`:291-303`）、`DynamicVarSet` 首次构建即固化（`Clone` 于 `:547`）。
- 实现：`src/Localization/ReplicaLoc.cs:55` 描述改为带 `{Charges}`/`{Options}` 的模板；`GoodCardReplica.CanonicalVars` 提供兜底值（patch 失效时不显示裸花括号）；新增 `src/Models/RelicDescriptionPatch.cs`（Harmony postfix on `RelicModel.DynamicDescription` getter，遇本遗物就用 `ReplicaRuntimeConfig.ForThisRun` 覆盖两个变量），因此数字跟"本局冻结的那份设置"一致，改设置下一局即变。
- 验收（我本机）：`dotnet build -c Release` 0 警告 0 错误，dll 66560 B，与 `mods/sts2-replica/` 副本逐字节一致；dll 内 UTF-16 字面量含 `{Charges} 点充满`、`{Options} 张候选`；无头启动 exit=0，`patches applied` 正常、`patch failed` 计数 0、模组行显示 `(0.3.1-beta)`；全日志唯一 `[ERROR]` 属工坊模组 MultiplayerModSync（缺 dll），与本模组无关。
- 未实测：悬停/图鉴里的实际渲染（headless 不渲染 UI；需用户开一局看描述）。
- 发布：`dist/sts2-replica-0.3.1-beta.zip`；创意工坊条目 3840979834 用 `--update` 复用分支更新成功（首次实测该分支，输出见 `docs/workshop.md` §2）。
- 发布后发现：页面上正文第二行仍写「模组版本 0.3.0-beta」——那行是 `workshop/description.bbcode:2` 手写的，工具不校验内容。修法是一条护栏而不是再改一次数字：`tools/WorkshopUploader/Program.cs` 新增 `CheckVersionMentions`（从 `<content>/<id>/<id>.json` 读 `version`，正则扫描述里的「模组版本 / mod version」，不一致 `BAD` 并 exit 1），已用故意写旧版本的描述文件验证会拦（`BAD description says mod version 0.3.0-beta but the manifest says 0.3.1-beta`，exit 1）；改动描述版本号后重跑 `--update`，页面正文已更新。
- 同轮：把 `--update` 实测输出、版本号三处同升、限流 `HTTP 429` 的处理写进 skill `sts2-workshop-publish`。

