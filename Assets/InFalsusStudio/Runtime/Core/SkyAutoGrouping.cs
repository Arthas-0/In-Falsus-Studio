using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    // Explicit tool only. Rebuild chronological chains using the same joins as
    // automatic head detection. A merge inherits the most recent predecessor;
    // separate earlier roots are not retroactively merged or deprived of heads.
    public static class SkyAutoGrouping
    {
        public struct Result { public int Notes,Groups,Changed; }
        public static Result Assign(SpcDocument document)
        {
            var notes=document.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea)
                .OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
            foreach(var n in notes)
                if(ChartMath.InvalidReason(n)!=null)
                    throw new ArgumentException("SkyArea 无法判断连接，请先修正源行 "+(document.SourceIndex(n.SourceId)+1)+": "+ChartMath.InvalidReason(n));
            var assignments=new Dictionary<int,int>();
            var tails=new Dictionary<long,List<SpcEvent>>();
            var result=new Result{Notes=notes.Length};
            foreach(var n in notes)
            {
                SpcEvent prior=null;long tick=(long)Math.Floor(n.TimeMs/SkyGroups.TimeTolerance);
                for(long k=tick-1;k<=tick+1;k++)
                {
                    List<SpcEvent> candidates;if(!tails.TryGetValue(k,out candidates))continue;
                    foreach(var p in candidates)
                        if(SkyGroups.Joins(p,n)&&(prior==null||p.TimeMs>prior.TimeMs||p.TimeMs==prior.TimeMs&&p.SourceId<prior.SourceId))prior=p;
                }
                assignments[n.SourceId]=prior==null?result.Groups++:assignments[prior.SourceId];
                long end=(long)Math.Floor(n.EndMs/SkyGroups.TimeTolerance);List<SpcEvent> bucket;
                if(!tails.TryGetValue(end,out bucket))tails[end]=bucket=new List<SpcEvent>();
                bucket.Add(n);
            }
            foreach(var n in notes)
            {
                int group=assignments[n.SourceId];
                if(n.Args.Length>10&&n.Get(10)==group)continue;
                document.SetOptionalNumber(n.SourceId,10,group);result.Changed++;
            }
            return result;
        }
    }
}
