using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace InFalsusStudio.Core
{
    public static class Release100SelfTests
    {
        private static bool Throws(Action action){try{action();return false;}catch{return true;}}
        private static Dictionary<string,byte[]> Snapshot(string root)
        {return Directory.GetFiles(root,"*",SearchOption.AllDirectories).Where(x=>!x.EndsWith(".bak",StringComparison.OrdinalIgnoreCase)&&!x.Contains(Path.DirectorySeparatorChar+"SaveHistory"+Path.DirectorySeparatorChar)).ToDictionary(x=>x,x=>File.ReadAllBytes(x));}
        private static bool Same(Dictionary<string,byte[]> snapshot)
        {return snapshot.All(x=>File.Exists(x.Key)&&File.ReadAllBytes(x.Key).SequenceEqual(x.Value));}
        public static void Run(Action<bool,string> check)
        {
            check(new Version(ReleaseIdentity.Version).Major==1,"100 release retains major version 1");
            check(DesktopPresentationPolicy.PlayContains(54,122),"100 arrow inside hit target");
            check(!DesktopPresentationPolicy.PlayContains(110,128),"100 score interior not clickable as Play");
            check(!DesktopPresentationPolicy.PlayContains(84,112),"100 slanted outside corner not clickable");
            check(DesktopPresentationPolicy.PlayContains(86,138),"100 bottom play nook clickable");
            string filter=NativeDialogFilter.Make("png,jpg,jpeg");
            check(filter.EndsWith("\0\0")&&filter.Contains("*.png;*.jpg;*.jpeg\0"),"100 multi-pattern Unicode filter");
            check(NativeDialogFilter.Make("")=="All files\0*.*\0\0","100 empty-extension all-files filter");
            check(NativeDialogFilter.DefaultExtension("png,jpg")==null,"100 no invalid multi-extension default");
            check(NativeDialogFilter.DefaultExtension(".spc")=="spc","100 default SPC extension");
            check(Throws(()=>NativeDialogFilter.Make("png\0*.exe")),"100 filter rejects embedded NUL injection");
            foreach(var d in new[]{new DisplaySize(1920,1080),new DisplaySize(2560,1440),new DisplaySize(3840,2160),new DisplaySize(5120,2160),new DisplaySize(3440,1440)})
            {
                var chosen=DesktopPresentationPolicy.ExclusiveSize(d,new[]{new DisplaySize(1280,720),d});
                check(chosen.Width==d.Width&&chosen.Height==d.Height,"100 exclusive keeps desktop aspect "+d.Width);
            }
            check(Throws(()=>DesktopPresentationPolicy.ExclusiveSize(new DisplaySize(3440,1440),new[]{new DisplaySize(1920,1080)})),"100 reject unsupported stretched exclusive mode");
            check(ChartCompanionSession.ValidateDesigner("  Arthas  ")=="Arthas","100 designer normalized");
            check(ChartCompanionSession.ValidateDesigner("")=="","100 designer may be cleared");
            check(Throws(()=>ChartCompanionSession.ValidateDesigner("a\nb")),"100 designer single-line limit");
            check(Throws(()=>ChartCompanionSession.ValidateOffset(double.NaN)),"100 invalid offset rejected");
            check(ChartCompanionSession.ValidateOffset(-150)==-150&&ChartCompanionSession.ValidateOffset(150)==150,"100 offset retains both signs");
            string root=Path.Combine(Path.GetTempPath(),"ifstudio-100-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                // Dummy audio bytes are sufficient for Prepare's filesystem validation;
                // this core test does NOT pretend to decode audio or touch user songs.
                File.WriteAllBytes(Path.Combine(root,"music.ogg"),new byte[]{1,2,3});
                string source=Path.Combine(root,"song.json");
                File.WriteAllText(source,"{\"unrecognized_native_title\":\"leave alone\",\"largeID\":9223372036854775807,\"song_info\":{\"ChartInfos\":[{\"Difficulty\":8,\"DisplayChartDesigner\":\"native credit\"}]}}");
                var p=SongFolderProject.Prepare(root);p.Difficulties[3].Designer="native credit";p.CommitInitialWorkspace();
                var baseFiles=Snapshot(root);var session=new ManualSongSession(p);var notes=new ChartCompanionSession(p,session);
                check(Same(baseFiles)&&!File.Exists(p.Resolve(ChartCompanionSession.RelativePath)),"100 open creates no companion file");
                notes.SetDesigner(3,"Arthas");notes.SetOffset(3,125);notes.SetOffset(0,-75);
                check(session.HasChanges&&Same(baseFiles),"100 edits only RAM before Save");
                check(!File.Exists(p.Resolve(ChartCompanionSession.RelativePath)),"100 no eager write on Enter");
                check(p.AudioOffsetMs==0&&p.Difficulties[3].Designer=="Arthas","101 native designer draft updates, shared legacy offset unchanged");
                session.Save();notes.AcceptSaved();
                check(!session.HasChanges&&File.Exists(p.Resolve(ChartCompanionSession.RelativePath)),"100 explicit Save includes companion");
                check(File.ReadAllText(source)==new UTF8Encoding(false,true).GetString(baseFiles[source]).Replace("native credit","Arthas"),"101 only native designer changes, without requiring a recognized title");
                var manifest=ProjectJson.ReadFile(p.ManifestPath);
                var slots=ProjectJson.Array(ProjectJson.Get(manifest,"difficulties"));
                check(ProjectJson.Text(ProjectJson.Object(slots[3]),"designer")=="Arthas","101 native designer reflected in main manifest");
                var reopened=SongFolderProject.Prepare(root);var rs=new ManualSongSession(reopened);var rn=new ChartCompanionSession(reopened,rs);
                check(rn.Designer(3)=="Arthas"&&rn.Designer(0)=="","101 native per-difficulty designer survives reopen");
                check(!File.ReadAllText(reopened.Resolve(ChartCompanionSession.RelativePath)).Contains("chartDesigner"),"101 companion stores offsets only");
                check(rn.Offset(3)==125&&rn.Offset(0)==-75&&rn.Offset(1)==0,"100 chart offsets survive independently, no inversion");
                rn.SetDesigner(3,"temporary");rn.SetDesigner(3,"Arthas");check(!rs.HasChanges,"100 reverting to saved signature removes pending change");
                var saved=Snapshot(root);rn.SetDesigner(3,"unsaved");
                var abandonedProject=SongFolderProject.Prepare(root);
                var abandoned=new ChartCompanionSession(abandonedProject,new ManualSongSession(abandonedProject));
                check(abandoned.Designer(3)=="Arthas"&&Same(saved),"100 exiting without Save loses only RAM draft");
                reopened.Difficulties[0].Document.Append("tap(1000,1,1)");
                check(Throws(()=>rs.Save((i,path)=>{if(i==1)throw new IOException("injected save failure");})),"100 batch failure propagated");
                check(Same(saved)&&rs.HasChanges,"100 rollback chart and companion, drafts retained");
                rn.SetOffset(3,126); // This save explicitly changes the companion too.
                string companion=reopened.Resolve(ChartCompanionSession.RelativePath);File.AppendAllText(companion," ");byte[] external=File.ReadAllBytes(companion);
                check(Throws(()=>rs.Save()),"100 external companion changes reject whole batch");
                check(File.ReadAllBytes(companion).SequenceEqual(external)&&File.ReadAllBytes(reopened.Difficulties[0].WorkingPath).SequenceEqual(saved[reopened.Difficulties[0].WorkingPath]),"100 conflict does not overwrite external file or chart");
            }
            finally{try{Directory.Delete(root,true);}catch{}}
        }
    }
}
