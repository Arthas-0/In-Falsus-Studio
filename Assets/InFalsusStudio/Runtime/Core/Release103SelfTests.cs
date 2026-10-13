using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    // C# executable regression checks. Python/source checks do not execute this class.
    public static class Release103SelfTests
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static Dictionary<string, object> Obj(string json) { return ProjectJson.Object(ProjectJson.Parse(json)); }
        private static bool Throws(Action f) { try { f(); return false; } catch (ArgumentException) { return true; } catch (InvalidDataException) { return true; } }
        private static string Wrapper(Dictionary<string, object> record)
        {
            return "icp_event(" + ProjectJson.Number(record, "timestamp_ms").ToString("G17", CI) + "," +
                ProjectJson.Integer(record, "type").ToString(CI) + "," + Convert.ToBase64String(Encoding.UTF8.GetBytes(ProjectJson.Write(record))) + ")";
        }
        private static string Body(string call) { return call.Substring(call.IndexOf('(') + 1, call.LastIndexOf(')') - call.IndexOf('(') - 1); }
        private const string Cold = "{\"id\":0,\"timestamp_ms\":137931,\"type\":1,\"payload_hex\":\"0000000000c065400000c64200000000\",\"value\":174,\"transition\":99}";
        private static string Chart(string events) { return "{\"format\":\"ICP1\",\"version\":1,\"bpm\":120,\"beats_per_bar\":4,\"notes\":[],\"events\":[" + events + "]}"; }

        public static void Run(Action<bool, string> check)
        {
            check(ReleaseIdentity.Version == ReleaseIdentity.DisplayVersion, "103 display/build identity remains consistent");
            double[] args; string why;
            var row = Obj(Cold);
            check(IcpTimingEvents.TryDecode(row, out args, out why) && args.SequenceEqual(new[] {137931d,174d,99d}), "103 actual type1 value/transition decodes BPM/meter99");
            var initial = Obj("{\"timestamp_ms\":0,\"type\":1,\"value\":225,\"transition\":999,\"payload_hex\":\"0000000000206c4000c0794400000000\"}");
            check(IcpTimingEvents.TryDecode(initial,out args,out why)&&args[1]==225&&args[2]==999,"103 meter999 is not rejected or rewritten as BPM");
            check(IcpTimingEvents.TryDecode(Obj("{\"timestamp_ms\":12,\"type\":88,\"bpm\":174,\"beats_per_bar\":99}"),out args,out why),"103 keep historical explicitly named schema compatibility");
            foreach(string bad in new[]{
                "{\"timestamp_ms\":1,\"type\":0,\"value\":174,\"transition\":4}",
                "{\"timestamp_ms\":1,\"type\":99,\"value\":174,\"transition\":4}",
                "{\"timestamp_ms\":1,\"type\":1,\"value\":0,\"transition\":4}",
                "{\"timestamp_ms\":1,\"type\":1,\"value\":174}",
                "{\"timestamp_ms\":1,\"type\":1,\"value\":174,\"transition\":0}",
                "{\"timestamp_ms\":-1,\"type\":1,\"value\":174,\"transition\":4}",
                "{\"timestamp_ms\":1,\"type\":1,\"value\":\"174\",\"transition\":4}",
                Cold.Replace("\"value\":174","\"value\":175"),
                Cold.Replace("\"transition\":99","\"transition\":4"),
                Cold.Replace("0000000000c065400000c64200000000","0000000000c065400000c64201000000"),
                Cold.Replace("0000000000c065400000c64200000000","0000"),
                Cold.Replace("\"value\":174","\"bpm\":175,\"beats_per_bar\":99,\"value\":174")})
                check(!IcpTimingEvents.TryDecode(Obj(bad),out args,out why),"103 unsupported/conflicting timing stays opaque: "+bad);
            string wrapped = Wrapper(row);
            check(IcpTimingEvents.TryReadRetained(Body(wrapped),out args,out why)&&args[2]==99,"103 legacy complete base64 record recovers");
            foreach(string malformed in new[]{"137931,1,e30=", "137931,1,!!", "137932,1,"+Body(wrapped).Split(',')[2], "137931,3,"+Body(wrapped).Split(',')[2], "0,1,"+new string('A',65540)})
                check(!IcpTimingEvents.TryReadRetained(malformed,out args,out why),"103 malformed/contradictory wrapper is not guessed");

            var doc=SpcDocument.Parse("chart(174,4)\r\n// user comment\r\ntap(1000,1,1)\n  "+wrapped+"\r\nunknown_future(123,9)",true);
            byte[] before=doc.ToBytes();var parsed=doc.ReadEvents();
            check(doc.RecoveredIcpTimingCount==1&&parsed.Count(x=>x.Kind==EventKind.Bpm)==1,"103 legacy parsed once as typed BPM");
            check(parsed.First(x=>x.Kind==EventKind.Bpm).SourceId==3,"103 recovered timing preserves stable source ID");
            check(doc.ToBytes().SequenceEqual(before),"103 parse/recovery preserves bytes, BOM, mixed newlines, comments");
            check(doc.ArgumentTokens(3).SequenceEqual(new[]{"137931","174","99"}),"103 logical numeric tokens do not expose base64 as meter");
            doc.SetNumber(3,1,174);
            check(doc.ToBytes().SequenceEqual(before),"103 no-op parameter set does not canonicalize wrapper");
            var history=new EditHistory(doc);history.BeforeCommit=AuthoringSafety.BeforeCommit;
            history.Execute(d=>d.SetNumber(3,2,4));
            check(history.UndoCount==1&&history.Document.ReadEvents().First(x=>x.Kind==EventKind.Bpm).Get(2)==4,"103 explicit numeric edit targets meter not base64");
            check(history.Document.SourceLine(3).Contains("bpm(137931,174,4)")&&history.Document.SourceLine(3).Contains("legacy_icp_event_base64="),"103 first edit preserves original wrapper in inert base64 comment");
            check(history.Document.SourceLine(2)==doc.SourceLine(2)&&history.Document.SourceLine(4)==doc.SourceLine(4),"103 editing recovered timing preserves note and unknown lines");
            string backup=history.Document.SourceLine(3).Split(new[]{"legacy_icp_event_base64="},StringSplitOptions.None)[1];
            check(Encoding.UTF8.GetString(Convert.FromBase64String(backup))==doc.SourceLine(3),"103 original wrapper backup decodes byte-for-byte line");
            check(history.Undo()&&history.Document.ToBytes().SequenceEqual(before),"103 undo restores exact legacy syntax");
            check(history.Document.ReadEvents().Any(x=>x.Kind==EventKind.Bpm),"103 restored legacy still has known timing/count semantics");
            check(history.Redo()&&history.Document.ReadEvents().Count(x=>x.Kind==EventKind.Bpm)==1,"103 redo does not duplicate timing");
            byte[] modified=history.Document.ToBytes();
            check(SpcDocument.FromBytes(modified).ToBytes().SequenceEqual(modified),"103 promoted syntax reload roundtrip");
            var optional=doc.Clone();optional.SetOptionalNumber(3,3,-1);
            check(optional.ReadEvents().First(x=>x.Kind==EventKind.Bpm).Get(3)==-1,"103 optional reserved edit promotes safely");
            var failed=doc.Clone();
            check(Throws(()=>failed.SetToken(3,5,"4"))&&failed.ToBytes().SequenceEqual(before),"103 invalid argument index does not promote or mutate");
            check(!TickCountResearch.Analyze(parsed).Supported,"103 genuine unknown records still block complete count");
            check(TickCountResearch.Analyze(parsed).Warning.Contains("unknown_future"),"103 unavailable status names the blocking record");

            var clean=SpcDocument.Parse("chart(174,4)\n"+wrapped+"\ntap(1000,1,1)\n");
            check(TickCountResearch.Analyze(clean.ReadEvents()).Supported&&new ProvisionalComboTimeline(clean.ReadEvents()).At(200000).Total==1,"103 wrapper recovery enables the existing candidate count");
            var imported=IcpJsonImport.Convert(Obj(Chart(Cold+",{\"timestamp_ms\":0,\"type\":0,\"value\":-1}")));
            check(imported.Document.ReadEvents().Any(x=>x.Kind==EventKind.Bpm&&x.Get(2)==99),"103 fresh JSON import emits BPM");
            check(imported.Document.ReadEvents().Any(x=>x.Kind==EventKind.Track&&x.Get(1)==-1),"103 negative track is not negative BPM");
            check(!imported.Document.ToText().Contains("icp_event("),"103 recognized type1 no longer produces opaque calls");
            var unknown=IcpJsonImport.Convert(Obj(Chart("{\"timestamp_ms\":0,\"type\":1,\"value\":0,\"transition\":4}")));
            check(unknown.Document.ToText().Contains("icp_event(")&&!TickCountResearch.Analyze(unknown.Document.ReadEvents()).Supported,"103 malformed timing retained and count not falsely enabled");
            var changed=SpcDocument.Parse("chart(225,999)\nhold(49467,1,2,3382)\nhold(49467,3,2,3382)\nbpm(49600,214,4)\nbpm(52061,159,4)\n");
            var timeline=new ProvisionalComboTimeline(changed.ReadEvents());
            check(timeline.Supported&&timeline.Notes.All(x=>x.StartBpm==225&&x.Total==27&&x.CrossesBpm),"103 actual changing BPM supported under explicit provisional onset-BPM rule");
            check(timeline.At(52849).Total==54&&PreviewScore.At(54,54)==100000054L,"103 candidate end combo and max score are available");
        }

        // Uses supplied full pairs. Files are read only; no source / user song folder is written.
        public static void RunPairs(string folder, Action<bool,string> check)
        {
            Pair(folder,"coldsea",1475,3,0,2339,633,793,663,250,0,check);
            Pair(folder,"chronophobia",2071,24,165,3181,954,969,775,483,2,check);
        }
        private static void Pair(string folder,string song,int objects,int bpmCount,int trackCount,int total,int tap,int hold,int sky,int flick,int crosses,Action<bool,string> check)
        {
            string prefix=Path.Combine(folder,"Forbidden_"+song+"3");
            byte[] spc=File.ReadAllBytes(prefix+".spc.txt"),jsonBytes=File.ReadAllBytes(prefix+".json");
            var decoded=Obj(Encoding.UTF8.GetString(jsonBytes));
            var plain=SpcDocument.FromBytes(spc);var result=IcpJsonImport.Convert(decoded);var imported=result.Document;
            string originalJson=ProjectJson.Write(decoded);
            var p=plain.ReadEvents();var e=imported.ReadEvents();
            check(result.ImportedNotes==objects&&result.SourceNotes==objects,"103 "+song+" complete note import");
            check(e.Count(x=>x.Kind==EventKind.Bpm)==bpmCount&&e.Count(x=>x.Kind==EventKind.Track)==trackCount,"103 "+song+" correct timing type split");
            check(e.All(x=>x.Kind!=EventKind.Unknown),"103 "+song+" zero unknown note/timing records");
            check(Keys(p).SequenceEqual(Keys(e)),"103 "+song+" ALL typed event arguments equal paired SPC");
            check(ProjectJson.Write(decoded)==originalJson&&plain.ToBytes().SequenceEqual(spc),"103 "+song+" input JSON object and SPC bytes unchanged");
            var sum=TickCountResearch.Analyze(e);var tl=new ProvisionalComboTimeline(e);
            check(sum.Supported&&tl.Supported&&sum.Total==total&&tl.Totals.Total==total,"103 "+song+" complete candidate total (not measured official total)");
            check(sum.Tap==tap&&sum.Hold==hold&&sum.SkyArea==sky&&sum.Flick==flick,"103 "+song+" candidate category totals");
            check(tl.Notes.Count(n=>n.CrossesBpm)==crosses,"103 "+song+" true crossing-BPM notes counted");
            var reference=new ProvisionalComboTimeline(p);
            foreach(double t in new[]{0d,3200d,36000d,49467d,52000d,52849d,77425d,140000d,153025d,156360d,200000d})
                check(tl.At(t).Total==reference.At(t).Total,"103 "+song+" random-access count matches plain @"+t.ToString(CI));
            check(tl.At(200000).Total==total&&PreviewScore.At(total,total)==PreviewScore.Maximum(total),"103 "+song+" end score reaches existing estimated max");
            // Reconstruct the pre-fix importer output, including complete retained BPM JSON.
            var legacy=imported.Clone();var oldBpm=legacy.ReadEvents().Where(x=>x.Kind==EventKind.Bpm).ToArray();
            var jsonBpm=ProjectJson.Array(ProjectJson.Get(decoded,"events")).Select(ProjectJson.Object).Where(x=>ProjectJson.Integer(x,"type")==1).ToArray();
            for(int i=0;i<oldBpm.Length;i++)legacy.ReplaceSourceLine(oldBpm[i].SourceId,Wrapper(jsonBpm[i]));
            // Simulate a real, already edited work chart. Opening must not overwrite its notes.
            var note=legacy.ReadEvents().First(x=>x.Kind==EventKind.Tap);
            legacy.SetNumber(note.SourceId,0,note.TimeMs+1);byte[] oldBytes=legacy.ToBytes();
            var recovered=legacy.ReadEvents();
            check(legacy.RecoveredIcpTimingCount==bpmCount,"103 "+song+" cached legacy BPMs recovered");
            check(new ProvisionalComboTimeline(recovered).Supported,"103 "+song+" cached work chart now counts");
            check(legacy.ToBytes().SequenceEqual(oldBytes)&&recovered.First(x=>x.SourceId==note.SourceId).TimeMs==note.TimeMs+1,"103 "+song+" cached edits preserved; no auto write/reimport");
            check(TimingReferenceMerge.Prepare(imported,plain).Length==0,"103 "+song+" no duplicate BPM from subsequent paired merge");
        }
        private static string[] Keys(IEnumerable<SpcEvent> events)
        {
            return events.Select(e=>e.Name+"|"+string.Join("|",e.Args.Select(a=>a.ToString("G17",CI)).ToArray())).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
    }
}
