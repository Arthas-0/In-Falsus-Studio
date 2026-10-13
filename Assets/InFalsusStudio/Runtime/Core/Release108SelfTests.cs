using System;
using System.Linq;

namespace InFalsusStudio.Core
{
    public static class Release108SelfTests
    {
        public const string UserExample="skyarea(103504,1490410,3611170,361117,120454,366390,36639,0,0,21,-1)";
        private static SpcEvent Find(SpcDocument d,int id){return d.ReadEvents().First(n=>n.SourceId==id);}
        private static bool Throws(Action action){try{action();return false;}catch{return true;}}
        public static bool Capped(SpcDocument d,SpcEvent n)
        {
            return Release107SelfTests.IntegerParts(d,n)&&n.Get(2)<=128&&(n.Kind!=EventKind.SkyArea||n.Get(5)<=128);
        }
        public static bool NearEdges(SkyRange a,SkyRange b)
        {
            return Math.Abs(a.Left-b.Left)<=RationalAirCoordinates.SampleEdgeTolerance&&Math.Abs(a.Right-b.Right)<=RationalAirCoordinates.SampleEdgeTolerance;
        }
        public static void Run(Action<bool,string> check)
        {
            check(new Version(ReleaseIdentity.Version)>=new Version("1.0.8")&&ReleaseIdentity.DisplayVersion==ReleaseIdentity.Version,"108 version remains supported");
            check(ReleaseNotesCatalog.Recent.Length==3&&ReleaseNotesCatalog.Recent[0].Version==ReleaseIdentity.Version&&
                new Version(ReleaseNotesCatalog.Recent[0].Version)>new Version(ReleaseNotesCatalog.Recent[1].Version)&&
                new Version(ReleaseNotesCatalog.Recent[1].Version)>new Version(ReleaseNotesCatalog.Recent[2].Version),"108 latest three announcements remain ordered");
            check(RationalAirCoordinates.MaxSampleSplit==128,"108 sampled coordinate divisor cap");
            var example=SpcDocument.Parse(UserExample+"\n");var original=Find(example,0);
            AirCoordinateRepair.LimitSplits(example);var fitted=Find(example,0);
            check(Capped(example,fitted),"108 user million-part example is fitted within 128");
            check(NearEdges(ChartMath.SkyAt(fitted,fitted.TimeMs),ChartMath.SkyAt(original,original.TimeMs))&&NearEdges(ChartMath.SkyAt(fitted,fitted.EndMs),ChartMath.SkyAt(original,original.EndMs)),"108 user example retains both original boundary footprints at bounded precision");
            check(fitted.TimeMs==103504&&fitted.DurationMs==21&&fitted.Get(7)==0&&fitted.Get(8)==0&&fitted.Get(10)==-1,"108 compression keeps example timing, ease and group");
            var exact=SpcDocument.Parse("flick(0,"+string.Join(",",RationalAirCoordinates.Sample(new SkyRange(.4,.6),100))+",16)\n");
            check(Find(exact,0).Get(1)==50&&Find(exact,0).Get(2)==100&&Find(exact,0).Get(3)==20,"108 existing exact ratios within the cap retain their divisor");
            var thirds=SpcDocument.Parse("flick(0,"+string.Join(",",RationalAirCoordinates.Sample(new SkyRange(1d/3-.05,1d/3+.05),200))+",16)\n");
            check(Capped(thirds,Find(thirds,0))&&Find(thirds,0).Get(2)==30,"108 an exact small common ratio is kept without enlarging the divisor");
            SplitCases(check);RepairCases(check);
        }
        private static void SplitCases(Action<bool,string> check)
        {
            for(int left=0;left<3;left++)for(int right=0;right<3;right++)
            {
                var d=SpcDocument.Parse("chart(120,4)\nskyarea(500,30,100,20,70,100,30,"+left+","+right+",500,-1,999) # keep\n");var curve=Find(d,1);
                foreach(bool grid in new[]{false,true})foreach(bool flick in new[]{false,true})
                {
                    var output=d.Clone();
                    int[] ids=grid?SkySplitOperations.Grid(output,new[]{1},new AuthoringGrid(new BeatTimeline(d.ReadEvents())),4,flick):
                        SkySplitOperations.Equal(output,new[]{1},4,flick);
                    var notes=ids.Select(id=>Find(output,id)).OrderBy(n=>n.TimeMs).ToArray();
                    check(notes.Length==4&&notes.All(n=>Capped(output,n)),"108 all "+(grid?"grid":"equal")+(flick?" Flick":" Sky")+" divisors within cap "+left+"/"+right);
                    check(notes.All(n=>flick?NearEdges(ChartMath.FlickRange(n),ChartMath.SkyAt(curve,n.TimeMs)):
                        NearEdges(ChartMath.SkyAt(n,n.TimeMs),ChartMath.SkyAt(curve,n.TimeMs))&&NearEdges(ChartMath.SkyAt(n,n.EndMs),ChartMath.SkyAt(curve,n.EndMs))),
                        "108 both sampled boundaries fit original "+grid+"/"+flick+" "+left+"/"+right);
                    if(!flick)
                        check(Enumerable.Range(1,3).All(i=>output.ArgumentTokens(notes[i-1].SourceId).Skip(4).Take(3).SequenceEqual(output.ArgumentTokens(notes[i].SourceId).Skip(1).Take(3)))&&
                            SkyGroups.Heads(notes).Count==1,"108 neighboring Sky pieces share integer joins and one automatic head "+grid+" "+left+"/"+right);
                }
            }
            var many=SpcDocument.Parse("skyarea(0,50,100,10,50,100,10,0,0,10000,-1)\n");
            var manyIds=SkySplitOperations.Equal(many,new[]{0},129);
            check(manyIds.Length==129&&manyIds.All(id=>Capped(many,Find(many,id))),"108 coordinate cap does not cap the requested number of time pieces");
            var thin=SpcDocument.Parse("skyarea(0,1,10000,1,1,10000,1,0,0,1000,-1)\n");
            var flicks=SkySplitOperations.Equal(thin,new[]{0},4,true);
            check(flicks.All(id=>Find(thin,id).Get(3)>0&&Capped(thin,Find(thin,id))&&AirReachBounds.Inside(ChartMath.FlickRange(Find(thin,id)))),"108 very thin positive samples remain visible Flicks inside reach bounds");
            var triangle=SpcDocument.Parse("skyarea(0,10,20,0,10,20,20,1,1,1000,-1)\n");
            var pieces=SkySplitOperations.Equal(triangle,new[]{0},4);
            check(Find(triangle,pieces[0]).Get(3)==0&&pieces.All(id=>Capped(triangle,Find(triangle,id))),"108 original zero-width Sky tips remain zero while capped");
        }
        private static void RepairCases(Action<bool,string> check)
        {
            var d=SpcDocument.Parse("chart(120,4)\r\n  "+UserExample+", comment\r\n"+
                "skyarea(103525,50,100,20,50,100,20,1,2,100,23,123.5) # unchanged\r\n"+
                "flick(200000,1490410,3611170,361117,16,321) # kept\nmystery(1.5)\r\n",true);
            var original=d.Clone();var h=new EditHistory(d);h.BeforeCommit=AuthoringSafety.ValidateChanged;
            AirCoordinateRepair.Result result=default;h.Execute(doc=>result=AirCoordinateRepair.LimitSplits(doc));
            check(result.Notes==2&&result.Fields==9&&h.Document.ReadEvents().Where(AirReachBounds.IsAir).All(n=>Capped(h.Document,n)),"108 compression reports only oversized endpoints, with integer parts");
            check(h.Document.SourceLine(2)==original.SourceLine(2)&&h.Document.SourceLine(4)==original.SourceLine(4)&&h.Document.SourceLine(1).EndsWith(", comment")&&h.Document.SourceLine(3).EndsWith(" # kept")&&h.Document.HasUtf8Bom,"108 compression keeps small ratios, opaque rows, comments and BOM byte-exact");
            check(new[]{0,7,8,9,10}.All(i=>h.Document.ArgumentTokens(1)[i]==original.ArgumentTokens(1)[i])&&new[]{0,4,5}.All(i=>h.Document.ArgumentTokens(3)[i]==original.ArgumentTokens(3)[i]),"108 compression retains timing, duration, easing, group, direction and extras");
            byte[] compressed=h.Document.ToBytes();h.Execute(doc=>result=AirCoordinateRepair.LimitSplits(doc));
            check(result.Notes==0&&h.UndoCount==1&&h.Document.ToBytes().SequenceEqual(compressed),"108 repeated compression is byte-identical without an extra undo");
            check(h.Undo()&&h.Document.ToBytes().SequenceEqual(original.ToBytes())&&h.Redo()&&h.Document.ToBytes().SequenceEqual(compressed),"108 one undo/redo restores original large ratios exactly");
            var bad=SpcDocument.Parse(UserExample+"\nflick(0,10000000,200,20,16)\n");byte[] before=bad.ToBytes();
            check(Throws(()=>AirCoordinateRepair.LimitSplits(bad))&&bad.ToBytes().SequenceEqual(before),"108 an unsupported endpoint rejects the entire compression before any write");
        }
    }
}
