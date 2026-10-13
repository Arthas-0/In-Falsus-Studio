using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    public static class Release109SelfTests
    {
        public static string LastReport {get;private set;}="";
        private static readonly Encoding Utf8=new UTF8Encoding(false,true);
        private static bool Rejects(Action action)
        {try{action();return false;}catch(InvalidDataException){return true;}catch(IOException){return true;}catch(ArgumentException){return true;}catch(InvalidOperationException){return true;}}
        private static SongFolderProject Fixture(string root,bool native)
        {
            Directory.CreateDirectory(root);File.WriteAllBytes(Path.Combine(root,"audio.mp3"),new byte[]{1,2,3});
            for(int i=0;i<4;i++)File.WriteAllText(Path.Combine(root,i+".spc"),"chart(100,4)\r\ntap("+(100+i)+",1,1)\r\ncustom_original(64)\r\n",Utf8);
            if(native)File.WriteAllBytes(Path.Combine(root,"song.json"),Utf8.GetBytes(NativeFixture()));
            var project=SongFolderProject.Prepare(root);project.CommitInitialWorkspace();return project;
        }
        private static string NativeFixture()
        {
            var rows=new List<string>();var mirrors=new List<string>();
            for(int i=0;i<4;i++)
            {
                rows.Add("{\"Difficulty\":"+(1<<i)+",\"binary_path\":\""+i+".spc\",\"DisplayChartDesigner\":\"Chart"+i+"\",\"DisplayJacketDesigner\":\"Artist"+i+"\",\"Rating\":5,\"LevelSectionIndicator\":\"5\"}");
                mirrors.Insert(0,"{\"Difficulty\":"+(1<<i)+",\"DisplayChartDesigner\":\"Chart"+i+"\",\"DisplayJacketDesigner\":\"Artist"+i+"\",\"Rating\":5,\"LevelSectionIndicator\":\"5\"}");
            }
            return "\uFEFF{\r\n  \"title\":\"Fixture\",\"artist\":\"Composer\",\r\n  \"charts\":["+string.Join(",",rows)+"],\r\n  \"song_info\":{\"PreviewStartSeconds\":22.343000411987305,\"PreviewEndSeconds\":62.79000473022461,\"ChartInfos\":["+string.Join(",",mirrors)+"]},\r\n  \"unknown\":{\"texture_path_id\":1021763634941600098,\"small_id\":-1753900809042789876,\"escaped\":\"a\\\"b\",\"rate\":1.00e+2}\r\n}\r\n";
        }
        private static Dictionary<string,byte[]> Snapshot(SongFolderProject project)
        {
            return new[]{project.ManifestPath,project.Resolve("song.json")}.Concat(project.Difficulties.Select(d=>d.WorkingPath))
                .Where(File.Exists).ToDictionary(path=>path,File.ReadAllBytes);
        }
        private static bool Same(Dictionary<string,byte[]> snapshot)
        {return snapshot.All(p=>File.Exists(p.Key)&&File.ReadAllBytes(p.Key).SequenceEqual(p.Value));}
        public static void Run(Action<bool,string> check)
        {
            var report=new StringBuilder();int count=0;Action<bool,string> outer=check;LastReport="";
            check=(ok,label)=>{count++;report.AppendLine((ok?"PASS ":"FAIL ")+label);outer(ok,label);};
            check(new Version(ReleaseIdentity.Version)>=new Version("1.0.9")&&ReleaseIdentity.DisplayVersion==ReleaseIdentity.Version,"109 running release identity remains current");
            check(ReleaseNotesCatalog.Recent.Length==3&&ReleaseNotesCatalog.Recent[0].Version==ReleaseIdentity.Version&&
                new Version(ReleaseNotesCatalog.Recent[0].Version)>new Version(ReleaseNotesCatalog.Recent[1].Version)&&
                new Version(ReleaseNotesCatalog.Recent[1].Version)>new Version(ReleaseNotesCatalog.Recent[2].Version),"109 latest three announcements remain ordered");
            check(!Rejects(()=>NativeSongDetails.ValidatePreview(0,0))&&!Rejects(()=>NativeSongDetails.ValidatePreview(22.343,62.79)),"109 unset and ordered fractional preview seconds");
            foreach(double bad in new[]{-1d,double.NaN,double.PositiveInfinity,double.NegativeInfinity})
                check(Rejects(()=>NativeSongDetails.Seconds(bad)),"109 invalid preview scalar rejected "+bad);
            check(Rejects(()=>NativeSongDetails.ValidatePreview(3,2))&&Rejects(()=>NativeSongDetails.ValidatePreview(3,3)),"109 preview range cannot reverse or have zero length");
            check(NativeSongDetails.ValidateCredit("  曲绘画师  ")=="曲绘画师"&&NativeSongDetails.ValidateCredit("")=="","109 jacket credit permits Unicode and clearing");
            check(Rejects(()=>NativeSongDetails.ValidateCredit("a\nb"))&&Rejects(()=>NativeSongDetails.ValidateCredit(new string('x',129))),"109 multiline/oversize credits rejected");
            string temp=Path.Combine(Path.GetTempPath(),"ifstudio109_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
            try
            {
                MetadataChecks(Path.Combine(temp,"metadata"),check);
                MediaChecks(Path.Combine(temp,"media"),check);
                SupplementChecks(Path.Combine(temp,"supplement"),check);
            }
            finally
            {
                string safe=Path.GetFullPath(temp),parent=Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\','/');
                if(Path.GetDirectoryName(safe).Equals(parent,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(safe).StartsWith("ifstudio109_",StringComparison.Ordinal)&&Directory.Exists(safe))Directory.Delete(safe,true);
            }
            LastReport=report.AppendLine("TOTAL: "+count+" actual C# metadata/media checks.").ToString();
        }
        private static void MetadataChecks(string root,Action<bool,string> check)
        {
            var p=Fixture(root,true);var session=new ManualSongSession(p);var before=Snapshot(p);
            check(p.PreviewStartSeconds==22.343000411987305&&p.PreviewEndSeconds==62.79000473022461,"109 native seconds read at source precision");
            check(Enumerable.Range(0,4).All(i=>p.Difficulties[i].JacketDesigner=="Artist"+i),"109 native jacket credits map by difficulty mask, with reversed mirror order");
            check(!session.HasChanges,"109 fresh committed native project is clean");
            NativeSongDetails.StagePreviewTime(p,true,30.125);NativeSongDetails.StagePreviewTime(p,false,70.875);NativeSongDetails.StageJacketDesigner(p,2,"画师");
            check(session.MetadataDirty&&Same(before),"109 preview/credit edits are RAM-only before Save");
            session.Save();string actual=Utf8.GetString(File.ReadAllBytes(p.Resolve("song.json")));
            string expected=NativeFixture().Replace("22.343000411987305","30.125").Replace("62.79000473022461","70.875").Replace("\"Artist2\"","\"画师\"");
            check(actual==expected,"109 Save changes only authorized tokens; BOM, CRLF, 64-bit IDs and unknown numbers remain exact");
            check(p.Difficulties.All(d=>File.ReadAllBytes(d.WorkingPath).SequenceEqual(before[d.WorkingPath])),"109 metadata Save leaves all chart bytes unchanged");
            var saved=SongFolderProject.Prepare(root);var savedSession=new ManualSongSession(saved);
            check(saved.PreviewStartSeconds==30.125&&saved.PreviewEndSeconds==70.875&&saved.Difficulties[2].JacketDesigner=="画师"&&!savedSession.HasChanges,"109 preview and per-difficulty credits survive reopen");
            var manifest=ProjectJson.ReadFile(p.ManifestPath);
            check(ProjectJson.Number(manifest,"previewStartSeconds")==30.125&&ProjectJson.Text(ProjectJson.Object(ProjectJson.Array(ProjectJson.Get(manifest,"difficulties"))[2]),"jacketDesigner")=="画师","109 editor manifest mirrors new native values");
            before=Snapshot(saved);NativeSongDetails.StageJacketDesigner(saved,0,"discarded");NativeSongDetails.StagePreviewTime(saved,false,90);
            var discarded=SongFolderProject.Prepare(root);
            check(Same(before)&&discarded.Difficulties[0].JacketDesigner=="Artist0"&&discarded.PreviewEndSeconds==70.875,"109 unsaved preview/credit drafts do not survive reopen");
            var clean=new ManualSongSession(discarded);before=Snapshot(discarded);
            NativeSongDetails.StagePreviewTime(discarded,true,99);
            check(Rejects(()=>clean.Save())&&Same(before),"109 temporary reversed pair can be typed but Save rejects without writing");
            NativeSongDetails.StagePreviewTime(discarded,false,100);clean.Save();
            check(SongFolderProject.Prepare(root).PreviewStartSeconds==99,"109 entering start before end is supported");
            var changed=SongFolderProject.Prepare(root);var changedSession=new ManualSongSession(changed);NativeSongDetails.StageJacketDesigner(changed,1,"");changedSession.Save();
            var patch=new LosslessJsonPatch(File.ReadAllBytes(changed.Resolve("song.json")));
            check((string)patch.Get("/charts/1/DisplayJacketDesigner")==""&&(string)patch.Get("/song_info/ChartInfos/2/DisplayJacketDesigner")=="","109 clearing credit updates both native mirrors");
            byte[] absent=Utf8.GetBytes("\uFEFF{\r\n\"song_info\":{\"future\":9007199254740993},\"charts\":[{\"Difficulty\":1,\"id\":1021763634941600098}]\r\n}");
            byte[] inserted=NativeSongDetails.PatchJacketDesigner(NativeSongDetails.PatchPreview(absent,1.25,2.75),0,"图师");
            var insertedPatch=new LosslessJsonPatch(inserted);string insertedText=Utf8.GetString(inserted);
            check((double)insertedPatch.Get("/song_info/PreviewStartSeconds")==1.25&&(string)insertedPatch.Get("/charts/0/DisplayJacketDesigner")=="图师","109 missing optional preview/credit members inserted in recognized parents");
            check(insertedText.StartsWith("\uFEFF{\r\n")&&insertedText.Contains("\"future\":9007199254740993")&&insertedText.Contains("\"id\":1021763634941600098"),"109 inserted members preserve BOM, formatting and untouched large integer lexemes");
            byte[] noInfo=NativeSongDetails.PatchPreview(Utf8.GetBytes("{\"unknown\":9007199254740993}"),2,3);
            check(Utf8.GetString(noInfo).Contains("\"unknown\":9007199254740993")&&(double)new LosslessJsonPatch(noInfo).Get("/song_info/PreviewEndSeconds")==3,"109 absent song_info gets only the new preview object");
            check(Rejects(()=>NativeSongDetails.PatchJacketDesigner(absent,3,"missing")),"109 missing difficulty credit rejects instead of inventing chart entries");
            check(Rejects(()=>NativeSongDetails.PatchPreview(Utf8.GetBytes("{\"song_info\":[]}\n"),1,2)),"109 invalid native preview parent rejected");
            check(NativeSongDetails.PatchPreview(noInfo,2,3).SequenceEqual(noInfo),"109 unchanged preview patch keeps exact bytes");
            var stale=SongFolderProject.Prepare(root);stale.PreviewStartSeconds=1;stale.Difficulties[0].JacketDesigner="stale";stale.WriteManifest();
            var native=SongFolderProject.Prepare(root);
            check(native.PreviewStartSeconds==99&&native.Difficulties[0].JacketDesigner=="Artist0","109 native JSON overrides stale manifest cache");
            var conflictSession=new ManualSongSession(native);NativeSongDetails.StagePreviewTime(native,false,101);
            string source=native.Resolve("song.json");File.AppendAllText(source," ",Utf8);before=Snapshot(native);
            check(Rejects(()=>conflictSession.Save())&&Same(before),"109 external metadata changes block the entire Save batch");
            var rollback=SongFolderProject.Prepare(root);var rollbackSession=new ManualSongSession(rollback);before=Snapshot(rollback);
            rollback.Difficulties[0].Document.Append("tap(700,2,1)");NativeSongDetails.StagePreviewTime(rollback,false,102);NativeSongDetails.StageJacketDesigner(rollback,3,"rollback");
            bool failed=Rejects(()=>rollbackSession.Save((i,path)=>{if(path==rollback.ManifestPath)throw new IOException("Injected replacement failure.");}));
            check(failed&&Same(before)&&rollbackSession.HasChanges,"109 source/chart writes roll back if the manifest replacement fails; drafts retained");
            var simple=Fixture(Path.Combine(root,"no_native"),false);var simpleSession=new ManualSongSession(simple);
            NativeSongDetails.StagePreviewTime(simple,false,12.5);NativeSongDetails.StageJacketDesigner(simple,3,"Editor artist");simpleSession.Save();
            var simpleAgain=SongFolderProject.Prepare(simple.Root);
            check(simpleAgain.PreviewEndSeconds==12.5&&simpleAgain.Difficulties[3].JacketDesigner=="Editor artist"&&!File.Exists(simpleAgain.Resolve("song.json")),"109 music-only project stores parameters in manifest without fabricating native source");
        }
        private static void MediaChecks(string root,Action<bool,string> check)
        {
            var p=Fixture(root,false);var session=new ManualSongSession(p);p.Difficulties[0].Document.Append("hold(400,1,1,800)");session.Save();
            var before=Snapshot(p);string mp3=p.Resolve("audio.mp3"),ogg=p.Resolve("audio.ogg");File.Move(mp3,ogg);
            var renamed=SongFolderProject.Prepare(root);
            check(renamed.AudioPath==ogg&&renamed.Notices.Any(n=>n.Contains("rediscovered")),"109 missing MP3 path rediscovers the replacement OGG");
            check(Same(before)&&renamed.Difficulties[0].Document.ToBytes().SequenceEqual(before[p.Difficulties[0].WorkingPath]),"109 music rediscovery does not rewrite metadata or edited chart workspace");
            var replacementSession=new ManualSongSession(renamed);check(replacementSession.MetadataDirty,"109 rediscovered resource path is an unsaved metadata draft");replacementSession.Save();
            var again=SongFolderProject.Prepare(root);check(again.AudioPath==ogg&&!new ManualSongSession(again).HasChanges,"109 explicit Save persists the replacement music path");
            string oldAudioHash=SongFolderProject.FileHash(ogg);File.WriteAllBytes(ogg,new byte[]{7,8,9});
            check(SongFolderProject.FileHash(SongFolderProject.Prepare(root).AudioPath)!=oldAudioHash,"109 same-name music replacements use fresh file bytes");
            string one=p.Resolve("new_cover.PNG");File.WriteAllBytes(one,new byte[]{1,2,3,4});before=Snapshot(again);
            var single=SongFolderProject.Prepare(root);
            check(single.JacketPath==one&&!single.JacketChoiceRequired&&single.Difficulties.All(d=>d.JacketPath==one),"109 one newly added image becomes the jacket in an existing project");
            check(Same(before),"109 image discovery leaves existing files untouched");new ManualSongSession(single).Save();
            string nested=p.Resolve("Art/second.jpeg");Directory.CreateDirectory(Path.GetDirectoryName(nested));File.WriteAllBytes(nested,new byte[]{5,6,7});
            var multiple=SongFolderProject.Prepare(root);
            check(multiple.JacketCandidates.Length==2&&multiple.JacketChoiceRequired,"109 additional PNG/JPEG including nested art requires a choice");
            check(Rejects(()=>multiple.CommitInitialWorkspace()),"109 unresolved multiple-image choice cannot commit/open a workspace");
            check(Rejects(()=>SongMediaDiscovery.ChooseJacket(multiple,ogg)),"109 jacket choice must come from discovered image candidates");
            SongMediaDiscovery.ChooseJacket(multiple,nested);
            check(!multiple.JacketChoiceRequired&&multiple.Difficulties.All(d=>d.JacketPath==nested),"109 chosen jacket is applied to all difficulty slots");
            before=Snapshot(multiple);var unsavedOpen=SongFolderProject.Prepare(root);
            check(SongMediaDiscovery.UseRememberedJacket(unsavedOpen,multiple.Relative(nested),multiple.JacketInventorySha256)&&!unsavedOpen.JacketChoiceRequired&&unsavedOpen.JacketPath==nested&&Same(before),"109 local choice can restore an unsaved selection without song-file writes");
            new ManualSongSession(multiple).Save();var stable=SongFolderProject.Prepare(root);
            check(!stable.JacketChoiceRequired&&stable.JacketPath==nested&&stable.JacketInventorySha256==multiple.JacketInventorySha256,"109 unchanged multiple-image inventory preserves the saved selection");
            check(SongMediaDiscovery.PreferenceKey(root)==SongMediaDiscovery.PreferenceKey(root.ToUpperInvariant())&&SongMediaDiscovery.PreferenceKey(root)!=SongMediaDiscovery.PreferenceKey(root+"_other"),"109 per-folder local keys are case insensitive and isolated");
            File.WriteAllBytes(nested,new byte[]{6,6,6});var replaced=SongFolderProject.Prepare(root);
            check(replaced.JacketChoiceRequired&&replaced.JacketInventorySha256!=stable.JacketInventorySha256,"109 same-name same-length image replacement invalidates old inventory");
            check(!SongMediaDiscovery.UseRememberedJacket(replaced,stable.Relative(nested),stable.JacketInventorySha256),"109 stale local selection cannot suppress a changed-image prompt");
            check(Rejects(()=>SongMediaDiscovery.ReapplyOpeningChoice(stable,replaced)),"109 images changing during opening reject stale installation");
            File.Delete(nested);var removed=SongFolderProject.Prepare(root);
            check(removed.JacketCandidates.Length==1&&!removed.JacketChoiceRequired&&removed.JacketPath==one,"109 removed selected image falls back to the sole remaining image");
            File.Delete(one);var empty=SongFolderProject.Prepare(root);
            check(empty.JacketCandidates.Length==0&&empty.JacketPath==""&&empty.Difficulties.All(d=>d.JacketPath==""),"109 removing all images clears stale covers");
            string hidden=p.Resolve(".ifstudio/SaveHistory/private.png");Directory.CreateDirectory(Path.GetDirectoryName(hidden));File.WriteAllBytes(hidden,new byte[]{1});
            check(SongFolderProject.Prepare(root).JacketCandidates.Length==0,"109 internal workspace/history images are excluded from discovery");
            File.Delete(ogg);File.WriteAllBytes(p.Resolve("replacement.mp3"),new byte[]{1});File.WriteAllBytes(p.Resolve("another.ogg"),new byte[]{2});
            bool ambiguous=false;try{SongFolderProject.Prepare(root);}catch(SongAudioConflict ex){ambiguous=ex.Files.Length==2&&!ex.CanUseDeclaredPrimary;}
            check(ambiguous,"109 multiple replacement tracks require explicit main selection");
            var chosen=SongFolderProject.Prepare(root,true,p.Resolve("replacement.mp3"));
            check(chosen.AudioPath==p.Resolve("replacement.mp3")&&chosen.ExcludedSupplementary.SequenceEqual(new[]{p.Resolve("another.ogg")}),"109 explicit main selection preserves and records other audio files");
            check(Rejects(()=>SongFolderProject.Prepare(root,true,p.Resolve("0.spc"))),"109 main selection cannot use a non-audio source file");
            new ManualSongSession(chosen).Save();
            check(SongFolderProject.Prepare(root).AudioPath==chosen.AudioPath,"109 ambiguous main selection survives Save/reopen");
            File.Delete(chosen.Difficulties[1].WorkingPath);
            check(Rejects(()=>SongFolderProject.Prepare(root)),"109 media repair still refuses missing edited chart workspace");
        }
        private static void SupplementChecks(string root,Action<bool,string> check)
        {
            var p=Fixture(root,false);string extra=p.Resolve("preview.ogg");File.WriteAllBytes(extra,new byte[]{4});p.ExcludedSupplementary.Add(extra);p.WriteManifest();
            File.Delete(p.AudioPath);string replacement=p.Resolve("renamed.ogg");File.WriteAllBytes(replacement,new byte[]{5});
            var recovered=SongFolderProject.Prepare(root);
            check(recovered.AudioPath==replacement&&recovered.ExcludedSupplementary.Contains(extra),"109 sole non-supplementary candidate replaces missing main music");
            File.Delete(replacement);
            check(Rejects(()=>SongFolderProject.Prepare(root)),"109 an excluded supplementary track is not silently substituted as main");
        }
    }
}
