using System;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 internal sealed class SpacePreviewState {
  internal bool Active;internal double Start;internal EditHistory History;
  internal bool Begin(EditHistory history,double time){if(Active)return false;Active=true;Start=time;History=history;return true;}
  internal bool End(EditHistory history,out double time){time=Start;bool restore=Active&&ReferenceEquals(history,History);Active=false;History=null;return restore;}
 }
 public sealed partial class NativeEditorUIPlugin {
  ConfigEntry<bool> holdSpaceConfig;readonly SpacePreviewState spacePreview=new SpacePreviewState();
  void InitSpacePreview(){
   holdSpaceConfig=Config.Bind("Playback","HoldSpacePreview",false,"Hold Space to preview; releasing it pauses and returns to the exact starting chart time. Text input keeps Space for typing.");
   harmony.Patch(Method("HandleCommittedInput"),new HarmonyMethod(typeof(PreviewInputHooks).GetMethod("Committed",F)){priority=Priority.First});
  }
  bool SpaceKey(Event e,bool textFocused){
   if(e.keyCode!=KeyCode.Space)return false;
   if(e.type==EventType.KeyUp&&spacePreview.Active){StopSpacePreview();e.Use();return true;}
   if(holdSpaceConfig==null||!holdSpaceConfig.Value||textFocused||e.control||e.command||e.alt||e.type!=EventType.KeyDown)return false;
   if(!spacePreview.Active&&boot.HasDocument&&(bool)Call(boot,"CommitAllInputFieldsNow")){
    double start=boot.CurrentMs;if(Transport.Playing)Transport.Pause();
    previewRequest=true;try{Call(boot,"TogglePlayback07");if(Transport.Playing){spacePreview.Begin(boot.History,start);formal=false;}}finally{previewRequest=false;}
   }
   e.Use();return true;
  }
  void StopSpacePreview(){
   if(boot==null)return;double start;if(!spacePreview.End(boot.History,out start))return;
   Transport.Pause();Transport.Seek(start);Set(boot,"timeText",start.ToString("0.##########",CultureInfo.InvariantCulture));formal=false;
  }
  static bool legacySpaceInput=true;static System.Reflection.PropertyInfo inputKeyboard,inputSpace,inputPressed;
  static bool SpaceStillHeld(){
   if(legacySpaceInput){try{return Input.GetKey(KeyCode.Space);}catch(InvalidOperationException){legacySpaceInput=false;var keyboard=Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");if(keyboard!=null){inputKeyboard=keyboard.GetProperty("current");inputSpace=keyboard.GetProperty("spaceKey");if(inputSpace!=null)inputPressed=inputSpace.PropertyType.GetProperty("isPressed");}}}
   if(inputKeyboard!=null&&inputSpace!=null&&inputPressed!=null){var keyboard=inputKeyboard.GetValue(null,null);return keyboard!=null&&(bool)inputPressed.GetValue(inputSpace.GetValue(keyboard,null),null);}
   return true; // KeyUp and focus-loss paths still terminate preview.
  }
  void TickSpacePreview(){if(spacePreview.Active&&(!holdSpaceConfig.Value||!Application.isFocused||!SpaceStillHeld()))StopSpacePreview();}
  void OnApplicationFocus(bool focused){if(!focused&&spacePreview.Active)StopSpacePreview();}
  void CommitTimingAndDeselect(EditHistory history,string name){
   StatementGuard(history,name);if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;
   Call(boot,"Select",-1);statementFocus=-1;GUI.FocusControl(null);numericPointer.End();
  }
  static class PreviewInputHooks {
   static bool Committed(Event __0,ref bool __result){
    if(I==null||!I.ready||I.boot==null||I.panel!="时序"||__0.type!=EventType.KeyDown||(__0.keyCode!=KeyCode.Return&&__0.keyCode!=KeyCode.KeypadEnter)||!string.IsNullOrEmpty(Input.compositionString))return true;
    string focus=GUI.GetNameOfFocusedControl();if(I.statementSelected<0&&!focus.StartsWith("field_timing_",StringComparison.Ordinal))return true;
    var history=I.statementHistory;string name=I.statementGroup;I.Queue(delegate{I.CommitTimingAndDeselect(history,name);});__0.Use();__result=true;return false;
   }
  }
 }
}
