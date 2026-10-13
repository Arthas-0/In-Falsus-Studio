using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    // Sample BOTH original eased edges in chart time before replacing any source.
    // Linear pieces interpolate these samples; they never restart a sine curve.
    public static class SkySplitOperations
    {
        public const int MaxPieces=4096,MaxGenerated=20000;
        private sealed class Plan { public int Id;public string[] Lines; }
        public static void ResetGroups(SpcDocument document)
        {
            foreach(var n in document.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea).ToArray())
                SkyGroupSyntax.Clear(document,n.SourceId);
        }
        public static int[] Equal(SpcDocument document,IEnumerable<int> ids,int count,bool flick=false,int direction=16)
        {
            if(count<2||count>MaxPieces)throw new ArgumentException("N must be an integer in 2..4096.");
            var selected=Selected(document,ids);if(selected.Length!=1)throw new ArgumentException("Equal splitting requires exactly one SkyArea.");
            var n=selected[0];var cuts=new double[count+1];cuts[0]=n.TimeMs;cuts[count]=n.EndMs;
            for(int i=1;i<count;i++)cuts[i]=Math.Round(n.TimeMs+n.DurationMs*i/count,10,MidpointRounding.AwayFromZero);
            return Apply(document,new[]{MakePlan(document,n,cuts,flick,direction)});
        }
        public static int[] Grid(SpcDocument document,IEnumerable<int> ids,AuthoringGrid grid,int division,bool flick=false,int direction=16)
        {
            if(grid==null)throw new ArgumentNullException("grid");var plans=new List<Plan>();int total=0;
            foreach(var n in Selected(document,ids))
            {
                var cuts=new SortedSet<double>{n.TimeMs,n.EndMs};
                var lines=grid.Between(n.TimeMs,n.EndMs,division,MaxPieces+2);
                foreach(var line in lines)if(line.TimeMs>n.TimeMs&&line.TimeMs<n.EndMs)cuts.Add(line.TimeMs);
                if(cuts.Count-1>MaxPieces||lines.Count>=MaxPieces+2)throw new ArgumentException("This grid creates more than 4096 pieces for one SkyArea. Use a coarser subdivision.");
                total+=cuts.Count-1;if(total>MaxGenerated)throw new ArgumentException("More than 20000 generated notes. Split a smaller selection.");
                plans.Add(MakePlan(document,n,cuts.ToArray(),flick,direction));
            }
            return Apply(document,plans);
        }
        private static SpcEvent[] Selected(SpcDocument document,IEnumerable<int> ids)
        {
            var wanted=new HashSet<int>(ids);var notes=document.ReadEvents().Where(n=>wanted.Contains(n.SourceId)).ToArray();
            if(notes.Length==0||notes.Length!=wanted.Count||notes.Any(n=>n.Kind!=EventKind.SkyArea))
                throw new ArgumentException("Select SkyAreas only.");
            foreach(var n in notes)
            {
                string reason=ChartMath.InvalidReason(n)??AirReachBounds.Violation(n);
                if(reason!=null||n.DurationMs<=0)throw new ArgumentException(reason??"SkyArea needs a positive duration.");
                double progress;if(ChartMath.SkyMinimumWidth(n,out progress)<-1e-10)throw new ArgumentException("Crossed SkyArea edges must be fixed before splitting.");
            }
            return notes.OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
        }
        private static Plan MakePlan(SpcDocument document,SpcEvent n,double[] cuts,bool flick,int direction)
        {
            if(flick&&direction!=4&&direction!=16)throw new ArgumentException("Flick direction must be 4 or 16.");
            var original=document.ArgumentTokens(n.SourceId);var lines=new string[cuts.Length-1];
            string raw=document.SourceLine(n.SourceId);int close=raw.LastIndexOf(')');
            string suffix=close>=0?raw.Substring(close+1):"";
            string indent=raw.Substring(0,raw.Length-raw.TrimStart().Length);
            for(int i=0;i<lines.Length;i++)
            {
                if(cuts[i+1]-cuts[i]<.001)throw new ArgumentException("A split interval is shorter than 0.001 ms. Reduce N or use a coarser grid.");
                var start=ChartMath.SkyAt(n,cuts[i]);var a=RationalAirCoordinates.Sample(start,n.Get(2));
                if(flick)
                {
                    if(start.Width<=0||a[2]=="0")throw new ArgumentException("A Flick sample has zero integer width. Adjust the SkyArea endpoint before converting.");
                    lines[i]="flick("+EditOperations.Number(cuts[i])+","+string.Join(",",a)+","+direction+")";
                    if(i==0)lines[i]+=suffix+" // converted_skyarea_base64="+Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                }
                else
                {
                    var b=RationalAirCoordinates.Sample(ChartMath.SkyAt(n,cuts[i+1]),n.Get(2));var tokens=original.Take(10).ToList();
                    tokens[0]=EditOperations.Number(cuts[i]);
                    for(int k=0;k<3;k++){tokens[1+k]=a[k];tokens[4+k]=b[k];}
                    tokens[7]="0";tokens[8]="0";tokens[9]=EditOperations.Number(cuts[i+1]-cuts[i]);
                    lines[i]="skyarea("+string.Join(",",tokens)+")"+SkyGroupSyntax.ExtraProvenance(raw)+(i==0?suffix:"");
                }
            }
            lines[0]=indent+lines[0];return new Plan{Id=n.SourceId,Lines=lines};
        }
        private static int[] Apply(SpcDocument document,IEnumerable<Plan> plans)
        {
            var ids=new List<int>();
            foreach(var plan in plans)
            {
                document.ReplaceSourceLine(plan.Id,plan.Lines[0]);ids.Add(plan.Id);
                for(int i=1;i<plan.Lines.Length;i++)ids.Add(document.Append(plan.Lines[i]));
            }
            return ids.ToArray();
        }
    }
}
