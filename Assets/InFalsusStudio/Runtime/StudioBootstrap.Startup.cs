using System;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        // An empty document has NO chart header. Do not equate a non-null History with a loaded chart.
        public bool HasDocument { get; private set; }
        private void InitializeEmptyWorkspace()
        {
            HasDocument=false;
            History=new EditHistory(SpcDocument.Parse(""));History.BeforeCommit=AuthoringSafety.BeforeCommit;
            ClearInputDrafts();ResetEditingForDocument();selectedId=-1;fieldValues=new string[0];
            events.Clear();scroll=new ScrollTimeline(events);beats=new BeatTimeline(events);authoringGrid=new AuthoringGrid(beats);
            transport.Pause();transport.SetClip(null);transport.Seek(0);timeText="0";durationMs=0;
            loadedPath="";workingCopyPath="";showNotes=false;documentDirty=false;
            comboTimeline=null;comboSummary=null;cursorMotion.Rebuild(events);
            keySounds.Rebuild(new HitSoundPlan(events,SkyAttackPolicy.ContinuousRegionStart,true));
            notes.UpdateConnections(events);hitFeedback.Rebuild(events);ApplyViewNormalization();
        }
        private void DrawEmptyWorkspace()
        {
            if(HasDocument||!showUi)return;
            // Settings remain accessible; do not draw the welcome overlay across an open panel.
            if(guiToolsExpanded||guiShowCalibration||guiShowSongPanel||guiShowHelp)return;
            Rect r=HR(575,370,770,251);HudFill(r,new Color(.015f,.03f,.04f,.97f));
            HudLabel(HR(600,393,720,38),L("尚未打开歌曲工程","No song project is open"),27,Color.white,TextAnchor.MiddleCenter);
            HudLabel(HR(606,441,708,102),L(
                "请选择一个可写的歌曲文件夹。\n文件夹中需要且只需一份 MP3 或 OGG 音乐。\n没有谱面或工程文件时，会创建 100 BPM / 4 拍的四难度空工程。",
                "Choose a writable song folder containing exactly one MP3 or OGG.\nWith no chart/project files, four empty 100 BPM / 4-beat charts are created."),17,new Color(.73f,.89f,.94f),TextAnchor.MiddleCenter);
            if(GUI.Button(HR(790,557,340,43),L("打开谱面文件夹…","Open song folder…")))QueueGui(ChooseSongFolder);
        }
    }
}
