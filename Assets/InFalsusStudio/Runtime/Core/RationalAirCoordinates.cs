using System;
using System.Globalization;

namespace InFalsusStudio.Core
{
    /// <summary>Mouse-authored ratios are stored as integer numerator/divisor pairs,
    /// just like the supplied charts. 1/3 never becomes 0.333 followed by accumulated drift.
    /// Only the edited endpoint is re-encoded; raw text edits are not normalized.</summary>
    public static class RationalAirCoordinates
    {
        public const int MaxSampleSplit=128;
        public const double SampleEdgeTolerance=2d/MaxSampleSplit;
        private const long Limit=100000000;
        private static long Gcd(long a,long b){while(b!=0){long t=a%b;a=b;b=t;}return Math.Abs(a);}
        private static bool Fraction(double value,out long numerator,out long denominator)
        {
            numerator=0;denominator=1;
            if(double.IsNaN(value)||double.IsInfinity(value)||Math.Abs(value)>1000)return false;
            long sign=value<0?-1:1;double target=Math.Abs(value),x=target;
            long h0=0,h1=1,k0=1,k1=0;
            for(int i=0;i<40;i++)
            {
                double whole=Math.Floor(x);if(whole>Limit)return false;long a=(long)whole;
                if(a>0&&(h1>(Limit-h0)/a||k1>(Limit-k0)/a))return false;
                long h=a*h1+h0,k=a*k1+k0;if(k<=0||k>Limit)return false;
                if(Math.Abs(target-h/(double)k)<=1e-11){numerator=sign*h;denominator=k;return true;}
                h0=h1;h1=h;k0=k1;k1=k;double rem=x-whole;if(rem<=1e-15)return false;x=1/rem;
            }
            return false;
        }
        public static bool Encode(double center,double width,double preferredSplit,out double x,out double split,out double w)
        {
            x=center*preferredSplit;w=width*preferredSplit;split=preferredSplit;
            if(preferredSplit>0&&preferredSplit<=Limit&&preferredSplit==Math.Truncate(preferredSplit)&&
               Math.Abs(x-Math.Round(x))<1e-10&&Math.Abs(w-Math.Round(w))<1e-10)
            {x=Math.Round(x);w=Math.Round(w);return true;}
            if(!AuthoringSafety.Finite(center)||!AuthoringSafety.Finite(width)||Math.Abs(center)>1000||Math.Abs(width)>1000)return false;
            long cn,cd,wn,wd;
            if(Fraction(center,out cn,out cd)&&Fraction(width,out wn,out wd))
            {
                long a=cd/Gcd(cd,wd);
                if(a<=Limit/wd)
                {long common=a*wd;x=cn*(common/cd);w=wn*(common/wd);split=common;return true;}
            }
            // Curved samples may be irrational. Quantize ONCE on a bounded integer
            // lattice, then reduce all three parts together; never emit decimal parts.
            double left=Math.Round((center-width*.5)*Limit,MidpointRounding.AwayFromZero);
            double right=Math.Round((center+width*.5)*Limit,MidpointRounding.AwayFromZero);
            x=left+right;w=2*(right-left);split=2*Limit;
            if(Math.Abs(x)>1000000000||Math.Abs(w)>1000000000)return false;
            long divisor=Gcd(Gcd(Math.Abs((long)x),Math.Abs((long)w)),2*Limit);
            x/=divisor;w/=divisor;split/=divisor;return true;
        }
        public static string[] Tokens(double x,double split,double width)
        {
            var parts=new[]{x,split,width};var tokens=new string[3];
            for(int i=0;i<parts.Length;i++)
            {
                double v=parts[i];
                if(!AuthoringSafety.Finite(v)||v!=Math.Truncate(v)||v<int.MinValue||v>int.MaxValue)
                    throw new ArgumentException("Air X / split / width must be integer parameters.");
                tokens[i]=((int)v).ToString(CultureInfo.InvariantCulture);
            }
            if(split<=0)throw new ArgumentException("Air split must be a positive integer.");
            return tokens;
        }
        public static string[] Sample(SkyRange range,double preferredSplit)
        {
            double x,split,w;
            if(!EncodeLimited(range.Center,Math.Max(0,range.Width),preferredSplit,out x,out split,out w))
                throw new ArgumentException("Air sample cannot be fitted within 128 coordinate parts.");
            return Tokens(x,split,w);
        }
        // Bounded precision for sampled curves. Fit BOTH edges with integer position and
        // width on one divisor, preserving valid reach bounds and exact 0/1 anchors.
        // A shared sample produces the same join tokens for both neighboring pieces.
        public static bool EncodeLimited(double center,double width,double preferredSplit,out double x,out double split,out double w)
        {
            x=0;split=1;w=0;
            if(!AuthoringSafety.Finite(center)||!AuthoringSafety.Finite(width)||Math.Abs(center)>1000||Math.Abs(width)>1000)return false;
            double sign=width<0?-1:1,targetWidth=Math.Abs(width);
            var target=new SkyRange(center-targetWidth*.5,center+targetWidth*.5);
            bool inside=AirReachBounds.Inside(target),leftAnchor=inside&&Math.Abs(target.Left)<1e-12,
                rightAnchor=inside&&Math.Abs(target.Right-1)<1e-12;
            if(preferredSplit>=1&&preferredSplit<=MaxSampleSplit&&preferredSplit==Math.Truncate(preferredSplit)&&
                Math.Abs(center*preferredSplit-Math.Round(center*preferredSplit))<1e-10&&
                Math.Abs(width*preferredSplit-Math.Round(width*preferredSplit))<1e-10&&
                (targetWidth==0||Math.Round(targetWidth*preferredSplit)>0))
            {x=Math.Round(center*preferredSplit);split=preferredSplit;w=Math.Round(width*preferredSplit);return true;}
            double best=double.PositiveInfinity,bestSum=double.PositiveInfinity;
            for(int divisor=1;divisor<=MaxSampleSplit;divisor++)
            {
                int lowX=(int)Math.Floor(center*divisor)-1,highX=(int)Math.Ceiling(center*divisor)+1;
                int lowW=targetWidth==0?0:Math.Max(1,(int)Math.Floor(targetWidth*divisor)-1),
                    highW=targetWidth==0?0:Math.Max(1,(int)Math.Ceiling(targetWidth*divisor)+1);
                for(int numerator=lowX;numerator<=highX;numerator++)for(int wide=lowW;wide<=highW;wide++)
                {
                    var candidate=new SkyRange((numerator-wide*.5)/divisor,(numerator+wide*.5)/divisor);
                    if(inside&&!AirReachBounds.Inside(candidate)||leftAnchor&&Math.Abs(candidate.Left)>1e-12||
                        rightAnchor&&Math.Abs(candidate.Right-1)>1e-12)continue;
                    double left=Math.Abs(candidate.Left-target.Left),right=Math.Abs(candidate.Right-target.Right),
                        error=Math.Max(left,right),sum=left+right;
                    if(error<best-1e-12||Math.Abs(error-best)<=1e-12&&sum<bestSum-1e-12)
                    {best=error;bestSum=sum;x=numerator;split=divisor;w=sign*wide;}
                }
                if(best<=1e-12)return true; // smallest exact divisor, unless source already fit
            }
            return !double.IsInfinity(best);
        }
        public static void WriteEndpoint(SpcDocument doc,SpcEvent e,bool tail,double center,double width)
        {
            int ci=e.Kind==EventKind.SkyArea&&tail?4:1;
            double x,split,w;
            double oldSplit=e.Get(ci+1);
            // Keep the divisor only when BOTH position and width are integers.
            // 104.5 / 200 becomes 209 / 400, with width rescaled too, preserving edges.
            if(!Encode(center,width,oldSplit,out x,out split,out w))
                throw new ArgumentException("坐标超出整数参数安全范围，操作未提交。 Air coordinate exceeds the supported integer range.");
            var tokens=Tokens(x,split,w);
            for(int i=0;i<3;i++)if(doc.ArgumentTokens(e.SourceId)[ci+i].Trim()!=tokens[i])
                doc.SetToken(e.SourceId,ci+i,tokens[i]);
        }
    }
}
