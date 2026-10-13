using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace InFalsusStudio.Core
{
    public static class Release104SelfTests
    {
        public static void Run(Action<bool,string> check)
        {
            var doc=SpcDocument.Parse("chart(163,4)\ntrack(36810,99999)\ntrack(36811,.1)\ntrack(37546,1)\ntrack(37592,99999)\ntrack(37593,.1)\nflick(38282,8,20,4,4)\n");
            var source=doc.ToBytes();var ev=doc.ReadEvents();var tl=new ScrollTimeline(ev);
            check(ev.All(e=>AuthoringSafety.Reason(e)==null),"104 actual 99999 impulse is supported, not quarantined");
            check(Math.Abs(tl.Delta(37593,37592)-99999)<1e-8,"104 1ms impulse integrates ALL displacement");
            check(Math.Abs(tl.Delta(38282,37593)*.015-1.0335)<1e-8,"104 next group near judge after pulse");
            check(tl.Delta(38282,37592)*.015>65,"104 next group beyond horizon before pulse");
            check(!ScrollVisibility.Point(tl,38282,37592,.015,-1.5,65),"104 no offscreen future Flick before pulse");
            check(ScrollVisibility.Point(tl,38282,37593,.015,-1.5,65),"104 Flick visible after pulse");
            check(Math.Abs(tl.Delta(37600,37585)-(7+99999+.7))<1e-8,"104 render frame skipping the impulse still includes it");
            check(tl.Delta(37593,37593)==0&&Math.Abs(tl.Delta(37592,37593)+99999)<1e-8,"104 seek/reverse antisymmetry");
            check(doc.ToBytes().SequenceEqual(source),"104 evaluation never changes source");
            var huge=SpcDocument.Parse("chart(120,4)\ntrack(0,1000000)\ntrack(80000000,0.1)\n").ReadEvents();var local=new ScrollTimeline(huge);
            check(Math.Abs(local.Delta(80000001.25,80000001)-.025)<1e-12,"104 local small delta remains precise after huge prefix");
            foreach(double speed in new[]{-99999d,0,.1,1,100,99999})
            {
                var plan=new ScrollTimeline(new[]{new SpcEvent{Kind=EventKind.Track,Args=new[]{0d,speed}}});
                var spans=ScrollVisibility.Between(plan,0,300000,100000,.015,-1.5,65);
                foreach(var slice in spans)
                {
                    check(slice.Start>=0&&slice.End<=300000&&slice.End>=slice.Start,"104 clipped time in original range");
                    foreach(double t in new[]{slice.Start,(slice.Start+slice.End)*.5,slice.End})
                    {double z=plan.Delta(t,100000)*.015;check(z>=-1.50001&&z<=65.00001,"104 clip before float mesh @ speed "+speed);}
                }
            }
            var back=new ScrollTimeline(SpcDocument.Parse("track(0,1)\ntrack(1000,-1)\ntrack(2000,1)\n").ReadEvents());
            var disjoint=ScrollVisibility.Between(back,0,3000,0,.01,2,4);
            check(disjoint.Count==3,"104 reverse scroll returns all THREE disjoint visible sections");
            check(disjoint.Zip(disjoint.Skip(1),(x,y)=>x.End<y.Start).All(x=>x),"104 never join invisible gap with a giant quad");
            check(AuthoringSafety.Reason(SpcDocument.Parse("track(0,1000001)").ReadEvents()[0])!=null,"104 still bound malicious track magnitude");
            check(AuthoringSafety.Reason(SpcDocument.Parse("hold(0,1,1,300001)").ReadEvents()[0])!=null,"104 five-minute Hold safeguard remains");
            for(int w=1;w<=4;w++)
            {
                var hold=SpcDocument.Parse("hold(1000,1,"+w+",2000)").ReadEvents()[0];
                var tap=SpcDocument.Parse("tap(1000,"+w+",1)").ReadEvents()[0];
                check(NoteSkinPolicy.Ground(hold)==GroundSkin104.HoldBlue,"104 central Hold widths 1/2/3/4 share ONE blue material");
                check(NoteSkinPolicy.Ground(tap)==(w==1?GroundSkin104.TapBlue:GroundSkin104.TapWideMint),"104 only wide Tap has mint-white material");
                check(!NoteSkinPolicy.Active(hold,999,true)&&NoteSkinPolicy.Active(hold,1000,true)&&NoteSkinPolicy.Active(hold,2999,true)&&!NoteSkinPolicy.Active(hold,3000,true),"104 active interval is [head,tail), exact stop");
                check(!NoteSkinPolicy.Active(hold,2000,false),"104 no fake activation with autoplay off");
            }
            check(NoteSkinPolicy.Ground(SpcDocument.Parse("hold(0,0,1,1000)").ReadEvents()[0])==GroundSkin104.HoldPurple,"104 left Hold texture route");
            check(NoteSkinPolicy.Ground(SpcDocument.Parse("hold(0,5,1,1000)").ReadEvents()[0])==GroundSkin104.HoldPink,"104 right Hold texture route");
            check(NoteSkinPolicy.Ground(SpcDocument.Parse("tap(0,1,0)").ReadEvents()[0])==GroundSkin104.TapPurple,"104 left Tap tint route");
            check(NoteSkinPolicy.Ground(SpcDocument.Parse("tap(0,1,5)").ReadEvents()[0])==GroundSkin104.TapPink,"104 right Tap tint route");
        }
        public static void RunPair(string folder,Action<bool,string> check)
        {
            string prefix=Path.Combine(folder,"Forbidden_codeleviathan3");
            byte[] bytes=File.ReadAllBytes(prefix+".spc.txt");
            var plain=SpcDocument.FromBytes(bytes);var data=ProjectJson.Object(ProjectJson.Parse(File.ReadAllText(prefix+".json")));
            var imported=IcpJsonImport.Convert(data).Document;var a=plain.ReadEvents();var b=imported.ReadEvents();
            Func<SpcEvent,string> key=e=>e.Name+"("+string.Join(",",e.Args.Select(n=>n.ToString("G17",System.Globalization.CultureInfo.InvariantCulture)).ToArray())+")";
            check(a.Select(key).OrderBy(x=>x).SequenceEqual(b.Select(key).OrderBy(x=>x)),"104 full paired JSON/SPC semantic equivalence");
            check(a.Count(e=>e.IsNote)==1731&&a.Count(e=>e.Kind==EventKind.Track)==33,"104 full source counts");
            check(a.Count(e=>e.Kind==EventKind.Track&&e.Get(1)==99999)==7,"104 exactly seven high-speed events");
            check(b.Where(e=>e.Kind==EventKind.Track).All(e=>AuthoringSafety.Reason(e)==null),"104 all 33 retained/imported track events enter scrolling");
            var counts=TickCountResearch.Analyze(b);
            check(counts.Supported&&counts.Total==2433,"104 existing combo total unchanged: 2433");
            var oldCount=new ProvisionalComboTimeline(a);var newCount=new ProvisionalComboTimeline(b);
            foreach(double now in new[]{0d,36810d,37546d,37592d,37593d,38282d,41228d,100000d,170000d})
                check(oldCount.At(now).Total==newCount.At(now).Total,"104 count does not use scroll impulses @"+now);
            var scroll=new ScrollTimeline(b.Where(e=>AuthoringSafety.Reason(e)==null));
            foreach(var pulse in b.Where(e=>e.Kind==EventKind.Track&&e.Get(1)==99999))
            {
                check(Math.Abs(scroll.Delta(pulse.TimeMs+1,pulse.TimeMs)-99999)<1e-5,"104 actual pulse integrates exact width1ms @"+pulse.TimeMs);
                foreach(double now in new[]{pulse.TimeMs-.5,pulse.TimeMs,pulse.TimeMs+.5,pulse.TimeMs+1,pulse.TimeMs+20})
                foreach(var note in b.Where(e=>(e.Kind==EventKind.Hold||e.Kind==EventKind.SkyArea)&&ChartMath.InvalidReason(e)==null))
                foreach(var span in ScrollVisibility.Between(scroll,Math.Max(now,note.TimeMs),note.EndMs,now,.015,-1.5,65))
                {
                    double z0=scroll.Delta(span.Start,now)*.015,z1=scroll.Delta(span.End,now)*.015;
                    check(z0>=-1.50001&&z0<=65.00001&&z1>=-1.50001&&z1<=65.00001,"104 real long-note visible endpoints bounded");
                }
            }
            check(plain.ToBytes().SequenceEqual(bytes),"104 entire source file remains byte-identical");
        }
    }
}
