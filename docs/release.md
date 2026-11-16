# 发布（0.1.0-beta）

## 包内容

`tools/pack-release.ps1` 会先构建，再把 `dist/sts2-replica/` 打成一个 zip：

```
dist/sts2-replica-0.1.0-beta.zip
└── sts2-replica/
    ├── sts2-replica.dll      # 模组本体（图标 PNG 已内嵌）
    └── sts2-replica.json     # manifest（id/version/min_game_version/affects_gameplay）
```

压缩包顶层就是 `sts2-replica/`，解压到 `<游戏根>\mods\` 即可，不需要再套一层目录。
包里**不含**任何前置框架的 dll：RitsuLib 只作为可选编译期引用，发布产物仍是纯 DLL 模组。

## 安装 / 卸载

1. 解压到 `<Steam>\steamapps\common\Slay the Spire 2\mods\`，得到 `mods\sts2-replica\sts2-replica.{dll,json}`。
2. 启动游戏，日志 `%AppData%\SlayTheSpire2\logs\godot.log` 应出现 `[sts2-replica] initialized` 行。
   用 `tools/check-log.ps1` 一条命令筛出来。
3. 卸载 = 删除 `mods\sts2-replica\` 目录；配置在 `mods\config\sts2-replica.json`，一并删掉即恢复默认。

## 验证状态（照实写）

已实机验证（v0.113.1，无头启动，日志为证）：

- manifest 被识别、dll 被加载、`[ModInitializer]` 被调用；
- 配置读取与默认值生效；`GoodCardReplica` 进入共享遗物池排队；
- 装 RitsuLib 0.6.13 时设置页注册成功（`RitsuLib settings page registered: mod_id=sts2-replica, page_id=replica-settings`）；
- 无异常/无 `[ERROR]` 指向本模组；JSON-only 构建同样正常初始化。
- `0.1.0-beta` 能通过 `SemanticVersion` 解析：完整日志里**没有** `declares version ... which is not a valid
  Semantic Version` 警告。游戏内模组列表那一行显示的是 `(0.1.0)`（推测该列表只拼 Major.Minor.Patch；
  `SemanticVersion.ToString()` 本身会保留 `-beta`，见 `MegaCrit.Sts2.Core.Debug/SemanticVersion.cs:243-255`），属显示差异，不影响加载。

**未实机验证**（首次上手请注意）：

- 卡牌奖励界面里【制模】/【取样】按钮的实际显示与点击流程；
- 取样网格的候选项、取消是否退还充能；
- 遗物图标（小图标/描边/大图）的实际观感；
- 双端联机实测（源码级评审已完成，见下节「多人联机」；双开验证未做）。

## 已知问题

- 设置页是「拖动即写盘」：每次值变化都立刻落 `mods/config/sts2-replica.json`。开发期出现过大一次无头启动后
  文件被写成 `charge_required: 2` / `start_with_relic: true`（默认 3 / false）的情况，带日志连续三次干净启动均未复现。
  写回调现在会记 `[sts2-replica] settings write <key>=<value>`，下次出现时按该行定位；文件本身只影响本机玩法，不写坏存档。
- 手改 JSON 时非法值一律回落到默认并在日志留痕（`ReplicaConfig.Parse`），不会抛异常中断启动。

## 多人联机

源码级评审（v0.113.1 反编译核对）结论：本模组**不自造任何网络消息**，全部行为靠「各端同构执行 + 索引同步」，
与原版 PaelsWing 同构。由此带来三条硬要求：

1. **所有玩家装同一版本**：握手只比 `id-version` 字符串（`ModManager.cs:1133-1142` → `HandshakeManager.cs:119-125`），
   装/不装、版本不同都会被拒绝入局。
2. **所有玩家用同一份 `sts2-replica.json`**：奖励界面同步的只是**选项索引**（`CardReward.cs:230-231/:235/:256/:297`），
   对端用本地配置**重建**选项列表。`charge_required` 不同 → 两端跑不同分支（加卡 vs 充能 +1）；
   `offer_count`/权重不同 → 对端按同一索引取到**另一张卡**（静默错卡）。握手不比配置，
   故加载时若配置非默认会打一条 `non-default config is active` 警告（`src/Config/ReplicaConfigStore.cs` 的 `Load()`）。
3. **同时持有 PaelsWing 或 Driftwood 时本模组按钮让位（消失）**：PaelsWing 无条件加第三个选项
   （`Models.Relics/PaelsWing.cs:79`），Driftwood 让 `CanReroll` 成真（`Driftwood.cs:22`），而原版
   `CardRewardAlternative.Generate` 在 3 项时抛 `InvalidOperationException`（`CardRewardAlternative.cs:74-77`）。
   这是原版既有 bug，我们只保证自己不当触发者。

已核实为安全的点：`Charges` 是 `[SavedProperty]`，进校验和与重连包（`NetFullCombatState.cs:186/486-491`、
`SerializableRelic.cs:9-33`），无需显式同步；取样 Rng 由 `RunState.Rng.Seed + 玩家槽位 + 遗物 Id + 楼层` 派生
（`Rng.cs:61-64`），不推进原版奖励链；`start_with_relic` 靠各端各自 `RunStarted` 发放
（`RelicCmd.Obtain` 全程无网络消息，`RelicCmd.cs:35-55`），并在 `TotalFloor > 0`（读档）时跳过。
已知限制：同一玩家同一楼层取样两次，候选完全相同（派生 Rng 的 mixin 只含楼层）。

## 版本号与兼容

- 版本号是语义化版本，游戏用 `SemanticVersion.TryFromString` 解析（`MegaCrit.Sts2.Core.Debug/SemanticVersion.cs:41`），
  `0.1.0-beta` 这类 prerelease 合法；解析失败会记 `declares version ... which is not a valid Semantic Version`。
- `min_game_version` = `0.113.1`：当前 Steam 默认分支。游戏大版本更新后需要复核反编译 API 再抬版本。
- `affects_gameplay: true`（也是字段缺省值，见 `MegaCrit.Sts2.Core.Modding/ModManifest.cs:43`）：本模组改玩法，
  多人局必须人人安装同版本。
- `has_pck: false`：纯 DLL 模组，没有 `res://` 资源；本地化与图标都由 Harmony 补丁在运行时注入。

## 发工坊（需要你手动做）

游戏支持 Steam 创意工坊上传（v0.113.x 起），但上传动作要 Steam 客户端里操作，本仓库只负责产出可上传的目录：

1. 用 `tools/pack-release.ps1` 构建，或直接把 `dist\sts2-replica\` 作为上传内容根；
2. 工坊条目根目录应直接是 `sts2-replica.json` + `sts2-replica.dll`（与 `mods\sts2-replica\` 同构）；
3. 预览图、说明文字按工坊页面填，说明里请带上"多人需人人安装同版本 + 相同配置"。
