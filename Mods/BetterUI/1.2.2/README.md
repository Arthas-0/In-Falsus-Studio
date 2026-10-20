# Better UI 1.2.2

目标：InFalsus Studio Windows 1.0.10。该插件使用 BepInEx/Harmony 替换原制谱器的面板外观与布局，同时调用原有编辑命令和字段。

本版将节拍细分移至底栏指定空白处，移除网格面板中的重复入口，并移除自动播放图标下方的“开 / 关”文字。其它 UI 功能和点立得编辑流程沿用现有实现。

本插件不添加谱面语句或参数，不改变谱面保存和导出格式。安装需要同时安装 Native Visual 1.1.15，插件依赖其运行时服务。

## 构建

```powershell
.\Build.ps1 -Editor 'E:\RhythmGame\InfalsusStudio10.13'
```

构建需要目标程序的 `InFalsusStudio_Data/Managed` 与 `BepInEx/core` 引用。DLL 输出到本目录；将它和 `Icons` 文件夹一起放到 `BepInEx/plugins/NativeEditorUI`。版本化 mod 包的安装脚本负责备份和回滚。

验证记录：已完成编译与交付文件校验。用户安装副本未启动，实际界面交互尚未由用户验收。
