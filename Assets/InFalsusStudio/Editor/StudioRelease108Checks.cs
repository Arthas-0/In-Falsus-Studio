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
    public static class StudioRelease108Checks
    {
        internal static string RunCappedReferences(string songRoot)
        {
            var report=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,label)=>{count++;report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);};
            foreach(var file in Directory.GetFiles(Path.Combine(songRoot,"Charts"),"*.json").OrderBy(f=>f))
            {
                var d=IcpJsonImport.Load(file).Document;byte[] before=d.ToBytes();
                foreach(var n in d.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea&&n.DurationMs>=4&&ChartMath.InvalidReason(n)==null&&AirReachBounds.Violation(n)==null)
                    .GroupBy(n=>n.Get(7)+"/"+n.Get(8)).Select(g=>g.First()))
                {
                    var split=d.Clone();var ids=SkySplitOperations.Equal(split,new[]{n.SourceId},4);
                    check(ids.Select(id=>split.ReadEvents().First(e=>e.SourceId==id)).All(e=>Release108SelfTests.Capped(split,e)),
                        Path.GetFileName(file)+": actual curve "+n.SourceId+" has both coordinate divisors <=128");
                }
                var limited=d.Clone();AirCoordinateRepair.LimitSplits(limited);
                check(limited.ReadEvents().Where(AirReachBounds.IsAir).All(n=>Release108SelfTests.Capped(limited,n))&&d.ToBytes().SequenceEqual(before),
                    Path.GetFileName(file)+": in-memory whole-chart compression respects cap; original unchanged");
            }
            report.AppendLine("TOTAL: "+count+" actual reference cap checks. Original files are read-only.");return report.ToString();
        }
        public static void VerifyAndBuildBatch()
        {
            try
            {
                string output=Environment.GetEnvironmentVariable("INFALSUS_BUILD_OUTPUT");
                if(string.IsNullOrWhiteSpace(output)||Directory.Exists(output))throw new InvalidOperationException("Specify a NEW output folder with INFALSUS_BUILD_OUTPUT.");
                string grid=StudioGridSelectionChecks.RunReport();StudioRelease100.RunChecks();string meshes=StudioRelease106Checks.RunMeshReport();
                string reference=Environment.GetEnvironmentVariable("INFALSUS_REFERENCE_CHART");
                string referenceReport=string.IsNullOrWhiteSpace(reference)?"Reference checks not requested.":
                    StudioRelease107Checks.RunReferenceReport(reference)+"\n"+RunCappedReferences(reference);
                var example=SpcDocument.Parse(Release108SelfTests.UserExample+"\n");AirCoordinateRepair.LimitSplits(example);
                string oldVersion=PlayerSettings.bundleVersion,oldProduct=PlayerSettings.productName;
                try
                {
                    StudioBranding101.EnsureConfigured();PlayerSettings.bundleVersion=ReleaseIdentity.Version;PlayerSettings.productName=ReleaseIdentity.ProductName;
                    string exe=InFalsusStudio.Editor.StudioBuild08.BuildTo(output);
                    if(!File.Exists(exe))throw new IOException("Build returned without an executable.");
                    File.WriteAllText(Path.Combine(output,"GridSelectionChecks.txt"),grid);
                    File.WriteAllText(Path.Combine(output,"FeatureChecks106.txt"),meshes);
                    File.WriteAllText(Path.Combine(output,"EnigmaCoordinates108.txt"),referenceReport);
                    File.WriteAllText(Path.Combine(output,"Cap128Example.txt"),example.ToText());
                    File.WriteAllText(Path.Combine(output,"READ_ME.txt"),ReleaseIdentity.SettingsTitle+
                        "\r\nKeep the entire Data/DLL directory. Save/Ctrl+S writes drafts; exit never auto-saves.\r\n"+
                        "SkyArea equal/grid splits and Flick output use at most 128 coordinate parts.\r\n"+
                        "Tools > Coordinate repair > Fit large divisors within 128 handles previous oversized output in this chart.\r\n"+
                        "Integer coordinate fitting may slightly change positions/widths. Time, easing and groups stay unchanged. One undo step.\r\n");
                    File.WriteAllText(Path.Combine(output,"RELEASE_1.0.8.txt"),string.Join("\r\n\r\n",Array.ConvertAll(ReleaseNotesCatalog.Recent,n=>"v"+n.Version+"\r\n"+n.Chinese+"\r\n\r\n"+n.English)));
                    Debug.Log(grid+"\n"+meshes+"\n"+referenceReport+"\nExample: "+example.ToText()+"\n1.0.8 Windows build: "+exe);
                }
                finally{PlayerSettings.bundleVersion=oldVersion;PlayerSettings.productName=oldProduct;}
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
#endif
