using System;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    // The native automatic SkyArea form has exactly ten arguments. -1 is an older
    // editor convention, not the native spelling of an omitted optional group.
    public static class SkyGroupSyntax
    {
        private static void CallSpan(string raw,out int open,out int close)
        {
            open=raw.IndexOf('(');close=open<0?-1:raw.IndexOf(')',open+1);
            if(open<0||close<open)throw new ArgumentException("Expected a SkyArea statement.");
        }
        public static string ExtraProvenance(string raw)
        {
            int open,close;CallSpan(raw,out open,out close);
            if(raw.Substring(open+1,close-open-1).Split(',').Length<=11)return "";
            // Unknown fields after group cannot remain positional without becoming
            // the new group argument. Keep the complete old source in inert data.
            return " // ungrouped_skyarea_base64="+Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }
        public static string WithoutGroup(string raw)
        {
            int open,close;CallSpan(raw,out open,out close);
            var tokens=raw.Substring(open+1,close-open-1).Split(',');
            if(tokens.Length<=10)return raw;
            return raw.Substring(0,open+1)+string.Join(",",tokens.Take(10))+")"+ExtraProvenance(raw)+raw.Substring(close+1);
        }
        public static void Clear(SpcDocument document,int id)
        {
            string old=document.SourceLine(id);int open=old.IndexOf('(');
            if(!document.Contains(id)||open<0||!old.Substring(0,open).Trim().Equals("skyarea",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Select a SkyArea.");
            string next=WithoutGroup(old);if(old!=next)document.ReplaceSourceLine(id,next);
        }
    }
}
