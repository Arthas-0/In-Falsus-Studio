using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    public enum ChartIssueKind { UnknownStatement,TapOverlap,HoldOverlap,FlickDuplicate,HoldTooShort,SkyAreaTooShort }
    public sealed class ChartIssue
    {
        public readonly ChartIssueKind Kind;
        public readonly int[] SourceIds,Lines;
        public readonly double TimeMs;
        public readonly string SourceText;
        public ChartIssue(ChartIssueKind kind,int[] ids,int[] lines,double time,string source="")
        {Kind=kind;SourceIds=ids;Lines=lines;TimeMs=time;SourceText=source;}
    }
    // Read-only. Unknown source remains intact, and validation never repairs notes.
    public static class ChartHealthCheck
    {
        public static ChartIssue[] Run(SpcDocument document)
        {
            var events=document.ReadEvents();var byId=events.ToDictionary(n=>n.SourceId);var result=new List<ChartIssue>();
            for(int row=0;row<document.LineCount;row++)
            {
                int id=document.LineIdAt(row);string raw=document.SourceLine(id),trim=raw.TrimStart();SpcEvent e;
                if(trim.Length==0||trim.StartsWith("//",StringComparison.Ordinal)||trim.StartsWith("#",StringComparison.Ordinal))continue;
                if(!byId.TryGetValue(id,out e)||e.Kind==EventKind.Unknown||e.Kind==EventKind.Beam)
                    result.Add(new ChartIssue(ChartIssueKind.UnknownStatement,new[]{id},new[]{row+1},0,raw));
            }
            foreach(var note in events.Where(n=>(n.Kind==EventKind.Hold||n.Kind==EventKind.SkyArea)&&n.DurationMs<5))
                result.Add(new ChartIssue(note.Kind==EventKind.Hold?ChartIssueKind.HoldTooShort:ChartIssueKind.SkyAreaTooShort,
                    new[]{note.SourceId},new[]{document.SourceIndex(note.SourceId)+1},note.TimeMs,document.SourceLine(note.SourceId)));
            foreach(var kind in new[]{EventKind.Tap,EventKind.Hold})
            {
                var notes=events.Where(n=>n.Kind==kind&&ChartMath.InvalidReason(n)==null).OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
                var parent=Enumerable.Range(0,notes.Length).ToArray();var touched=new bool[notes.Length];
                for(int i=0;i<notes.Length;i++)
                    for(int j=i+1;j<notes.Length;j++)
                    {
                        if(kind==EventKind.Tap?notes[j].TimeMs-notes[i].TimeMs>=3:notes[j].TimeMs>=notes[i].EndMs)break;
                        if(!LanesOverlap(notes[i],notes[j]))continue;
                        if(kind==EventKind.Hold&&Math.Min(notes[i].EndMs,notes[j].EndMs)<=Math.Max(notes[i].TimeMs,notes[j].TimeMs))continue;
                        touched[i]=touched[j]=true;parent[Root(parent,j)]=Root(parent,i);
                    }
                foreach(var group in Enumerable.Range(0,notes.Length).Where(i=>touched[i]).GroupBy(i=>Root(parent,i)))
                    result.Add(Issue(document,kind==EventKind.Tap?ChartIssueKind.TapOverlap:ChartIssueKind.HoldOverlap,group.Select(i=>notes[i])));
            }
            // All argument tokens must match, including optional parameters. Formatting
            // around tokens/comments is ignored; equivalent but different ratios are not.
            foreach(var group in events.Where(n=>n.Kind==EventKind.Flick).GroupBy(n=>string.Join(",",document.ArgumentTokens(n.SourceId).Select(t=>t.Trim()))))
                if(group.Count()>1)result.Add(Issue(document,ChartIssueKind.FlickDuplicate,group));
            return result.OrderBy(n=>n.Lines.Min()).ThenBy(n=>n.Kind).ToArray();
        }
        private static int Root(int[] parent,int i)
        {while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
        private static bool LanesOverlap(SpcEvent a,SpcEvent b)
        {return Math.Max(a.Lane,b.Lane)<Math.Min(a.Lane+a.GroundWidth,b.Lane+b.GroundWidth);}
        private static ChartIssue Issue(SpcDocument document,ChartIssueKind kind,IEnumerable<SpcEvent> source)
        {
            var notes=source.OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
            return new ChartIssue(kind,notes.Select(n=>n.SourceId).ToArray(),notes.Select(n=>document.SourceIndex(n.SourceId)+1).ToArray(),notes.Min(n=>n.TimeMs));
        }
    }
}
