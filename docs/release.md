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
- 多人联机行为（评审进行中，见 `PROGRESS.md`）。

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
