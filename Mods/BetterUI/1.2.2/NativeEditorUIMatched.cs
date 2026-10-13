using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  const string CNFace="Chiron Hei HK N",NumFace="Bai Jamjuree Medium";
  readonly Dictionary<string,UiGlyph> uiGlyphs=new Dictionary<string,UiGlyph>();
  MethodInfo glyphMethod;
  bool audioDetails,onlyCurrentGroup;
  float contentY;
  struct UiGlyph{public Texture2D Image;public float Advance;public int Top,Bottom;public bool Ink;}
  string PathForIcon(string key){return Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location),"Icons",key+".png");}
  static Color Css(string code){Color color;ColorUtility.TryParseHtmlString(code,out color);return color;}
  UiGlyph UiLetter(string face,char ch){
   string key=face+":"+ch;UiGlyph result;if(uiGlyphs.TryGetValue(key,out result))return result;
   if(glyphMethod==null){var assembly=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="NativeVisualMod");glyphMethod=assembly.GetType("InFalsusStudio.NativeVisual.GdiText").GetMethod("Glyph",F);}
   var value=glyphMethod.Invoke(null,new object[]{face,ch,new[]{CNFace,"OT-PUD Shin Go Pr6N R","Supreme"}});
   var type=value.GetType();result=new UiGlyph{Image=(Texture2D)type.GetField("Texture",F).GetValue(value),Advance=Convert.ToSingle(type.GetField("Advance",F).GetValue(value)),Top=(int)type.GetField("InkTop",F).GetValue(value),Bottom=(int)type.GetField("InkBottom",F).GetValue(value),Ink=(bool)type.GetField("HasInk",F).GetValue(value)};
   uiGlyphs[key]=result;return result;
  }
  void UiText(Rect rect,string text,float size,Color color,string face=CNFace,int align=0){
   if(Event.current.type!=EventType.Repaint||string.IsNullOrEmpty(text))return;text=T(text);
   var glyphs=text.Select(c=>UiLetter(face,c)).ToArray();float natural=glyphs.Sum(g=>g.Advance),s=Mathf.Min(size*scale/72f,rect.width/Mathf.Max(1,natural));
   int top=glyphs.Where(g=>g.Ink).Select(g=>g.Top).DefaultIfEmpty(0).Min(),bottom=glyphs.Where(g=>g.Ink).Select(g=>g.Bottom).DefaultIfEmpty(0).Max();
   float x=rect.x+(align==1?(rect.width-natural*s)*.5f:align==2?rect.width-natural*s:0),y=rect.center.y-(bottom-top)*s*.5f-top*s;
   var old=GUI.color;GUI.color=old*color;foreach(var glyph in glyphs){GUI.DrawTexture(new Rect(x-8*s,y,glyph.Image.width*s,96*s),glyph.Image);x+=glyph.Advance*s;}GUI.color=old;
  }
  void UiParagraph(Rect rect,string text,float size,Color color){
   text=T(text);float s=size*scale/72f,y=rect.y;string line="";float width=0;
   foreach(char ch in text){float w=UiLetter(CNFace,ch).Advance*s;if(ch=='\n'||(line.Length>0&&width+w>rect.width)){UiText(new Rect(rect.x,y,rect.width,size*1.8f*scale),line,size,color);y+=size*1.8f*scale;line="";width=0;if(ch=='\n')continue;}line+=ch;width+=w;}
   if(line.Length>0)UiText(new Rect(rect.x,y,rect.width,size*1.8f*scale),line,size,color);
  }
  bool FlatButton(Rect rect,string text,float size=16,bool active=false,bool boxed=true){
   bool hover=rect.Contains(Event.current.mousePosition);
   if(boxed){Fill(rect,Css(hover?"#173743":"#102b36"));Border(rect,Css(active?"#82cfd6":"#375665"),scale);}
   else if(hover)Fill(rect,Css("#173743"));
   bool pressed=GUI.Button(rect,GUIContent.none,GUIStyle.none);UiText(rect,text,size,Css(active?"#bbf7f5":"#9dbfcd"),CNFace,1);return pressed;
  }
  string UiInput(Rect rect,string key,string value,Action<string> commit,float size=26,int align=2,bool boxed=true,bool empty=false){
   if(boxed){Fill(rect,Css("#091923"));Fill(new Rect(rect.x,rect.yMax-scale,rect.width,scale),Css("#4b7481"));}
   var previous=GUI.skin.textField;var field=new GUIStyle(number);field.normal.background=null;field.focused.background=null;field.hover.background=null;field.active.background=null;field.border=new RectOffset();int padding=boxed?(int)(5*scale):0;field.padding=new RectOffset(padding,padding,0,0);field.fontSize=Mathf.Max(11,(int)(size*scale));field.alignment=align==2?TextAnchor.MiddleRight:TextAnchor.MiddleLeft;
   Color ink=Css("#d3edf0");field.normal.textColor=ink;field.focused.textColor=ink;field.hover.textColor=ink;field.active.textColor=ink;field.onNormal.textColor=ink;field.onHover.textColor=ink;field.onActive.textColor=ink;field.onFocused.textColor=ink;GUI.skin.textField=field;string entered;
   try{entered=Edit(rect,key,value,commit,empty);}finally{GUI.skin.textField=previous;}
   bool focused=GUI.GetNameOfFocusedControl()==key;if(focused){int control=GUIUtility.keyboardControl;numericControls.Add(control);if(numericControl!=control||numericKey!=key){var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),control);editor.SelectAll();if(Event.current.type==EventType.MouseDown)numericSelectOnMouseUp=control;numericControl=control;numericKey=key;}if(Event.current.type==EventType.MouseUp&&numericSelectOnMouseUp==control){((TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),control)).SelectAll();numericSelectOnMouseUp=0;}}
   if(!focused&&Event.current.type==EventType.Repaint){Fill(rect,Css(boxed?"#091923":"#091c25"));if(boxed)Fill(new Rect(rect.x,rect.yMax-scale,rect.width,scale),Css("#4b7481"));UiText(new Rect(rect.x+padding,rect.y,rect.width-2*padding,rect.height),entered??value,size,ink,NumFace,align);}
   return entered;
  }
  Rect ContentRow(float height){Rect row=new Rect(0,contentY,405*scale,height*scale);contentY+=height*scale;return row;}
  void RowLine(Rect row){Fill(new Rect(row.x,row.yMax-scale,row.width,scale),Css("#2c4652"));}
  void MatchedField(string title,string key,string value,Action<string> commit,string unit="",bool empty=false){
   Rect row=ContentRow(79);RowLine(row);UiText(new Rect(0,row.y,245*scale,row.height),title,17,Css("#a6c3d1"));
   float unitWidth=unit.Length==0?0:41.2f*scale;
   Rect input=new Rect(row.xMax-unitWidth-95*scale,row.y+19*scale,95*scale,40*scale);UiInput(input,key,value,commit,26,2,true,empty);
   if(unit.Length>0)UiText(new Rect(input.xMax+12*scale,row.y,unitWidth-12*scale,row.height),unit,14,Css("#7b9cae"),"Supreme");
  }
  void MatchedToggle(string title,bool current,Action<bool> change){
   Rect row=ContentRow(62.796875f);RowLine(row);UiText(new Rect(0,row.y,row.width-48*scale,row.height),title,17,Css("#a6c3d1"));
   Rect toggle=new Rect(row.xMax-32*scale,row.center.y-8.5f*scale,32*scale,17*scale);Border(toggle,Css("#6aacb4"),scale);
   Fill(new Rect(current?toggle.xMax-11*scale:toggle.x+3*scale,toggle.y+3*scale,8*scale,9*scale),Css(current?"#bcf7f5":"#577582"));
   if(GUI.Button(row,GUIContent.none,GUIStyle.none))change(!current);
  }
  void MatchedFlag(string title,string field){MatchedToggle(title,Val<bool>(boot,field),delegate(bool value){Queue(delegate{Set(boot,field,value);});});}
  void MatchedLink(string title,Action action){
   Rect row=ContentRow(62.796875f);RowLine(row);UiText(new Rect(0,row.y,row.width-30*scale,row.height),title,17,Css("#a6c3d1"));UiText(new Rect(row.xMax-20*scale,row.y,20*scale,row.height),"›",21,Css("#83a9ba"),CNFace,1);if(GUI.Button(row,GUIContent.none,GUIStyle.none))action();
  }
  void MatchedSection(string title){contentY+=24*scale;Rect row=ContentRow(32);UiText(row,title,14,Css("#7c9faf"));Fill(new Rect(0,row.yMax-scale,row.width,scale),Css("#2c4652"));}
  void MatchedSlider(string title,float current,Action<float> change){
   Rect row=ContentRow(79);RowLine(row);UiText(new Rect(0,row.y,245*scale,40*scale),title,17,Css("#a6c3d1"));UiText(new Rect(row.xMax-85*scale,row.y,85*scale,40*scale),Mathf.RoundToInt(current*100)+"%",22,Css("#d3edf0"),NumFace,2);
   float next=GUI.HorizontalSlider(new Rect(0,row.y+49*scale,row.width,17*scale),current,0,1);if(next!=current)change(next);
  }
  void DrawMatchedPanel(){
   if(panel=="时序"||panel=="检查"||panel=="工程"||panel=="帮助"||(panel=="工具"&&toolsLegacy)||(panel=="参数高级")||(panel=="设置"&&(settingsTab=="相机参数"||settingsTab=="背景参数"||settingsTab=="操作参数"))){
    DrawLegacyMatched();return;
   }
   Fill(panelRect,Css("#0c202bfa"));Fill(new Rect(panelRect.x,panelRect.y,scale,panelRect.height),Css("#48636f"));
   GUI.BeginGroup(panelRect);try{
    string title=panel=="网格"?"网格与吸附":panel=="放置"?"放置默认值":panel=="波形"?"音频波形":panel=="参数"?"物件参数":panel=="编辑"?"编辑操作":panel=="工具"?"谱面工具":panel=="选取过滤"?"选中过滤":panel=="段落过滤"?"段落选中过滤":panel;
    UiText(new Rect(28*scale,27*scale,360*scale,39.390625f*scale),title,24,Css("#c9e9ee"));
    if(FlatButton(new Rect(398*scale,27*scale,32*scale,39.390625f*scale),"×",26,false,false))Open(panel);
    Fill(new Rect(28*scale,87.390625f*scale,405*scale,scale),Css("#35515c"));
    float top=88.390625f*scale;
    if(panel=="设置"){
     string[] tabs={"显示","音频","相机","操作"};for(int i=0;i<4;i++){string tab=tabs[i];Rect r=new Rect((28+i*105)*scale,top+24*scale,83*scale,28*scale);if(FlatButton(r,tab,15,settingsTab==tab,false))Queue(delegate{settingsTab=tab;panelScroll=Vector2.zero;});if(settingsTab==tab)Fill(new Rect(r.x,r.yMax+6*scale,r.width,2*scale),Css("#b4efef"));}
     top+=80*scale;
    }
    Rect area=new Rect(28*scale,top,417*scale,panelRect.height-top-27*scale);
    float expected=MatchedContentHeight();
    panelScroll=GUI.BeginScrollView(area,panelScroll,new Rect(0,0,405*scale,Mathf.Max(area.height,expected*scale)),false,false);
    try{contentY=0;if(panel=="网格")MatchedGrid();else if(panel=="组浏览")MatchedGroups();else if(panel=="设置")MatchedSettings();else if(panel=="放置")MatchedPlacement();else if(panel=="参数")MatchedNotes();else if(panel=="编辑")MatchedEditing();else if(panel=="波形")MatchedWave();else if(panel=="工具")MatchedTools();else if(panel=="选取过滤"||panel=="段落过滤")DrawSelectionFilters();}
    finally{GUI.EndScrollView();}
   }finally{GUI.EndGroup();}
  }
  float MatchedContentHeight(){
   if(panel=="网格")return 188.4f;
   if(panel=="组浏览")return 330;
   if(panel=="放置")return 442;
   if(panel=="编辑")return 440;
   if(panel=="波形")return 142;
   if(panel=="工具")return 126;
   if(panel=="选取过滤"||panel=="段落过滤")return 635;
   if(panel=="设置")return settingsTab=="显示"?686:settingsTab=="音频"?(audioDetails?950:748):settingsTab=="相机"?63:252;
   var form=Get(boot,"guiNoteForm") as NoteProperties;if(form==null)return 95;
   return form.Kind==EventKind.SkyArea?1183:form.Kind==EventKind.Flick?685:520;
  }
  void MatchedGrid(){
   MatchedToggle("显示节拍网格",Val<bool>(boot,"showGrid"),delegate(bool value){Command("SetBeatGrid",value);});
   MatchedFlag("始终显示小节线","showMeasureLines");
   MatchedToggle("时间吸附",Val<bool>(boot,"showGrid"),delegate(bool value){Command("SetBeatGrid",value);});
  }
  void MatchedGroups(){
   MatchedField("浏览组号","field_current_sky_group",Get(boot,"currentSkyGroup").ToString(),delegate(string value){Call(boot,"SetCurrentSkyGroup",value);});
   Rect row=ContentRow(81);RowLine(row);if(FlatButton(new Rect(0,row.y+20*scale,195*scale,41*scale),"‹ 上一组"))Command("JumpWithinSkyGroup",-1);if(FlatButton(new Rect(210*scale,row.y+20*scale,195*scale,41*scale),"下一组 ›"))Command("JumpWithinSkyGroup",1);
   MatchedToggle("仅显示当前组",onlyCurrentGroup,delegate(bool value){onlyCurrentGroup=value;});
   UiParagraph(new Rect(0,contentY+14*scale,405*scale,80*scale),"浏览切换只用于查看物件；物件的 groupid 在物件参数中编辑。",13,Css("#7c9dad"));
  }
  void MatchedSettings(){
   if(settingsTab=="显示"){
    MatchedFlag("显示物件","showNotes");MatchedFlag("裁去已播放物件","hidePast");MatchedToggle("节拍网格",Val<bool>(boot,"showGrid"),delegate(bool value){Command("SetBeatGrid",value);});MatchedFlag("小节线","showMeasureLines");MatchedFlag("空域地面投影","showAirShadows");
    MatchedToggle("单调时间布局",Val<bool>(boot,"linearTimeLayout"),delegate(bool value){Queue(delegate{Call(boot,"CancelGesture");Set(boot,"linearTimeLayout",value);});});MatchedSection("其他显示");MatchedFlag("连击数字","showCenterCombo");MatchedFlag("打击光效","showFeedback");MatchedToggle("自动游玩光标",cursorVisibleConfig.Value,delegate(bool value){Queue(delegate{cursorVisibleConfig.Value=value;});});MatchedLink("背景与窗口",delegate{Queue(delegate{settingsTab="背景参数";});});
   }else if(settingsTab=="音频"){
    MatchedSlider("音乐音量",Transport.Volume,delegate(float v){Queue(delegate{Transport.Volume=v;});});
    object sounds=Get(boot,"keySounds");if(sounds==null)return;
    MatchedSection("打击音");MatchedToggle("启用打击音",Read<bool>(sounds,"Enabled"),delegate(bool v){Queue(delegate{Write(sounds,"Enabled",v);});});
    MatchedSlider("总体音量",Read<float>(sounds,"MasterVolume"),delegate(float v){Queue(delegate{Write(sounds,"MasterVolume",v);});});
    MatchedSlider("攻击音量",Read<float>(sounds,"ShotVolume"),delegate(float v){Queue(delegate{Write(sounds,"ShotVolume",v);});});
    MatchedSlider("持续音量",Read<float>(sounds,"HoldVolume"),delegate(float v){Queue(delegate{Write(sounds,"HoldVolume",v);});});
    MatchedSection("头音");Rect row=ContentRow(63);RowLine(row);UiText(new Rect(0,row.y,155*scale,row.height),"SkyArea 头音",17,Css("#a6c3d1"));
    int policy=Convert.ToInt32(Get(boot,"skyAttackPolicy"));for(int i=0;i<2;i++){int p=i+1;Rect r=new Rect((165+i*120)*scale,row.y+16*scale,112*scale,31*scale);if(FlatButton(r,i==0?"每组首段":"关闭头音",14,policy==p))Queue(delegate{Set(boot,"skyAttackPolicy",Enum.ToObject(Field("skyAttackPolicy").FieldType,p));Call(boot,"RebuildKeySoundPlan");});}
    MatchedToggle("Hold 头部攻击音",Val<bool>(boot,"groundHoldAttacks"),delegate(bool v){Queue(delegate{Set(boot,"groundHoldAttacks",v);Call(boot,"RebuildKeySoundPlan");});});
    MatchedLink(audioDetails?"收起音频诊断":"音频诊断",delegate{audioDetails=!audioDetails;});
    if(audioDetails){string diagnostic=Convert.ToString(Prop(sounds,"Diagnostic"));UiParagraph(new Rect(0,contentY+10*scale,405*scale,130*scale),diagnostic,13,Css("#7c9dad"));contentY+=140*scale;MatchedLink("复制诊断",delegate{GUIUtility.systemCopyBuffer=Convert.ToString(Call(boot,"RuntimeStabilityReport010"));});}
   }else if(settingsTab=="相机"){MatchedLink("打开相机参数",delegate{Queue(delegate{settingsTab="相机参数";});});}
   else{MatchedLink("滚轮与导航参数",delegate{Queue(delegate{settingsTab="操作参数";});});LanguageChoice();MatchedLink("操作帮助",delegate{Open("帮助");});MatchedLink("更新公告",delegate{Command("OpenReleaseNotes");});}
  }
  static T Read<T>(object target,string name){return (T)target.GetType().GetField(name,F).GetValue(target);}
  static void Write(object target,string name,object value){target.GetType().GetField(name,F).SetValue(target,value);}
  void MatchedPlacement(){
   MatchedField("空域细分","field_air_split",Prop(Get(boot,"airGrid"),"Division").ToString(),OriginalCommit("<DrawToolDrawer>b__296_8"),"1 / N");
   foreach(string name in new[]{"tapWidth","holdWidth"}){string field=name;MatchedField(name=="tapWidth"?"Tap 默认宽度":"Hold 默认宽度","field_nativeui_"+name,Get(boot,name).ToString(),delegate(string value){int n;if(!int.TryParse(value,out n)||n<1||n>4)throw new FormatException(T("宽度须为 1～4"));Set(boot,field,n);},"轨");}
   MatchedField("SkyArea 默认宽度","field_sky_new_width",Num(Val<double>(boot,"skyWidthPercent")),OriginalCommit("<DrawToolDrawer>b__296_7"),"%");
   MatchedEase("左缓动","skyLeftEase");MatchedEase("右缓动","skyRightEase");
  }
  void MatchedEase(string title,string field){Rect row=ContentRow(63);RowLine(row);UiText(new Rect(0,row.y,120*scale,row.height),title,17,Css("#a6c3d1"));int old=Val<int>(boot,field);for(int i=0;i<3;i++){int next=i;Rect r=new Rect((140+i*87)*scale,row.y+16*scale,78*scale,31*scale);if(FlatButton(r,new[]{"Linear","SineOut","SineIn"}[i],13,old==i))Queue(delegate{Set(boot,field,next);});}}
  void MatchedNotes(){
   var form=Get(boot,"guiNoteForm") as NoteProperties;var values=Get(boot,"batchValues") as Dictionary<string,string>;if(form==null||values==null){UiParagraph(new Rect(0,10*scale,405*scale,70*scale),"选中物件后显示参数。Ctrl 多选，Alt 调整手柄。",17,Css("#a6c3d1"));return;}
   bool previous=GUI.enabled;GUI.enabled=previous&&!Transport.Playing;
   try{
    MatchedSection("时间");NoteField(values,"start","开始时间","ms");if(form.Kind==EventKind.Hold||form.Kind==EventKind.SkyArea)NoteField(values,"end","结束时间","ms");
    if(form.Kind==EventKind.SkyArea){MatchedSection("几何");NoteField(values,"sx","起点位置","%");NoteField(values,"ex","终点位置","%");NoteField(values,"sw","起点宽度","%");NoteField(values,"ew","终点宽度","%");NoteField(values,"bothWidths","统一宽度","%");NoteField(values,"leftEase","左缓动");NoteField(values,"rightEase","右缓动");MatchedSection("分组");NoteField(values,"group","Group ID");}
    else if(form.Kind==EventKind.Flick){NoteField(values,"x","中心位置","%");NoteField(values,"width","宽度","%");NoteField(values,"direction","方向");}
    else{MatchedLink("轨道与宽度",delegate{Queue(delegate{advancedNote=true;});});}
    MatchedSection("操作");MatchedLink("复制",delegate{Command("CopySelection",false);});MatchedLink("删除",delegate{Command("DeleteSelected");});MatchedLink("原始语句与高级参数",delegate{Queue(delegate{advancedNote=true;});});
   }finally{GUI.enabled=previous;}
  }
  void NoteField(Dictionary<string,string> values,string key,string title,string unit=""){string value;values.TryGetValue(key,out value);MatchedField(title,"nativeui_note_"+key,value??"",delegate(string next){Call(boot,"QueueBatchField",key,next);},unit,true);}
  void MatchedEditing(){MatchedLink("复制",delegate{Command("CopySelection",false);});MatchedLink("粘贴",delegate{Command("PasteSelection",false);});MatchedLink("复制副本",delegate{Command("PasteSelection",true);});MatchedLink("镜像",delegate{Command("MirrorSelection");});MatchedLink("删除选中物件",delegate{Command("DeleteSelected");});MatchedLink("段落选择",delegate{Command("BeginRangeSelection");});MatchedLink("对齐到网格",delegate{Command("AlignSelection");});}
  void MatchedWave(){MatchedFlag("显示波形","showDetailTimeline");MatchedSlider("音乐音量",Transport.Volume,delegate(float v){Queue(delegate{Transport.Volume=v;});});}
  void MatchedTools(){MatchedLink("组浏览",delegate{Open("组浏览");});MatchedLink("分组、拆分与坐标修复",delegate{Queue(delegate{toolsLegacy=true;Set(boot,"authoringToolsOpen",true);});});}
  void MatchedLoop(){GUILayout.BeginArea(new Rect(0,0,405*scale,500*scale));try{Call(boot,"DrawLoopControls");}finally{GUILayout.EndArea();}}
  void DrawLegacyMatched(){
   if(panel=="设置"){
    if(settingsTab=="相机参数"){LegacyPanel("DrawCalibration");return;}
    Fill(panelRect,Css("#0c202bfa"));GUILayout.BeginArea(panelRect,GUI.skin.box);try{if(GUILayout.Button(T("‹ 返回设置")))Queue(delegate{settingsTab="显示";});if(settingsTab=="背景参数")Call(boot,"DrawPresentationOptions");else Call(boot,"DrawNavigationOptions");}finally{GUILayout.EndArea();}return;
   }
   if(panel=="参数高级"){Set(boot,"guiNoteSourceExpanded",true);Set(boot,"noteSourceExpanded",true);LegacyPanel("DrawNotePanel");return;}
   DrawPanel();
  }
  void DrawDockedFooter(){
   DockFill(footerRect,false);float y=footerRect.y;
   for(int strip=0;strip<46;strip++)Fill(new Rect(0,y+strip*2*scale,104*scale,2*scale),Color.Lerp(Css("#164550"),Css("#112f39"),strip/45f));Fill(new Rect(103*scale,y,scale,92*scale),Css("#426570"));
   if(IconButton(new Rect(0,y,104*scale,92*scale),"play","正式播放 · 隐藏编辑界面",false))RequestPlay(false);
   if(IconButton(new Rect(104*scale,y,60*scale,92*scale),"undo","撤销",false))Command("Undo",false);
   if(IconButton(new Rect(164*scale,y,60*scale,92*scale),"redo","重做",false))Command("Undo",true);
   if(IconButton(new Rect(224*scale,y,60*scale,92*scale),"save","保存 · Ctrl + S",false))Command("SaveWorkingCopy",false);
   if(Transport==null||Profile==null)return;double bpm=Convert.ToDouble(Get(boot,"viewReferenceBpm"));if(bpm<=0)bpm=Convert.ToDouble(Prop(boot,"InitialBpm"));
   MatchedFooterCell(309,"bpm","基准 BPM · 视图","field_chrome_reference",Num(bpm),OriginalCommit("<DrawExternalChrome>b__55_1"),75,"");
   MatchedFooterCell(529,"offset","音频偏移","field_chrome_delay",Num(Transport.AudioOffsetMs),OriginalCommit("<DrawExternalChrome>b__55_2"),96,"ms");
   MatchedFooterCell(719,"clock","当前时间","field_chrome_time",Math.Truncate((double)Prop(boot,"CurrentMs")).ToString("0",CultureInfo.InvariantCulture),OriginalCommit("<DrawExternalChrome>b__55_3"),128,"ms");
   MatchedFooterCell(969,"speed","视速","field_chrome_scroll",Num(Profile.scrollMultiplier),OriginalCommit("<DrawExternalChrome>b__55_0"),49,"");
   MatchedFooterCell(1174,"rate","播放速度","field_nativeui_rate",Num(Transport.Rate*100),delegate(string v){double n=double.Parse(v,CultureInfo.InvariantCulture);if(double.IsNaN(n)||double.IsInfinity(n)||n<10||n>400)throw new ArgumentOutOfRangeException("播放速度");Transport.SetRate(n/100);},56,"%");
   foreach(float x in new[]{296f,1161f,1396f})Fill(new Rect(x*scale,y+24*scale,scale,44*scale),Css("#355360"));
   bool grid=Val<bool>(boot,"showGrid");if(IconButton(new Rect(1409*scale,y,78*scale,92*scale),"magnet","时间吸附",grid))Command("SetBeatGrid",!grid);
   FilterFooterButton(1490,"range","选中过滤 · 可多选","选取过滤",PickMask);FilterFooterButton(1576,"copy","段落选中过滤 · 可多选","段落过滤",RangeMask);
   Fill(new Rect(1712*scale,y+24*scale,scale,44*scale),Css("#355360"));
   MatchedFooterCell(1770,"grid","节拍细分","field_division07",Prop(boot,"Division").ToString(),delegate(string value){int n;if(!int.TryParse(value,out n))throw new FormatException(T("细分须为整数"));Call(boot,"SetSubdivision",n);},64,"1 / N",50);
   Fill(new Rect(Screen.width-100*scale,y+44*scale,4*scale,4*scale),Css("#7ccab7"));UiText(new Rect(Screen.width-85*scale,y+32*scale,75*scale,28*scale),(bool)Prop(boot,"HasUnsavedEdits")?"待保存":"已保存",13,Css("#7eabb8"));
  }
  void MatchedFooterCell(float x,string icon,string title,string key,string value,Action<string> commit,float inputWidth,string unit,float unitWidth=27){
   float y=footerRect.y;var previous=GUI.color;GUI.color=previous*Css("#77a5b9");GUI.DrawTexture(new Rect((x+25)*scale,y+33*scale,27*scale,27*scale),Icon(icon));GUI.color=previous;
   UiText(new Rect((x+67)*scale,y+19.40625f*scale,170*scale,18.1875f*scale),title,13,Css("#7799aa"));
   Rect input=new Rect((x+67)*scale,y+38.59375f*scale,inputWidth*scale,35*scale);UiInput(input,key,value,commit,26,0,false);
   if(unit.Length>0)UiText(new Rect(input.xMax+6*scale,y+47.09375f*scale,unitWidth*scale,18*scale),unit,13,Css("#6f92a5"),"Supreme");
  }
 }
}
