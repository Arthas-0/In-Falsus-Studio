using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    /// <summary>Explicit groups retain their first chronological head.
    /// Automatic (-1/absent) SkyAreas hide the head only when a predecessor touches.
    /// Reading never rewrites IDs. Automatic authoring stays automatic.</summary>
    public static class SkyGroups
    {
        // Native-style forgiving joins include a 2 ms gap OR a 2 ms overlap.
        // This changes connectivity only; source timestamps are never snapped.
        public const double TimeTolerance=2, SpaceTolerance=1e-9;
        public static bool Automatic(SpcEvent note){return note.Get(10,-1)==-1;}
        public static int GroupOf(SpcEvent note)
        {
            double g=note.Get(10,-1);
            return !double.IsNaN(g)&&!double.IsInfinity(g)&&g>=0&&g<=int.MaxValue&&g==Math.Truncate(g)?(int)g:-1;
        }
        public static HashSet<int> Heads(IEnumerable<SpcEvent> source)
        {
            var groups=new HashSet<int>();var heads=new HashSet<int>();
            var tails=new Dictionary<long,List<SpcEvent>>();
            foreach(var n in source.Where(n=>n.Kind==EventKind.SkyArea&&ChartMath.InvalidReason(n)==null)
                .OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId))
            {
                int group=GroupOf(n);bool joined=false,automatic=Automatic(n);
                if(automatic)
                {
                    long tick=(long)Math.Floor(n.TimeMs/TimeTolerance);
                    for(long k=tick-1;k<=tick+1&&!joined;k++)
                    {List<SpcEvent> candidates;if(tails.TryGetValue(k,out candidates))joined=candidates.Any(p=>Joins(p,n));}
                }
                if(group<0?(!automatic||!joined):groups.Add(group))heads.Add(n.SourceId);
                long end=(long)Math.Floor(n.EndMs/TimeTolerance);List<SpcEvent> bucket;
                if(!tails.TryGetValue(end,out bucket))tails[end]=bucket=new List<SpcEvent>();bucket.Add(n);
            }
            return heads;
        }
        public static bool Joins(SpcEvent earlier,SpcEvent later)
        {
            if(earlier.SourceId==later.SourceId||earlier.TimeMs>=later.TimeMs||
               Math.Abs(earlier.EndMs-later.TimeMs)>TimeTolerance)return false;
            var tail=ChartMath.SkyAt(earlier,earlier.EndMs);var head=ChartMath.SkyAt(later,later.TimeMs);
            return Math.Min(tail.Right,head.Right)+SpaceTolerance>=Math.Max(tail.Left,head.Left);
        }
        private static bool GeometryChanged(SpcEvent a,SpcEvent b)
        {
            if(a==null)return true;
            // Editing only an explicit group is an intentional override, not geometry editing.
            for(int i=0;i<10;i++)if(a.Get(i)!=b.Get(i))return true;
            return false;
        }
        /// <summary>Called on the cloned document BEFORE it enters undo history.
        /// A changed predecessor also propagates its group into unchanged touching successors.
        /// Breaking a connection does NOT split or renumber the existing group.</summary>
        public static void NormalizeAuthoredChanges(SpcDocument before,SpcDocument after)
        {
            var previous=before.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea).ToDictionary(n=>n.SourceId);
            var notes=after.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea&&GroupOf(n)>=0&&ChartMath.InvalidReason(n)==null)
                .OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
            var affected=new HashSet<int>();
            foreach(var n in notes)
            {
                SpcEvent old;previous.TryGetValue(n.SourceId,out old);
                if(GeometryChanged(old,n))affected.Add(n.SourceId);
            }
            foreach(var n in notes)
            {
                var candidates=notes.Where(p=>Joins(p,n));
                if(!affected.Contains(n.SourceId)&&!candidates.Any(p=>affected.Contains(p.SourceId)))continue;
                // Prefer the most recent predecessor, then the first source row. This is
                // deterministic when two predecessors overlap; do not union unrelated groups.
                var prior=candidates.OrderByDescending(p=>p.TimeMs).ThenBy(p=>p.SourceId).FirstOrDefault();
                if(prior==null)continue;
                int g=GroupOf(prior);
                if(GroupOf(n)!=g)
                {
                    after.SetOptionalNumber(n.SourceId,10,g);
                    n.Args=after.ReadEvents().First(e=>e.SourceId==n.SourceId).Args;
                }
                affected.Add(n.SourceId);
            }
        }
    }
}
