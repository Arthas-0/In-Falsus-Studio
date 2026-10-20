using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 internal static class AirGridRange {
  internal static ConfigEntry<double> Minimum,Maximum;
  internal static bool Active {get{return AirOverflow.Enabled&&Minimum!=null&&Valid(Minimum.Value,Maximum.Value);}}
  internal static double Low {get{return Active?Minimum.Value/100:0;}}
  internal static double High {get{return Active?Maximum.Value/100:1;}}
  internal static bool Expanded {get{return Low!=0||High!=1;}}
  internal static bool Valid(double lo,double hi){return AuthoringSafety.Finite(lo)&&AuthoringSafety.Finite(hi)&&lo>=-10000&&hi<=10000&&hi>lo&&hi-lo>=.0001;}
  internal static void Init(ConfigFile config,Harmony h){
   Minimum=config.Bind("Authoring","AirGridMinPercent",0d,"Air X ruler/snap minimum percent when air overflow is enabled. Minimum < Maximum, within -10000..10000. Does not move existing notes.");
   Maximum=config.Bind("Authoring","AirGridMaxPercent",100d,"Air X ruler/snap maximum percent when air overflow is enabled. Existing 0..100 is retained by default.");
   if(!Valid(Minimum.Value,Maximum.Value)){Minimum.Value=0;Maximum.Value=100;}
   h.Patch(typeof(AirAuthoringGrid).GetMethod("Snap"),new HarmonyMethod(typeof(AirGridRange).GetMethod("Snap",NativeEditorUIPlugin.F)));
   h.Patch(NativeEditorUIPlugin.Method("XOnRuler"),new HarmonyMethod(typeof(AirGridRange).GetMethod("Pointer",NativeEditorUIPlugin.F)));
   h.Patch(NativeEditorUIPlugin.Method("RefreshAuthoringHover"),postfix:new HarmonyMethod(typeof(AirGridRange).GetMethod("Hover",NativeEditorUIPlugin.F)));
  }
  internal static double SnapValue(AirAuthoringGrid grid,double input){
   if(!AuthoringSafety.Finite(input))throw new ArgumentException("Non-finite X.");double clamped=Math.Max(Low,Math.Min(High,input));double step=grid.Division>0?1d/grid.Division:(double)grid.StepPercent/100;
   double snapped=Math.Round(clamped/step,MidpointRounding.AwayFromZero)*step;snapped=Math.Max(Low,Math.Min(High,snapped));if(Math.Abs(clamped-Low)<Math.Abs(clamped-snapped))snapped=Low;if(Math.Abs(clamped-High)<Math.Abs(clamped-snapped))snapped=High;return snapped;
  }
  static bool Snap(AirAuthoringGrid __instance,double __0,ref double __result){if(!Expanded)return true;__result=SnapValue(__instance,__0);return false;}
  static bool Pointer(StudioBootstrap __instance,Vector2 __0,double __1,ref double __2,ref bool __result){
   if(!Expanded)return true;var p=__instance.Profile;var camera=__instance.ViewCamera;float z=(float)NativeEditorUIPlugin.Call(__instance,"Depth",__1);var a=camera.WorldToScreenPoint(new Vector3(-p.centralHalfWidth,p.skyHeight+.019f,z));var b=camera.WorldToScreenPoint(new Vector3(p.centralHalfWidth,p.skyHeight+.019f,z));
   __result=false;__2=.5;if(a.z>camera.nearClipPlane&&b.z>camera.nearClipPlane&&Math.Abs(b.x-a.x)>=1){__2=Math.Max(Low,Math.Min(High,(__0.x-a.x)/(b.x-a.x)));__result=true;}return false;
  }
  internal struct Tick {internal double Position;internal bool Label;}
  internal static Tick[] VisibleTicks(AirAuthoringGrid grid,double pixels,double scale,double lo,double hi){
   lo=Math.Max(Low,lo);hi=Math.Min(High,hi);if(hi<lo)return new Tick[0];double step=grid.Division>0?1d/grid.Division:(double)grid.StepPercent/100;
   double spacing=Math.Max(.000001,Math.Abs(pixels)*step);int stride=Math.Max(1,(int)Math.Ceiling(6*scale/spacing)),labels=Math.Max(1,(int)Math.Ceiling(64*scale/spacing));
   int first=(int)Math.Ceiling(lo/step-1e-8),last=(int)Math.Floor(hi/step+1e-8);var points=new SortedDictionary<double,bool>();
   for(long i=(long)Math.Ceiling(first/(double)stride)*stride;i<=last;i+=stride)points[i*step]=false;
   for(long i=(long)Math.Ceiling(first/(double)labels)*labels;i<=last;i+=labels)points[i*step]=true;
   if(lo<=Low&&High>=Low)points[Low]=true;if(hi>=High)points[High]=true;
   var result=new List<Tick>();foreach(var point in points)result.Add(new Tick{Position=point.Key,Label=point.Value});return result.ToArray();
  }
  // Restore read-only grid hover in Select mode; never enter point placement.
  static void Hover(StudioBootstrap __instance,Vector2 __0){
   var ui=NativeEditorUIPlugin.I;if(ui==null||!ui.ready||ui.formal||!__instance.HasDocument||!(bool)NativeEditorUIPlugin.Get(__instance,"showGrid")||!(bool)NativeEditorUIPlugin.Get(__instance,"showUi")||((StudioTransport)NativeEditorUIPlugin.Get(__instance,"transport")).Playing||NativeEditorUIPlugin.Get(__instance,"tool").ToString()!="Select"||(bool)NativeEditorUIPlugin.Prop(__instance,"WorkflowActive")||(bool)NativeEditorUIPlugin.Prop(__instance,"EditHeld")||(bool)NativeEditorUIPlugin.Prop(__instance,"PointerEditGesture")||!(bool)NativeEditorUIPlugin.Call(__instance,"AllowedCanvas",__0))return;
   object[] args={__0,__instance.CurrentMs,0d,0};if(!(bool)NativeEditorUIPlugin.Call(__instance,"PointerAuthoringTime",args))return;double time=(double)args[2];int lane=(int)args[3];float depth=(float)NativeEditorUIPlugin.Call(__instance,"Depth",time);var p=__instance.Profile;
   var a=__instance.ViewCamera.WorldToScreenPoint(BeatGuideGeometry.Point(p,lane,0,depth));var b=__instance.ViewCamera.WorldToScreenPoint(BeatGuideGeometry.Point(p,lane,1,depth));if(a.z<=0||b.z<=0)return;Vector2 aa=new Vector2(a.x,Screen.height-a.y),bb=new Vector2(b.x,Screen.height-b.y),delta=bb-aa;float along=delta.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector2.Dot(__0-aa,delta)/delta.sqrMagnitude);float scale=(float)NativeEditorUIPlugin.Prop(__instance,"OverlayScale");if(Vector2.Distance(__0,aa+delta*along)>10*scale)return;
   NativeEditorUIPlugin.Set(__instance,"guideActive",true);NativeEditorUIPlugin.Set(__instance,"guideTime",time);NativeEditorUIPlugin.Set(__instance,"guideAir",false);NativeEditorUIPlugin.Set(__instance,"hoverPointer",__0);
  }
 }
 public sealed partial class NativeEditorUIPlugin {
  void GridRangeFields(){
   bool enabled=GUI.enabled;GUI.enabled=enabled&&AirOverflow.Enabled;
   try{MatchedField("空域 X 最小值","field_air_grid_min",DisplayPrecision.Number(AirGridRange.Minimum.Value.ToString("R",CultureInfo.InvariantCulture),4),v=>SetGridRange(v,true),"%");MatchedField("空域 X 最大值","field_air_grid_max",DisplayPrecision.Number(AirGridRange.Maximum.Value.ToString("R",CultureInfo.InvariantCulture),4),v=>SetGridRange(v,false),"%");}finally{GUI.enabled=enabled;}
   Rect row=ContentRow(130);UiParagraph(new Rect(0,row.y+10*scale,405*scale,120*scale),"开启天空超界后，可自定 X 网格范围（百分比）。原轨道为 0～100；范围只影响标尺和吸附，不移动现有物件。",13,Css("#7c9dad"));
  }
  void SetGridRange(string value,bool minimum){double number;if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)||!AirGridRange.Valid(minimum?number:AirGridRange.Minimum.Value,minimum?AirGridRange.Maximum.Value:number))throw new ArgumentException(T("X 网格范围须在 -10000～10000 内，最小值小于最大值。"));Call(boot,"CancelGesture");if(minimum)AirGridRange.Minimum.Value=number;else AirGridRange.Maximum.Value=number;}
 }
}
