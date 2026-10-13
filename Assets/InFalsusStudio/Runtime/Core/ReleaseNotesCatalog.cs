using System.Linq;

namespace InFalsusStudio.Core
{
    public sealed class ReleaseNotesEntry
    {
        public readonly string Version,Chinese,English;
        public ReleaseNotesEntry(string version,string chinese,string english)
        {Version=version;Chinese=chinese;English=english;}
    }
    public static class ReleaseNotesCatalog
    {
        // New releases are inserted first. The popup always shows the latest three.
        private static readonly ReleaseNotesEntry[] entries={
            new ReleaseNotesEntry("1.0.10",
                "• SkyArea 首尾连接放宽到前后 2ms，包含小间隔与轻微重叠，仍检查横向接触。\n"+
                "• 谱面检查新增持续时间小于 5ms 的 Hold / SkyArea，可跳转并选中。\n"+
                "• 保存及导出时按时间稳定排序谱面语句，相同时间的先后顺序、注释和未知内容保留。\n"+
                "• 工具新增 Sky 自动分组：按时间与连接关系分配 groupid，可整次撤销；新建 Sky 仍默认不指定组。\n"+
                "• 更新 Flick 曲线：保持头部高度，大宽度尾部持续可见并在终点收尖，修正最后四分之一过早消失；左右方向一致。",
                "• SkyArea endpoint joins allow a gap or overlap up to 2ms, with lateral contact still required.\n"+
                "• Chart Check flags Hold / SkyArea durations below 5ms, with jump and selection.\n"+
                "• Save and export stably sort timed statements, retaining equal-time order, comments and unknown content.\n"+
                "• Tools adds explicit Sky auto-grouping by time and endpoint joins, with one undo step. New SkyAreas remain unassigned.\n"+
                "• Flicks keep a consistent head height and a readable tail through the final quarter, tapering at the actual tip. Both directions are mirrored."),
            new ReleaseNotesEntry("1.0.9",
                "• 歌曲信息新增预览开始/结束时间（秒），对应原版 song_info 参数；新增当前难度的曲绘画师信息。\n"+
                "• 已有工程的主音乐缺失时重新寻找 MP3/OGG，多首候选可选择；保留已有谱面工作稿。\n"+
                "• 每次打开重新检查曲绘：单张自动使用，多张由用户选择；图片未变时记住选择，同名图片替换也会重新加载。\n"+
                "• SkyArea 默认不指定组，直接省略最后的 groupid 参数；工具改为全部不指定组，仍按首尾连接隐藏头框，允许手动填写组号。\n"+
                "• 信息与资源路径变更先保留为草稿，Ctrl+S 写盘；最近曲绘选择另存本机记忆。",
                "• Song information adds native preview start/end seconds and per-difficulty Jacket Designer credits.\n"+
                "• Missing project music is rediscovered among MP3/OGG files, with a picker for ambiguous candidates. Edited charts remain intact.\n"+
                "• Every open scans jackets: one image is automatic, multiple images use your remembered choice until the image inventory changes. Same-path replacements reload.\n"+
                "• SkyAreas omit the optional group ID by default. Tools removes all assignments; automatic head joins and manual group overrides remain available.\n"+
                "• Metadata and media paths remain drafts until Save; successful jacket choices are also remembered locally."),
            new ReleaseNotesEntry("1.0.8",
                "• SkyArea 等分、网格切分及 Flick 输出的坐标份数最多为 128，避免产生百万级分母；相邻小段接点一致。\n"+
                "• 在上限内按原左右边界拟合，保持整数 X/份数/宽度，允许少量位置与宽度误差；不改变切分时间。\n"+
                "• 工具新增大分母压缩：将当前谱面超过 128 的端点按比例重编码，保留时间、缓动、组号，整次可撤销，保存才写盘。",
                "• Equal/grid SkyArea splits and Flick output use at most 128 coordinate parts, with matching neighboring joins.\n"+
                "• Integer X/split/width fit both original edges within the cap, allowing small positional/width changes. Split times stay unchanged.\n"+
                "• Tools compresses existing large divisors in the current chart, preserving time, ease and group IDs. One undo step; Save writes disk."),
            new ReleaseNotesEntry("1.0.7",
                "• 修正 SkyArea / Flick 坐标参数：原始 X、份数、宽度均以整数保存，切分与参数编辑保留对应的坐标比值。\n"+
                "• 修正拖动边界和续接时可能产生坐标小数的路径；曲线切分继续按原左右边界采样。\n"+
                "• 工具新增坐标小数修复：对当前谱面 SkyArea / Flick 的原始坐标参数四舍五入，可整次撤销；时间、缓动和组 ID 保留，保存才写盘。",
                "• SkyArea / Flick X, split and width are stored as integer parts, preserving coordinate ratios during splitting and property editing.\n"+
                "• Edge drags and continuations no longer generate fractional coordinate parts. Curve splits still sample both original edges.\n"+
                "• Tools adds decimal coordinate repair for the current chart, with one undo step. Time, ease and group IDs stay unchanged; only Save writes disk."),
            new ReleaseNotesEntry("1.0.6",
                "• 新建 SkyArea 默认组 ID 为 -1；自动检测首尾连接，并可一键重置当前谱面的组 ID。\n"+
                "• 新增工具栏：单条等分或多条按当前网格切分 SkyArea，也可按原边界生成 Flick；小 SkyArea 使用 Linear。\n"+
                "• SkyArea 更显眼、Flick 亮度降低；Hold 改用连续的镜像采样，修正重复接缝。\n"+
                "• 音乐减速保持音调，沿用原有时间、偏移和打击音同步。\n"+
                "• 新增谱面检查：报告未知语句及 Tap / Hold / Flick 叠键，支持跳转并选中；相机和帮助移入设置。",
                "• New SkyAreas default to group -1 with automatic geometric joins; reset all IDs in the current chart.\n"+
                "• Tools splits one SkyArea into N pieces or multiple SkyAreas along the current grid, with SkyArea / Flick output from sampled edges.\n"+
                "• More visible SkyAreas, softer Flicks and seamless mirrored Hold sampling.\n"+
                "• Slower music playback preserves pitch and existing transport / key-sound timing.\n"+
                "• Chart Check reports unknown statements and Tap / Hold / Flick conflicts, with jump and selection. Camera and Help moved to Settings."),
            new ReleaseNotesEntry("1.0.5",
                "• 关闭节拍网格后，常驻小节线改为灰色；开启网格时保留紫、蓝、绿、红、白的音乐拍位分级。\n"+
                "• 打开歌曲改用资源管理器式文件夹窗口，支持地址栏、搜索和文件夹列表，并记住上次成功打开的目录。\n"+
                "• 新增更新公告：每个新版本首次启动时展示最近三个版本，也可从设置随时查看。",
                "• Persistent measure lines are gray with the beat grid off; the enabled grid keeps its musical color hierarchy.\n"+
                "• Song folders use the Explorer-style picker with an address bar, search and folder list. The last successfully opened folder is remembered.\n"+
                "• What's new shows the latest three releases on first launch of each version and remains available in Settings."),
            new ReleaseNotesEntry("1.0.4",
                "• 修复极高滚速的短脉冲显示，保留真实滚速和停流行为。\n"+
                "• 接入六张 Hold 图集，改善 Tap、SkyArea 激活效果和侧轨对比。\n"+
                "• 本机后续修补：网格按小节、四分音、八分音、十六分音分级着色；段落选择完整包含 Hold/SkyArea 的头尾。",
                "• Fixed brief extreme-speed pulses while preserving chart speed and stops.\n"+
                "• Added six Hold atlases and improved Tap, active SkyArea and side-lane contrast.\n"+
                "• Follow-up fixes: musical grid colors and range selection requiring complete Hold/SkyArea spans."),
            new ReleaseNotesEntry("1.0.3",
                "• 修复 JSON 中 BPM/拍数事件的识别，恢复此前受未知时序阻断的预计物量与分数显示。\n"+
                "• 兼容已缓存工作谱中的旧 icp_event，无需删除工作目录或重新导入。\n"+
                "• 保留 99/999 拍及未知源数据；首次编辑可恢复事件时保存原文备份，继续遵循手动保存。",
                "• Recognized JSON BPM/meter events, restoring estimated counts and scores previously blocked by unknown timing.\n"+
                "• Reads recoverable legacy icp_event records in cached charts without deleting the workspace or reimporting.\n"+
                "• Preserves 99/999 meters and unknown source data; editing recovered events keeps the original record, with explicit Save.")
        };
        public static ReleaseNotesEntry[] Recent {get{return entries.Take(3).ToArray();}}
    }
}
