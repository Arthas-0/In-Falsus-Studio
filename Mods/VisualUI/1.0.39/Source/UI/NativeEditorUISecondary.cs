using System;
using UnityEngine;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  // Include nested drawing helpers: adapting only the page entry leaves their controls unchanged.
  static readonly string[] LegacyAdaptedMethods={"DrawTimingBrowser","DrawChartCheck","DrawSongPanel","DrawHelp07","DrawTrackHelp","DrawSongDetailsInputs","DrawDesignerInput","DrawComboDiagnostics","DrawAuthoringTools","DrawPresentationOptions","WindowPreset","WindowModeButton","DrawNavigationOptions","DrawCalibration","Slider","DrawNotePanel","LiveChoices","LiveGround"};
  static string LegacyPanelTitle(string page,string tab){
   if(page=="设置")return tab=="背景参数"?"背景与窗口":tab=="相机参数"?"相机参数":"滚轮与导航参数";
   if(page=="工具")return "分组、拆分与坐标修复";
   if(page=="参数高级")return "原始语句与高级参数";
   return page=="时序"?"时序":page=="检查"?"谱面检查":page=="帮助"?"操作帮助":"工程";
  }
  static string LegacyPanelMethod(string page,string tab){
   if(page=="设置")return tab=="背景参数"?"DrawPresentationOptions":tab=="相机参数"?"DrawCalibration":"DrawNavigationOptions";
   if(page=="工具")return "DrawAuthoringTools";
   if(page=="参数高级")return "DrawNotePanel";
   return page=="时序"?"DrawTimingBrowser":page=="检查"?"DrawChartCheck":page=="帮助"?"DrawHelp07":"DrawSongPanel";
  }
  void ReturnFromLegacy(){Queue(delegate{
   if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;
   if(panel=="设置")settingsTab=settingsTab=="相机参数"?"相机":settingsTab=="操作参数"?"操作":"显示";
   else if(panel=="工具")toolsLegacy=false;
   else if(panel=="参数高级"){panel="参数";advancedNote=false;}
   panelScroll=Vector2.zero;
  });}
  void DrawLegacyFragment(Rect body,string method){
   GUILayout.BeginArea(body,GUIStyle.none);try{
    panelScroll.x=0;panelScroll=GUILayout.BeginScrollView(panelScroll,false,true,GUIStyle.none,GUI.skin.verticalScrollbar);
    try{GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(100,legacyWidth)));try{Call(boot,method);}finally{GUILayout.EndVertical();}}
    finally{GUILayout.EndScrollView();}
   }finally{GUILayout.EndArea();}
  }
  static void LegacyBeginHorizontal(GUILayoutOption[] options){if(LegacyActive&&I.legacyStacked)GUILayout.BeginVertical(options);else GUILayout.BeginHorizontal(options);}
  static void LegacyEndHorizontal(){if(LegacyActive&&I.legacyStacked)GUILayout.EndVertical();else GUILayout.EndHorizontal();}
  static float LegacySlider(float value,float min,float max,GUILayoutOption[] options){
   if(!LegacyActive)return GUILayout.HorizontalSlider(value,min,max,options);
   Rect row=GUILayoutUtility.GetRect(0,Mathf.Max(25,32*I.scale),GUILayout.ExpandWidth(true));
   return GUI.HorizontalSlider(new Rect(row.x+2*I.scale,row.center.y-8*I.scale,row.width-4*I.scale,17*I.scale),value,min,max);
  }
  string SecondaryText(string text){
   if(legacyStacked&&(text=="Sky 自动分组"||text=="Auto-group SkyAreas"))return "相接 SkyArea 分组（仅未分组）";
   if(legacyStacked&&(text=="按时间和首尾连接分配组号（含前后 2ms 容差）；新建 Sky 仍不指定组。"||text=="Assign groups by time and touching endpoints (±2ms). New SkyAreas still omit groups."))return "仅为未指定 groupid（含旧 -1）的 SkyArea 分组；首尾 ±2ms 且横向接触。保留已有分组，不跨时间组连接。";

   if(!legacyStacked||!Val<bool>(boot,"chinese")||string.IsNullOrEmpty(text))return text;
   switch(text){
    case "FOV 50 degrees is a chosen model gauge.\nThe camera is not claimed to be the game's original camera.":return "当前相机采用经图像校准的模型参数，50° 视场角为该模型的参考值。";
    case "Restore image-calibrated defaults":return "恢复图像校准默认值";
    case "Load calibration JSON...":return "加载相机参数 JSON…";
    case "Write calibration JSON to persistent data":return "导出相机参数 JSON";
    case "Load reference PNG overlay...":return "加载参考 PNG 叠加图…";
    case "Reference overlay (F1 hides controls only)":return "显示参考叠加图（F1 隐藏操作界面）";
    case "Video is a reference at SPEED 4.50.\nScreenshots are SPEED 1.00. Do not compare spacing without matching this setting.":return "参考视频视速为 4.50，截图视速为 1.00。比较物件间距前请先统一视速。";
    case "Standalone: enter a full local file path":return "加载本地文件：输入完整路径";
    case "Local path":return "本地文件路径";
    case "Load SPC path":return "加载路径中的 SPC";
    case "Load audio path":return "加载路径中的音频";
    case "Wheel: up = next grid line; down = previous":return "滚轮向上：下一条网格线；向下：上一条网格线。";
    case "Input units per detent":return "滚轮每格输入单位";
    case "3 (Windows)":return "3（Windows）";
    case "Playback is continuous. With Beat grid ON, pause/seek returns the playhead to a grid line. Raw source time is not rounded.":return "播放连续进行。启用节拍网格时，暂停或定位会将播放头吸附到网格线，原始语句中的时间保持原值。";
   }
   string[] from={"Vertical FOV: ","Pitch down: ","Camera height: ","Camera back: ","Side rise: ","Side run: ","Sky height: ","Flick fixed height (width-independent): ","Flick diagonal cap skew: ","Reference alpha: "};
   string[] to={"垂直视场角：","相机俯角：","相机高度：","相机后移：","侧轨高度：","侧轨横距：","空域高度：","Flick 固定高度：","Flick 斜端面偏移：","参考图透明度："};
   for(int i=0;i<from.Length;i++)if(text.StartsWith(from[i],StringComparison.Ordinal))return to[i]+text.Substring(from[i].Length);
   return text.Replace("Current scale: ","当前输入单位：").Replace(" | partial deltas are accumulated.","；不足一格的输入会累积。").Replace("Last wheel delta: ","上次滚轮输入：").Replace(" grid steps"," 格网格线");
  }
  float ParagraphHeight(string text,float design,float width){return UiFontRaster.Wrap(T(text),CNFace,FontPixels(design),width).Length*Mathf.Max(FontPixels(design)+4,design*1.8f*scale);}
  void DrawReadableReleaseNotes(){
   var oldSkin=GUI.skin;float oldScale=scale;scale=Mathf.Max(.8f,scale);
   try{
    SetupSkin();float s=scale,w=Mathf.Min(1040*s,Screen.width-40*s),h=Mathf.Min(940*s,Screen.height-40*s);
    Rect frame=new Rect((Screen.width-w)*.5f,(Screen.height-h)*.5f,w,h);
    Fill(new Rect(0,0,Screen.width,Screen.height),new Color(0,0,0,.7f));Fill(frame,Css("#0c202bfa"));Border(frame,Css("#48636f"),s);
    UiText(new Rect(frame.x+28*s,frame.y+22*s,w-100*s,42*s),"更新公告",24,Css("#c9e9ee"));
    UiText(new Rect(frame.x+28*s,frame.y+66*s,w-56*s,27*s),"InFalsus Studio Visual & UI 1.0.26",15,Css("#83a9ba"));
    if(FlatButton(new Rect(frame.xMax-55*s,frame.y+24*s,32*s,36*s),"×",26,false,false))Command("CloseReleaseNotes");
    Fill(new Rect(frame.x+28*s,frame.y+104*s,w-56*s,s),Css("#35515c"));
    Rect viewport=new Rect(frame.x+28*s,frame.y+120*s,w-48*s,h-236*s);float contentWidth=viewport.width-24*s,total=0;
    bool chinese=Val<bool>(boot,"chinese");var entries=IntegratedReleaseNotes;
    foreach(var entry in entries)total+=52*s+ParagraphHeight(chinese?entry.Chinese:entry.English,17,contentWidth)+24*s;
    Vector2 scroll=GUI.BeginScrollView(viewport,Val<Vector2>(boot,"releaseNotesScroll"),new Rect(0,0,contentWidth,Mathf.Max(viewport.height,total)),false,true);
    try{float y=0;foreach(var entry in entries){UiText(new Rect(0,y,contentWidth,39*s),"v"+entry.Version,22,Css("#9adce1"));y+=52*s;string text=chinese?entry.Chinese:entry.English;float height=ParagraphHeight(text,17,contentWidth);UiParagraph(new Rect(0,y,contentWidth,height),text,17,Css("#a6c3d1"));y+=height+24*s;}}
    finally{GUI.EndScrollView();}Set(boot,"releaseNotesScroll",scroll);
    UiParagraph(new Rect(frame.x+28*s,frame.yMax-101*s,w-56*s,42*s),chinese?"整合插件最近三版；可在设置中随时查看。":"Latest three integrated plugin releases. Reopen from Settings.",14,Css("#83a9ba"));
    if(FlatButton(new Rect(frame.xMax-208*s,frame.yMax-52*s,180*s,35*s),chinese?"知道了":"Got it",17))Command("CloseReleaseNotes");
    var e=Event.current;if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){Command("CloseReleaseNotes");e.Use();}else if(e.isMouse||e.isKey||e.type==EventType.ScrollWheel)e.Use();
   }finally{GUI.skin=oldSkin;scale=oldScale;}
  }
 }
}
