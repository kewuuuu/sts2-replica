# PROGRESS

## 2026-11-15 立项

- 项目建立，目标：STS2 v0.113.1 的「好牌多抓」遗物模组 beta。
- 规格来源：用户设计（遗物驱动、制模/取样同一按钮、充值阈值 a=3、取样出 b=2 张、权重 c-g、可配置、可开局自带、进共享遗物池）。
- 待办：可编译骨架 → 遗物实现 → 设置 → 美术 → 多人验证 → 打包。
## 2026-11-15 骨架完成

- 可编译骨架：`net9.0` class library，`<Reference>` 指向游戏 `sts2.dll` / `0Harmony.dll` / `GodotSharp.dll` 且 `Private=false`（不随产物复制）。
- 入口 `Sts2Replica.ReplicaMod.Init`（`[ModInitializer("Init")]`，static/无参/void），把 `PlaceholderRelic` 排进 `SharedRelicPool`。
- 产物 `dist/sts2-replica/{sts2-replica.dll, sts2-replica.json}`，整目录丢进 `<游戏根>\mods\`；`install.ps1` 为手动脚本，不自动执行。
- 契约依据（`文件:行`）、实测 TFM/日志路径、踩坑与未确认清单见 `docs/build-notes.md`。
