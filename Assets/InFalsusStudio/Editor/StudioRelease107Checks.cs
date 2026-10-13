#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio.EditorTools
{
    public static class StudioRelease107Checks
    {
        internal static string RunReferenceReport(string songRoot)
        {
            var report=new StringBuilder();int count=0,total=0;
            Action<bool,string> check=(ok,label)=>{count++;report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);};
            foreach(var file in Directory.GetFiles(Path.Combine(songRoot,"Charts"),"*.json").OrderBy(f=>f))
            {
                var d=IcpJsonImport.Load(file).Document;var air=d.ReadEvents().Where(AirReachBounds.IsAir).ToArray();total+=air.Length;
                check(air.Length>0&&air.All(n=>Release107SelfTests.IntegerParts(d,n)),Path.GetFileName(file)+": all original air coordinate parts are integers ("+air.Length+" notes)");
                byte[] original=d.ToBytes();var result=AirCoordinateRepair.Round(d);
                check(result.Notes==0&&d.ToBytes().SequenceEqual(original),Path.GetFileName(file)+": valid original chart is byte-identical after repair");
                foreach(var n in air.Where(n=>n.Kind==EventKind.SkyArea&&n.DurationMs>=4&&ChartMath.InvalidReason(n)==null&&AirReachBounds.Violation(n)==null)
                    .GroupBy(n=>n.Get(7)+"/"+n.Get(8)).Select(g=>g.First()))
                {
                    var split=d.Clone();var ids=SkySplitOperations.Equal(split,new[]{n.SourceId},4);
                    check(ids.Select(id=>split.ReadEvents().First(e=>e.SourceId==id)).All(e=>Release107SelfTests.IntegerParts(split,e)),
                        Path.GetFileName(file)+": real SkyArea "+n.SourceId+" ease "+n.Get(7)+"/"+n.Get(8)+" split produces integer parts");
                }
            }
            report.AppendLine("TOTAL: "+count+" read-only reference checks, "+total+" original SkyArea/Flick notes. No chart saved.");
            return report.ToString();
        }
        public static void VerifyAndBuildBatch()
        {
            try
            {
                string output=Environment.GetEnvironmentVariable("INFALSUS_BUILD_OUTPUT");
                if(string.IsNullOrWhiteSpace(output)||Directory.Exists(output))throw new InvalidOperationException("Specify a NEW output folder with INFALSUS_BUILD_OUTPUT.");
                string grid=StudioGridSelectionChecks.RunReport();StudioRelease100.RunChecks();string meshes=StudioRelease106Checks.RunMeshReport();
                string reference=Environment.GetEnvironmentVariable("INFALSUS_REFERENCE_CHART");
                string referenceReport=string.IsNullOrWhiteSpace(reference)?"Reference chart checks not requested.":RunReferenceReport(reference);
                string oldVersion=PlayerSettings.bundleVersion,oldProduct=PlayerSettings.productName;
                try
                {
                    StudioBranding101.EnsureConfigured();PlayerSettings.bundleVersion=ReleaseIdentity.Version;PlayerSettings.productName=ReleaseIdentity.ProductName;
                    string exe=InFalsusStudio.Editor.StudioBuild08.BuildTo(output);
                    if(!File.Exists(exe))throw new IOException("Build returned without an executable.");
                    File.WriteAllText(Path.Combine(output,"GridSelectionChecks.txt"),grid);
                    File.WriteAllText(Path.Combine(output,"FeatureChecks106.txt"),meshes);
                    File.WriteAllText(Path.Combine(output,"EnigmaCoordinates107.txt"),referenceReport);
                    File.WriteAllText(Path.Combine(output,"READ_ME.txt"),ReleaseIdentity.SettingsTitle+
                        "\r\nKeep the entire Data/DLL directory with the exe.\r\nSave/Ctrl+S writes drafts. Exit never auto-saves.\r\n"+
                        "Tools: split SkyArea/Flick using integer coordinate ratios; repair decimal X/split/width parts in this chart.\r\n"+
                        "Repair is one undo step, preserving time, easing and group IDs. Displayed percentages may be fractional.\r\n");
                    File.WriteAllText(Path.Combine(output,"RELEASE_1.0.7.txt"),string.Join("\r\n\r\n",Array.ConvertAll(ReleaseNotesCatalog.Recent,n=>"v"+n.Version+"\r\n"+n.Chinese+"\r\n\r\n"+n.English)));
                    Debug.Log(grid+"\n"+meshes+"\n"+referenceReport+"\n1.0.7 Windows build: "+exe);
                }
                finally{PlayerSettings.bundleVersion=oldVersion;PlayerSettings.productName=oldProduct;}
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
#endif
