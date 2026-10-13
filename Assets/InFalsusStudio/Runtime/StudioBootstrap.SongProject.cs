using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private SongFolderProject songProject;
        private readonly EditHistory[] songHistories=new EditHistory[4];
        private readonly byte[][] songSavedBytes=new byte[4][];
        private readonly double[] songPlayheads=new double[4];
        private bool showSongPanel,guiShowSongPanel,showSongHud=true,guiShowSongHud=true,autoCursor=true;
        private Rect songPanelRect;
        private string songFolderInput="",songTitleInput="",songArtistInput="",songRatingInput="?",measuredComboInput="-1";
        private string scoreEvidenceStatus="Hold/Sky tick count is unverified. Objects are NOT combo.";
        private Texture2D songJacket;
        private string songJacketPath="";
        private ScoreEvidence scoreEvidence;
        private bool evidenceMatches;
        private ComboCountSummary comboSummary;
        private string comboReferenceJson;
        private bool comboReferencesLoaded;
        private string guiComboText="",guiComboWarning="";
        private readonly AutoplayCursor cursorMotion=new AutoplayCursor();
        private CursorPose cursorPose=new CursorPose(.5,-1,"Idle");
        private string[] guiSongNotices=new string[0];
        private int guiSongIndex=-1;
        private string guiSongTitle="No song project",guiSongArtist="",guiSongRoot="",guiSongRating="?",guiSongAudio="";
        private int guiSongObjects;
        private bool songOpenBusy;
        public void OpenSongFolder(string folder)
        {
            if(songOpenBusy)return;
            if(!initialized){status="Open Demo Scene and enter Play first.";return;}
            songOpenBusy=true;
#if UNITY_EDITOR
            try
            {
                var prepared=PrepareFolderWithConsent(folder);if(prepared==null)return;
                string chosen=prepared.AudioPath,hash=SongFolderProject.FileHash(chosen);
                AudioClip clip=PrepareProjectAudio(chosen);
                FinishOpeningFolder(folder,prepared,chosen,hash,clip,false);
            }
            catch(ExitGUIException){throw;}
            catch(Exception ex){ReportFolderFailure(ex.Message);}
            finally{songOpenBusy=false;}
#else
            StartCoroutine(OpenSongFolderPlayer(folder));
#endif
        }
        private SongFolderProject PrepareFolderWithConsent(string folder)
        {
            SongFolderProject prepared;
            try{prepared=SongFolderProject.Prepare(folder);}
            catch(SongAudioConflict ex)
            {
                if(ex.Files.Length==0)throw;
                if(ex.CanUseDeclaredPrimary)
                {
                    if(!StudioFileDialogs.Confirm("Multiple audio files",ex.Message+"\n\nMain: "+Path.GetFileName(ex.DeclaredPrimary)+
                        "\nOther files are declared supplementary_audio. Use ONLY this declared main? No audio will be deleted.","Use declared main","Cancel"))return null;
                    prepared=SongFolderProject.Prepare(folder,true);
                }
                else
                {
                    string audio=StudioFileDialogs.Open(L("发现多首音乐，请选择主音频","Multiple music files: choose the main audio"),folder,"mp3,ogg");
                    if(string.IsNullOrEmpty(audio))return null;
                    prepared=SongFolderProject.Prepare(folder,true,audio);
                }
            }
            string key=SongMediaDiscovery.PreferenceKey(prepared.Root);
            if(prepared.JacketChoiceRequired)SongMediaDiscovery.UseRememberedJacket(prepared,PlayerPrefs.GetString(key+".file",""),PlayerPrefs.GetString(key+".inventory",""));
            if(prepared.JacketChoiceRequired)
            {
                string image=StudioFileDialogs.Open(L("发现多张图片，请选择曲绘","Multiple images: choose the song jacket"),prepared.Root,"png,jpg,jpeg");
                if(string.IsNullOrEmpty(image))return null;
                SongMediaDiscovery.ChooseJacket(prepared,image);
            }
            return prepared;
        }
        private bool FinishOpeningFolder(string folder,SongFolderProject prepared,string chosenAudio,string chosenAudioHash,AudioClip clip,bool owned)
        {
            if(clip==null||clip.samples<=0)throw new IOException("Audio decoder returned no samples.");
            if(!LeaveCurrentDocumentSafely())return false;
            var checkedProject=SongFolderProject.Prepare(folder,true,chosenAudio);
            SongMediaDiscovery.ReapplyOpeningChoice(prepared,checkedProject);prepared=checkedProject;
            if(!prepared.AudioPath.Equals(chosenAudio,StringComparison.OrdinalIgnoreCase)||SongFolderProject.FileHash(prepared.AudioPath)!=chosenAudioHash)
                throw new IOException("Main audio changed while opening. Reopen the folder.");
            prepared.CommitInitialWorkspace();
            var newSession=new ManualSongSession(prepared);
            var newCompanion=new ChartCompanionSession(prepared,newSession);
            CancelGesture();audioRequest++;transport.Pause();scoreEvidence=null;evidenceMatches=false;songProject=prepared;projectSession=newSession;chartCompanion=newCompanion;projectDirty=false;
            Array.Clear(songHistories,0,4);Array.Clear(songSavedBytes,0,4);Array.Clear(songPlayheads,0,4);
            songFolderInput=prepared.Root; // ActivateSongDifficulty loads that chart's signed offset before refreshing.
            LoadSongJacket(""); // Reopen reloads image bytes, including same-path replacements.
            ActivateSongDifficulty(prepared.ActiveDifficulty,false);InstallAudioClip(clip,prepared.AudioPath,owned);
            transport.Seek(0);timeText="0";showNotes=true;autoCursor=true;showSongHud=true;
            status="Opened "+prepared.Title+" / "+SongFolderProject.Names[prepared.ActiveDifficulty]+". Music ready. Source charts untouched.";
            if(newCompanion.LegacyUpgradePending)status=L("旧版 Chart Designer 已暂存为迁移草稿；Ctrl+S 写入原版元数据，未保存退出不改变文件。","Legacy Chart Designer staged for native-metadata migration; Ctrl+S saves, exit without Save leaves files unchanged.");
            RememberOpenedSongFolder(prepared.Root);
            RememberOpenedJacket(prepared);
            Debug.Log("InFalsus project: "+prepared.ManifestPath,this);return true;
        }
        private void ReportFolderFailure(string message)
        {status="Open folder failed: "+message;Debug.LogWarning(status,this);StudioFileDialogs.Error("Open folder",message);}
        private System.Collections.IEnumerator OpenSongFolderPlayer(string folder)
        {
            SongFolderProject prepared=null;string chosen="",hash="",error=null;
            try{prepared=PrepareFolderWithConsent(folder);if(prepared!=null){chosen=prepared.AudioPath;hash=SongFolderProject.FileHash(chosen);}}
            catch(Exception ex){error=ex.Message;}
            if(error!=null){songOpenBusy=false;ReportFolderFailure(error);yield break;}
            if(prepared==null){songOpenBusy=false;yield break;}
            AudioClip clip=null;
            yield return DecodePlayerAudio(chosen,(c,e)=>{clip=c;error=e;});
            bool installed=false;
            try
            {
                if(error!=null)throw new IOException(error);
                installed=FinishOpeningFolder(folder,prepared,chosen,hash,clip,true);
            }
            catch(Exception ex){ReportFolderFailure(ex.Message);}
            finally{if(!installed&&clip!=null)Destroy(clip);songOpenBusy=false;}
        }
        private void ChooseSongFolder()
        {
            transport.Pause();
            string initial=DesktopReleasePreferences.InitialSongFolder(songFolderInput,PlayerPrefs.GetString(DesktopReleasePreferences.LastSongFolderKey,""));
            string folder=StudioFileDialogs.Folder("打开歌曲文件夹 / Open song folder",initial);
            if(!string.IsNullOrEmpty(folder))OpenSongFolder(folder);
        }
        private AudioClip PrepareProjectAudio(string file)
        {
#if UNITY_EDITOR
            string full=Path.GetFullPath(file),ext=Path.GetExtension(file).ToLowerInvariant();
            if(ext!=".ogg"&&ext!=".mp3")throw new IOException("Song folders accept .ogg and .mp3 main music.");
            string hash=SongFolderProject.FileHash(full);
            // Cache by file content, not just basename. Different songs called audio.ogg
            // cannot overwrite one another; reopening does not create another duplicate.
            string folder="Assets/InFalsusStudioUser/Audio";
            string diskFolder=Path.Combine(Application.dataPath,"InFalsusStudioUser","Audio");Directory.CreateDirectory(diskFolder);
            string dest=folder+"/song_"+hash+ext;
            string diskDestination=Path.Combine(diskFolder,"song_"+hash+ext);
            if(!File.Exists(diskDestination)){File.Copy(full,diskDestination,false);UnityEditor.AssetDatabase.ImportAsset(dest,UnityEditor.ImportAssetOptions.ForceSynchronousImport);}
            var importer=UnityEditor.AssetImporter.GetAtPath(dest) as UnityEditor.AudioImporter;
            if(importer==null){UnityEditor.AssetDatabase.ImportAsset(dest,UnityEditor.ImportAssetOptions.ForceSynchronousImport);importer=UnityEditor.AssetImporter.GetAtPath(dest) as UnityEditor.AudioImporter;}
            if(importer==null)throw new IOException("Unity did not recognize the selected audio; check that it is a valid MP3/OGG, not only a renamed extension.");
            var settings=importer.defaultSampleSettings;
            if(settings.loadType!=AudioClipLoadType.DecompressOnLoad||importer.loadInBackground||settings.compressionFormat!=AudioCompressionFormat.PCM||!settings.preloadAudioData)
            {settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.loadInBackground=false;importer.SaveAndReimport();}
            var clip=UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(dest);
            if(clip==null||clip.samples<=0||clip.channels<=0||!clip.LoadAudioData()||clip.loadState==AudioDataLoadState.Failed)
                throw new IOException("Unity could not decode that MP3/OGG clip.");
            return clip;
#else
            throw new NotSupportedException("This release's folder MP3/OGG decoder uses Unity Editor AudioImporter. It does not pretend Editor APIs work in a standalone Player. Use the Editor for this acceptance test.");
#endif
        }
        private bool LeaveCurrentDocumentSafely()
        {
            if(!CommitAllInputFieldsNow())return false;
            if(!HasUnsavedEdits&&!HasPendingMetadataInputs())return true;
            int answer=StudioFileDialogs.SaveDecision("保存所有修改过的难度和歌曲信息？\nSave ALL drafts before leaving? Switching and quitting never save automatically.");
            if(answer==1)return false;
            if(answer==0){SaveWorkingCopy(false);return !HasUnsavedEdits&&!HasPendingMetadataInputs();}
            return true;
        }
        private void ActivateSongDifficulty(int index,bool keepCurrent=true)
        {
            if(songProject==null||index<0||index>3)return;
            if(keepCurrent&&index==songProject.ActiveDifficulty)return;
            // Finishing a text field updates MEMORY only, never files.
            if(keepCurrent)StageMetadataInputsForSave();
            CancelHudField();CancelGesture();transport.Pause();
            if(keepCurrent)
            {
                SyncActiveSongDraft();int previous=songProject.ActiveDifficulty;
                songHistories[previous]=History;songPlayheads[previous]=CurrentMs;
            }
            songProject.ActiveDifficulty=index;
            transport.SetAudioOffset(chartCompanion!=null?chartCompanion.Offset(index):songProject.AudioOffsetMs);
            offsetText=transport.AudioOffsetMs.ToString("0.###",CI);
            var d=songProject.Difficulties[index];
            if(songHistories[index]==null)
            {LoadDocument(d.Document.Clone());songHistories[index]=History;}
            else
            {History=songHistories[index];History.BeforeCommit=AuthoringSafety.BeforeCommit;ClearInputDrafts();ResetEditingForDocument();RefreshDocument();RefreshSelectionFields();}
            savedBytes=projectSession.SavedChartBytes(index);songSavedBytes[index]=savedBytes;
            documentDirty=!History.Document.ToBytes().SequenceEqual(savedBytes);
            var h=events.First(e=>e.Kind==EventKind.Chart);timingBpm=h.Get(0).ToString("0.##########",CI);timingMeter=h.Get(1).ToString("0.##########",CI);
            songProject.ActiveDifficulty=index;loadedPath=d.SourcePath;workingCopyPath=d.WorkingPath;
            bookmarks.Clear();loopEnabled=false;transport.Seek(songPlayheads[index]);timeText=CurrentMs.ToString("0.###",CI);
            showNotes=true;UseTool(Tool.Select);radial.InPointMode=false;showInspector=false;
            songTitleInput=songProject.Title;songArtistInput=songProject.Artist;songRatingInput=d.Rating;
            LoadSongJacket(d.JacketPath.Length>0?d.JacketPath:songProject.JacketPath);LoadProjectScoreEvidence();
            RefreshProjectDirty();
            status="Difficulty "+index+" "+d.Name+" "+d.Rating+". Drafts kept IN MEMORY; switching does not save.";
        }
        private bool SaveSongDifficulty()
        {
            if(songProject==null||projectSession==null)return false;
            try
            {
                if(History.InTransaction)CancelGesture();StageMetadataInputsForSave();SyncActiveSongDraft();
                string message=projectSession.Save();if(chartCompanion!=null)chartCompanion.AcceptSaved();
                foreach(var history in songHistories)if(history!=null)history.AcceptSavedOrder();
                RefreshDocument();RefreshSelectionFields();
                for(int i=0;i<4;i++)songSavedBytes[i]=projectSession.SavedChartBytes(i);
                savedBytes=songSavedBytes[songProject.ActiveDifficulty];documentDirty=false;projectDirty=false;
                workingCopyPath=songProject.Difficulties[songProject.ActiveDifficulty].WorkingPath;
                status=message;SavePresentationPreferences();return true;
            }
            catch(Exception ex){RefreshProjectDirty();status="Save failed; drafts remain UNSAVED in memory: "+ex.Message;Debug.LogWarning(status,this);return false;}
        }
        private void DetachSongProject()
        {
            CancelHudField();songProject=null;projectSession=null;chartCompanion=null;looseDesigner="";looseCompanionDirty=false;projectDirty=false;Array.Clear(songHistories,0,4);Array.Clear(songSavedBytes,0,4);Array.Clear(songPlayheads,0,4);
            scoreEvidence=null;evidenceMatches=false;LoadSongJacket("");
        }
        private void ExportSongChart()
        {
            string work=workingCopyPath;
            try{ChooseSave();}finally{workingCopyPath=work;}
        }
        private void LoadSongJacket(string path)
        {
            if(path==songJacketPath)return;if(songJacket!=null)Destroy(songJacket);songJacket=null;songJacketPath="";
            if(string.IsNullOrEmpty(path)||!File.Exists(path))return;
            Texture2D tex=null;
            try
            {
                if(new FileInfo(path).Length>32*1024*1024)throw new IOException("Jacket exceeds 32 MiB limit.");
                tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
                if(!ImageConversion.LoadImage(tex,File.ReadAllBytes(path))||tex.width>8192||tex.height>8192)throw new IOException("Invalid/oversize jacket image.");
                tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;songJacket=tex;songJacketPath=path;
            }
            catch(Exception ex){if(tex!=null)Destroy(tex);Debug.LogWarning("Jacket not loaded: "+ex.Message,this);}
        }
        private string EvidencePath
        {get{return songProject==null?"":songProject.Resolve(".ifstudio/Score/"+songProject.ActiveDifficulty+".json");}}
        private void LoadProjectScoreEvidence()
        {
            scoreEvidence=null;evidenceMatches=false;measuredComboInput="-1";scoreEvidenceStatus="Judgement schedule is provisional. SCORE is not inferred.";
            if(songProject==null||projectSession==null)return;
            byte[] bytes=projectSession.EvidenceBytes(songProject.ActiveDifficulty);if(bytes==null)return;
            try{scoreEvidence=ScoreEvidence.FromBytes(bytes,History.Document.ToBytes());evidenceMatches=true;measuredComboInput=scoreEvidence.TotalCombo.ToString(CI);scoreEvidenceStatus="Measured evidence loaded; editing its value needs Save / Ctrl+S to persist.";}
            catch(Exception ex){scoreEvidenceStatus="Evidence ignored: "+ex.Message;}
        }
        private void SetMeasuredTotalCombo(string value)
        {
            int n;if(!int.TryParse(value,out n)||n < -1){status="Enter -1 for unknown or a measured final combo.";return;}
            if(songProject==null||projectSession==null){status="Open a song folder first.";return;}
            try
            {
                int index=songProject.ActiveDifficulty;byte[] old=projectSession.EvidenceBytes(index);
                Dictionary<string,object> obj;
                if(old!=null)
                {
                    var current=ScoreEvidence.FromBytes(old,History.Document.ToBytes());
                    if(n>=0&&current.Observations.Any(o=>o.Combo>n||o.Score>100000000L+n))throw new IOException("New total contradicts observations.");
                    obj=ProjectJson.Object(ProjectJson.Parse(System.Text.Encoding.UTF8.GetString(old)));obj["totalCombo"]=n;
                }
                else obj=new Dictionary<string,object>{{"format","InFalsusScoreEvidence"},{"version",1},{"chartSha256",SongFolderProject.Sha256(History.Document.ToBytes())},
                    {"totalCombo",n},{"note","User-entered measurement, not model output."},{"observations",new object[0]}};
                projectSession.StageEvidence(index,System.Text.Encoding.UTF8.GetBytes(ProjectJson.Write(obj)));LoadProjectScoreEvidence();RefreshProjectDirty();
                status="Measured total updated in MEMORY. Save project / Ctrl+S persists it.";
            }
            catch(Exception ex){status="Evidence edit failed: "+ex.Message;}
        }
        private void RefreshSongDerivedState()
        {
            cursorMotion.Rebuild(events);evidenceMatches=scoreEvidence!=null&&scoreEvidence.Matches(History.Document.ToBytes());
            try
            {
                if(!comboReferencesLoaded){comboReferencesLoaded=true;var asset=Resources.Load<TextAsset>("InFalsusStudio/ComboReferences");comboReferenceJson=asset!=null?asset.text:null;}
                comboSummary=TickCountResearch.Analyze(events,comboReferenceJson);comboTimeline=new ProvisionalComboTimeline(events);
            }
            catch(Exception ex){comboSummary=new ComboCountSummary{Supported=false,Warning="Count unavailable: "+ex.Message};comboTimeline=null;}
        }
        private void CaptureSongGuiFrame()
        {
            guiShowSongPanel=showSongPanel&&guiShowUi;guiShowSongHud=showSongHud;
            guiComboText=comboSummary==null||!comboSummary.Supported?"No supported count":
                (comboSummary.Verified?"MEASURED REFERENCE MATCH: ":"PROVISIONAL COUNT: ")+comboSummary.Total+
                " | Tap "+comboSummary.Tap+" Hold "+comboSummary.Hold+" Sky "+comboSummary.SkyArea+" Flick "+comboSummary.Flick;
            guiComboWarning=comboSummary==null?"":comboSummary.Warning+(comboSummary.Verified?" Reference: "+comboSummary.Evidence:"");
            guiSongIndex=songProject==null?-1:songProject.ActiveDifficulty;guiSongObjects=events.Count(e=>e.IsNote);
            guiChartDesigner=CurrentChartDesigner;
            guiSongTitle=songProject==null?(HasDocument?"Loose chart":L("未打开歌曲","No song open")):songProject.Title;guiSongArtist=songProject==null?"":songProject.Artist;
            guiSongRoot=songProject==null?"No folder project. Open a folder containing one MP3/OGG.":songProject.Root;
            guiSongRating=guiSongIndex<0?"?":songProject.Difficulties[guiSongIndex].Rating;guiSongAudio=songProject==null?loadedAudioPath:songProject.AudioPath;
            guiSongNotices=songProject==null?new string[0]:songProject.Notices.Concat(songProject.Difficulties[guiSongIndex].Notices).Select(n=>n.Contains("easing mask/name import profile needs")?"Legacy import notice resolved in 0.5: 565 paired Enigma plaintext Sky segments confirm the easing mapping. Work chart not reimported.":n).Distinct().Take(6).ToArray();
        }
        private void DrawSongPanel()
        {
            GUILayout.BeginArea(songPanelRect,GUI.skin.box);BeginPanelContent(ref songPanelScroll,songPanelRect);
            GUILayout.Label(L("歌曲工程 / 四个难度","Song project / four difficulties"));
            if(GUILayout.Button(L("保存整个工程","Save project")+(HasUnsavedEdits?" *":"")+" (Ctrl+S)"))QueueGui(()=>SaveWorkingCopy(false));
            GUILayout.Label(L("回车或离开输入框：只更新草稿。全局保存才写入文件。","Enter / blur updates the draft. Global Save writes files."));
            songFolderInput=CommittedRow(L("工程目录（粘贴后回车打开）","Folder (Enter to open)"),"field_songfolder",songProject!=null?songProject.Root:songFolderInput,value=>OpenSongFolder(value));
            if(GUILayout.Button(L("选择歌曲文件夹…","Choose song folder…")))QueueGui(ChooseSongFolder);
            GUILayout.Label(L("主音频：","Music: ")+Path.GetFileName(guiSongAudio));
            GUILayout.Label(L("谱面记录：","Source objects: ")+guiSongObjects+L("（不是连击物量）"," (not combo)"));
            var expected=songProject;int index=guiSongIndex;
            if(expected!=null&&index>=0)
            {
                songTitleInput=CommittedRow(L("曲名","Title"),"field_song_title",expected.Title,v=>ApplyMetadata(expected,index,v,expected.Artist,expected.Difficulties[index].Rating));
                songArtistInput=CommittedRow(L("曲师","Artist"),"field_song_artist",expected.Artist,v=>ApplyMetadata(expected,index,expected.Title,v,expected.Difficulties[index].Rating));
                songRatingInput=CommittedRow(L("当前难度标级","Difficulty rating"),"field_song_rating",expected.Difficulties[index].Rating,v=>ApplyMetadata(expected,index,expected.Title,expected.Artist,v));
                DrawSongDetailsInputs(expected,index);
            }
            if(HasDocument)DrawDesignerInput();
            autoCursor=GUILayout.Toggle(autoCursor,L("自动指针（可视预演）","Visual autoplay cursor"));
            bool hud=GUILayout.Toggle(guiShowSongHud,L("显示分数与曲目信息","Show score / song HUD"));if(hud!=guiShowSongHud)QueueGui(()=>showSongHud=hud);
            GUILayout.Label(L("指针状态：","Cursor: ")+cursorPose.Phase+"  X="+(cursorPose.X*100).ToString("0.##",CI));
            GUILayout.Label(guiComboText);GUILayout.Label(guiComboWarning);DrawComboDiagnostics();
            GUILayout.Label(L("实测物量证据（不是对象数）","Measured combo evidence (not object count)"));
            GUILayout.Label(scoreEvidenceStatus);
            measuredComboInput=CommittedRow(L("实测总物量，未知填 -1","Measured total, -1 unknown"),"field_measuredcombo",scoreEvidence!=null?scoreEvidence.TotalCombo.ToString(CI):"-1",SetMeasuredTotalCombo);
            foreach(string message in guiSongNotices)GUILayout.Label(message);
            if(GUILayout.Button(L("关闭","Close")))QueueGui(()=>showSongPanel=false);
            EndPanelContent();GUILayout.EndArea();
        }
        private Vector2 songPanelScroll;
    }
}
