using System;
using System.Globalization;
using System.Linq;

namespace InFalsusStudio.Core
{
    public static class Release107SelfTests
    {
        private static SpcEvent Find(SpcDocument d,int id){return d.ReadEvents().First(n=>n.SourceId==id);}
        private static bool Near(double a,double b){return Math.Abs(a-b)<1e-8;}
        private static bool Throws(Action action){try{action();return false;}catch{return true;}}
        public static bool IntegerParts(SpcDocument d,SpcEvent n)
        {
            var tokens=d.ArgumentTokens(n.SourceId);int last=n.Kind==EventKind.SkyArea?6:3;
            for(int i=1;i<=last;i++){int value;if(!int.TryParse(tokens[i].Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out value))return false;}
            return n.Get(2)>0&&(n.Kind!=EventKind.SkyArea||n.Get(5)>0);
        }
        public static void Run(Action<bool,string> check)
        {
            check(new Version(ReleaseIdentity.Version)>=new Version("1.0.7")&&ReleaseIdentity.DisplayVersion==ReleaseIdentity.Version,"107 version identity remains current");
            check(ReleaseNotesCatalog.Recent.Length==3&&ReleaseNotesCatalog.Recent[0].Version==ReleaseIdentity.Version&&
                new Version(ReleaseNotesCatalog.Recent[0].Version)>new Version(ReleaseNotesCatalog.Recent[1].Version)&&
                new Version(ReleaseNotesCatalog.Recent[1].Version)>new Version(ReleaseNotesCatalog.Recent[2].Version),"107 latest three announcements remain ordered");
            Writers(check);Splits(check);Repair(check);
        }
        private static void Writers(Action<bool,string> check)
        {
            var d=SpcDocument.Parse("chart(120,4)\r\nskyarea(1000.25,100,200,2,1,2,2,1,2,923,7,123.5) # keep\r\nmystery(2.5)\r\n",true);
            string other=d.SourceLine(2);var n=Find(d,1);
            EditOperations.AirEdge(d,1,false,true,.55);var edited=Find(d,1);
            check(IntegerParts(d,edited)&&edited.Get(1)==209&&edited.Get(2)==400&&edited.Get(3)==22,"107 half-tick edge edits store 209/400 and width 22/400");
            check(Near(ChartMath.SkyAt(edited,edited.TimeMs).Left,.495)&&Near(ChartMath.SkyAt(edited,edited.TimeMs).Right,.55),"107 edge drag preserves opposite edge and target edge");
            check(d.ArgumentTokens(1)[0]=="1000.25"&&edited.Get(5)==2&&edited.Get(10)==7&&edited.Get(11)==123.5&&d.SourceLine(1).EndsWith(" # keep")&&d.SourceLine(2)==other&&d.HasUtf8Bom,"107 edited endpoint preserves time, tail, metadata, suffix and BOM");
            var f=SpcDocument.Parse("chart(120,4)\nflick(2000.125,50,100,20,16,987) // keep\n");
            var form=NoteProperties.Capture(f,Find(f,1));form.Values["width"]="25.5";form.Apply(f);
            var flick=Find(f,1);
            check(IntegerParts(f,flick)&&flick.Get(1)==100&&flick.Get(2)==200&&flick.Get(3)==51,"107 25.5 percent width uses integer 51/200 instead of 25.5/100");
            check(Near(ChartMath.FlickRange(flick).Width,.255)&&f.ArgumentTokens(1)[0]=="2000.125"&&flick.Get(4)==16&&flick.Get(5)==987,"107 property editing preserves footprint and unrelated Flick fields");
            var move=SpcDocument.Parse("chart(120,4)\nskyarea(0,50,100,25,60,100,20,0,0,1000,-1)\n");
            var before=Find(move,1);EditOperations.Move(move,new[]{1},0,0,.005);var moved=Find(move,1);
            check(IntegerParts(move,moved)&&Near(ChartMath.SkyAt(moved,0).Left,ChartMath.SkyAt(before,0).Left+.005)&&Near(ChartMath.SkyAt(moved,1000).Right,ChartMath.SkyAt(before,1000).Right+.005),"107 moving both endpoints preserves shape using integer ratios");
            var thirds=SpcDocument.Parse("chart(120,4)\nflick(0,50,100,10,16)\n");
            RationalAirCoordinates.WriteEndpoint(thirds,Find(thirds,1),false,1d/3,.1);var third=Find(thirds,1);
            check(IntegerParts(thirds,third)&&third.Get(1)==10&&third.Get(2)==30&&third.Get(3)==3,"107 recurring thirds remain exact integer ratios");
            var legacy=SpcDocument.Parse("chart(120,4)\nskyarea(0,50,100,20,50.5,100,25.5,1,2,500,42,999) # original\n");
            string raw=legacy.SourceLine(1);int continued=EditOperations.ContinueSky(legacy,1,1000);var c=Find(legacy,continued);
            check(IntegerParts(legacy,c)&&legacy.SourceLine(1)==raw&&c.Args.Length==10&&legacy.SourceLine(continued).Contains("ungrouped_skyarea_base64="),"109 continuation encodes legacy endpoints, omits group and preserves nonstandard source as provenance");
            check(NearSample(ChartMath.SkyAt(c,c.TimeMs).Left,ChartMath.SkyAt(Find(legacy,1),500).Left)&&NearSample(ChartMath.SkyAt(c,c.EndMs).Right,ChartMath.SkyAt(Find(legacy,1),500).Right),"108 continued footprint fits within the 128-part precision");
            var boundary=SpcDocument.Parse("chart(120,4)\nskyarea(0,75,100,50,65,100,70,1,0,1000,-1)\n");var b=Find(boundary,1);
            bool valid=true;
            for(int i=1;i<100;i++)
            {
                var range=ChartMath.SkyAt(b,i*10);var tokens=RationalAirCoordinates.Sample(range,100);
                var sample=SpcDocument.Parse("flick(0,"+string.Join(",",tokens)+",16)\n");var note=sample.ReadEvents()[0];
                valid&=IntegerParts(sample,note)&&AirReachBounds.Inside(ChartMath.FlickRange(note))&&Near(ChartMath.FlickRange(note).Right,1);
            }
            check(valid,"107 irrational curve samples retain a fixed right boundary at 100 percent");
            check(Throws(()=>RationalAirCoordinates.Tokens(25.5,100,10)),"107 integer serializer rejects fractional raw coordinate parts");
        }
        private static void Splits(Action<bool,string> check)
        {
            for(int left=0;left<3;left++)for(int right=0;right<3;right++)
            {
                var original=SpcDocument.Parse("chart(130,4)\nskyarea(1000,30,100,20,70,100,30,"+left+","+right+",1000,-1)\n");var curve=Find(original,1);
                var sky=original.Clone();var ids=SkySplitOperations.Equal(sky,new[]{1},7);
                var pieces=ids.Select(id=>Find(sky,id)).OrderBy(n=>n.TimeMs).ToArray();
                check(pieces.Length==7&&pieces.All(n=>IntegerParts(sky,n)),"107 all N-split coordinate parts parse as game integers "+left+"/"+right);
                check(pieces.All(n=>NearSample(ChartMath.SkyAt(n,n.TimeMs).Left,ChartMath.SkyAt(curve,n.TimeMs).Left)&&NearSample(ChartMath.SkyAt(n,n.EndMs).Right,ChartMath.SkyAt(curve,n.EndMs).Right)),"108 integer N-split samples fit curved edges within 128 parts "+left+"/"+right);
                check(Enumerable.Range(1,pieces.Length-1).All(i=>sky.ArgumentTokens(pieces[i-1].SourceId).Skip(4).Take(3).SequenceEqual(sky.ArgumentTokens(pieces[i].SourceId).Skip(1).Take(3))),"107 neighboring pieces share identical integer join coordinates "+left+"/"+right);
                var flickDoc=original.Clone();var flickIds=SkySplitOperations.Grid(flickDoc,new[]{1},new AuthoringGrid(new BeatTimeline(original.ReadEvents())),4,true,16);
                var flicks=flickIds.Select(id=>Find(flickDoc,id)).ToArray();
                check(flicks.All(n=>IntegerParts(flickDoc,n)&&NearSample(ChartMath.FlickRange(n).Left,ChartMath.SkyAt(curve,n.TimeMs).Left)&&NearSample(ChartMath.FlickRange(n).Right,ChartMath.SkyAt(curve,n.TimeMs).Right)),"108 grid Flicks fit both original boundaries with at most 128 parts "+left+"/"+right);
            }
        }
        private static bool NearSample(double a,double b){return Math.Abs(a-b)<=RationalAirCoordinates.SampleEdgeTolerance;}
        private static void Repair(Action<bool,string> check)
        {
            var d=SpcDocument.Parse("chart(120.5,4)\r\n  skyarea(1000.25, 50.5 ,100.5,25.5,49.49,100,12.49,1,2,500.125,7,123.5) # keep\r\n"+
                "flick(2000.125,40.5,1e2,10.49,16,123.25) // keep\n"+
                "skyarea(0,1.0,2e0,0.0,1,2,2,0,0,100,-1)\r\n"+
                "tap(500.25,1,1)\r\nhold(600.25,1,1,100.125)\ntrack(50.25,1.5)\r\nmystery(10.5)\r\n",true);
            var original=d.Clone();var h=new EditHistory(d);h.BeforeCommit=AuthoringSafety.ValidateChanged;
            AirCoordinateRepair.Result result=default;h.Execute(doc=>result=AirCoordinateRepair.Round(doc));
            check(result.Notes==3&&result.Fields==11,"107 repair reports all affected notes and coordinate fields");
            check(h.Document.ReadEvents().Where(AirReachBounds.IsAir).All(n=>IntegerParts(h.Document,n)),"107 explicit repair writes integer tokens including decimal integers and exponent notation");
            var s=Find(h.Document,1);var f=Find(h.Document,2);
            check(s.Get(1)==51&&s.Get(2)==101&&s.Get(3)==26&&s.Get(4)==49&&s.Get(6)==12&&f.Get(1)==41&&f.Get(2)==100&&f.Get(3)==10,"107 repair rounds original parts rather than normalized percentages");
            check(new[]{0,7,8,9,10,11}.All(i=>h.Document.ArgumentTokens(1)[i]==original.ArgumentTokens(1)[i])&&
                new[]{0,4,5}.All(i=>h.Document.ArgumentTokens(2)[i]==original.ArgumentTokens(2)[i]),"107 repair retains time, duration, ease, group, direction and compatibility parameters");
            check(Enumerable.Range(4,4).All(i=>h.Document.SourceLine(i)==original.SourceLine(i))&&h.Document.SourceLine(1).StartsWith("  skyarea(")&&h.Document.SourceLine(1).EndsWith(" # keep")&&h.Document.HasUtf8Bom,"107 repair retains other note types, timing, unknown source, indentation, suffix and BOM");
            check(h.Document.ToText().Count(ch=>ch=='\r')==original.ToText().Count(ch=>ch=='\r')&&h.Document.NextId==original.NextId,"107 repair preserves mixed line endings and stable IDs");
            byte[] repaired=h.Document.ToBytes();h.Execute(doc=>result=AirCoordinateRepair.Round(doc));
            check(result.Notes==0&&result.Fields==0&&h.UndoCount==1&&h.Document.ToBytes().SequenceEqual(repaired),"107 repeated repair is byte-identical and adds no undo entry");
            check(h.Undo()&&h.Document.ToBytes().SequenceEqual(original.ToBytes())&&h.Redo()&&h.Document.ToBytes().SequenceEqual(repaired),"107 repair has one undo/redo restoring exact bytes");
            var signed=SpcDocument.Parse("flick(0,-0.5,10,5.5,16)\n");AirCoordinateRepair.Round(signed);
            check(signed.ReadEvents()[0].Get(1)==-1&&signed.ReadEvents()[0].Get(3)==6,"107 signed half values round away from zero");
            var bad=SpcDocument.Parse("flick(0,50.5,100,25.5,16)\nflick(10,0.25,0.49,0.1,16)\n");byte[] badBytes=bad.ToBytes();
            check(Throws(()=>AirCoordinateRepair.Round(bad))&&bad.ToBytes().SequenceEqual(badBytes),"107 nonpositive rounded divisor rejects the entire repair before writes");
            var groups=new EditHistory(SpcDocument.Parse("skyarea(0,50.4,100,20,50.4,100,20,0,0,100,12)\nskyarea(100,50.4,100,20,50.4,100,20,0,0,100,23)\n"));
            groups.BeforeCommit=AuthoringSafety.ValidateChanged;groups.Execute(doc=>AirCoordinateRepair.Round(doc));
            check(groups.Document.ReadEvents().Select(n=>n.Get(10)).SequenceEqual(new[]{12d,23}),"107 repair preserves explicit group IDs even when rounded segments touch");
        }
    }
}
