using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    /// <summary>Pure C# regressions. Run in Unity or .NET; never touches a user's song folder.</summary>
    public static class Release101SelfTests
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private static bool Throws(Action action) { try { action(); return false; } catch { return true; } }
        private static Dictionary<string, byte[]> Tracked(string root)
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(x => !x.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) && !x.Contains(Path.DirectorySeparatorChar + "SaveHistory" + Path.DirectorySeparatorChar))
                .ToDictionary(x => x, File.ReadAllBytes);
        }
        private static bool Same(Dictionary<string, byte[]> snapshot)
        { return snapshot.All(x => File.Exists(x.Key) && File.ReadAllBytes(x.Key).SequenceEqual(x.Value)); }
        private static string Source()
        {
            string rows = string.Join(",\r\n", Enumerable.Range(0,4).Select(i =>
                "{\"Difficulty\":" + (1<<i) + ",\"DisplayChartDesigner\":\"native"+i+"\",\"TextureID\":9223372036854775807}"));
            return "\uFEFF{\r\n\"unrecognized_title\":\"Do not require a title to edit a credit\",\r\n" +
                "\"charts\":["+rows+"],\r\n\"song_info\":{\"ChartInfos\":["+rows+"]},\r\n" +
                "\"unknown\":{\"braces\":\"{ } [ ] \\\" slash / tilde ~\",\"negativeID\":-9223372036854775808}}\r\n";
        }
        private static string Companion(SongFolderProject p)
        {
            var rows = new List<object>();
            for(int i=0;i<4;i++)rows.Add(new Dictionary<string,object> {
                {"difficulty",i}, {"chart",p.Relative(p.Difficulties[i].WorkingPath)},
                {"chartDesigner",i==3?"legacy user":"native"+i}, {"audioOffsetMs",i==3?-75.5:0} });
            return ProjectJson.Write(new Dictionary<string,object> { {"format","InFalsusStudioChartNotes"},{"version",1},{"charts",rows} });
        }
        private static SongFolderProject Empty(string root)
        {
            Directory.CreateDirectory(root);File.WriteAllBytes(Path.Combine(root,"music.ogg"),new byte[]{1,2,3});
            var p=SongFolderProject.Prepare(root);p.CommitInitialWorkspace();return p;
        }
        public static void Run(Action<bool,string> check)
        {
            // Patch releases retain the 1.x identity/attribution contract; the current
            // version is asserted by Visual102SelfTests, not pinned to this historical suite.
            check(new Version(ReleaseIdentity.Version)>=new Version(1,0,1),"101 release version is not older than its introducing patch");
            check(ReleaseIdentity.SettingsTitle=="In Falsus Studio "+ReleaseIdentity.DisplayVersion+" · By Arthas","101 settings attribution");
            double area=((DesktopPresentationPolicy.PlayRightTop-DesktopPresentationPolicy.PlayLeft)+(DesktopPresentationPolicy.PlayRightBottom-DesktopPresentationPolicy.PlayLeft))*.5*(DesktopPresentationPolicy.PlayBottom-DesktopPresentationPolicy.PlayTop);
            check(area>=1.9*1682,"101 play hit area is at least 1.9 times the 1.0 area");
            check(DesktopPresentationPolicy.PlayContains(16,100)&&DesktopPresentationPolicy.PlayContains(92,140),"101 expanded upper and lower corners clickable");
            check(!DesktopPresentationPolicy.PlayContains(105,125)&&!DesktopPresentationPolicy.PlayContains(138,120),"101 play never captures score interior");
            check(!DesktopPresentationPolicy.PlayContains(86,99),"101 right cutout not clickable");
            byte[] original=Utf8.GetBytes(Source());string credit="Arthas \"谱师\" / \\ test";
            byte[] modified=NativeChartDesigner.Patch(original,3,credit);
            string encoded=ProjectJson.Write(credit).TrimEnd('\r','\n');
            check(Utf8.GetString(modified)==Source().Replace("\"native3\"",encoded),"101 two mirrored tokens only, preserves BOM CRLF huge integers escapes");
            check(NativeChartDesigner.Patch(original,3,"native3").SequenceEqual(original),"101 unchanged designer is byte-identical");
            var cleared=new LosslessJsonPatch(NativeChartDesigner.Patch(original,0,""));
            check((string)cleared.Get("/charts/0/DisplayChartDesigner")==""&&(string)cleared.Get("/song_info/ChartInfos/0/DisplayChartDesigner")=="","101 clear mirrors empty designer");
            string optional="{\"charts\":[{\"Difficulty\":8,\"n\":9223372036854775807,\"weird\":{\"a/b~\":\"} ] \\\"\"}}]}";
            string inserted=Utf8.GetString(NativeChartDesigner.Patch(Utf8.GetBytes(optional),3,"Arthas"));
            check(inserted.Replace(",\"DisplayChartDesigner\":\"Arthas\"","")==optional,"101 inserts missing optional key without rewriting its object");
            check(Throws(()=>NativeChartDesigner.Patch(Utf8.GetBytes("{\"charts\":[{\"Difficulty\":8},{\"Difficulty\":8}]}"),3,"a")),"101 duplicate difficulty rejects write");
            check(Throws(()=>NativeChartDesigner.Patch(Utf8.GetBytes("{\"charts\":[{\"Difficulty\":8,\"DisplayChartDesigner\":17}]}"),3,"a")),"101 unsupported designer type rejects write");
            check(Throws(()=>NativeChartDesigner.Patch(Utf8.GetBytes("{\"other\":3}"),3,"a")),"101 unknown schema rejects designer save, no guessed native row");
            check(Throws(()=>NativeChartDesigner.Validate(new string('x',129)))&&Throws(()=>NativeChartDesigner.Validate("x\ny")),"101 invalid designer input rejected");
            string root=Path.Combine(Path.GetTempPath(),"ifstudio-101-"+Guid.NewGuid().ToString("N"));
            try
            {
                var p=Empty(Path.Combine(root,"native"));string source=p.Resolve("song.json");File.WriteAllBytes(source,original);
                var session=new ManualSongSession(p);var offsets=new ChartCompanionSession(p,session);var before=Tracked(p.Root);
                check(p.Difficulties[3].Designer=="native3"&&!session.HasChanges,"101 native designer overrides stale cache, opening alone does not write");
                offsets.SetDesigner(3,"Arthas");offsets.SetDesigner(0,"MIN designer");
                check(session.MetadataDirty&&Same(before),"101 two designer drafts are RAM-only");
                var abandoned=SongFolderProject.Prepare(p.Root);var abandonedSession=new ManualSongSession(abandoned);
                check(abandoned.Difficulties[3].Designer=="native3"&&Same(before),"101 reopening without save retains native values");
                session.Save();offsets.AcceptSaved();
                var savedSource=new LosslessJsonPatch(File.ReadAllBytes(source));
                check((string)savedSource.Get("/charts/3/DisplayChartDesigner")=="Arthas"&&(string)savedSource.Get("/song_info/ChartInfos/0/DisplayChartDesigner")=="MIN designer","101 Save commits all changed designers to native mirrors");
                check(!File.Exists(p.Resolve(ChartCompanionSession.RelativePath)),"101 designer-only edit creates NO companion file");
                var main=ProjectJson.ReadFile(p.ManifestPath);var slots=ProjectJson.Array(ProjectJson.Get(main,"difficulties"));
                check(ProjectJson.Text(ProjectJson.Object(slots[3]),"designer")=="Arthas","101 main project cache stays consistent");
                var saved=Tracked(p.Root);offsets.SetDesigner(3,"temporary");offsets.SetDesigner(3,"Arthas");
                check(!session.HasChanges&&Same(saved),"101 returning designer to saved value clears dirty state");
                p.Difficulties[0].Document.Append("tap(500,1,1)");offsets.SetDesigner(3,"next");
                check(Throws(()=>session.Save((i,path)=>{if(i==1)throw new IOException("injected");})),"101 mixed chart and metadata batch failure propagated");
                check(Same(saved)&&session.HasChanges,"101 failed save rolls back actual chart/source; draft survives");
                File.AppendAllText(source," ");byte[] external=File.ReadAllBytes(source);
                check(Throws(()=>session.Save())&&external.SequenceEqual(File.ReadAllBytes(source)),"101 external source changes preserved on conflict");
                check(File.ReadAllBytes(p.Difficulties[0].WorkingPath).SequenceEqual(saved[p.Difficulties[0].WorkingPath]),"101 preflight source conflict prevents chart partial save");

                var m=Empty(Path.Combine(root,"migration"));File.WriteAllBytes(m.Resolve("song.json"),original);
                string legacyPath=m.Resolve(ChartCompanionSession.RelativePath);Directory.CreateDirectory(Path.GetDirectoryName(legacyPath));File.WriteAllText(legacyPath,Companion(m),Utf8);
                var ms=new ManualSongSession(m);var migrationBefore=Tracked(m.Root);var mc=new ChartCompanionSession(m,ms);
                check(mc.LegacyUpgradePending&&mc.Designer(3)=="legacy user"&&ms.HasChanges&&Same(migrationBefore),"101 legacy v1 migration is staged, not eager-written");
                ms.Save();mc.AcceptSaved();
                string migrated=File.ReadAllText(legacyPath);var json=ProjectJson.ReadFile(legacyPath);
                check(ProjectJson.Integer(json,"version")==2&&!migrated.Contains("chartDesigner"),"101 migration removes designer from offset companion");
                check((string)new LosslessJsonPatch(File.ReadAllBytes(m.Resolve("song.json"))).Get("/charts/3/DisplayChartDesigner")=="legacy user","101 legacy credit reaches native field on Save");
                var reopened=SongFolderProject.Prepare(m.Root);var rs=new ManualSongSession(reopened);var rc=new ChartCompanionSession(reopened,rs);
                check(rc.Offset(3)==-75.5&&rc.Offset(0)==0&&!rc.LegacyUpgradePending&&!rs.HasChanges,"101 v2 offsets survive; no recurring migration on reopen");

                var n=Empty(Path.Combine(root,"music-only"));var ns=new ManualSongSession(n);var nc=new ChartCompanionSession(n,ns);
                nc.SetDesigner(2,"new chart");ns.Save();nc.AcceptSaved();
                check(!File.Exists(n.Resolve("song.json"))&&!File.Exists(n.Resolve(ChartCompanionSession.RelativePath)),"101 music-only project needs no fabricated native file or signer sidecar");
                var nn=SongFolderProject.Prepare(n.Root);check(nn.Difficulties[2].Designer=="new chart","101 music-only designer persists in main project");

                var u=Empty(Path.Combine(root,"unknown-source"));File.WriteAllText(u.Resolve("song.json"),"{\"unknown_title\":\"x\",\"id\":9223372036854775807}",Utf8);
                var us=new ManualSongSession(u);var uc=new ChartCompanionSession(u,us);byte[] unchanged=File.ReadAllBytes(u.Resolve("song.json"));
                uc.SetOffset(3,150);us.Save();uc.AcceptSaved();
                check(unchanged.SequenceEqual(File.ReadAllBytes(u.Resolve("song.json"))),"101 offset-only save never needs native title/designer schema");
            }
            finally { if(Directory.Exists(root))Directory.Delete(root,true); }
        }
    }
}
