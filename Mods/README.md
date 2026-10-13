# 非官方编辑器运行时扩展

这里保存面向 InFalsus Studio Windows 1.0.10 的 BepInEx 插件源码。插件由独立发行包安装，不属于 Unity 编辑器核心；不修改谱面语法、谱面保存或官方导出格式。

当前配套版本：

- [Better UI 1.2.2](BetterUI/1.2.2/README.md)：改进制谱器 UI。
- [Native Visual 1.1.15](NativeVisual/1.1.15/README.md)：还原原生轨道、HUD 与背景视觉，并提供美化进度条。

## 构建

在 PowerShell 中传入已安装的 InFalsus Studio 1.0.10 目录。目标目录需要包含 `InFalsusStudio_Data/Managed` 和 `BepInEx/core`：

```powershell
.\Mods\NativeVisual\1.1.15\Build.ps1 -Editor 'E:\RhythmGame\InfalsusStudio10.13'
.\Mods\BetterUI\1.2.2\Build.ps1 -Editor 'E:\RhythmGame\InfalsusStudio10.13'
```

构建会在对应源码目录生成 DLL。仓库不跟踪生成的 DLL、原版 AssetBundle、字体或其提取分析记录；完整运行资源仍由各自版本的 mod 发行包提供。

Better UI 依赖 `local.infalsusstudio.nativevisual`，因此安装时需要同时安装 Native Visual。安装前退出制谱器，先备份目标目录；实际操作和玩家验收按 mod 发行包说明进行。
