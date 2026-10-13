using System;
using System.Linq;

namespace InFalsusStudio.Core
{
    // Product safety limits, not a claimed restriction in the original SPC format.
    public static class AuthoringSafety
    {
        public const double MaxTrackSpeedMagnitude=1000000; // Numeric budget, NOT a game-format limit. Includes 1ms 99999 impulses.
        public const double MaxDurationMs=300000; // A single Hold/Sky: five minutes, inclusive.
        public const double MaxTimelineMs=86400000; // A generous separate 24-hour navigation bound.
        public static bool Finite(double v){return !double.IsNaN(v)&&!double.IsInfinity(v);}
        public static double Time(double v)
        {if(!Finite(v)||v<0||v>MaxTimelineMs)throw new ArgumentOutOfRangeException("time","时间必须在 0～86400000 ms（24小时）之间。 Time outside safety range.");return v;}
        public static void Duration(double start,double end)
        {Time(start);Time(end);if(end<=start||end-start>MaxDurationMs)throw new ArgumentOutOfRangeException("duration","Hold / SkyArea 持续时间须 >0 且 ≤300000 ms（5分钟）。");}
        public static string Reason(SpcEvent e)
        {
            if(e==null)return "Missing event.";
            if(e.Kind==EventKind.Unknown||e.Kind==EventKind.Beam)return null;
            if(e.Args==null)return "Missing event parameters.";
            for(int i=0;i<e.Args.Length;i++)if(!Finite(e.Args[i]))return "Non-finite event parameter.";
            if(!Finite(e.TimeMs)||e.TimeMs<0||e.TimeMs>MaxTimelineMs)return "Event time exceeds the 24-hour safety limit.";
            if(e.Kind==EventKind.Hold||e.Kind==EventKind.SkyArea)
                if(!Finite(e.DurationMs)||e.DurationMs<0||e.DurationMs>MaxDurationMs||!Finite(e.EndMs)||e.EndMs>MaxTimelineMs)
                    return "Hold / SkyArea exceeds 300000 ms (5 minutes); source retained, preview disabled.";
            if(e.Kind==EventKind.Chart||e.Kind==EventKind.Bpm)
            {
                double bpm=e.Get(e.Kind==EventKind.Chart?0:1),meter=e.Get(e.Kind==EventKind.Chart?1:2,4);
                if(Math.Abs(bpm)>10000||(bpm!=0&&Math.Abs(bpm)<.01)||meter>1000000)return "BPM/meter exceeds supported safety range.";
            }
            if(e.Kind==EventKind.Flick||e.Kind==EventKind.SkyArea)
            {
                int last=e.Kind==EventKind.Flick?3:6;
                for(int i=1;i<=last;i++)if(Math.Abs(e.Get(i))>1000000000)return "Air coordinate exceeds safe numeric magnitude.";
                if(e.Get(2)<1e-9||e.Kind==EventKind.SkyArea&&e.Get(5)<1e-9)return "Air denominator too small for stable geometry.";
            }
            if(e.Kind==EventKind.Track&&Math.Abs(e.Get(1))>MaxTrackSpeedMagnitude)return "Track speed exceeds +/-1000000 numeric safety range; source retained.";
            return null;
        }
        public static void BeforeCommit(SpcDocument before,SpcDocument after)
        {
            ValidateChanged(before,after); // Reject before normalization or render/count work.
            SkyGroups.NormalizeAuthoredChanges(before,after);
            ValidateChanged(before,after);
        }
        public static void ValidateChanged(SpcDocument before,SpcDocument after)
        {
            foreach(var e in after.ReadEvents())
            {
                if(before.Contains(e.SourceId)&&before.SourceLine(e.SourceId)==after.SourceLine(e.SourceId))continue;
                string reason=Reason(e);if(reason!=null)throw new ArgumentException(reason);
                if(e.Kind==EventKind.Unknown && (e.Name=="hold"||e.Name=="skyarea"||e.Name=="tap"||e.Name=="flick"||e.Name=="bpm"||e.Name=="track"||e.Name=="chart"))
                    throw new ArgumentException("Invalid numeric statement. Previous value retained.");
            }
        }
    }
}
