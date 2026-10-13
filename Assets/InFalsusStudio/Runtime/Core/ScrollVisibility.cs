using System;
using System.Collections.Generic;

namespace InFalsusStudio.Core
{
    /// <summary>Clip each affine scroll segment in DOUBLE time/position before converting
    /// it to float mesh coordinates. Handles impulses, stops, and reverse scroll without
    /// connecting disjoint visible pieces across an invisible interval.</summary>
    public static class ScrollVisibility
    {
        public struct Slice { public double Start,End; public Slice(double a,double b){Start=a;End=b;} }
        public static List<Slice> Between(ScrollTimeline timeline,double start,double end,double now,
            double unitsPerMs,double near,double far)
        {
            var output=new List<Slice>();
            if(timeline==null||!AuthoringSafety.Finite(start)||!AuthoringSafety.Finite(end)||
                !AuthoringSafety.Finite(now)||!AuthoringSafety.Finite(unitsPerMs)||unitsPerMs<=0||
                !AuthoringSafety.Finite(near)||!AuthoringSafety.Finite(far)||far<near||end<start)return output;
            var points=timeline.Points;
            for(int i=0;i<points.Count;i++)
            {
                var point=points[i];double a=Math.Max(start,point.Time);
                double b=i+1<points.Count?Math.Min(end,points[i+1].Time):end;
                if(b<=a)continue;
                double z=timeline.Delta(a,now)*unitsPerMs,slope=point.Speed*unitsPerMs;
                if(!AuthoringSafety.Finite(z)||!AuthoringSafety.Finite(slope))continue;
                if(Math.Abs(slope)<1e-15)
                {if(z>=near&&z<=far)output.Add(new Slice(a,b));continue;}
                double n=a+(near-z)/slope,f=a+(far-z)/slope;
                double first=Math.Max(a,Math.Min(n,f)),last=Math.Min(b,Math.Max(n,f));
                if(last>first)output.Add(new Slice(first,last));
            }
            return output;
        }
        public static bool Intersects(ScrollTimeline timeline,double start,double end,double now,
            double unitsPerMs,double near,double far)
        {
            if(timeline==null||end<=start||unitsPerMs<=0)return false;
            var points=timeline.Points;
            for(int i=0;i<points.Count;i++)
            {
                double a=Math.Max(start,points[i].Time);
                double b=i+1<points.Count?Math.Min(end,points[i+1].Time):end;
                if(b<=a)continue;
                double za=timeline.Delta(a,now)*unitsPerMs,zb=timeline.Delta(b,now)*unitsPerMs;
                if(AuthoringSafety.Finite(za)&&AuthoringSafety.Finite(zb)&&Math.Max(za,zb)>=near&&Math.Min(za,zb)<=far)return true;
            }
            return false;
        }
        public static bool Point(ScrollTimeline timeline,double time,double now,double unitsPerMs,double near,double far)
        {double z=timeline.Delta(time,now)*unitsPerMs;return AuthoringSafety.Finite(z)&&z>=near&&z<=far;}
    }
}
