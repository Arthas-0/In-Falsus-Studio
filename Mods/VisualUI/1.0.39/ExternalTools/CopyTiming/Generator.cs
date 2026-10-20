using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Web.Script.Serialization;
namespace InfStudio.External {
 public sealed class Row {public int id;public string source;public double time;}
 public sealed class Data {public string action,scope;public double start,end,target;public Row[] rows;}
 public sealed class Request {public int version;public string tool;public Data timingEdit;}
 public static class TimingEdit {
  public static void Run(string requestPath,string outputPath){var json=new JavaScriptSerializer{MaxJsonLength=8000000};var r=json.Deserialize<Request>(File.ReadAllText(requestPath));var d=r.timingEdit;if(r.version!=1||r.tool!="CopyTiming"||d==null||d.action!="copy"||d.rows==null||d.rows.Length==0||d.rows.Length>32000)throw new ArgumentException("Invalid timing edit request.");var lines=d.action=="delete"?new string[0]:d.rows.Select(row=>Regex.Replace(row.source,@"^(\s*(?:track|bpm)\s*\(\s*)[^,]+",m=>m.Groups[1].Value+(row.time+d.target-d.start).ToString("0.##########",CultureInfo.InvariantCulture))).ToArray();File.WriteAllLines(outputPath,lines,new UTF8Encoding(false));File.WriteAllText(outputPath+".info.json",json.Serialize(new{segments=0,timings=d.rows.Length,message=""}),new UTF8Encoding(false));}
 }
}
