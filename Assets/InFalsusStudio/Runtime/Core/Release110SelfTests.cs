using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    public static class Release110SelfTests
    {
        public static string LastReport {get;private set;}="";
        private static readonly CultureInfo CI=CultureInfo.InvariantCulture;
        private static SpcEvent Find(SpcDocument d,int id){return d.ReadEvents().First(e=>e.SourceId==id);}
        private static string Sky(double t,double duration,double center=50,string group="")
        {return "skyarea("+t.ToString("R",CI)+","+center.ToString(CI)+",100,20,"+center.ToString(CI)+",100,20,0,0,"+duration.ToString("R",CI)+(group.Length>0?","+group:"")+")";}
        private static bool Rejects(Action action)
        {try{action();return false;}catch(IOException){return true;}catch(ArgumentException){return true;}catch(InvalidOperationException){return true;}}
        public static void Run(Action<bool,string> outer)
        {
            var report=new StringBuilder();int count=0;LastReport="";
            Action<bool,string> check=(ok,label)=>{count++;report.AppendLine((ok?"PASS ":"FAIL ")+label);outer(ok,label);};
            check(ReleaseIdentity.Version=="1.0.10"&&ReleaseIdentity.DisplayVersion==ReleaseIdentity.Version,"110 release identity");
            check(ReleaseNotesCatalog.Recent.Select(n=>n.Version).SequenceEqual(new[]{"1.0.10","1.0.9","1.0.8"}),"110 latest three announcements");
            Connections(check);Groups(check);Health(check);Sorting(check);Saves(check);
            report.AppendLine("TOTAL: "+count+" actual C# 1.0.10 authoring/save checks passed.");LastReport=report.ToString();
        }
        private static void Connections(Action<bool,string> check)
        {
            foreach(double delta in new[]{-2.0001,-2,-1.5,0,1.5,2,2.0001})
            {
                var d=SpcDocument.Parse("chart(120,4)\n"+Sky(100,100)+"\n"+Sky(200+delta,100)+"\n");
                var before=d.ToBytes();bool expected=Math.Abs(delta)<=2;
                check(SkyGroups.Joins(Find(d,1),Find(d,2))==expected&&SkyGroups.Heads(d.ReadEvents()).Contains(2)!=expected,"110 gap/overlap boundary "+delta.ToString(CI)+" ms");
                check(d.ToBytes().SequenceEqual(before),"110 connection check leaves timestamps and raw source intact "+delta.ToString(CI));
            }
            var away=SpcDocument.Parse("chart(120,4)\n"+Sky(100,100,10)+"\n"+Sky(202,100,90)+"\n");
            check(SkyGroups.Heads(away.ReadEvents()).Count==2,"110 timing tolerance does not join spatially disjoint endpoints");
            var equal=SpcDocument.Parse("chart(120,4)\n"+Sky(100,1)+"\n"+Sky(100,1)+"\n");
            check(SkyGroups.Heads(equal.ReadEvents()).Count==2,"110 simultaneous starts do not form cyclic predecessors");
            var explicitGroup=SpcDocument.Parse("chart(120,4)\n"+Sky(0,100,10,"7")+"\n"+Sky(1000,100,90,"7")+"\n");
            check(SkyGroups.Heads(explicitGroup.ReadEvents()).SetEquals(new[]{1}),"110 explicit group rendering retains chronological first-head behavior");
        }
        private static void Groups(Action<bool,string> check)
        {
            var original=SpcDocument.Parse("chart(120,4)\r\n"+Sky(200,100,50,"20,123.50")+" # retained\r\n"+
                Sky(0,100,25,"7")+"\n"+Sky(102,98,25,"-1")+"\r\n"+Sky(50,50,75,"7")+"\n"+Sky(198,100,25)+"\r\nfuture(42)",true);
            var before=original.ToBytes();var automatic=original.Clone();SkySplitOperations.ResetGroups(automatic);
            var expectedHeads=SkyGroups.Heads(automatic.ReadEvents());
            var history=new EditHistory(original.Clone()){BeforeCommit=AuthoringSafety.BeforeCommit};
            SkyAutoGrouping.Result result=default;history.Execute(d=>result=SkyAutoGrouping.Assign(d));
            check(result.Notes==5&&result.Groups==3&&result.Changed==5,"110 explicit tool rebuilds all five SkyAreas into three chronological chains");
            check(new[]{2,3,5}.All(id=>Find(history.Document,id).Get(10)==0)&&Find(history.Document,4).Get(10)==1&&Find(history.Document,1).Get(10)==2,"110 numbering starts at zero in time order, joins gaps and overlaps, replaces old IDs");
            check(SkyGroups.Heads(history.Document.ReadEvents()).SetEquals(expectedHeads),"110 auto-grouped heads match automatic geometric heads");
            check(history.Document.ArgumentTokens(1).Take(10).SequenceEqual(original.ArgumentTokens(1).Take(10))&&history.Document.SourceLine(1).EndsWith(",2,123.50) # retained"),"110 only group token changes; nonstandard suffix tokens stay positional and byte-exact");
            check(history.Document.LineIdAt(1)==1&&history.Document.HasUtf8Bom&&history.Document.SourceLine(6)=="future(42)","110 grouping retains source order, IDs, BOM and unknown statement");
            check(history.UndoCount==1&&history.Undo()&&history.Document.ToBytes().SequenceEqual(before)&&history.Redo(),"110 grouping is one exact undo/redo step");
            int undo=history.UndoCount;history.Execute(d=>result=SkyAutoGrouping.Assign(d));
            check(result.Changed==0&&history.UndoCount==undo,"110 repeated grouping is deterministic and creates no empty undo");
            int created=-1;history.Execute(d=>created=EditOperations.Add(d,EventKind.SkyArea,500,600,1,1,.5,.5,.1));
            check(Find(history.Document,created).Args.Length==10,"110 new Sky remains unassigned after using auto-group tool");
            var merge=SpcDocument.Parse("chart(120,4)\n"+Sky(0,100,30)+"\n"+Sky(0,100,70)+"\n"+
                "skyarea(102,50,100,60,50,100,60,0,0,100)\n"+Sky(204,100,30)+"\n"+Sky(204,100,70)+"\n");
            var heads=SkyGroups.Heads(merge.ReadEvents());var joined=SkyAutoGrouping.Assign(merge);
            check(joined.Groups==2&&SkyGroups.Heads(merge.ReadEvents()).SetEquals(heads),"110 merge/branch preserves both original roots and connected successor heads");
            check(new[]{1,3,4,5}.All(id=>Find(merge,id).Get(10)==0)&&Find(merge,2).Get(10)==1,"110 equal-time merge tie chooses first source predecessor without merging unrelated earlier roots");
            var bad=SpcDocument.Parse("chart(120,4)\n"+Sky(0,100)+"\nskyarea(100,50,0,20,50,100,20,0,0,100)\n");byte[] bytes=bad.ToBytes();
            check(Rejects(()=>SkyAutoGrouping.Assign(bad))&&bad.ToBytes().SequenceEqual(bytes),"110 invalid geometry rejects whole auto-group command before any mutation");
            check(SkyAutoGrouping.Assign(SpcDocument.Parse("chart(120,4)\n")).Groups==0,"110 empty chart grouping is a no-op");
        }
        private static void Health(Action<bool,string> check)
        {
            var d=SpcDocument.Parse("chart(120,4)\n"+string.Join("\n",new[]{
                "hold(100,1,1,4.999)","hold(200,1,1,5)","hold(300,1,1,0)","hold(400,1,1,-1)",
                Sky(500,4.999),Sky(600,5),Sky(700,0),Sky(800,-1),"unknown(9)"})+"\n");
            var before=d.ToBytes();var issues=ChartHealthCheck.Run(d);
            check(issues.Where(i=>i.Kind==ChartIssueKind.HoldTooShort).Select(i=>i.SourceIds[0]).SequenceEqual(new[]{1,3,4}),"110 Hold durations below 5ms include fractional, zero and negative values");
            check(issues.Where(i=>i.Kind==ChartIssueKind.SkyAreaTooShort).Select(i=>i.SourceIds[0]).SequenceEqual(new[]{5,7,8}),"110 Sky durations below 5ms include fractional, zero and negative values");
            check(!issues.Any(i=>i.SourceIds.Contains(2)||i.SourceIds.Contains(6)),"110 exactly 5ms Hold/Sky passes duration rule");
            check(issues.All(i=>i.Lines[0]==d.SourceIndex(i.SourceIds[0])+1)&&issues.First().TimeMs==100&&issues.First().SourceText==d.SourceLine(1),"110 duration issue contains source line, original statement and jump/selection payload");
            check(issues.Count(i=>i.Kind==ChartIssueKind.UnknownStatement)==1&&d.ToBytes().SequenceEqual(before),"110 short-duration check coexists with unknown statements without rewriting notes");
        }
        private static void Sorting(Action<bool,string> check)
        {
            string retained="icp_event(150,1,"+Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"timestamp_ms\":150,\"type\":1,\"value\":180,\"transition\":4}"))+")";
            string raw="chart(120,4)\r\n// fixed comment\n  tap(300,1,1) # inline\r\nfuture(20)\n"+
                "track(100,2)\r\nbpm(100,180,4)\n"+retained+"\r\ntrack(100,3)\r\nhold(50,1,1,10)";
            var doc=SpcDocument.Parse(raw,true);var before=doc.ToBytes();var original=doc.Clone();
            var beforeScroll=new ScrollTimeline(doc.ReadEvents());
            check(doc.ToBytes().SequenceEqual(before)&&doc.ReadEvents().Any(n=>n.TimeMs==300),"110 parsing preserves unsorted source until Save");
            check(doc.SortTimedStatements(),"110 unsorted recognized timed statements are reordered");
            check(doc.ReadEvents().Where(n=>n.Kind!=EventKind.Chart&&n.Kind!=EventKind.Unknown).Select(n=>n.TimeMs).SequenceEqual(new[]{50d,100,100,100,150,300}),"110 header is not a timestamp; all known note/timing/track statements sort chronologically");
            check(doc.LineIdAt(0)==0&&doc.LineIdAt(1)==1&&doc.LineIdAt(3)==3&&doc.SourceLine(3)=="future(20)","110 header, standalone comment and opaque statement remain in their original slots");
            check(doc.ReadEvents().Where(n=>n.TimeMs==100).Select(n=>n.SourceId).SequenceEqual(new[]{4,5,7}),"110 equal-time ordering is stable across event types");
            check(new ScrollTimeline(doc.ReadEvents()).PositionAt(200)==beforeScroll.PositionAt(200),"110 equal-time track override and integrated scroll survive sorting");
            check(Enumerable.Range(0,original.LineCount).All(i=>doc.SourceLine(i)==original.SourceLine(i))&&doc.SourceIndex(2)==8,"110 source identity, raw numeric tokens and inline comments follow statements");
            check(doc.SourceLine(6)==retained&&doc.ReadEvents().Single(n=>n.SourceId==6).Kind==EventKind.Bpm,"110 recovered legacy timing sorts by decoded timestamp without replacing its wrapper");
            check(doc.HasUtf8Bom&&System.Text.RegularExpressions.Regex.Matches(doc.ToText(),"\r\n|\r|\n").Cast<System.Text.RegularExpressions.Match>().Select(m=>m.Value)
                .SequenceEqual(System.Text.RegularExpressions.Regex.Matches(raw,"\r\n|\r|\n").Cast<System.Text.RegularExpressions.Match>().Select(m=>m.Value))&&!doc.ToText().EndsWith("\n"),"110 BOM, mixed separator layout and absent final newline survive sorting");
            check(!doc.SortTimedStatements()&&SpcDocument.FromBytes(doc.ToBytes()).ToBytes().SequenceEqual(doc.ToBytes()),"110 repeat sorting is a no-op and saved bytes round-trip");
            var h=new EditHistory(original.Clone());h.Execute(d=>d.SetNumber(2,0,250));int revision=h.Revision;
            h.AcceptSavedOrder();
            check(h.Revision==revision+1&&h.UndoCount==1&&h.Document.SourceIndex(2)==8,"110 successful Save ordering invalidates stale source-line checks and keeps undo count");
            check(h.Undo()&&h.Document.ToBytes().SequenceEqual(before)&&h.Redo()&&Find(h.Document,2).TimeMs==250,"110 existing edit undo/redo survives Save ordering");
            var retimed=SpcDocument.Parse("chart(120,4)\ntrack(300,2)\ntrack(100,3)\n");retimed.SortTimedStatements();retimed.SetNumber(2,0,300);
            double effective=new ScrollTimeline(retimed.ReadEvents()).PositionAt(400);retimed.SortTimedStatements();
            check(new ScrollTimeline(SpcDocument.FromBytes(retimed.ToBytes()).ReadEvents()).PositionAt(400)==effective,"110 retiming after an earlier sort retains equal-time runtime precedence after reload");
        }
        private static void Saves(Action<bool,string> check)
        {
            string root=Path.Combine(Path.GetTempPath(),"InFalsus110_"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var loose=SpcDocument.Parse("chart(120,4)\r\ntap(200,1,1)\r\ntap(50,1,2)",true);
                string copy=Path.Combine(root,"export.spc");loose.Save(copy);
                check(SpcDocument.Load(copy).ReadEvents().Where(n=>n.IsNote).Select(n=>n.TimeMs).SequenceEqual(new[]{50d,200}),"110 standalone Save/export writes chronological statements");
                check(File.ReadAllBytes(copy).SequenceEqual(loose.ToBytes()),"110 successful standalone Save adopts exactly the written ordering");
                var fail=SpcDocument.Parse("chart(120,4)\ntap(200,1,1)\ntap(50,1,2)\n");byte[] raw=fail.ToBytes();
                check(Rejects(()=>fail.Save(root))&&fail.ToBytes().SequenceEqual(raw),"110 failed standalone save preserves draft order");
                string song=Path.Combine(root,"song");Directory.CreateDirectory(song);File.WriteAllBytes(Path.Combine(song,"audio.mp3"),new byte[]{1,2,3});
                var project=SongFolderProject.Prepare(song);project.CommitInitialWorkspace();
                foreach(var d in project.Difficulties)File.WriteAllBytes(d.WorkingPath,raw);
                project=SongFolderProject.Prepare(song);var session=new ManualSongSession(project);
                check(!session.HasChanges&&project.Difficulties.All(d=>d.Document.ReadEvents().First(n=>n.IsNote).TimeMs==200),"110 reopening an unsorted project does not silently sort or dirty it");
                var original=project.Difficulties.Select(d=>File.ReadAllBytes(d.WorkingPath)).ToArray();
                check(Rejects(()=>session.Save((i,path)=>{if(i==1)throw new IOException("Injected sort-save failure.");})),"110 multi-chart sorted save reports an injected write failure");
                check(project.Difficulties.All(d=>File.ReadAllBytes(d.WorkingPath).SequenceEqual(original[d.Index])&&d.Document.ToBytes().SequenceEqual(raw)),"110 rollback restores all file bytes and leaves all in-memory chart orders unchanged");
                session.Save();
                check(project.Difficulties.All(d=>d.Document.ReadEvents().Where(n=>n.IsNote).Select(n=>n.TimeMs).SequenceEqual(new[]{50d,200})&&File.ReadAllBytes(d.WorkingPath).SequenceEqual(d.Document.ToBytes())),"110 explicit project Save sorts and adopts all four chart outputs");
                check(!session.HasChanges&&session.Save().StartsWith("Already saved",StringComparison.Ordinal),"110 sorted save resets dirty baselines and repeated Save writes nothing");
                var conflict=SongFolderProject.Prepare(song);var conflicting=new ManualSongSession(conflict);
                conflict.Difficulties[0].Document.Append("tap(10,1,3)");byte[] draft=conflict.Difficulties[0].Document.ToBytes();
                File.AppendAllText(conflict.Difficulties[0].WorkingPath,"external(999)\n");
                check(Rejects(()=>conflicting.Save())&&conflict.Difficulties[0].Document.ToBytes().SequenceEqual(draft)&&File.ReadAllText(conflict.Difficulties[0].WorkingPath).Contains("external(999)"),"110 external conflict blocks sorting save without altering draft or external bytes");
            }
            finally
            {
                string full=Path.GetFullPath(root),prefix=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith("InFalsus110_",StringComparison.Ordinal))Directory.Delete(full,true);
            }
        }
    }
}
