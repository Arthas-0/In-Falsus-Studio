using System;
using System.Linq;

namespace InFalsusStudio.Core
{
    // Regressions for the user-specified musical colors and complete range membership.
    public static class GridSelectionSelfTests
    {
        private static AuthoringGrid Grid(string source)
        {return new AuthoringGrid(new BeatTimeline(SpcDocument.Parse(source).ReadEvents()));}
        private static string Signature(System.Collections.Generic.IEnumerable<BeatGridPoint> lines)
        {
            return string.Concat(lines.Select(q=>q.Kind==BeatGridKind.Bar?"P":q.Kind==BeatGridKind.Quarter?"B":
                q.Kind==BeatGridKind.Eighth?"G":q.Kind==BeatGridKind.Sixteenth?"R":"W"));
        }
        public static void Run(Action<bool,string> check)
        {
            // P=purple bar, B=blue quarter, G=green eighth, R=red sixteenth, W=white.
            const string fourBeatBar="PRGRBRGRBRGRBRGRP";
            foreach(int bpm in new[]{120,130,163,178,999})
            {
                var grid=Grid("chart("+bpm+",4)\n");
                var lines=grid.Between(0,AuthoringGrid.RoundMs(240000.0/bpm),4);
                check(Signature(lines)==fourBeatBar,"grid 1/4 musical sequence, including rounded ms, BPM "+bpm);
                check(lines.All(q=>grid.Nearest(q.TimeMs,4).TimeMs==q.TimeMs),"colored lines retain authored snap times, BPM "+bpm);
            }
            var five=Grid("chart(120,5)\n").Between(0,2500,4);
            check(Signature(five)=="PRGRBRGRBRGRBRGRBRGRP","meter5 has five quarters per purple measure");
            check(five.Where(q=>q.Kind==BeatGridKind.Bar).Select(q=>q.TimeMs).SequenceEqual(new[]{0d,2500d}),"meter5 purple boundaries independent of subdivision");
            check(five.Count(q=>q.Kind==BeatGridKind.Quarter)==4,"meter5 blue quarters are overridden at measure boundaries");
            check(Signature(Grid("chart(120,4)\n").Between(0,500,3))=="PWWB","1/3 non-binary subdivisions are white");
            check(Signature(Grid("chart(120,4)\n").Between(0,500,5))=="PWWWWB","1/5 non-binary subdivisions are white");
            check(Signature(Grid("chart(120,4)\n").Between(0,500,6))=="PWWGWWB","1/6 retains green half-beat among white subdivisions");
            check(Signature(Grid("chart(120,4)\n").Between(0,500,8))=="PWRWGWRWB","1/8 colors actual eighth/sixteenth notes, others white");
            check(Signature(Grid("chart(120,4)\n").Between(0,500,12))=="PWWRWWGWWRWWB","1/12 positions keep binary musical hierarchy");
            var reset=Grid("chart(120,4)\nbpm(1123,150,5)\nbpm(3001,178,-1)\n");
            check(Signature(reset.Between(1123,1523,4))=="PRGRB","new timing origin restarts purple/quarter phase");
            check(reset.Nearest(3001,7).Kind==BeatGridKind.Bar,"inherited meter also restarts purple at next timing event");
            foreach(int division in new[]{1,2,3,4,5,6,7,8,12,16,96,1024})
            {
                var lines=reset.Between(3001,AuthoringGrid.RoundMs(3001+300000.0/178),division);
                check(lines.Where(q=>q.IsBar).Select(q=>q.TimeMs).SequenceEqual(new[]{3001d,4686d}),"meter5 inherited purple boundaries, N="+division);
            }
            var priority=Grid("chart(120,4)\n").Between(0,500,8);
            check(priority[0].Priority>priority[8].Priority&&priority[8].Priority>priority[4].Priority&&
                priority[4].Priority>priority[2].Priority&&priority[2].Priority>priority[1].Priority,
                "coincident guides prefer purple, blue, green, red, white in order");

            var doc=SpcDocument.Parse("chart(120,4)\n"+
                "tap(999,1,1)\ntap(1000,1,1)\nflick(2000,50,100,10,4)\n"+
                "hold(500,1,1,1500)\nhold(1500,1,1,1000)\n"+
                "skyarea(500,50,100,10,50,100,10,0,0,1000)\n"+
                "hold(1000,1,1,1000)\nskyarea(1000,50,100,10,50,100,10,0,0,1000)\n"+
                "hold(1100,1,1,500)\nskyarea(1200,50,100,10,50,100,10,0,0,200)\n"+
                "hold(500,1,1,2000)\ntap(2001,1,1)\nbpm(1500,150,4)\nhold(2000,1,1,0)\n"+
                "hold(1000,1,1,1000.125)\nskyarea(999.875,50,100,10,50,100,10,0,0,500)\n");
            byte[] before=doc.ToBytes();var events=doc.ReadEvents();
            var expected=new[]{2,3,7,8,9,10,14};
            check(SelectionAuthoring.InRange(events,1000,2000).SequenceEqual(expected),"range includes fully contained points and Hold/Sky only");
            check(SelectionAuthoring.InRange(events,2000,1000).SequenceEqual(expected),"reverse range clicks preserve full containment");
            check(SelectionAuthoring.InRange(events,2000,2000).SequenceEqual(new[]{3,14}),"zero-width range excludes long notes ending at the boundary");
            check(SelectionAuthoring.InRange(events,500,2000).Contains(4),"long-note head and tail exactly on range bounds are included");
            check(!SelectionAuthoring.InRange(events,1000,2000).Contains(15)&&!SelectionAuthoring.InRange(events,1000,2000).Contains(16),"fractional endpoint protrusions are excluded");
            check(doc.ToBytes().SequenceEqual(before),"range selection preserves source bytes");
        }
    }
}
