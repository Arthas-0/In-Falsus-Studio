using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    // Native seconds and per-difficulty jacket credits. Only explicit Save calls Patch.
    public static class NativeSongDetails
    {
        private const string Credit="DisplayJacketDesigner";
        private static List<string> Rows(LosslessJsonPatch patch,int index)
        {
            var result=new List<string>();
            foreach(string list in new[]{"/charts","/song_info/ChartInfos"})
            {
                if(!patch.Contains(list)||patch.Get(list)==null)continue;
                if(!(patch.Get(list) is List<object>))throw new InvalidDataException("Expected a native chart array: "+list);
                int hits=0;
                for(int i=0;patch.Contains(list+"/"+i);i++)
                {
                    string row=list+"/"+i;object mask=patch.Get(row+"/Difficulty");
                    if(!(mask is double)||(double)mask!=(1<<index))continue;
                    if(++hits>1)throw new InvalidDataException("Duplicate native difficulty in "+list);
                    result.Add(row);
                }
            }
            return result;
        }
        public static double Seconds(double value)
        {
            if(double.IsNaN(value)||double.IsInfinity(value)||value<0)throw new ArgumentException("Preview time must be a finite, non-negative number of seconds.");
            return value;
        }
        public static void ValidatePreview(double start,double end)
        {
            Seconds(start);Seconds(end);
            if((start!=0||end!=0)&&end<=start)throw new ArgumentException("Preview end must be after its start; 0 / 0 leaves preview unset.");
        }
        public static string ValidateCredit(string value)
        {
            value=(value??"").Trim();
            if(value.Length>128||value.Any(char.IsControl))throw new ArgumentException("Jacket Designer: up to 128 characters, one line only.");
            return value;
        }
        public static void StagePreviewTime(SongFolderProject project,bool start,double value)
        {
            if(project==null)throw new ArgumentException("Open a song project first.");
            Seconds(value);if(start)project.PreviewStartSeconds=value;else project.PreviewEndSeconds=value;
            // Fields may be entered in either order. Save validates the complete pair.
        }
        public static void StageJacketDesigner(SongFolderProject project,int index,string value)
        {
            if(project==null||index<0||index>3)throw new ArgumentException("Open a song and select a difficulty first.");
            project.Difficulties[index].JacketDesigner=ValidateCredit(value);
        }
        public static void ReadInto(SongFolderProject project,byte[] source)
        {
            if(source==null)return;
            var patch=new LosslessJsonPatch(source);
            foreach(bool start in new[]{true,false})
            {
                string path="/song_info/"+(start?"PreviewStartSeconds":"PreviewEndSeconds");
                if(!patch.Contains(path)||patch.Get(path)==null)continue;
                object value=patch.Get(path);
                if(!(value is double))throw new InvalidDataException("Preview time is not a number: "+path);
                if(start)project.PreviewStartSeconds=(double)value;else project.PreviewEndSeconds=(double)value;
            }
            for(int i=0;i<4;i++)foreach(string row in Rows(patch,i))
            {
                if(!patch.Contains(row+"/"+Credit))continue;
                object value=patch.Get(row+"/"+Credit);
                if(value!=null&&!(value is string))throw new InvalidDataException("Jacket credit is not text: "+row);
                project.Difficulties[i].JacketDesigner=value as string??"";break;
            }
        }
        public static byte[] PatchPreview(byte[] source,double start,double end)
        {
            ValidatePreview(start,end);
            var patch=new LosslessJsonPatch(source);
            if(!patch.Contains("/song_info"))return InsertMember(source,"","song_info",new Dictionary<string,object>{{"PreviewStartSeconds",start},{"PreviewEndSeconds",end}});
            if(!(patch.Get("/song_info") is Dictionary<string,object>))throw new InvalidDataException("song_info must be an object to save preview times.");
            byte[] result=SetOrInsert(source,"/song_info","PreviewStartSeconds",start);
            return SetOrInsert(result,"/song_info","PreviewEndSeconds",end);
        }
        public static byte[] PatchJacketDesigner(byte[] source,int index,string value)
        {
            if(index<0||index>3)throw new ArgumentOutOfRangeException("index");
            value=ValidateCredit(value);var patch=new LosslessJsonPatch(source);var rows=Rows(patch,index);
            if(rows.Count==0)throw new InvalidDataException("Jacket Designer cannot be saved: no native entry for difficulty "+index+". No files changed.");
            byte[] result=source;
            foreach(string row in rows)result=SetOrInsert(result,row,Credit,value);
            return result;
        }
        private static byte[] SetOrInsert(byte[] source,string parent,string key,object value)
        {
            var patch=new LosslessJsonPatch(source);string path=parent+"/"+key;
            if(!patch.Contains(path))return InsertMember(source,parent,key,value);
            object old=patch.Get(path);
            if(old!=null&&(value is string?!(old is string):!(old is double)))throw new InvalidDataException("Unexpected native metadata type: "+path);
            if(old is double&&value is double&&(double)old==(double)value)return source;
            patch.Set(path,value);return patch.ToBytes();
        }
        // Insert only absent known members. Existing objects and 64-bit number lexemes
        // are never reserialized. The protected LosslessJsonPatch remains unchanged.
        private static byte[] InsertMember(byte[] source,string parent,string key,object value)
        {
            string text=new UTF8Encoding(false,true).GetString(source);var ends=new ObjectEnds(text);
            int at;if(!ends.Ends.TryGetValue(parent,out at))throw new InvalidDataException("Native metadata parent must be an object: "+parent);
            var row=ProjectJson.Object(new LosslessJsonPatch(source).Get(parent));
            if(row.ContainsKey(key))throw new InvalidDataException("Native member already exists: "+key);
            string addition=(row.Count==0?"":",")+ProjectJson.Write(key).TrimEnd('\r','\n')+":"+ProjectJson.Write(value).TrimEnd('\r','\n');
            string result=text.Insert(at,addition);ProjectJson.Parse(result);return new UTF8Encoding(false,true).GetBytes(result);
        }
        private sealed class ObjectEnds
        {
            private readonly string text;private int p;
            public readonly Dictionary<string,int> Ends=new Dictionary<string,int>(StringComparer.Ordinal);
            public ObjectEnds(string value){text=value;ProjectJson.Parse(text);p=text.Length>0&&text[0]=='\uFEFF'?1:0;Scan("",0);}
            private void Space(){while(p<text.Length&&char.IsWhiteSpace(text[p]))p++;}
            private string String()
            {
                int at=p++;
                while(p<text.Length){char c=text[p++];if(c=='\\'){p++;continue;}if(c=='"')return (string)ProjectJson.Parse(text.Substring(at,p-at));}
                throw new InvalidDataException("Unterminated JSON string.");
            }
            private static string Child(string parent,string key){return parent+"/"+key.Replace("~","~0").Replace("/","~1");}
            private void Scan(string path,int depth)
            {
                if(depth>64)throw new InvalidDataException("Metadata too deep.");Space();char c=text[p];
                if(c=='{')
                {
                    p++;Space();if(text[p]!='}')while(true){string key=String();Space();p++;Scan(Child(path,key),depth+1);Space();if(text[p]!=',')break;p++;Space();}
                    Ends.Add(path,p);p++;
                }
                else if(c=='[')
                {
                    p++;Space();int i=0;if(text[p]!=']')while(true){Scan(Child(path,(i++).ToString(CultureInfo.InvariantCulture)),depth+1);Space();if(text[p]!=',')break;p++;Space();}p++;
                }
                else if(c=='"')String();
                else while(p<text.Length&&text[p]!=','&&text[p]!=']'&&text[p]!='}'&&!char.IsWhiteSpace(text[p]))p++;
            }
        }
    }
}
