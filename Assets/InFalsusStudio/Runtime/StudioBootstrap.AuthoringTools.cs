using System;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private bool authoringToolsOpen,guiAuthoringToolsOpen,chartCheckOpen,guiChartCheckOpen;
        private bool guiSplitSelectionValid,guiCheckFresh;
        private int splitCount=4,splitFlickDirection=16,guiSplitSelectedCount,checkPage,guiCheckPage;
        private Vector2 authoringToolsScroll,chartCheckScroll;
        private ChartIssue[] checkIssues=new ChartIssue[0],guiCheckIssues=new ChartIssue[0];
        private EditHistory checkHistory;
        private int checkRevision=-1;
        private void CaptureAuthoringToolsFrame()
        {
            guiAuthoringToolsOpen=authoringToolsOpen;guiChartCheckOpen=chartCheckOpen;
            var selected=events.Where(n=>selection.Contains(n.SourceId)).ToArray();
            guiSplitSelectedCount=selected.Length;
            guiSplitSelectionValid=selected.Length>0&&selected.All(n=>n.Kind==EventKind.SkyArea);
            guiCheckFresh=checkHistory==History&&checkRevision==History.Revision;
            checkPage=Mathf.Clamp(checkPage,0,Math.Max(0,(checkIssues.Length-1)/50));guiCheckPage=checkPage;
            guiCheckIssues=checkIssues.Skip(checkPage*50).Take(50).ToArray();
        }
        private bool PrepareAuthoringCommand()
        {
            if(!HasDocument||transport.Playing||WorkflowActive)return false;
            if(!CommitAllInputFieldsNow()||!CommitPendingNoteNow())return false;
            CancelGesture();return true;
        }
        private void SplitSelectedSky(bool grid,bool flick)
        {
            if(!PrepareAuthoringCommand())return;
            var ids=selection.ToArray();int[] generated=null;
            if(Change(d=>generated=grid?SkySplitOperations.Grid(d,ids,authoringGrid,Division,flick,splitFlickDirection):
                SkySplitOperations.Equal(d,ids,splitCount,flick,splitFlickDirection)))
            {
                SetSelection(generated,generated[0]);showNotes=true;
                status=L("已生成 ","Created ")+generated.Length+(flick?" Flick":" SkyArea")+L("，可撤销；保存才写盘。","; one undo step, Save writes to disk.");
            }
        }
        private void ResetAllSkyGroups()
        {
            if(!PrepareAuthoringCommand())return;
            if(Change(SkySplitOperations.ResetGroups)){currentSkyGroup=-1;groupFollowingId=-1;status=L("当前谱面所有 SkyArea 已移除组号参数；可撤销，尚未保存。","All SkyAreas in this chart now omit the group argument; undoable, not saved.");}
        }
        private void RepairAllAirCoordinates(bool limitSplits)
        {
            if(!PrepareAuthoringCommand())return;
            var history=History;var validator=history.BeforeCommit;
            try
            {
                // This repair changes only numeric coordinate parts. Keep explicit group
                // IDs as authored instead of regrouping notes that newly touch after rounding.
                history.BeforeCommit=AuthoringSafety.ValidateChanged;
                AirCoordinateRepair.Result repaired=default;
                if(Change(d=>repaired=limitSplits?AirCoordinateRepair.LimitSplits(d):AirCoordinateRepair.Round(d)))
                    status=repaired.Notes==0?L("没有需要修复的 SkyArea/Flick 坐标参数。","No SkyArea/Flick coordinate parts need repair."):
                        L("已修复 ","Repaired ")+repaired.Notes+L(" 个音符、"," notes, ")+repaired.Fields+
                        L(" 个坐标参数；可撤销，保存才写盘。"," coordinate parts; undoable, Save writes disk.");
            }
            finally{history.BeforeCommit=validator;}
        }
        private void AutoGroupAllSky()
        {
            if(!PrepareAuthoringCommand())return;
            SkyAutoGrouping.Result result=default;
            if(Change(d=>result=SkyAutoGrouping.Assign(d)))
            {
                currentSkyGroup=0;groupFollowingId=-1;
                status=L("已将 ","Grouped ")+result.Notes+L(" 条 SkyArea 分为 "," SkyAreas into ")+result.Groups+
                    L(" 组；可整次撤销，保存才写盘。"," groups; one undo step, Save writes disk.");
            }
        }
        private void DrawAuthoringTools()
        {
            GUILayout.BeginArea(inspectorRect,GUI.skin.box);BeginPanelContent(ref authoringToolsScroll,inspectorRect);
            GUILayout.BeginHorizontal();GUILayout.Label(L("工具","TOOLS"));
            if(GUILayout.Button("×",GUILayout.Width(26)))QueueGui(()=>authoringToolsOpen=false);GUILayout.EndHorizontal();
            GUILayout.Label(L("已选择 ","Selected ")+guiSplitSelectedCount+L(" 个音符。"," notes."));
            bool enabled=GUI.enabled;GUI.enabled=enabled&&HasDocument&&!transport.Playing&&!guiWorkflowActive;
            if(GUILayout.Button(L("本谱全部 SkyArea 不指定组","Remove all SkyArea group assignments")))QueueGui(ResetAllSkyGroups);
            if(GUILayout.Button(L("Sky 自动分组","Auto-group SkyAreas")))QueueGui(AutoGroupAllSky);
            GUILayout.Label(L("按时间和首尾连接分配组号（含前后 2ms 容差）；新建 Sky 仍不指定组。","Assign groups by time and touching endpoints (±2ms). New SkyAreas still omit groups."));
            GUILayout.Space(10);GUILayout.Label(L("1. 等分切分（仅单条 SkyArea）","1. Equal split (one SkyArea)"));
            CommittedRow(L("分段数量 N","Piece count N"),"field_split_count",splitCount.ToString(CI),value=>
            {int n;if(!int.TryParse(value,out n)||n<2||n>SkySplitOperations.MaxPieces)throw new FormatException("N: 2..4096");splitCount=n;});
            bool operationEnabled=GUI.enabled;GUI.enabled=operationEnabled&&guiSplitSelectionValid&&guiSplitSelectedCount==1;
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(L("切成 N 条 SkyArea","Split into N SkyAreas")))QueueGui(()=>SplitSelectedSky(false,false));
            if(GUILayout.Button(L("转成 N 个 Flick","Convert to N Flicks")))QueueGui(()=>SplitSelectedSky(false,true));
            GUILayout.EndHorizontal();
            GUI.enabled=operationEnabled;GUILayout.Space(10);
            GUILayout.Label(L("2. 按当前网格切分（支持多条 SkyArea）","2. Current grid split (multiple SkyAreas)"));
            GUILayout.Label(L("当前细分：1/","Subdivision: 1/")+Division+L(" 拍；两端不足一格也保留。"," beat; partial end intervals are retained."));
            GUI.enabled=operationEnabled&&guiSplitSelectionValid;
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(L("按网格切 SkyArea","Grid → SkyAreas")))QueueGui(()=>SplitSelectedSky(true,false));
            if(GUILayout.Button(L("按网格转 Flick","Grid → Flicks")))QueueGui(()=>SplitSelectedSky(true,true));
            GUILayout.EndHorizontal();GUI.enabled=operationEnabled;
            GUILayout.Space(8);
            if(GUILayout.Button(L("生成 Flick 方向：","Flick direction: ")+(splitFlickDirection==16?L("向左","Left"):L("向右","Right"))))
                QueueGui(()=>splitFlickDirection=splitFlickDirection==16?4:16);
            GUI.enabled=enabled;
            GUILayout.Label(L("分别采样原来的左右曲线，坐标最多 128 份，允许少量拟合误差；小 SkyArea 两侧为 Linear，不指定组。Flick 在每段起点采样，不额外生成尾点。","Both edges are sampled with at most 128 coordinate parts and bounded fitting error. Sky pieces use Linear edges and omit group assignment. Flicks sample interval starts; no extra tail Flick."));
            GUILayout.Space(10);GUILayout.Label(L("3. 坐标修复","3. Coordinate repair"));
            GUI.enabled=operationEnabled;
            if(GUILayout.Button(L("当前谱面 SkyArea/Flick 坐标自动取整","Round SkyArea/Flick coordinates in this chart")))QueueGui(()=>RepairAllAirCoordinates(false));
            if(GUILayout.Button(L("将本谱大分母压缩到 128 份以内","Fit large divisors within 128 coordinate parts")))QueueGui(()=>RepairAllAirCoordinates(true));
            GUI.enabled=enabled;
            GUILayout.Label(L("将原始 X、份数、宽度四舍五入为整数（0.5 向远离零方向取整）；不改时间、缓动或组 ID。百分比显示仍可有小数。可撤销，保存才写盘。","Round stored X, split and width to integers (halves away from zero). Time, ease and group stay unchanged. Displayed percentages may be fractional. Undoable; Save writes disk."));
            GUILayout.Label(L("压缩会按原比例重新拟合大于 128 的份数，已在上限内的端点不动。它可能略微改变位置和宽度，可整次撤销，保存才写盘。","Compression refits divisors above 128 in proportion. Endpoints already within the cap are untouched. Position and width may shift slightly; one undo step, Save writes disk."));
            EndPanelContent();GUILayout.EndArea();
        }
        private void OpenChartCheck()
        {
            if(!HasDocument||!CommitAllInputFieldsNow()||!CommitPendingNoteNow())return;
            CancelGesture();transport.Pause();CloseLeftPanels();chartCheckOpen=true;RunChartCheck();
        }
        private void RunChartCheck()
        {
            if(!HasDocument||!CommitAllInputFieldsNow()||!CommitPendingNoteNow())return;
            checkIssues=ChartHealthCheck.Run(History.Document);checkHistory=History;checkRevision=History.Revision;
            checkPage=0;chartCheckScroll=Vector2.zero;
            status=L("谱面检查完成：","Chart check complete: ")+checkIssues.Length+L(" 项问题。"," issues.");
            Debug.Log("Chart check: "+checkIssues.Length+" issues\n"+string.Join("\n",checkIssues.Select(issue=>
                issue.Kind+" · lines "+string.Join(", ",issue.Lines.Select(n=>n.ToString()))+
                (issue.Kind==ChartIssueKind.UnknownStatement?"\n"+issue.SourceText:" · "+issue.TimeMs.ToString("0.##########",CI)+" ms"))),this);
        }
        private void JumpToChartIssue(ChartIssue issue)
        {
            if(checkHistory!=History||checkRevision!=History.Revision){status=L("谱面已修改，请重新检查。","Chart changed; run the check again.");return;}
            CancelGesture();transport.Pause();SetSelection(issue.SourceIds,issue.SourceIds[0]);
            double time=Math.Max(0,Math.Min(AuthoringSafety.MaxTimelineMs,issue.TimeMs));
            transport.Seek(time);exactGroupJump=time;timeText=time.ToString("0.##########",CI);
            showNotes=true;chartCheckOpen=false;
            status=L("已跳转并选中问题音符：","Jumped to and selected affected notes: ")+issue.SourceIds.Length;
        }
        private void DrawChartCheck()
        {
            GUILayout.BeginArea(inspectorRect,GUI.skin.box);BeginPanelContent(ref chartCheckScroll,inspectorRect);
            GUILayout.BeginHorizontal();GUILayout.Label(L("谱面检查","CHART CHECK"));
            if(GUILayout.Button("×",GUILayout.Width(26)))QueueGui(()=>chartCheckOpen=false);GUILayout.EndHorizontal();
            if(GUILayout.Button(L("重新检查","Check again")))QueueGui(RunChartCheck);
            if(!guiCheckFresh)GUILayout.Label(L("谱面已修改，请重新检查后跳转。","Chart changed; check again before jumping."));
            GUILayout.Label(L("共 ","Total: ")+checkIssues.Length+L(" 项。SkyArea 不进行叠键检查。"," issues. SkyArea overlap is not checked."));
            if(checkIssues.Length==0)GUILayout.Label(L("未发现未知语句、叠键或不足 5ms 的 Hold / SkyArea。","No unknown statements, overlaps, or Hold / SkyArea durations below 5ms."));
            foreach(var issue in guiCheckIssues)
            {
                string label=issue.Kind==ChartIssueKind.UnknownStatement?L("未知 / 不支持的语句","Unknown / unsupported statement"):
                    issue.Kind==ChartIssueKind.TapOverlap?L("Tap 轨道重叠且时间差 <3ms","Tap lane overlap, time difference <3ms"):
                    issue.Kind==ChartIssueKind.HoldOverlap?L("Hold 轨道与持续时间重叠","Hold lane and duration overlap"):
                    issue.Kind==ChartIssueKind.HoldTooShort?L("Hold 持续时间 <5ms","Hold duration <5ms"):
                    issue.Kind==ChartIssueKind.SkyAreaTooShort?L("SkyArea 持续时间 <5ms","SkyArea duration <5ms"):L("Flick 语句重复","Duplicate Flick statement");
                GUILayout.Space(8);GUILayout.Label(label+"\n"+L("源行：","Source lines: ")+string.Join(", ",issue.Lines.Select(n=>n.ToString())));
                if(issue.Kind==ChartIssueKind.UnknownStatement)
                    GUILayout.Label(issue.SourceText.Length<=600?issue.SourceText:issue.SourceText.Substring(0,600)+L("\n…完整原文已输出到检查日志。","\n…Full source is in the check log."));
                else
                {
                    bool old=GUI.enabled;GUI.enabled=old&&guiCheckFresh;
                    if(GUILayout.Button(L("跳转并选中问题音符","Jump and select affected notes")+" · "+issue.TimeMs.ToString("0.##########",CI)+" ms"))
                    {var target=issue;QueueGui(()=>JumpToChartIssue(target));}GUI.enabled=old;
                }
            }
            if(checkIssues.Length>50)
            {
                GUILayout.Space(8);GUILayout.BeginHorizontal();
                if(GUILayout.Button(L("上一页","Previous")))QueueGui(()=>{checkPage--;chartCheckScroll=Vector2.zero;});
                GUILayout.Label((guiCheckPage+1)+"/"+((checkIssues.Length+49)/50));
                if(GUILayout.Button(L("下一页","Next")))QueueGui(()=>{checkPage++;chartCheckScroll=Vector2.zero;});
                GUILayout.EndHorizontal();
            }
            EndPanelContent();GUILayout.EndArea();
        }
    }
}
