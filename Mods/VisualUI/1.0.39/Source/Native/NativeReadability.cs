using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeVisual {
 internal static class NativeReadability {
  #pragma warning disable 0649
  [Serializable] internal sealed class Data {public float nativeHalfWidth,judgementLift,uiReferenceWidth,uiReferenceHeight,judgementPixels;}
  #pragma warning restore 0649
  internal static ConfigEntry<bool> Judgements;internal static Data Catalog;
  static ProvisionalComboTimeline timeline;static Entry[] entries=new Entry[0];static double[] maxEnds=new double[0];static readonly Entry[] recent=new Entry[7];static readonly double[] ages=new double[7];
  sealed class Entry {internal ProvisionalComboNote Tick;internal SpcEvent Note;}
  internal static void Init(ConfigFile cfg,Harmony harmony,string folder){
   Judgements=cfg.Bind("Display","ShowJudgements",true,"Preview native judgement artwork: autoplay = EXACT+, otherwise the native BREAK (miss) artwork. Independent of hit-effect quality. Does not simulate input or alter scores.");
   Catalog=InFalsusStudio.NativeEditorUI.ToolJson.Read<Data>(File.ReadAllText(Path.Combine(folder,"Presentation/native37.json")));
   if(Catalog==null||Catalog.nativeHalfWidth<=0||Catalog.uiReferenceWidth<=0||Catalog.uiReferenceHeight<=0||Catalog.judgementPixels<=0)throw new InvalidDataException("Judgement catalog invalid");
   // Restore the editor's original key-label renderer. Only append feedback;
   // do not suppress or replace the original lettering.
   harmony.Patch(typeof(StudioBootstrap).GetMethod("DrawKeyLabels",NativeVisualPlugin.F),postfix:new HarmonyMethod(typeof(NativeReadability).GetMethod("DrawJudgements",NativeVisualPlugin.F)));
  }
  // CoreScene.Update fits the runtime UI to a 3840x2160 logical canvas.
  // The serialized image Transform and camera viewport are editor snapshots.
  internal static float JudgementSize(float width,float height){return Catalog.judgementPixels*Mathf.Min(width/Catalog.uiReferenceWidth,height/Catalog.uiReferenceHeight);}
  internal static Vector3 JudgementAnchor(StudioProfile p,int lane,double x){
   var world=lane<6?StageSpace.LanePoint(p,lane,.5f,0):new Vector3(StageSpace.AirX(p,x),p.skyHeight,0);
   world.y=p.skyHeight+Catalog.judgementLift*p.centralHalfWidth/Catalog.nativeHalfWidth;return world;
  }
  static void Index(StudioBootstrap boot){
   var source=(ProvisionalComboTimeline)NativeVisualPlugin.Get(boot,"comboTimeline");if(ReferenceEquals(source,timeline))return;timeline=source;var byId=boot.Events.Where(n=>n.IsNote).ToDictionary(n=>n.SourceId);entries=source==null||!source.Supported?new Entry[0]:source.Notes.Where(n=>byId.ContainsKey(n.Id)&&!TimingGroups.NoInput(byId[n.Id])).Select(n=>new Entry{Tick=n,Note=byId[n.Id]}).OrderBy(e=>e.Tick.Start).ToArray();maxEnds=new double[entries.Length];double end=double.NegativeInfinity;for(int i=0;i<entries.Length;i++){end=Math.Max(end,entries[i].Tick.End);maxEnds[i]=end;}
  }
  internal static int VisibleCount;internal static string LastGrade="";
  static void DrawJudgements(StudioBootstrap __instance){
   VisibleCount=0;if(Event.current.type!=EventType.Repaint||!NativeVisualPlugin.Active||Judgements==null||!Judgements.Value||!__instance.HasDocument)return;var transport=(StudioTransport)NativeVisualPlugin.Get(__instance,"transport");if(!transport.Playing||!(bool)NativeVisualPlugin.Get(__instance,"showNotes"))return;
   Index(__instance);double now=__instance.CurrentMs;Array.Clear(recent,0,recent.Length);for(int j=0;j<ages.Length;j++)ages[j]=double.PositiveInfinity;int lo=0,hi=entries.Length;while(lo<hi){int mid=(lo+hi)/2;if(maxEnds[mid]+450<now)lo=mid+1;else hi=mid;}
   for(int i=lo;i<entries.Length;i++){var e=entries[i];if(e.Tick.Start>now)break;int done=e.Tick.At(now);if(done<=0)continue;double tick=e.Tick.TickTime(done-1);if(tick<transport.PlaybackStartMs)continue;double age=now-tick;if(age<0||age>=450||TimingGroups.Hidden(e.Note,now))continue;int lane=e.Note.Kind==EventKind.SkyArea||e.Note.Kind==EventKind.Flick?6:e.Note.Lane;if(age<ages[lane]){ages[lane]=age;recent[lane]=e;}}
   bool auto=(bool)NativeVisualPlugin.Get(__instance,"autoCursor");LastGrade=auto?"EXACT+":"BREAK";var texture=NativePresentation.Images[auto?"judgement-exactplus":"judgement-lost"];var cam=__instance.ViewCamera;var p=__instance.Profile;var old=GUI.color;
   try{for(int lane=0;lane<7;lane++){var e=recent[lane];if(e==null)continue;double x=.5;if(lane==6)x=e.Note.Kind==EventKind.SkyArea?(ChartMath.SkyAt(e.Note,now).Left+ChartMath.SkyAt(e.Note,now).Right)*.5:e.Note.Get(1)/e.Note.Get(2);var at=cam.WorldToScreenPoint(JudgementAnchor(p,lane,x));if(at.z<=cam.nearClipPlane)continue;float t=(float)(ages[lane]/1000),alpha=t<.1f?t/.1f:t<.25f?1:1-Mathf.Clamp01((t-.25f)/.2f);float size=JudgementSize(Screen.width,Screen.height);if(auto&&t<.2f){float burst=size*Mathf.Lerp(.7f,1,t/.2f);GUI.color=new Color(1,1,1,1-t/.2f);GUI.DrawTexture(new Rect(at.x-burst*.5f,Screen.height-at.y-burst*.5f-20*size/Catalog.judgementPixels*(t/.2f),burst,burst),NativePresentation.Images["judgement-exactplus-burst"]);}GUI.color=new Color(1,1,1,alpha);float height=size*Mathf.Lerp(.85f,1,Mathf.Clamp01(t/.2f));GUI.DrawTexture(new Rect(at.x-size*.5f,Screen.height-at.y-height*.5f,size,height),texture);VisibleCount++;}}
   finally{GUI.color=old;}
  }
  internal static void Dispose(){timeline=null;entries=new Entry[0];maxEnds=new double[0];Catalog=null;}
 }
}

