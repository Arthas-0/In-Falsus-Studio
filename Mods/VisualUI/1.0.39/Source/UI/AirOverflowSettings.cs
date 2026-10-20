using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using InFalsusStudio.Core;

namespace InFalsusStudio.NativeEditorUI {
 // The option changes air reach policy, never the chart, stage, cursor reach or grid.
 internal static class AirOverflow {
  internal static ConfigEntry<bool> Setting;
  internal static bool Enabled {get{return Setting!=null&&Setting.Value;}}
  internal static string RestrictedViolation(SpcEvent e){
   if(!AirReachBounds.IsAir(e)||ChartMath.InvalidReason(e)!=null)return null;
   var a=e.Kind==EventKind.Flick?ChartMath.FlickRange(e):ChartMath.SkyAt(e,e.TimeMs);
   var b=e.Kind==EventKind.Flick?a:ChartMath.SkyAt(e,e.EndMs);
   return !AirReachBounds.Inside(a)||!AirReachBounds.Inside(b)?"Air range exceeds the playable 0..100 boundary. Source retained; preview is clipped.":null;
  }
  internal static void Init(ConfigFile config,Harmony h){
   Setting=config.Bind("Authoring","AllowAirOverflow",false,"Allow SkyArea/Flick geometry and ordinary parameter edits outside normalized 0..1. No chart rewriting. Grid range is configured separately in the Grid panel.");
   Patch(h,typeof(AirReachBounds),"Violation","Violation");
   Patch(h,typeof(AirReachBounds),"Require","Require");
   Patch(h,typeof(AirReachBounds),"CenterWithin","Center");
   Patch(h,typeof(AirReachBounds),"ClampDelta","Delta");
   Patch(h,typeof(NoteProperties),"AirPercent","Percent");
   Patch(h,typeof(NoteRenderer),"SetAirClip","Clip");
  }
  static void Patch(Harmony h,Type t,string method,string hook){
   h.Patch(t.GetMethod(method,NativeEditorUIPlugin.F),new HarmonyMethod(typeof(AirOverflow).GetMethod(hook,NativeEditorUIPlugin.F)));
  }
  static bool Violation(ref string __result){if(!Enabled)return true;__result=null;return false;}
  static bool Require(SkyRange __0){
   if(!Enabled)return true;
   if(!AuthoringSafety.Finite(__0.Left)||!AuthoringSafety.Finite(__0.Right)||__0.Left>__0.Right+AirReachBounds.Tolerance)throw new ArgumentException("Invalid air range.");
   return false;
  }
  static bool Center(double __0,double __1,ref double __result){
   if(!Enabled)return true;
   if(!AuthoringSafety.Finite(__0)||!AuthoringSafety.Finite(__1)||__1<0)throw new ArgumentException("Invalid air position or width.");
   __result=__0;return false;
  }
  static bool Delta(double __1,ref double __result){
   if(!Enabled)return true;if(!AuthoringSafety.Finite(__1))throw new ArgumentException("Non-finite edit.");__result=__1;return false;
  }
  static void Clip(ref bool __0){if(Enabled)__0=false;}
  static readonly FieldInfo Initial=typeof(NoteProperties).GetField("initial",NativeEditorUIPlugin.F);
  static double Read(NoteProperties form,string key){
   double v;if(!double.TryParse(form.Values[key],NumberStyles.Float,CultureInfo.InvariantCulture,out v)||!AuthoringSafety.Finite(v))throw new ArgumentException("Invalid finite number: "+key);
   return v/100;
  }
  static bool Percent(NoteProperties __instance,SpcDocument __0,SpcEvent __1,bool __2,string __3,string __4){
   if(!Enabled)return true;
   var initial=(Dictionary<string,string>)Initial.GetValue(__instance);var values=__instance.Values;
   bool centerChanged=values[__3]!=initial[__3],widthChanged=values[__4]!=initial[__4];
   if(!centerChanged&&!widthChanged)return false;
   int c=__2?4:1;double split=__1.Get(c+1),center=centerChanged?Read(__instance,__3):__1.Get(c)/split,width=widthChanged?Read(__instance,__4):__1.Get(c+2)/split;
   if(widthChanged&&width<=0)throw new ArgumentException("Air width must be positive.");
   RationalAirCoordinates.WriteEndpoint(__0,__1,__2,center,width);return false;
  }
 }
 public sealed partial class NativeEditorUIPlugin {
  void InitAirOverflow(){AirOverflow.Init(Config,harmony);AirGridRange.Init(Config,harmony);}
  void SetAirOverflow(bool value){
   Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;Call(boot,"CancelGesture");AirOverflow.Setting.Value=value;});
  }
 }
}
