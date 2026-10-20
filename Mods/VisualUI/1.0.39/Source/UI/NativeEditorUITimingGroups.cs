using System;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  sealed class TimingRow {internal string Name;internal int Notes,Timing;}
  TimingRow[] timingRows=new TimingRow[0];EditHistory timingHistory;
  string frameTimingName="",timingRename="",timingErrors="";bool timingCanDelete,timingNoInput,timingNoClip;
  void CaptureTimingGroups(){
   GroupRuntime.Bind(boot);bool changed=!ReferenceEquals(timingHistory,boot.History);timingHistory=boot.History;timingRows=new TimingRow[0];timingCanDelete=false;timingErrors="";
   if(timingHistory==null)return;var model=TimingGroups.Model(timingHistory.Document);var events=TimingGroups.Events(timingHistory.Document);string current=TimingGroups.Current;
   if(changed||frameTimingName!=current)timingRename=current;frameTimingName=current;
   timingRows=new[]{""}.Concat(model.Groups.Select(g=>g.Name)).Distinct(StringComparer.Ordinal).Select(name=>new TimingRow{Name=name,Notes=events.Count(e=>e.IsNote&&model.Of(e.SourceId)==name),Timing=events.Count(e=>(e.Kind==EventKind.Bpm||e.Kind==EventKind.Track||model.Visibility.ContainsKey(e.SourceId))&&model.Of(e.SourceId)==name)}).ToArray();
   var block=model.Find(current);timingNoInput=block!=null&&!block.NoClip&&block.Properties.Contains("noinput");timingNoClip=block!=null&&block.NoClip;
   timingCanDelete=current!=""&&block!=null;timingErrors=string.Join("\n",model.Errors.ToArray());
  }
  void GroupChange(EditHistory expected,Action<SpcDocument> edit){
   if(boot.History!=expected)throw new InvalidOperationException("谱面已切换，请重新操作。");
   if(Transport!=null&&Transport.Playing)throw new InvalidOperationException("请暂停播放后编辑。");
   Call(boot,"CancelGesture");if(!(bool)Call(boot,"Change",edit))throw new ArgumentException(Val<string>(boot,"status"));GroupRuntime.Sync(boot);
  }
  void ActivateTiming(string name,EditHistory expected){
   if(boot.History!=expected)throw new InvalidOperationException("谱面已切换。");if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;
   Call(boot,"CancelGesture");GroupRuntime.Activate(boot,name);GUI.FocusControl(null);
  }
  void StatementGuardGroup(EditHistory history,string name){if(!ReferenceEquals(boot.History,history)||TimingGroups.Current!=name)throw new InvalidOperationException("谱面或 timinggroup 已切换。");}
  void TimingNameField(string title,string key,string value,Action<string> commit){
   var row=ContentRow(65);RowLine(row);UiText(new Rect(0,row.y,100*scale,row.height),title,16,Css("#a6c3d1"));UiInput(new Rect(108*scale,row.y+13*scale,297*scale,39*scale),key,value,commit,18,0,true,false,CNFace,false);
  }
  void MatchedTimingGroups(){
   UiParagraph(new Rect(0,contentY+10*scale,405*scale,116*scale),"非官方扩展：官方本体不支持时间组。保存与另存为保留此语法；请使用支持该语法的非官方壳子。",15,Css("#edc48c"));contentY+=126*scale;
   if(timingHistory==null||!boot.HasDocument){UiParagraph(new Rect(0,contentY,405*scale,100*scale),"请先打开歌曲或谱面。",17,Css("#a6c3d1"));return;}
   var history=timingHistory;string active=frameTimingName;bool previous=GUI.enabled;
   foreach(var entry in timingRows){var name=entry.Name;var row=ContentRow(69);RowLine(row);bool current=name==active;if(current)Fill(new Rect(0,row.y,405*scale,row.height),Css("#173743"));
    if(FlatButton(new Rect(0,row.y+5*scale,240*scale,59*scale),TimingGroupCaption(name),18,current,false))Queue(delegate{ActivateTiming(name,history);});
    UiText(new Rect(245*scale,row.y+4*scale,160*scale,30*scale),entry.Notes+" 个物件",14,Css("#b3d0da"),CNFace,2);UiText(new Rect(245*scale,row.y+31*scale,160*scale,30*scale),entry.Timing+" 个时序事件",13,Css("#789eae"),CNFace,2);
   }
   if(timingErrors.Length>0){UiParagraph(new Rect(0,contentY+10*scale,405*scale,110*scale),timingErrors,15,Css("#ee9294"));contentY+=120*scale;}
   GUI.enabled=previous&&(Transport==null||!Transport.Playing);try{
    var add=ContentRow(58);if(FlatButton(new Rect(0,add.y+8*scale,405*scale,42*scale),"+ 新建 timinggroup",16))Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;string name=TimingGroups.NextName(boot.History.Document);GroupChange(history,d=>TimingGroups.Add(d,name));ActivateTiming(name,history);timingRename=name;});
    MatchedSection("当前 timinggroup");
    if(active!=""){
     TimingNameField("当前组名称","field_timinggroup_rename_"+active,timingRename,delegate(string value){timingRename=value.Trim();});var row=ContentRow(58);
     if(FlatButton(new Rect(0,row.y+8*scale,197*scale,42*scale),"重命名当前组",16))Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;string next=timingRename;GroupChange(history,d=>TimingGroups.Rename(d,active,next));GroupRuntime.Activate(boot,next);});
     bool enabled=GUI.enabled;GUI.enabled=enabled&&timingCanDelete;try{if(FlatButton(new Rect(207*scale,row.y+8*scale,198*scale,42*scale),"删除组及内容",16))Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;StatementGuardGroup(history,active);GroupChange(history,d=>TimingGroups.DeleteGroup(d,active));GroupRuntime.Activate(boot,"");GUI.FocusControl(null);});}finally{GUI.enabled=enabled;}
     MatchedToggle("noinput · 不判定",timingNoInput,delegate(bool value){Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;StatementGuardGroup(history,active);GroupChange(history,d=>GroupProperties.SetProperty(d,active,"noinput",value));});});
     MatchedToggle("noclip · 不判定且不截断",timingNoClip,delegate(bool value){Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;StatementGuardGroup(history,active);GroupChange(history,d=>GroupProperties.SetProperty(d,active,"noclip",value));});});
     var help=ContentRow(96);UiParagraph(help,"两项互斥，选择一项会关闭另一项。noinput 不判定并截断；noclip 不判定且不截断。",14,Css("#789eae"));
     var deleteHelp=ContentRow(96);UiParagraph(deleteHelp,"删除组会一并删除组内全部物件和时序事件。Ctrl+Z 可一次撤销。",14,Css("#789eae"));
    }else{var row=ContentRow(65);RowLine(row);UiText(row,"默认group",17,Css("#789eae"));}
   }finally{GUI.enabled=previous;}
   MatchedLink("编辑当前组时序",delegate{Queue(delegate{ActivateTiming(active,history);panel="时序";Set(boot,"showInspector",true);panelScroll=Vector2.zero;});});
   contentY+=12*scale;UiParagraph(new Rect(0,contentY,405*scale,104*scale),"物件归属在参数弹窗底部的 timinggroup 中修改。BPM / track / hidegroup 请进入时序列表，使用行内 + / ×。",14,Css("#789eae"));contentY+=114*scale;
  }
 }
}
