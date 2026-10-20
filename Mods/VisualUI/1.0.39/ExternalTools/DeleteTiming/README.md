# 删除 Timing / Delete Timing

通过段落选择点击两个时间戳（不依赖区间内是否有音符），含首尾。仅处理当前时间组明确写出的 track/BPM，不复制或删除继承事件、chart 头或其他语句。删除范围内匹配事件，其他物件和时间组保留。整批一次撤销，仍需手动保存。

Use Range Selection to pick two timestamps, including endpoints; notes are not required. Only explicit track/BPM in the current timinggroup are processed, excluding inherited events, chart headers and other statements. Delete matching events in the range, preserving notes and other timinggroups. One Undo for the batch; Save manually.

Compatibility: timinggroup scope follows the existing nonofficial 1.0.6 shell extension. Base chart track/BPM keep native syntax.
