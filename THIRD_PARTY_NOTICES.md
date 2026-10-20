# 素材与依赖来源记录

本仓库保存维护者提供的当前编辑器工程。**代码、游戏素材、参考谱面和 Unity 依赖的权利范围分别处理**；公开仓库不为历史素材自动追加授权，本次初始化没有选择一个覆盖全部内容的统一开源许可证。

| 范围 | 当前来源记录 |
| --- | --- |
| 编辑器代码 | 当前 In Falsus Studio 工程，由 Arthas 维护；本次导入保留现有代码与修补 |
| `Assets/InFalsusStudio/Resources/InFalsusStudio/NoteSkin104/*.png` | 维护者提供的六张原始 Hold 图集，字节未改；原文件名、尺寸和 SHA-256 见 [来源清单](docs/ASSET_PROVENANCE_1.0.4.json) |
| HUD、背景、Branding 和 KeySounds | 现有工程内随附素材；本次不改变原文件，也不为其声明新的许可 |
| `Editor/CountRegression103`、`Editor/VisualRegression104` 及示例谱面 | 现有谱面回归/示例数据，保留格式兼容性检查用途；不包含个人完整歌曲音频缓存 |
| Unity / URP / 其他 Unity 包 | 版本见 `Packages/manifest.json` 和 `packages-lock.json`，许可按各包随附声明 |
| 字体 | 应用使用系统字体回退；仓库不捆绑字体文件 |
| `Mods/BetterUI/1.2.2/Icons` | Better UI 插件配套的界面图标；随插件源码保留。 |
| `Mods/VisualUI/1.0.39` | Visual & UI 组合插件及 GradientTiming、DeleteTiming、CopyTiming 外置工具源码；不包含提取的原生图集、AssetBundle、字体或 BepInEx 二进制。该节点的作者性代码由社区维护者提供；不为原游戏语法、名称或素材声明权利。 |
| Native Visual 运行资源 | 原版 AssetBundle、字体和游戏界面图未纳入本仓库；配套 mod 发行包从本机 InFalsus Studio 1.0.10 提取并提供，不在此重复分发。 |

原游戏名称、素材及参考谱面仍属于各自权利人。本编辑器的社区维护和壳子扩展不表示官方背书。新增第三方代码、素材或数据时，记录作者、来源、许可证及必要的版权/署名信息；不要把其他项目的代码许可证套用到其素材上。
