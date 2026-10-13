using System;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private bool showReleaseNotes,guiShowReleaseNotes;
        private Vector2 releaseNotesScroll;
        private void InitializeReleaseNotes()
        {showReleaseNotes=PlayerPrefs.GetInt(DesktopReleasePreferences.ReleaseSeenKey(ReleaseIdentity.Version),0)==0;}
        private void OpenReleaseNotes()
        {
            transport.Pause();radial.Cancel();editModifier.Reset();progressDragging=false;releaseCapture=true;
            showReleaseNotes=true;releaseNotesScroll=Vector2.zero;
        }
        private void CloseReleaseNotes()
        {
            showReleaseNotes=false;editModifier.Reset();
            try{PlayerPrefs.SetInt(DesktopReleasePreferences.ReleaseSeenKey(ReleaseIdentity.Version),1);PlayerPrefs.Save();}
            catch(Exception ex){Debug.LogWarning("Release announcement preference: "+ex.Message,this);}
        }
        private void RememberOpenedSongFolder(string folder)
        {
            // Desktop preferences are independent of chart drafts and global Save.
            try{PlayerPrefs.SetString(DesktopReleasePreferences.LastSongFolderKey,folder);PlayerPrefs.Save();}
            catch(Exception ex){Debug.LogWarning("Last song folder preference: "+ex.Message,this);}
        }
        private void DrawReleaseNotes()
        {
            Rect bounds=CameraGuiRect;float s=Mathf.Max(.1f,hudScale);
            float width=Mathf.Min(1040*s,bounds.width-32*s),height=Mathf.Min(840*s,bounds.height-32*s);
            Rect panel=new Rect(bounds.center.x-width*.5f,bounds.center.y-height*.5f,width,height);
            HudFill(new Rect(0,0,Screen.width,Screen.height),new Color(0,0,0,.70f));
            HudFill(panel,new Color(.025f,.047f,.065f,.98f));
            HudFill(new Rect(panel.x,panel.y,panel.width,3*s),new Color(.2f,.8f,.9f));
            HudLabel(new Rect(panel.x+28*s,panel.y+18*s,panel.width-100*s,42*s),L("更新公告","What's new"),28,new Color(.88f,.97f,1));
            HudLabel(new Rect(panel.x+28*s,panel.y+63*s,panel.width-56*s,24*s),ReleaseIdentity.SettingsTitle,15,new Color(.55f,.8f,.87f));
            if(GUI.Button(new Rect(panel.xMax-53*s,panel.y+19*s,30*s,30*s),"×"))QueueGui(CloseReleaseNotes);
            Rect viewport=new Rect(panel.x+24*s,panel.y+101*s,panel.width-48*s,panel.height-204*s);
            var body=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=Mathf.Max(11,Mathf.RoundToInt(17*s))};
            body.normal.textColor=new Color(.83f,.91f,.94f);body.padding=new RectOffset(0,0,0,0);
            var heading=new GUIStyle(body){fontStyle=FontStyle.Bold,fontSize=Mathf.Max(12,Mathf.RoundToInt(23*s))};
            heading.normal.textColor=new Color(.3f,.83f,.94f);
            var recent=ReleaseNotesCatalog.Recent;float contentWidth=viewport.width-24*s,contentHeight=0;
            foreach(var entry in recent)contentHeight+=39*s+body.CalcHeight(new GUIContent(chinese?entry.Chinese:entry.English),contentWidth)+27*s;
            releaseNotesScroll=GUI.BeginScrollView(viewport,releaseNotesScroll,new Rect(0,0,contentWidth,contentHeight));
            float y=0;
            foreach(var entry in recent)
            {
                GUI.Label(new Rect(0,y,contentWidth,33*s),"v"+entry.Version,heading);y+=39*s;
                string text=chinese?entry.Chinese:entry.English;float h=body.CalcHeight(new GUIContent(text),contentWidth);
                GUI.Label(new Rect(0,y,contentWidth,h),text,body);y+=h+27*s;
            }
            GUI.EndScrollView();
            HudLabel(new Rect(panel.x+28*s,panel.yMax-88*s,panel.width-56*s,27*s),L("每个新版本首次启动时显示；设置 → 更新公告可随时查看。","Shown once for each new version. Reopen from Settings → What's new."),14,new Color(.55f,.76f,.81f));
            if(GUI.Button(new Rect(panel.xMax-208*s,panel.yMax-53*s,180*s,36*s),L("知道了","Got it")))QueueGui(CloseReleaseNotes);
            Event e=Event.current;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){QueueGui(CloseReleaseNotes);e.Use();}
            else if(e.isMouse||e.isKey||e.type==EventType.ScrollWheel)e.Use();
        }
    }
}
