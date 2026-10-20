# Native Visual 1.1.15 · NoHitFX

目标：InFalsus Studio Windows 1.0.10。该插件通过 BepInEx/Harmony 绘制原生轨道、音符、HUD、背景粒子与歌曲进度条；不替换打击动画，不修改谱面数据、编辑操作、保存或导出格式。

中央背景按本体 Constrained2D 的 3840×2160 参考画布、顶部对齐偏移和 1200×800 输出尺寸计算。材质加载失败时，中央背景效果单独停用，其它模块继续运行。

## 构建

```powershell
.\Build.ps1 -Editor 'E:\RhythmGame\InfalsusStudio10.13'
```

构建需要目标程序的 `InFalsusStudio_Data/Managed` 与 `BepInEx/core` 引用。运行时还需要配套发行包中的 `Assets`、`Presentation` 资源目录；这些目录包含从原版程序提取的 AssetBundle、字体和界面图，不纳入本源码仓库。安装时保留发行包中的资源布局。

验证记录：此前在独立空工程使用 Direct3D 11 检查原生材质载入、连续帧逸散效果及 16:9、4:3、宽屏位置；用户最终副本未启动，实际游戏内效果尚未由用户验收。
