using System;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private ChartCompanionSession chartCompanion;
        private string looseDesigner="",guiChartDesigner="";
        private bool looseCompanionDirty,showTrackHelp,guiTrackHelp;
        private string CurrentChartDesigner
        {
            get { return songProject!=null && chartCompanion!=null ? chartCompanion.Designer(songProject.ActiveDifficulty) : looseDesigner; }
        }
        private void SetChartDesigner(SongFolderProject expected,int index,string value)
        {
            if(expected==null||expected!=songProject||index!=expected.ActiveDifficulty||chartCompanion==null)
                throw new InvalidOperationException("Chart Designer belongs to a different song/difficulty.");
            chartCompanion.SetDesigner(index,value);RefreshProjectDirty();
            status=L("Chart Designer 已更新到草稿；Ctrl+S 写回 song.json 的 DisplayChartDesigner。",
                "Chart Designer updated in RAM; Ctrl+S writes native DisplayChartDesigner in song.json.");
        }
        private void SetChartAudioOffset(double value)
        {
            ChartCompanionSession.ValidateOffset(value);
            if(songProject!=null&&chartCompanion!=null)chartCompanion.SetOffset(songProject.ActiveDifficulty,value);
            else if(transport.AudioOffsetMs!=value)looseCompanionDirty=true;
            if(transport.AudioOffsetMs!=value)transport.SetAudioOffset(value);offsetText=value.ToString("0.###",CI);
            SyncActiveSongDraft();RefreshProjectDirty();RefreshDocument();
        }
        private void DrawDesignerInput()
        {
            GUILayout.Label("Chart Designer");
            if(songProject==null){GUILayout.Label(L("请打开歌曲文件夹后编辑 Chart Designer。独立 SPC 不包含歌曲元数据。","Open a song folder to edit Chart Designer. A loose SPC has no song metadata."));return;}
            Rect r=GUILayoutUtility.GetRect(30,Mathf.Max(22,25*hudScale),GUILayout.ExpandWidth(true));
            var expected=songProject;int index=expected!=null?expected.ActiveDifficulty:-1;
            CommittedField(r,"field_chart_designer_"+index,CurrentChartDesigner,value=>
            {
                if(expected!=null)SetChartDesigner(expected,index,value);
                else
                {
                    if(songProject!=null)throw new InvalidOperationException("Song changed while editing designer.");
                    looseDesigner=ChartCompanionSession.ValidateDesigner(value);looseCompanionDirty=true;
                }
            },false,true);
            GUILayout.Label(L("当前难度的原版谱师字段。回车只更新内存，全局保存才写入 song.json；无 song.json 的新工程写入主工程元数据。",
                "Native credit for this difficulty. Enter updates RAM; Save writes song.json (or the main manifest for a new music-only project)."));
            if(chartCompanion!=null && chartCompanion.LegacyUpgradePending)
                GUILayout.Label(L("检测到 1.0 旧署名：已作为未保存草稿载入，保存时迁移；旧文件的原字节会备份。","Legacy 1.0 credit staged for migration on Save; original bytes will be backed up."));
        }
        private void DrawTrackHelp()
        {
            GUILayout.Label(L(
                "track(时间毫秒, 速度倍率)\n例如 track(22154,0.3) 从 22154 ms 起以 0.3 倍滚动，直到下一条 track。track(25385,1) 恢复正常。\n1 = 正常，0.5 = 半速，2 = 双速，0 = 停流，负数 = 逆向滚动。多个事件可以做分段渐快/渐慢。\n只改变音符与小节线的纵深运动，不改音乐播放速度、BPM、音符时间或物量。没有 track 时按 1 处理。\n点击列表中的 track 后，在语句框改两个数；回车或离开输入框应用到内存，Ctrl+S 才保存。新建按钮在当前播放头插入。\nbpm(时间,BPM,每小节拍数) 则改变节拍/小节网格，两种语句不能互换。停流/倒流难以编辑时可用单调时间布局。",
                "track(timeMs, speedMultiplier)\ntrack(22154,0.3) scrolls at 0.3x from 22154 ms until the next track; track(25385,1) restores normal speed.\n1 normal, 0.5 half, 2 double, 0 stop, negative reverse. Multiple steps make a gradual transition.\nOnly visual longitudinal motion changes, not music rate, BPM, note timestamps or combo counts. Default speed is 1.\nSelect a track row, edit its source, then Enter/blur applies to RAM. Only Ctrl+S saves. Add inserts at the playhead.\nbpm(time,BPM,beatsPerBar) controls musical grids, not track speed. Use monotonic-time layout to edit stops/reversals."));
        }
        private void DrawLetterboxMask()
        {
            if(Event.current.type!=EventType.Repaint||ViewCamera==null||!Profile.lock16By9)return;
            Rect v=ViewCamera.pixelRect;float top=Screen.height-v.yMax,bottom=Screen.height-v.y;
            if(top>0)HudFill(new Rect(0,0,Screen.width,top),Color.black);
            if(bottom<Screen.height)HudFill(new Rect(0,bottom,Screen.width,Screen.height-bottom),Color.black);
            if(v.x>0)HudFill(new Rect(0,top,v.x,v.height),Color.black);
            if(v.xMax<Screen.width)HudFill(new Rect(v.xMax,top,Screen.width-v.xMax,v.height),Color.black);
        }
        public string RuntimeReport100()
        {
            return ReleaseIdentity.ProductName+" "+ReleaseIdentity.Version+" / Unity "+Application.unityVersion+
                "\nRender: "+Screen.width+" x "+Screen.height+" / "+Screen.fullScreenMode+
                "\nViewport: "+ViewCamera.pixelRect+" / aspect "+ViewCamera.aspect+
                "\nWindow: "+(studioWindow!=null?studioWindow.Message:"")+
                "\nOffset: "+transport.AudioOffsetMs+" (audio = chart + offset; no sign conversion)"+
                "\nDesigner: "+CurrentChartDesigner+" / native DisplayChartDesigner; explicit global Save"+
                "\nDialog: "+StudioFileDialogs.LastDiagnostic;
        }
    }
}
