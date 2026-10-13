using System;
using System.Linq;

namespace InFalsusStudio.Core
{
    public static class Release106SelfTests
    {
        private static SpcEvent Find(SpcDocument d,int id){return d.ReadEvents().First(n=>n.SourceId==id);}
        private static bool Near(double a,double b){return Math.Abs(a-b)<=RationalAirCoordinates.SampleEdgeTolerance;}
        private static bool Throws(Action action){try{action();return false;}catch{return true;}}
        public static void Run(Action<bool,string> check)
        {
            check(new Version(ReleaseIdentity.Version)>=new Version("1.0.6")&&ReleaseIdentity.DisplayVersion==ReleaseIdentity.Version,"106 running release identity remains current");
            check(ReleaseNotesCatalog.Recent.Length==3&&ReleaseNotesCatalog.Recent[0].Version==ReleaseIdentity.Version&&
                new Version(ReleaseNotesCatalog.Recent[0].Version)>new Version(ReleaseNotesCatalog.Recent[1].Version)&&
                new Version(ReleaseNotesCatalog.Recent[1].Version)>new Version(ReleaseNotesCatalog.Recent[2].Version),"106 latest three announcements remain ordered");
            var groups=SpcDocument.Parse("chart(120,4)\nskyarea(1000,50,100,20,50,100,20,0,0,500,-1)\n"+
                "skyarea(1500,50,100,20,50,100,20,0,0,500,-1)\nskyarea(2000,90,100,10,90,100,10,0,0,500,-1)\n"+
                "skyarea(3000,50,100,10,50,100,10,0,0,500,7)\nskyarea(5000,80,100,10,80,100,10,0,0,500,7)\n");
            var bytes=groups.ToBytes();
            check(SkyGroups.Heads(groups.ReadEvents()).SetEquals(new[]{1,3,4}),"106 automatic joins hide touching heads, gaps remain heads, explicit groups keep first head");
            check(groups.ToBytes().SequenceEqual(bytes),"106 head detection does not rewrite group IDs");
            var nonstandard=SpcDocument.Parse("chart(120,4)\nskyarea(0,50,100,10,50,100,10,0,0,500,-2)\nskyarea(500,50,100,10,50,100,10,0,0,500,-2)\n");
            check(SkyGroups.Heads(nonstandard.ReadEvents()).Count==2&&SkyGroupNavigation.Members(nonstandard.ReadEvents(),-1).Length==0,"106 only -1/absent IDs use automatic rendering; other negative IDs retain legacy independent heads");
            check(SkyGroupNavigation.Members(groups.ReadEvents(),-1).Length==3,"106 upper group browser can navigate automatic SkyAreas");
            var history=new EditHistory(groups.Clone());history.BeforeCommit=AuthoringSafety.BeforeCommit;
            int created=-1;history.Execute(d=>created=EditOperations.Add(d,EventKind.SkyArea,2000,2300,1,1,.5,.5,.2,16,100));
            check(Find(history.Document,created).Args.Length==10,"109 authoring normalization never allocates a group argument to a new SkyArea");
            history.Execute(d=>d.SetOptionalNumber(created,10,42));
            check(Find(history.Document,created).Get(10)==42,"106 manual group override remains available");
            history.Execute(SkySplitOperations.ResetGroups);
            check(history.Document.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea).All(n=>n.Args.Length==10),"109 reset removes group assignments from every SkyArea in the current chart");
            check(history.Undo()&&Find(history.Document,created).Get(10)==42,"106 reset is undoable");
            Splits(check);Health(check);Audio(check);
        }
        private static void Splits(Action<bool,string> check)
        {
            for(int left=0;left<3;left++)for(int right=0;right<3;right++)
            {
                var original=SpcDocument.Parse("chart(120,4)\r\nskyarea(1000,30,100,20,70,100,30,"+left+","+right+",1000,9,123) # kept\r\nmystery(1)\r\n",true);
                var curve=Find(original,1);var h=new EditHistory(original.Clone());h.BeforeCommit=AuthoringSafety.BeforeCommit;int[] ids=null;
                h.Execute(d=>ids=SkySplitOperations.Equal(d,new[]{1},4));
                var pieces=h.Document.ReadEvents().Where(n=>ids.Contains(n.SourceId)).OrderBy(n=>n.TimeMs).ToArray();
                check(pieces.Length==4&&ids[0]==1&&pieces[0].TimeMs==1000&&pieces[3].EndMs==2000,"106 equal split count, source ID and endpoints "+left+"/"+right);
                foreach(var piece in pieces)
                {
                    var a=ChartMath.SkyAt(curve,piece.TimeMs);var b=ChartMath.SkyAt(curve,piece.EndMs);
                    var pa=ChartMath.SkyAt(piece,piece.TimeMs);var pb=ChartMath.SkyAt(piece,piece.EndMs);
                    check(Near(a.Left,pa.Left)&&Near(a.Right,pa.Right)&&Near(b.Left,pb.Left)&&Near(b.Right,pb.Right),"106 independent original edge samples "+left+"/"+right+" @"+piece.TimeMs);
                }
                check(pieces.All(n=>n.Get(7)==0&&n.Get(8)==0&&n.Args.Length==10&&h.Document.SourceLine(n.SourceId).Contains("ungrouped_skyarea_base64=")),"109 linear unassigned pieces retain nonstandard fields as provenance "+left+"/"+right);
                check(h.Document.SourceLine(1).EndsWith(" # kept")&&h.Document.SourceLine(2)=="mystery(1)"&&h.Document.HasUtf8Bom,"106 source suffix, unknown row and BOM preserved "+left+"/"+right);
                check(h.UndoCount==1&&h.Undo()&&h.Document.ToBytes().SequenceEqual(original.ToBytes())&&h.Redo(),"106 split is a single reversible transaction "+left+"/"+right);
                var flickDoc=original.Clone();var flicks=SkySplitOperations.Equal(flickDoc,new[]{1},4,true,4).Select(id=>Find(flickDoc,id)).ToArray();
                check(flicks.Length==4&&flicks.All(n=>n.Kind==EventKind.Flick&&n.Get(4)==4)&&flicks.Last().TimeMs==1750,"106 N Flicks sample interval starts, no extra tail "+left+"/"+right);
                check(flicks.All(n=>Near(ChartMath.FlickRange(n).Left,ChartMath.SkyAt(curve,n.TimeMs).Left)&&Near(ChartMath.FlickRange(n).Right,ChartMath.SkyAt(curve,n.TimeMs).Right)),"106 Flick footprint matches both original edges "+left+"/"+right);
            }
            var gridDoc=SpcDocument.Parse("chart(120,4)\nskyarea(500,50,100,10,50,100,10,0,0,500,-1)\nskyarea(510,50,100,10,50,100,10,0,0,480,4)\n");
            var grid=new AuthoringGrid(new BeatTimeline(gridDoc.ReadEvents()));var originalBytes=gridDoc.ToBytes();
            check(Throws(()=>SkySplitOperations.Equal(gridDoc,new[]{1,2},4))&&gridDoc.ToBytes().SequenceEqual(originalBytes),"106 equal split rejects a multiple selection without mutations");
            var single=gridDoc.Clone();var ids1=SkySplitOperations.Grid(single,new[]{1},grid,4);
            check(ids1.Length==4&&ids1.Select(id=>Find(single,id).TimeMs).SequenceEqual(new[]{500d,625,750,875}),"106 blue-to-blue quarter interval contains four 1/4-grid pieces");
            var multi=gridDoc.Clone();var multiIds=SkySplitOperations.Grid(multi,new[]{1,2},grid,4);
            check(multiIds.Length==8&&Find(multi,2).TimeMs==510&&multi.ReadEvents().Where(n=>multiIds.Contains(n.SourceId)).Max(n=>n.EndMs)==1000,"106 grid supports multiple SkyAreas and retains partial edge intervals");
            var changedTiming=SpcDocument.Parse("chart(120,4)\nbpm(750,240,5)\nskyarea(500,50,100,10,50,100,10,0,0,500,-1)\n");
            var timedGrid=new AuthoringGrid(new BeatTimeline(changedTiming.ReadEvents()));
            var timed=SkySplitOperations.Grid(changedTiming,new[]{2},timedGrid,4).Select(id=>Find(changedTiming,id)).ToArray();
            check(timed.Select(n=>n.TimeMs).SequenceEqual(new[]{500d,625,750,813,875,938}),"106 grid split follows timing changes and actual rounded lines");
            var tooFine=new EditHistory(SpcDocument.Parse("chart(120,4)\nskyarea(0,50,100,10,50,100,10,0,0,1,-1)\n"));
            byte[] tiny=tooFine.Document.ToBytes();
            check(Throws(()=>tooFine.Execute(d=>SkySplitOperations.Equal(d,new[]{1},4096)))&&tooFine.Document.ToBytes().SequenceEqual(tiny)&&tooFine.UndoCount==0,"106 invalid sub-ms split fails atomically");
        }
        private static void Health(Action<bool,string> check)
        {
            var d=SpcDocument.Parse("chart(120,4)\ntap(1000,2,1)\ntap(1002,1,2)\ntap(1003,1,1)\ntap(1001,1,3)\n"+
                "hold(2000,1,2,500)\nhold(2499,2,1,500)\nhold(2500,1,1,100)\n"+
                "flick(4000,50,100,10,16)\n  flick(4000, 50,100,10,16) // duplicate\nflick(4000,5,10,1,16)\n"+
                "skyarea(1000,50,100,10,50,100,10,0,0,1000,-1)\nskyarea(1000,50,100,10,50,100,10,0,0,1000,-1)\n"+
                "mystery(50,2)\nopaque data\nbeam(opaque)\n// comment\n# comment\n");
            byte[] before=d.ToBytes();var issues=ChartHealthCheck.Run(d);
            check(issues.Count(n=>n.Kind==ChartIssueKind.UnknownStatement)==3&&issues.Where(n=>n.Kind==ChartIssueKind.UnknownStatement).Select(n=>n.Lines[0]).SequenceEqual(new[]{14,15,16}),"106 unknown and unsupported statements have exact 1-based source lines");
            check(issues.Single(n=>n.Kind==ChartIssueKind.TapOverlap).SourceIds.OrderBy(n=>n).SequenceEqual(new[]{1,2}),"106 wide Tap lane overlap and strict <3ms; adjacent lanes and exact 3ms excluded");
            check(issues.Single(n=>n.Kind==ChartIssueKind.HoldOverlap).SourceIds.OrderBy(n=>n).SequenceEqual(new[]{5,6}),"106 Hold spatial/time intersection; touching endpoints excluded");
            check(issues.Single(n=>n.Kind==ChartIssueKind.FlickDuplicate).SourceIds.OrderBy(n=>n).SequenceEqual(new[]{8,9}),"106 Flick whole argument match; equivalent alternative ratios are not duplicate statements");
            check(issues.Length==6&&d.ToBytes().SequenceEqual(before),"106 SkyArea overlap ignored and check never changes source");
            var chain=ChartHealthCheck.Run(SpcDocument.Parse("chart(120,4)\ntap(1000,1,1)\ntap(1002,1,1)\ntap(1004,1,1)\n"));
            check(chain.Length==1&&chain[0].SourceIds.Length==3,"106 jump payload includes every note in a connected overlap component");
            var sides=ChartHealthCheck.Run(SpcDocument.Parse("chart(120,4)\ntap(0,1,0)\ntap(1,1,1)\ntap(0,1,5)\nhold(0,0,1,100)\nhold(0,1,4,100)\n"));
            check(sides.Length==0,"106 side lanes remain independent of the central four lanes");
        }
        private static void Audio(Action<bool,string> check)
        {
            const int frequency=8000,frames=16000;var pcm=new float[frames*2];
            for(int i=0;i<frames;i++){pcm[i*2]=(float)(.7*Math.Sin(2*Math.PI*440*i/frequency));pcm[i*2+1]=-pcm[i*2];}
            foreach(double rate in new[]{.25,.5,.75,1d,2d})
            {
                var processor=new WsolaTimeStretch(pcm,2,frequency,rate);var output=new float[processor.OutputFrames*2];
                int at=0;while(at<output.Length)
                {
                    int size=Math.Min(514,output.Length-at);var chunk=new float[size];processor.Read(chunk);Array.Copy(chunk,0,output,at,size);at+=size;
                }
                int crossings=0,begin=800,end=processor.OutputFrames-800;
                for(int i=begin+1;i<end;i++)if(output[(i-1)*2]<=0&&output[i*2]>0)crossings++;
                double detected=crossings*frequency/(double)(end-begin);
                check(Math.Abs(detected-440)<2,"106 pitch preserved near 440Hz at "+rate+"x (measured "+detected+")");
                check(processor.OutputFrames==(int)Math.Ceiling(frames/rate)&&output.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)&&Math.Abs(x)<=.701f),"106 exact stretched duration and finite bounded PCM "+rate+"x");
                check(Enumerable.Range(0,processor.OutputFrames).All(i=>Math.Abs(output[i*2]+output[i*2+1])<1e-7),"106 stereo anti-phase alignment retained "+rate+"x");
                var tail=new float[100];processor.Read(tail);check(tail.All(x=>x==0),"106 output ends with silence "+rate+"x");
                int seek=processor.OutputFrames/3;processor.ResetOutputPosition(seek);var seekPcm=new float[128];processor.Read(seekPcm);
                int source=(int)Math.Round(seek*rate);
                check(Math.Abs(seekPcm[0]-pcm[source*2])<1e-7,"106 random seek maps output samples to original music time "+rate+"x");
            }
            var quiet=new WsolaTimeStretch(new float[8000],1,8000,.25);var silent=new float[8000];quiet.Read(silent);
            check(silent.All(n=>n==0),"106 silence remains finite silence");
        }
    }
}
