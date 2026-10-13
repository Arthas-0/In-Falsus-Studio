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
    public static class StudioRelease109Checks
    {
        internal static string ReferenceDetails(string root)
        {
            var report=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,label)=>{count++;report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);};
            var files=Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(f=>f,SongFolderProject.FileHash);
            var source=ProjectJson.ReadFile(Path.Combine(root,"song.json"));var info=ProjectJson.Object(ProjectJson.Get(source,"song_info"));
            var project=SongFolderProject.Prepare(root,true);
            check(project.PreviewStartSeconds==ProjectJson.Number(info,"PreviewStartSeconds"),"Enigma native PreviewStartSeconds = "+project.PreviewStartSeconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            check(project.PreviewEndSeconds==ProjectJson.Number(info,"PreviewEndSeconds"),"Enigma native PreviewEndSeconds = "+project.PreviewEndSeconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            foreach(var chart in ProjectJson.Array(ProjectJson.Get(source,"charts")))
            {
                var row=ProjectJson.Object(chart);int mask=ProjectJson.Integer(row,"Difficulty");int index=mask==1?0:mask==2?1:mask==4?2:3;
                check(project.Difficulties[index].JacketDesigner==ProjectJson.Text(row,"DisplayJacketDesigner"),"Enigma "+SongFolderProject.Names[index]+" native jacket credit = "+project.Difficulties[index].JacketDesigner);
            }
            check(project.JacketCandidates.Length==2,"Enigma discovers both supplied PNG images");
            string signature=project.JacketInventorySha256;SongMediaDiscovery.ChooseJacket(project,Path.Combine(root,"jacket_large.png"));
            var reopen=SongFolderProject.Prepare(root,true);
            check(SongMediaDiscovery.UseRememberedJacket(reopen,"jacket_large.png",signature)&&!reopen.JacketChoiceRequired,"Enigma identical image inventory restores the local selection in RAM");
            check(project.Difficulties.All(d=>d.Document!=null)&&project.Existing,"Enigma keeps all four existing workspace charts");
            check(files.All(p=>File.Exists(p.Key)&&SongFolderProject.FileHash(p.Key)==p.Value)&&Directory.GetFiles(root,"*",SearchOption.AllDirectories).Length==files.Count,"Enigma all files unchanged after metadata/media read checks");
            report.AppendLine("TOTAL: "+count+" actual Enigma metadata/media checks; no source writes.");return report.ToString();
        }
        public static void VerifyAndBuildBatch()
        {
            try
            {
                string output=Environment.GetEnvironmentVariable("INFALSUS_BUILD_OUTPUT");
                if(string.IsNullOrWhiteSpace(output)||Directory.Exists(output))throw new InvalidOperationException("Specify a NEW output folder with INFALSUS_BUILD_OUTPUT.");
                string grid=StudioGridSelectionChecks.RunReport();StudioRelease100.RunChecks();string feature=Release109SelfTests.LastReport,groups=Release109SkyGroupSelfTests.LastReport;
                string meshes=StudioRelease106Checks.RunMeshReport(),reference=Environment.GetEnvironmentVariable("INFALSUS_REFERENCE_CHART");
                string references=string.IsNullOrWhiteSpace(reference)?"Reference checks not requested.":
                    StudioRelease107Checks.RunReferenceReport(reference)+"\n"+StudioRelease108Checks.RunCappedReferences(reference)+"\n"+ReferenceDetails(reference);
                string oldVersion=PlayerSettings.bundleVersion,oldProduct=PlayerSettings.productName;
                try
                {
                    StudioBranding101.EnsureConfigured();PlayerSettings.bundleVersion=ReleaseIdentity.Version;PlayerSettings.productName=ReleaseIdentity.ProductName;
                    string exe=InFalsusStudio.Editor.StudioBuild08.BuildTo(output);
                    if(!File.Exists(exe))throw new IOException("Build returned without an executable.");
                    File.WriteAllText(Path.Combine(output,"GridSelectionChecks.txt"),grid);
                    File.WriteAllText(Path.Combine(output,"FeatureChecks106.txt"),meshes);
                    File.WriteAllText(Path.Combine(output,"SongMetadataMedia109Checks.txt"),feature);
                    File.WriteAllText(Path.Combine(output,"SkyGroup109Checks.txt"),groups);
                    File.WriteAllText(Path.Combine(output,"EnigmaReference109Checks.txt"),references);
                    File.WriteAllText(Path.Combine(output,"READ_ME.txt"),ReleaseIdentity.SettingsTitle+
                        "\r\nKeep the entire Data/DLL directory. Save/Ctrl+S writes drafts; exit never auto-saves.\r\n"+
                        "Song information includes native preview start/end seconds and per-difficulty Jacket Designer credits.\r\n"+
                        "Every open scans music and PNG/JPG/JPEG images, including ordinary subfolders. Hidden workspace directories are excluded.\r\n"+
                        "Missing MP3/OGG is rediscovered; multiple candidates allow explicit selection. Existing chart workspaces stay intact.\r\n"+
                        "One jacket is automatic. Multiple images require a choice when the image inventory changes; successful choices are remembered locally.\r\n"+
                        "Metadata and rediscovered paths remain in RAM until explicit Save. Same-path image replacements reload on open.\r\n");
                    File.AppendAllText(Path.Combine(output,"READ_ME.txt"),"SkyArea authoring omits the optional group ID. Tools removes all group assignments with one undo step.\r\n");
                    File.WriteAllText(Path.Combine(output,"RELEASE_1.0.9.txt"),string.Join("\r\n\r\n",Array.ConvertAll(ReleaseNotesCatalog.Recent,n=>"v"+n.Version+"\r\n"+n.Chinese+"\r\n\r\n"+n.English)));
                    Debug.Log(grid+"\n"+meshes+"\n"+feature+"\n"+groups+"\n"+references+"\n1.0.9 Windows build: "+exe);
                }
                finally{PlayerSettings.bundleVersion=oldVersion;PlayerSettings.productName=oldProduct;}
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
#endif
