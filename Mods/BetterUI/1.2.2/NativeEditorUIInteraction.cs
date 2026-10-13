using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using InFalsusStudio;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  ConfigEntry<int> pickMaskConfig,rangeMaskConfig;int rangeDepth,timelineDepth;bool keyboardSelectAll;
  Dictionary<int,SpcEvent> filterNotes=new Dictionary<int,SpcEvent>();EditHistory filterHistory;int filterRevision=-1;
  float railCell=96;Rect waveRect;
  NoteFilterMask PickMask{get{return (NoteFilterMask)(pickMaskConfig.Value&127);}}
  NoteFilterMask RangeMask{get{return (NoteFilterMask)(rangeMaskConfig.Value&127);}}
  void InitInteraction(){
   pickMaskConfig=Config.Bind("Selection","PickTypes",127,"Bitmask: ground tap, side tap, ground hold, side hold, SkyArea, Flick L, Flick R.");
   rangeMaskConfig=Config.Bind("Selection","RangeTypes",127,"Independent multi-type filter for box and complete time-range selections.");
   Patch("SetSelection","BeforeSelection","SelectionChanged");Patch("ClickWorkflow","BeforeRange","AfterRange");
   Patch("HandleDetailTimeline","BeforeTimeline","AfterTimeline");Patch("Select","FilterTimelineSelect",null);
   Patch("DrawDetailTimeline","BeforeWave","AfterWave");
   harmony.Patch(typeof(NoteRenderer).GetMethod("PickAll",F),new HarmonyMethod(typeof(InteractionHooks).GetMethod("BeforePick",F)),new HarmonyMethod(typeof(InteractionHooks).GetMethod("PickAll",F)));
   harmony.Patch(typeof(NoteRenderer).GetMethod("Pick",F),new HarmonyMethod(typeof(InteractionHooks).GetMethod("BeforePick",F)),new HarmonyMethod(typeof(InteractionHooks).GetMethod("Pick",F)));
   harmony.Patch(typeof(NoteRenderer).GetMethod("BoxPick",F),new HarmonyMethod(typeof(InteractionHooks).GetMethod("BeforePick",F)),new HarmonyMethod(typeof(InteractionHooks).GetMethod("Box",F)));
  }
  void RefreshFilterNotes(){
   var history=(EditHistory)Prop(boot,"History");if(history==null)return;
   if(ReferenceEquals(history,filterHistory)&&history.Revision==filterRevision)return;
   filterHistory=history;filterRevision=history.Revision;filterNotes=history.Document.ReadEvents().Where(e=>e.Kind==EventKind.Tap||e.Kind==EventKind.Hold||e.Kind==EventKind.SkyArea||e.Kind==EventKind.Flick).ToDictionary(e=>e.SourceId);
  }
  bool AllowsId(int id,NoteFilterMask mask){RefreshFilterNotes();SpcEvent note;return filterNotes.TryGetValue(id,out note)&&NoteFilterPolicy.Allows(note,mask);}
  List<int> FilterIds(IEnumerable<int> ids,NoteFilterMask mask){RefreshFilterNotes();return NoteFilterPolicy.Filter(ids,filterNotes,mask);}
  bool WaveVisible{get{return boot!=null&&!formal&&Val<bool>(boot,"showUi")&&Val<bool>(boot,"showDetailTimeline")&&(bool)Prop(boot,"HasDocument");}}
  void LayoutWave(){
   float overlay=WaveVisible?Convert.ToSingle(Prop(boot,"OverlayScale")):scale;
   float height=WaveVisible?Mathf.Min(204*overlay,Mathf.Max(100*scale,Screen.height*.3f)):0;
   waveRect=new Rect(0,footerRect.y-height,Screen.width,height);
   if(boot!=null){Set(boot,"detailRect",WaveVisible?waveRect:Rect.zero);Set(boot,"guiShowDetail",WaveVisible);}
  }
  void DrawSelectionFilters(){
   bool range=panel=="段落过滤";NoteFilterMask mask=range?RangeMask:PickMask;
   Rect buttons=ContentRow(70);if(FlatButton(new Rect(0,buttons.y+15*scale,195*scale,40*scale),"全选"))SetMask(range,NoteFilterMask.All);
   if(FlatButton(new Rect(210*scale,buttons.y+15*scale,195*scale,40*scale),"全部取消"))SetMask(range,NoteFilterMask.None);
   string[] names={"地面 Tap","侧轨 Tap · Shift / Space","地面 Hold","侧轨 Hold · Shift / Space","SkyArea · 紫条","Flick 左 · 黄色","Flick 右 · 绿色"};
   for(int i=0;i<7;i++){var bit=(NoteFilterMask)(1<<i);MatchedToggle(names[i],(mask&bit)!=0,delegate(bool on){SetMask(range,on?mask|bit:mask&~bit);});}
   UiParagraph(new Rect(0,contentY+16*scale,405*scale,100*scale),range?"独立应用于框选和两次点击的段落选择；长条仍须首尾完整落入区间。":"应用于鼠标点选、重叠物件切换、时间轴选择和 Ctrl+A。可同时允许多种物件；已选物件保持原样。",13,Css("#7c9dad"));
  }
  void SetMask(bool range,NoteFilterMask mask){Queue(delegate{(range?rangeMaskConfig:pickMaskConfig).Value=(int)mask;Set(boot,"lastPick",new List<int>());Set(boot,"cycleIndex",0);});}
  void FilterFooterButton(float x,string icon,string title,string target,NoteFilterMask mask){
   Rect r=new Rect(x*scale,footerRect.y,86*scale,footerRect.height);bool filtered=mask!=NoteFilterMask.All,opened=panel==target;
   if(filtered||opened)Fill(r,Css("#173743"));if(filtered)Fill(new Rect(r.x+15*scale,r.y+13*scale,r.width-30*scale,2*scale),Css("#8ee9e8"));
   if(IconButton(r,icon,title,filtered||opened))Open(target);
   int count=0;for(int i=0;i<7;i++)if(((int)mask&(1<<i))!=0)count++;
   UiText(new Rect(r.x,r.yMax-25*scale,r.width,18*scale),count+"/7",11,Css(filtered?"#c4f6f4":"#799dab"),NumFace,1);
  }
  internal static class InteractionHooks {
   static bool Active{get{return I!=null&&I.ready&&I.boot!=null&&!I.formal;}}
   static void BeforePick(ref int __1){if(Active)__1=0;}
   static void PickAll(ref List<int> __result){if(Active)__result=I.FilterIds(__result,I.PickMask);}
   static void Pick(NoteRenderer __instance,Ray __0,ref int __result){if(!Active||__result<0||I.AllowsId(__result,I.PickMask))return;var candidates=__instance.PickAll(__0,0);__result=candidates.Count>0?candidates[0]:-1;}
   static void Box(ref HashSet<int> __result){if(Active)__result=new HashSet<int>(I.FilterIds(__result,I.RangeMask));}
  }
  internal sealed class TimelineContext {internal bool Entered;internal IList Hits;}
  internal static partial class Hooks {
   static void BeforeSelection(StudioBootstrap __instance,ref IEnumerable<int> __0,ref int __1){
    if(!Active(__instance))return;
    if(I.rangeDepth>0||I.timelineDepth>0||I.keyboardSelectAll){var ids=I.FilterIds(__0,I.rangeDepth>0?I.RangeMask:I.PickMask);__0=ids;if(!ids.Contains(__1))__1=-1;}
   }
   static void SelectionChanged(StudioBootstrap __instance){if(Active(__instance))I.RefreshPopupSelection();}
   static void BeforeRange(StudioBootstrap __instance,out bool __state){__state=Active(__instance)&&Val<bool>(__instance,"rangeActive");if(__state)I.rangeDepth++;}
   static void AfterRange(bool __state){if(__state&&I!=null)I.rangeDepth--;}
   static void BeforeTimeline(StudioBootstrap __instance,out TimelineContext __state){
    __state=new TimelineContext();if(!Active(__instance)||I.formal)return;
    __state.Entered=true;I.timelineDepth++;var hits=Get(__instance,"timelineHits") as IList;__state.Hits=hits;
    if(hits==null)return;var filtered=(IList)Activator.CreateInstance(hits.GetType());
    foreach(var hit in hits)if(I.AllowsId((int)hit.GetType().GetField("Id",F).GetValue(hit),I.PickMask))filtered.Add(hit);
    Set(__instance,"timelineHits",filtered);
   }
   static void AfterTimeline(StudioBootstrap __instance,TimelineContext __state){
    if(__state!=null&&__state.Entered&&I!=null){I.timelineDepth--;if(__state.Hits!=null)Set(__instance,"timelineHits",__state.Hits);}
   }
   static bool FilterTimelineSelect(StudioBootstrap __instance,int __0){return !Active(__instance)||I.timelineDepth==0||__0<0||I.AllowsId(__0,I.PickMask);}
   static void BeforeWave(StudioBootstrap __instance,out Rect __state){
    __state=Val<Rect>(__instance,"bottomRect");if(!Active(__instance)||!I.WaveVisible)return;
    I.LayoutWave();float overlay=Convert.ToSingle(Prop(__instance,"OverlayScale"));var rect=I.waveRect;
    Set(__instance,"bottomRect",new Rect(rect.x-5*overlay,rect.y-28*overlay,rect.width+10*overlay,rect.height+56*overlay));
   }
   static void AfterWave(StudioBootstrap __instance,Rect __state){if(I!=null&&I.ready)Set(__instance,"bottomRect",__state);}
  }
 }
}
