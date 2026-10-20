namespace InFalsusStudio.NativeEditorUI {
 public static partial class UILocalization {
  static void AddEnglishExtras(){
   string rows=@"
判定显示|Judgement display
垂直同步（跟随屏幕）|VSync (match display)
显示帧时间波动|Frame-time details
平滑播放显示|Smooth visual playback
同步开启时跟随屏幕刷新率；关闭后使用上方帧率上限。同步可能增加少量输入延迟，可随时切换。|VSync follows the display refresh rate. Turn it off to use the frame cap above. VSync may add some input latency; switch at any time.
平滑音频缓冲之间的物件运动；音频调度不变，暂停与定位保持原时间。帧时间统计：最近5秒的 P99、最长帧和超过16.7ms的次数。|Smooths motion between audio buffer updates. Audio scheduling, pause and seek times stay unchanged. Statistics show the last 5 seconds: P99, worst frame and frames over 16.7ms.
空域 X 最小值|Air X minimum
空域 X 最大值|Air X maximum
开启天空超界后，可自定 X 网格范围（百分比）。原轨道为 0～100；范围只影响标尺和吸附，不移动现有物件。|With sky overflow enabled, set an X grid range in percent. The original track spans 0..100. This only affects the ruler and snapping; existing notes stay in place.
X 网格范围须在 -10000～10000 内，最小值小于最大值。|X grid bounds must be within -10000..10000, with minimum below maximum.
目标帧率|Target frame rate
无限制|Unlimited
允许天空物件超界|Allow out-of-bounds sky notes
未打开谱面|No chart open
操作设置已保存。|Operation settings saved.
操作设置保存失败；上次设置已保留：|Could not save operation settings; previous preferences retained: 
背景已应用到视图；全局保存后记住。|Background applied to view; use Save to remember it.
背景已清除。|Background cleared.
当前组名称|Name
重命名当前组|Rename
删除空组|Delete empty
非官方时间组：按各组起点 BPM 估算物量；尚未在目标壳子验证判定规则。|Unofficial timinggroups: counts use each group's starting BPM. Judgment rules remain unverified in the target runtime.
非官方时间组：各组独立 BPM 的候选计数，尚非本体判定认证。|Unofficial timinggroups: provisional counts use each group's BPM. These are not verified game judgments.
配对谱面的当前时间组内容不匹配，已取消补入。|The paired chart does not match the current timinggroup. Import cancelled.
配对谱面的 BPM 超出支持范围。|The paired chart BPM is outside the supported range.
当前时间组存在冲突的 BPM，请手动处理。|This timinggroup has conflicting BPM events. Resolve them manually.
未知的组属性。|Unknown timinggroup property.
默认group 无组头属性。|Default group has no header properties.
 行：时间组不能嵌套。|: timinggroups cannot be nested.
 行：组属性语法无效。|: invalid timinggroup property syntax.
 行：时间组名称无效。|: invalid timinggroup name.
重复的时间组名称：|Duplicate timinggroup name:
 行：groupend() 没有对应的时间组。|: groupend() has no matching timinggroup.
 行：应为 hidegroup(ms,0/1)，时间须在 0～86400000 ms。|: expected hidegroup(ms,0/1), with time from 0 to 86400000 ms.
 行：chart 头部只能放在基础组。|: the chart header must be in the default group.
 行：时间组语法无效。|: invalid timinggroup syntax.
 缺少 groupend()。| is missing groupend().
[非官方时间组] |[Unofficial timinggroup] 
目标时间组不存在。|The target timinggroup does not exist.
不能把时间组边界作为物件移动。|Timinggroup boundaries cannot be moved as notes.
只能移动物件、BPM、track 和 hidegroup 事件。|Only notes, BPM, track and hidegroup events can be moved.
时间组边界已变化。|The timinggroup boundaries changed.
目标 timinggroup 已不存在，请重新选择。|The target timinggroup no longer exists. Select it again.
选中的物件已变化，请重新选择。|The selected notes changed. Select them again.
名称限 1～64 个中文、字母、数字、下划线或连字符，首字符不能为数字。|Use 1 to 64 letters, Chinese characters, digits, underscores or hyphens. The first character cannot be a digit.
时间组名称已存在。|This timinggroup name already exists.
时间组名称无效。|Invalid timinggroup name.
时间组不存在或新名称重复。|The timinggroup does not exist or the new name is already in use.
基础组不能删除。|The default group cannot be deleted.
请先移出该组物件及事件，再删除空组。|Move this group's notes and events elsewhere before deleting the empty group.
timinggroup 已变化，请重新选择。|The timinggroup changed. Select it again.
时序语句已变化，请重新选择。|The timing statement changed. Select it again.
该字段不可编辑。|This field cannot be edited.
请输入有效数值。|Enter a valid number.
hidegroup 只接受 0（显示）或 1（隐藏）。|hidegroup accepts only 0 (show) or 1 (hide).
BPM / 拍数须大于 0。|BPM and beats per measure must be greater than 0.
继承行已变化，请重新选择。|The inherited row changed. Select it again.
全谱初始 BPM / 拍号不能删除。|The initial chart BPM and time signature cannot be deleted.
此处只能编辑当前时序语句。|Only the current timing statement can be edited here.
谱面检查|Chart check
时序 / 滚速事件|Timing / scroll events
歌曲工程 / 四个难度|Song project / four difficulties
背景 / 窗口|Background / window
拟合总物量；判定时刻与首尾判定规则尚未完全确定。|Fitted total count; tick times and endpoint rules are not fully established.
选中物件以查看候选判定数与起始 BPM。|Select a note to inspect provisional ticks and starting BPM.
导出当前播放头估算报告…|Export count estimate at playhead...
估算模型 A：Hold 按头部、周期判定与尾部计数；Sky 按开始与周期判定计数。具体判定时刻及跨 BPM 行为尚未确定。track 只影响视觉滚速。|Estimate A counts Hold heads, periodic ticks and tails, and Sky starts and periodic ticks. Exact tick times and cross-BPM behavior remain unverified. track affects visual scroll speed only.
估算总物量：|Estimated total count:
匹配实测参考：|Measured reference match:
播放头估算 @ |Estimate at playhead @ 
播放头分项：|Playhead breakdown:
选中 ID |Selected ID 
起始 BPM |Starting BPM 
间隔 |Interval 
｜跨 BPM（按起始 BPM 估算）| / crosses BPM (starting-BPM estimate)
关闭|Close
当前相机采用经图像校准的模型参数，50° 视场角为该模型的参考值。|The current camera uses image-calibrated model parameters, with a reference field of view of 50 degrees.
恢复图像校准默认值|Restore image-calibrated defaults
加载相机参数 JSON…|Load camera JSON...
导出相机参数 JSON|Export camera JSON
加载参考 PNG 叠加图…|Load reference PNG overlay...
显示参考叠加图（F1 隐藏操作界面）|Show reference overlay (F1 hides controls)
参考视频视速为 4.50，截图视速为 1.00。比较物件间距前请先统一视速。|The reference video uses scroll speed 4.50; screenshots use 1.00. Match scroll speed before comparing note spacing.
加载本地文件：输入完整路径|Load a local file: enter the full path
本地文件路径|Local file path
加载路径中的 SPC|Load SPC from path
加载路径中的音频|Load audio from path
滚轮向上：下一条网格线；向下：上一条网格线。|Wheel up: next grid line. Wheel down: previous grid line.
滚轮每格输入单位|Input units per wheel step
播放连续进行。启用节拍网格时，暂停或定位会将播放头吸附到网格线，原始语句中的时间保持原值。|Playback is continuous. With the beat grid enabled, pause and seek snap the playhead to a grid line. Source timestamps stay unchanged.
垂直视场角：|Vertical field of view:
相机俯角：|Camera pitch:
相机高度：|Camera height:
相机后移：|Camera distance:
侧轨高度：|Side lane rise:
侧轨横距：|Side lane run:
空域高度：|Sky height:
Flick 固定高度：|Flick fixed height:
Flick 斜端面偏移：|Flick cap skew:
参考图透明度：|Reference image opacity:
当前输入单位：|Current input unit:
；不足一格的输入会累积。|; partial steps accumulate.
上次滚轮输入：|Last wheel input:
 格网格线| grid steps
整合插件最近三版；可在设置中随时查看。|Latest three integrated plugin releases. Reopen from Settings.
知道了|Got it
谱面或 timinggroup 已切换，请重新选择。|The chart or timinggroup changed. Select it again.
 拍| beats
同刻存在多条事件；保留所有语句，按列表顺序以后者生效。|Events share a timestamp. All statements are retained; the last in list order takes effect.
从配对明文补入 BPM / 拍号…|Import BPM / meter from paired text...
track 用法说明|About track events
track 只改变本组物件滚速，不改变歌曲播放速度。0 为停流，负数为反向滚动。BPM 控制本组节拍网格；统一流速可辅助编辑停流和反向段。|track changes this group's note scroll speed, not music playback speed. Zero stops scrolling; negative values reverse it. BPM controls this group's beat grid. Uniform scroll speed helps edit stopped or reversed sections.
高级：未识别内容提示（|Advanced: unrecognized content (
谱面已切换，请重新操作。|The chart changed. Try the action again.
请暂停播放后编辑。|Pause playback before editing.
谱面已切换。|The chart changed.
谱面或 timinggroup 已切换。|The chart or timinggroup changed.
松开右键\n空白处取消|Release RMB\nBlank = cancel
选区操作 · 在按钮上松开右键|Selection / release RMB on a button
快捷菜单 · 在按钮上松开右键|Quick menu / release RMB on a button
点立得 · 停留 0.5 秒展开，松开选择|Quick Place / hover 0.5s to expand; release to select
Flick 方向 · 松开选择|Flick direction / release to select
默认宽度 · 仅中央四轨生效|Default width / four center lanes only
网格线|Beat grid
点立得|Quick Place
剪贴|Cut
退出|Exit
 轨宽| lanes
开启网格线|Grid ON
关闭网格线|Grid OFF
对齐网格线|Align to grid
不创建|No creation
向左 / 黄色|Left / Yellow
向右 / 绿色|Right / Green
1 轨宽|1 lane
2 轨宽|2 lanes
3 轨宽|3 lanes
4 轨宽|4 lanes
";
   foreach(string row in rows.Split('\n')){var pair=row.TrimEnd('\r').Split('|');if(pair.Length==2)Words[pair[0].Replace("\\n","\n")]=pair[1].Replace("\\n","\n");}
  }
 }
}
