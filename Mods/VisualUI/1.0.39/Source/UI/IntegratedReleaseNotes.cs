using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  static readonly ReleaseNotesEntry[] IntegratedReleaseNotes={
   new ReleaseNotesEntry("1.0.39",
    "• 键位字符恢复为修改前的制谱器原有显示，撤销自定义字体、拉伸、颜色和发光处理。\n• 移除侧边音符显示增强及设置项。保留判定显示和其他已有功能。",
    "• Restores the editor's original key labels, removing custom font, stretch, color and glow processing.\n• Removes side-note guides and their setting. Retains judgement display and other existing features."),
   new ReleaseNotesEntry("1.0.38",
    "• 侧边音符引导改为跟随音符头部的同一坐标映射，修正本体与制谱器相反的纵深方向及左右侧轨对齐。\n• 长条头部击打后移除侧边引导，不再把光效固定在判定线持续显示。\n• 修正键位文字半透明颜色换算，去掉过亮光晕，保留本体字体、颜色比例、透明度与透视拉伸。",
    "• Side-note guides now share the note-head coordinate mapping, including the reversed native/editor depth direction and left/right alignment.\n• Removes Hold head guides after the head is hit instead of pinning them to the judgement line.\n• Corrects translucent key-label color conversion to remove excessive halos, retaining the native font, color ratios, opacity and perspective stretch."),
   new ReleaseNotesEntry("1.0.37",
    "• 修正侧边音符引导：补全本体运行时左右旋转、音符锚点及远近缩放，避免重复叠加侧轨斜面。\n• 所有判定图统一置于天空判定线上方，按本体运行时 3840×2160 逻辑画布与 512 像素图片尺寸适配分辨率，修正 EXACT+ 闪光透明度和位移。\n• 键位文字恢复本体 HDR 填充颜色、填充透明度与描边颜色，并适配 Gamma/Linear 色彩空间以保留辉光，保留字体、透视和拉伸比例。",
    "• Corrects side-note guides using the native runtime left/right rotations, note anchors and distance scaling, without applying the side slope twice.\n• Places all judgement artwork above the sky judgement line using the native runtime 3840x2160 logical canvas and 512-pixel artwork size. Corrects EXACT+ burst opacity and motion.\n• Restores native HDR key-label fill, fill opacity and stroke color, with Gamma/Linear conversion to preserve glow, while retaining the font, perspective and stretch ratio."),

  };
 }
}

