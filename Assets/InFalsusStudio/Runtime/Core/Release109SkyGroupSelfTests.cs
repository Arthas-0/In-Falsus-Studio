using System;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    public static class Release109SkyGroupSelfTests
    {
        public static string LastReport {get;private set;}="";
        public const string UserExample="skyarea(157753,50,100,10,76,119,86,0,0,21,-1)";
        private static SpcEvent Find(SpcDocument d,int id){return d.ReadEvents().First(n=>n.SourceId==id);}
        private static bool Ungrouped(SpcDocument d,int id){return Find(d,id).Args.Length==10&&d.ArgumentTokens(id).Length==10;}
        private static string Archived(string raw)
        {
            const string marker="ungrouped_skyarea_base64=";int at=raw.IndexOf(marker,StringComparison.Ordinal);
            if(at<0)return "";string value=raw.Substring(at+marker.Length).Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries)[0];
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        public static void Run(Action<bool,string> outer)
        {
            var report=new StringBuilder();int count=0;LastReport="";
            Action<bool,string> check=(ok,label)=>{count++;report.AppendLine((ok?"PASS ":"FAIL ")+label);outer(ok,label);};
            string exact=UserExample.Substring(0,UserExample.Length-4)+")";
            check(SkyGroupSyntax.WithoutGroup(UserExample)==exact&&exact=="skyarea(157753,50,100,10,76,119,86,0,0,21)","109 user example omits exactly the final group argument");
            check(SkyGroupSyntax.WithoutGroup(exact)==exact,"109 already omitted group is byte-identical");
            var original=SpcDocument.Parse("// original\r\n"+UserExample+"\r\n  skyarea (0, 50,100,10,50,100,10,0,0,500,42) # retained\n"+
                "skyarea(500,50,100,10,50,100,10,0,0,500)\r\nskyarea(1000,50,100,10,50,100,10,0,0,500,4,123.50,999) # extra\r\nmystery(800)\n",true);
            byte[] before=original.ToBytes();string extra=original.SourceLine(4),untouched=original.SourceLine(3);
            var history=new EditHistory(original.Clone()){BeforeCommit=AuthoringSafety.BeforeCommit};history.Execute(SkySplitOperations.ResetGroups);
            check(history.Document.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea).All(n=>Ungrouped(history.Document,n.SourceId)),"109 reset removes group parameters rather than writing -1");
            check(history.Document.SourceLine(1)==exact&&history.Document.SourceLine(2)=="  skyarea (0, 50,100,10,50,100,10,0,0,500) # retained","109 reset preserves all other numeric tokens, indent and suffix");
            check(history.Document.HasUtf8Bom&&history.Document.SourceLine(3)==untouched&&history.Document.SourceLine(5)=="mystery(800)"&&history.Document.LineIdAt(4)==4,"109 reset preserves BOM, source IDs, unassigned note and unknown row");
            check(Archived(history.Document.SourceLine(4))==extra&&history.Document.SourceLine(4).EndsWith(" # extra"),"109 nonstandard fields after group retain complete original source as inert provenance");
            check(history.Document.ToText().Contains("\r\n")&&history.Document.ToText().Contains(" # retained\n"),"109 group removal preserves individual mixed line endings");
            int undoCount=history.UndoCount;history.Execute(SkySplitOperations.ResetGroups);
            check(history.UndoCount==undoCount,"109 repeated group removal is a no-op");
            check(history.Undo()&&history.Document.ToBytes().SequenceEqual(before)&&history.Redo(),"109 whole-chart unassignment is one exact undo/redo transaction");
            check(original.ToBytes().SequenceEqual(before),"109 group reads/edits on clone never rewrite the original source");
            var added=SpcDocument.Parse("chart(120,4)\n");int id=EditOperations.Add(added,EventKind.SkyArea,0,500,1,1,.5,.5,.1);
            check(Ungrouped(added,id),"109 default new SkyArea has exactly ten arguments");
            int next=EditOperations.ContinueSky(added,id,1000);
            check(Ungrouped(added,next)&&SkyGroups.Heads(added.ReadEvents()).SetEquals(new[]{id}),"109 continuation omits group and hides its connected head");
            check(SkyGroupNavigation.Members(added.ReadEvents(),-1).Length==2,"109 top browser sentinel still navigates unassigned notes without serializing -1");
            var form=NoteProperties.Capture(added,Find(added,id));check(form.Values["group"]=="","109 unspecified group field displays blank");
            form.Values["group"]="7";form.Apply(added);
            check(Find(added,id).Get(10)==7&&added.ArgumentTokens(id).Length==11,"109 manual positive group override remains serializable");
            form=NoteProperties.Capture(added,Find(added,id));form.Values["group"]="";form.Apply(added);
            check(Ungrouped(added,id),"109 clearing the group field removes the optional parameter");
            form=NoteProperties.Capture(added,Find(added,id));form.Values["group"]="9";form.Apply(added);
            form=NoteProperties.Capture(added,Find(added,id));form.Values["group"]="-1";form.Apply(added);
            check(Ungrouped(added,id),"109 legacy -1 field input clears group instead of writing negative ID");
            var withExtra=SpcDocument.Parse(extra+"\n");string raw=withExtra.SourceLine(0);
            int continuation=EditOperations.ContinueSky(withExtra,0,2000);
            check(Ungrouped(withExtra,continuation)&&Archived(withExtra.SourceLine(continuation))==raw&&withExtra.SourceLine(0)==raw,"109 continuation from explicit group retains unknown extras as source provenance");
            foreach(bool grid in new[]{false,true})
            {
                var split=SpcDocument.Parse("chart(120,4)\n"+extra+"\n");var timeline=new AuthoringGrid(new BeatTimeline(split.ReadEvents()));
                var ids=grid?SkySplitOperations.Grid(split,new[]{1},timeline,4):SkySplitOperations.Equal(split,new[]{1},4);
                check(ids.Length==4&&ids.All(n=>Ungrouped(split,n))&&SkyGroups.Heads(split.ReadEvents()).Count==1,"109 "+(grid?"grid":"equal")+" Sky pieces omit group while keeping automatic joins");
                check(ids.All(n=>Archived(split.SourceLine(n))==extra),"109 "+(grid?"grid":"equal")+" pieces preserve nonstandard source extras without positional group slots");
            }
            var clipboard=new NoteClipboard();var clip=SpcDocument.Parse(extra+"\n"+UserExample+"\n");clipboard.Copy(clip,new[]{0,1});
            var pasted=SpcDocument.Parse("chart(120,4)\n");var pasteIds=clipboard.Paste(pasted,2000,true);
            check(pasteIds.All(n=>Ungrouped(pasted,n))&&Archived(pasted.SourceLine(pasteIds[0])).EndsWith(",4,123.50,999) # extra"),"109 default paste removes assignment, preserving nonstandard extras in provenance");
            var retained=clipboard.Paste(pasted,3000,false);
            check(Find(pasted,retained[0]).Get(10)==4&&Find(pasted,retained[0]).Get(11)==123.5,"109 explicit preserve-groups paste option keeps source arguments");
            ImportChecks(check);
            LastReport=report.AppendLine("TOTAL: "+count+" actual C# omitted-group checks.").ToString();
        }
        private static void ImportChecks(Action<bool,string> check)
        {
            const string rational="{\"numerator\":50,\"denominator\":100,\"value\":0.5}";
            const string width="{\"numerator\":10,\"denominator\":100,\"value\":0.1}";
            foreach(string group in new[]{",\"group_id\":-1","",",\"group_id\":7"})
            {
                string json="{\"format\":\"ICP1\",\"version\":1,\"bpm\":120,\"beats_per_bar\":4,\"events\":[],\"notes\":[{"+
                    "\"id\":5,\"type\":5,\"side\":4,\"start_ms\":0,\"end_ms\":500,\"start_x\":"+rational+",\"end_x\":"+rational+",\"start_width\":"+width+",\"end_width\":"+width+",\"extra_flags\":36,\"packed_flags\":1284,\"relay\":0"+group+"}]}";
                var doc=IcpJsonImport.Convert(ProjectJson.Object(ProjectJson.Parse(json))).Document;var sky=doc.ReadEvents().First(n=>n.Kind==EventKind.SkyArea);
                check(group.EndsWith(":7")?sky.Args.Length==11&&sky.Get(10)==7:Ungrouped(doc,sky.SourceId),"109 JSON Sky import uses native omitted form for unspecified/legacy -1 while retaining explicit IDs "+group);
            }
        }
    }
}
