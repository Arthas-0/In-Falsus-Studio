using System;
using System.Linq;
using System.Collections.Generic;
namespace InFalsusStudio.NativeEditorUI {
 public static class UILocalization {
  static readonly Dictionary<string,string> Words=new Dictionary<string,string>();
  static readonly KeyValuePair<string,string>[] Fragments;
  static UILocalization(){
   string rows=@"
设置|Settings
显示|Display
音频|Audio
相机|Camera
操作|Controls
参数|Properties
参数高级|Advanced properties
时序|Timing
时序事件|Timing events
工程|Song project
工具|Tools
检查|Chart check
帮助|Help
网格|Grid
放置|Placement
编辑|Edit
背景|Background
波形|Waveform
循环|Loop
物件参数|Note properties
编辑操作|Edit actions
谱面工具|Chart tools
音频波形|Audio waveform
网格与吸附|Grid & snapping
放置默认值|Placement defaults
选中过滤|Pick filter
选取过滤|Pick filter
段落过滤|Range filter
段落选中过滤|Range selection filter
界面语言|Language
自动播放：开|Autoplay: ON
自动播放：关|Autoplay: OFF
自动播放|Autoplay
开|ON
关|OFF
自动游玩光标|Autoplay cursor
保存 · Ctrl + S|Save · Ctrl + S
正式播放 · 隐藏编辑界面|Play · hide editor UI
撤销|Undo
重做|Redo
已保存|Saved
待保存|Unsaved
· 已保存|· Saved
◇ 待保存|◇ Unsaved
点立得 · 右键选择物件|Point Mode · right-click menu
段落选择|Range selection
复制与批量编辑|Copy & batch edit
镜像选中物件|Mirror selected notes
网格与吸附设置|Grid & snapping
空域放置默认值|Air-note placement defaults
操作帮助|Help & shortcuts
退出制谱器|Exit editor
切换难度 · |Switch difficulty ·
基准 BPM · 视图|Reference BPM · View
音频偏移|Audio offset
当前时间|Current time
视速|Scroll speed
播放速度|Playback rate
时间吸附|Time snapping
时间吸附：|Time snapping:
选取：|Pick:
选取全部|Pick all
仅选地面|Ground notes only
仅选空域|Air notes only
全部物件|All notes
地面物件|Ground notes
空域物件|Air notes
地面|Ground
空域|Air
全部|All
选中过滤 · 可多选|Pick filter · multi-select
段落选中过滤 · 可多选|Range filter · multi-select
全选|Select all
全部取消|Clear all
地面 Tap|Ground Tap
侧轨 Tap · Shift / Space|Side Tap · Shift / Space
地面 Hold|Ground Hold
侧轨 Hold · Shift / Space|Side Hold · Shift / Space
SkyArea · 紫条|SkyArea · purple strip
Flick 左 · 黄色|Flick left · yellow
Flick 右 · 绿色|Flick right · green
独立应用于框选和两次点击的段落选择；长条仍须首尾完整落入区间。|Applies independently to box and time-range selections. Both ends of a long note must remain inside the range.
应用于鼠标点选、重叠物件切换、时间轴选择和 Ctrl+A。可同时允许多种物件；已选物件保持原样。|Applies to clicks, overlapping candidates, timeline picks and Ctrl+A. Enable several types together; existing selections are retained.
显示物件|Show notes
裁去已播放物件|Clip past notes
节拍网格|Beat grid
小节线|Measure lines
空域地面投影|Air-note ground projections
单调时间布局|Linear time layout
其他显示|Other display options
连击数字|Combo number
打击光效|Hit feedback
背景与窗口|Background & window
音乐音量|Music volume
音乐音量　|Music volume
打击音|Hit sounds
启用打击音|Enable hit sounds
总体音量|Master volume
攻击音量|Attack volume
持续音量|Sustain volume
头音|Head attacks
SkyArea 头音|SkyArea head attack
每组首段|First segment
关闭头音|OFF
Hold 头部攻击音|Hold head attacks
收起音频诊断|Hide audio diagnostics
音频诊断|Audio diagnostics
复制诊断|Copy diagnostics
打开相机参数|Camera parameters
滚轮与导航参数|Wheel & navigation
更新公告|Release notes
简体中文|Chinese
相机参数|Camera parameters
背景参数|Background parameters
操作参数|Navigation parameters
‹ 返回设置|‹ Back to settings
‹ 返回显示设置|‹ Back to display
节拍细分|Beat subdivision
显示节拍网格|Show beat grid
始终显示小节线|Always show measure lines
浏览组号|Group to browse
‹ 上一组|‹ Previous
下一组 ›|Next ›
仅显示当前组|Current group only
浏览切换只用于查看物件；物件的 groupid 在物件参数中编辑。|Browsing only changes the view. Edit a note's group ID in its properties.
空域细分|Air subdivision
Tap 默认宽度|Default Tap width
Hold 默认宽度|Default Hold width
SkyArea 默认宽度|Default SkyArea width
左缓动|Left easing
右缓动|Right easing
轨|lanes
几何|Geometry
分组|Grouping
时间|Time
开始时间|Start time
结束时间|End time
起点位置|Start position
终点位置|End position
起点宽度|Start width
终点宽度|End width
统一宽度|Both widths
中心位置|Center position
宽度|Width
起始轨道|Start lane
中央轨宽度|Ground width
线性|Linear
方向|Direction
左 · 黄|Left · yellow
右 · 绿|Right · green
跳到开始时间|Jump to start
跳至起点|Jump to start
原始语句 / 高级参数|Source / advanced
收起原始语句|Hide source
原始语句 · Enter / 失焦应用|Source · Enter / blur to apply
原始语句编辑仅支持单个物件。|Select one note to edit its source statement.
原始语句与高级参数|Source & advanced
源语句已变化，请重新选择。|The source changed. Select the note again.
拖动标题移动 · Enter / 失焦应用 · Ctrl 多选|Drag title · Enter / blur · Ctrl multi-select
多类型选择|Mixed selection
 个物件| notes
 个| notes
已选中多种类型。选择一类物件后编辑其参数；不会更改其他类型的物件。|Several note types are selected. Choose a type to edit its parameters; the other types will stay unchanged.
复制|Copy
粘贴|Paste
复制副本|Duplicate
镜像|Mirror
删除|Delete
删除选中物件|Delete selected notes
段落选择|Range selection
对齐到网格|Align to grid
组浏览|Group browser
分组、拆分与坐标修复|Groups, splitting & coordinates
显示波形|Show waveform
细分须为整数|Subdivision must be an integer.
宽度须为 1～4|Width must be between 1 and 4.
轨道与宽度|Lane & width
选中物件后显示参数。Ctrl 多选，Alt 调整手柄。|Select a note to show its properties. Ctrl selects more; Alt adjusts handles.
选中物件后显示对应参数。\n按 Ctrl 可多选；Alt 调整手柄。|Select notes to view their properties. Ctrl for multi-select; Alt for handles.
轨道与物件|Track & notes
显示音符|Show notes
裁去已播放的音符|Clip past notes
轨道连击数字（预计）|Estimated combo
打击光效 / 粒子|Hit feedback / particles
网格与时间|Grid & time
节拍网格与时间吸附|Beat grid & time snapping
单调时间布局（仅视图）|Linear time layout (view only)
展开已有背景 / 窗口选项|Background / window options
这里整理选项入口；现有视觉参数保持原值。|These options control the view; existing visual settings are retained.
音乐|Music
滚轮与定位|Wheel & navigation
语言与快捷键|Language & shortcuts
界面语言：简体中文 → English|Language: Chinese → English
UI Language: English → 简体中文|Language: English → Chinese
Ctrl + S 保存　·　Ctrl 多选　·　Alt 手柄\n鼠标右键：点立得菜单　·　F11 恢复窗口|Ctrl+S: save · Ctrl: multi-select · Alt: handles\nRMB: Point Mode menu · F11: restore window
播放方式|Playback
空格预览保留边栏。点击播放进入正式播放，编辑层淡出。Esc 返回编辑。|Space previews with the editor visible. Play hides the editor. Esc returns to editing.
打开完整操作帮助|Open full help
单个物件|Single note
批量 × |Batch ×
混合值留空，只修改主动编辑的字段。|Mixed values stay blank. Only edited fields are changed.
开始时间 / ms|Start time / ms
结束时间 / ms|End time / ms
时间 / ms|Time / ms
空域几何|Air geometry
中心 X / %|Center X / %
宽度 / %|Width / %
Flick 方向|Flick direction
首尾位置与宽度|Endpoint positions & widths
起点 X / %|Start X / %
终点 X / %|End X / %
起点宽度 / %|Start width / %
终点宽度 / %|End width / %
统一首尾宽度 / %|Both widths / %
边缘缓动|Edge easing
左边缘|Left edge
右边缘|Right edge
Group ID（留空 = 未指定）|Group ID (blank = unspecified)
0 也是有效组号。打开面板不会修改分组或物件顺序。|Zero is a valid group ID. Opening this panel does not change groups or note order.
移除组号指定|Remove explicit group
物件操作|Note actions
高级：原始语句与诊断|Advanced: source & diagnostics
选取范围|Selection scope
当前选择：|Selected:
按时间选择段落|Select a time range
对齐选中物件到网格|Align selected notes to grid
新建|New
加载 SPC|Load SPC
另存为|Save as
选择音频|Choose audio
打开歌曲文件夹|Open song folder
新建 SkyArea 宽度 / %|New SkyArea width / %
空域坐标细分 1/N|Air subdivision 1/N
左边缘缓动|Left edge easing
右边缘缓动|Right edge easing
新建 SkyArea 默认不指定组号；在物件参数页按需设置。|New SkyAreas have no explicit group. Set a group in note properties when needed.
设置 / |Settings /
数值：Enter / 失焦应用　·　保存后写入文件|Numbers: Enter / blur to apply · Save writes files
1 / N 拍|1 / N beat
显示与吸附|Display & snapping
节拍网格 / 时间吸附|Beat grid / time snapping
浏览组|Group browser
上一组起点|Previous start
下一组起点|Next start
这里用于浏览。物件 groupid 仍在物件参数中编辑。|This panel browses groups. Edit group IDs in note properties.
显示详细波形时间轴|Detailed waveform timeline
分组与几何|Groups & geometry
完整谱面工具|Full chart tools
分组、拆分与修复继续使用原有工具。|Grouping, splitting and repairs use the original tools.
保存|Save
0～5|0–5
";
   foreach(string row in rows.Split('\n')){var parts=row.TrimEnd('\r').Split('|');if(parts.Length==2)Words[parts[0].Replace("\\n","\n")]=parts[1].Replace("\\n","\n");}
   Fragments=Words.OrderByDescending(p=>p.Key.Length).ToArray();
  }
  public static string Get(string text,bool chinese){
   if(chinese||string.IsNullOrEmpty(text))return text;
   string translated;if(Words.TryGetValue(text,out translated))return translated;
   foreach(var pair in Fragments)if(text.Contains(pair.Key))text=text.Replace(pair.Key,pair.Value);
   return text;
  }
 }
}
