using System;
using System.Collections.Generic;
using System.Linq;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 [Flags] public enum NoteFilterMask {None=0,GroundTap=1,SideTap=2,GroundHold=4,SideHold=8,SkyArea=16,FlickLeft=32,FlickRight=64,All=127}
 public static class NoteFilterPolicy {
  public static NoteFilterMask TypeOf(SpcEvent note){
   if(note==null)return NoteFilterMask.None;
   if(note.Kind==EventKind.SkyArea)return NoteFilterMask.SkyArea;
   if(note.Kind==EventKind.Flick)return note.Get(4,0)==16?NoteFilterMask.FlickLeft:NoteFilterMask.FlickRight;
   int lane=(int)note.Get(1,0);bool side=lane==0||lane==5;
   if(note.Kind==EventKind.Tap)return side?NoteFilterMask.SideTap:NoteFilterMask.GroundTap;
   if(note.Kind==EventKind.Hold)return side?NoteFilterMask.SideHold:NoteFilterMask.GroundHold;
   return NoteFilterMask.None;
  }
  public static bool Allows(SpcEvent note,NoteFilterMask mask){return (TypeOf(note)&mask)!=0;}
  public static List<int> Filter(IEnumerable<int> ids,IDictionary<int,SpcEvent> notes,NoteFilterMask mask){
   var result=new List<int>();foreach(int id in ids){SpcEvent note;if(notes.TryGetValue(id,out note)&&Allows(note,mask))result.Add(id);}return result;
  }
 }
}
