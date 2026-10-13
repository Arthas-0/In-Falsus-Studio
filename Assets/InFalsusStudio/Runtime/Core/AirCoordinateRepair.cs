using System;
using System.Collections.Generic;
using System.Globalization;

namespace InFalsusStudio.Core
{
    // Explicit repair of stored SPC PARTS, not normalized percentages or chart times.
    // Plan the whole chart before writing; the caller supplies one undo transaction.
    public static class AirCoordinateRepair
    {
        public struct Result { public int Notes,Fields; }
        private sealed class Edit { public int Id,Index;public string Token; }
        public static Result Round(SpcDocument document)
        {
            var edits=new List<Edit>();var result=new Result();
            foreach(var n in document.ReadEvents())
            {
                if(n.Kind!=EventKind.SkyArea&&n.Kind!=EventKind.Flick)continue;
                var tokens=document.ArgumentTokens(n.SourceId);bool changed=false;
                int last=n.Kind==EventKind.SkyArea?6:3;
                for(int i=1;i<=last;i++)
                {
                    double value=Math.Round(n.Get(i),MidpointRounding.AwayFromZero);
                    if(!AuthoringSafety.Finite(value)||value<int.MinValue||value>int.MaxValue)
                        throw new ArgumentException("Line "+(document.SourceIndex(n.SourceId)+1)+": coordinate is outside the integer range; repair cancelled.");
                    if((i==2||i==5)&&value<=0)
                        throw new ArgumentException("Line "+(document.SourceIndex(n.SourceId)+1)+": split rounds below 1; fix the divisor first. Repair cancelled.");
                    string token=((int)value).ToString(CultureInfo.InvariantCulture);
                    if(tokens[i].Trim()==token)continue;
                    edits.Add(new Edit{Id=n.SourceId,Index=i,Token=token});changed=true;result.Fields++;
                }
                if(changed)result.Notes++;
            }
            foreach(var edit in edits)document.SetToken(edit.Id,edit.Index,edit.Token);
            return result;
        }
        public static Result LimitSplits(SpcDocument document)
        {
            var edits=new List<Edit>();var result=new Result();
            foreach(var n in document.ReadEvents())
            {
                if(n.Kind!=EventKind.SkyArea&&n.Kind!=EventKind.Flick)continue;
                var tokens=document.ArgumentTokens(n.SourceId);bool changed=false;
                for(int c=1;c<=(n.Kind==EventKind.SkyArea?4:1);c+=3)
                {
                    double originalSplit=n.Get(c+1);
                    if(originalSplit<=RationalAirCoordinates.MaxSampleSplit)continue;
                    double x,split,width;
                    if(!RationalAirCoordinates.EncodeLimited(n.Get(c)/originalSplit,n.Get(c+2)/originalSplit,originalSplit,out x,out split,out width))
                        throw new ArgumentException("Line "+(document.SourceIndex(n.SourceId)+1)+": cannot fit coordinate parts within 128; repair cancelled.");
                    var encoded=RationalAirCoordinates.Tokens(x,split,width);
                    for(int i=0;i<3;i++)
                    {
                        if(tokens[c+i].Trim()==encoded[i])continue;
                        edits.Add(new Edit{Id=n.SourceId,Index=c+i,Token=encoded[i]});changed=true;result.Fields++;
                    }
                }
                if(changed)result.Notes++;
            }
            foreach(var edit in edits)document.SetToken(edit.Id,edit.Index,edit.Token);
            return result;
        }
    }
}
