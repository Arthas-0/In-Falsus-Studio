# 渐变 Timing / Gradient Timing

入口：工具 → 外置工具 → 渐变 Timing。Start 的 `+` 使用当前播放头。执行只修改内存，Ctrl+Z 一次撤销，Ctrl+S 手动保存。可先点“计算物件数”而不插入。

流速用 track；等效 BPM 会除以参考 BPM 转为倍率。节拍 BPM 另选 bpm。包含精确终值；只在当前时间组内替换所选种类的区间事件。

黑线输出固定为宽度 1、split 100 的 SkyArea；每段分配独立 groupid 和 1 ms 界外辅助段藏头。所有 SkyArea 时长至少 1 ms。默认新建 noinput 组；关闭新建组时必须选择已有 noinput / noclip 组。没有 Y 参数。需要在设置开启天空超界。

参数可在制谱器面板反复调整并记忆；编辑 defaults.json 后点“重载默认值”。脚本每次执行重新读取。Windows PowerShell 和 .NET Framework 为本机内置组件，不需要 Python。工具请求/返回缓存在 VisualUIData/ScriptCache，用后清理。工具文件均在本文件夹内。不要把 shell 指令写进数值字段。

English: Tools → External Tools → Gradient Timing. The `+` button uses the playhead. Run inserts one undoable in-memory batch; Save writes the chart. Count-only generation does not insert. Use track for scroll, with effective BPM divided by reference BPM. Choose bpm for beat timing. Includes the exact end value; replacement affects only this event kind in the current timinggroup.

Generated blacklines are head-hidden, width-1 SkyAreas at split 100, with strictly positive durations and unique group IDs. New groups default to noinput. No Y parameters. Uses built-in Windows PowerShell/.NET Framework. All tool files are local to this folder. This relies on the editor copy's existing nonofficial timinggroup/noinput syntax.

本工具只写入当前时间组的 track 或 bpm，不新建 noinput 组。track 的 0/负数为停流/倒流；bpm 必须大于 0。INF 的 bpm 不负责变速，因此等效 BPM 模式仍生成 track。
This tool edits timing in the current group without creating a noinput group. Stop/reverse use track; bpm must be positive.
