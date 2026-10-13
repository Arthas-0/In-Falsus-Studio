#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio.EditorTools
{
    public static class StudioRelease105Checks
    {
        // UI acceptance is performed in the built Player. Headless checks do not
        // mark this computer's real release announcement as read or change its folder.
        public static void VerifyAndBuildBatch()
        {
            try
            {
                string output=Environment.GetEnvironmentVariable("INFALSUS_BUILD_OUTPUT");
                if(string.IsNullOrWhiteSpace(output)||Directory.Exists(output))throw new InvalidOperationException("Specify a NEW output folder with INFALSUS_BUILD_OUTPUT.");
                string gridChecks=StudioGridSelectionChecks.RunReport();StudioRelease100.RunChecks();
                string oldVersion=PlayerSettings.bundleVersion,oldProduct=PlayerSettings.productName;
                try
                {
                    StudioBranding101.EnsureConfigured();PlayerSettings.bundleVersion=ReleaseIdentity.Version;PlayerSettings.productName=ReleaseIdentity.ProductName;
                    string exe=InFalsusStudio.Editor.StudioBuild08.BuildTo(output);
                    if(!File.Exists(exe))throw new IOException("Build returned without an executable.");
                    File.WriteAllText(Path.Combine(output,"GridSelectionChecks.txt"),gridChecks);
                    File.WriteAllText(Path.Combine(output,"READ_ME.txt"),ReleaseIdentity.SettingsTitle+"\r\nKeep the entire Data/DLL directory with the exe.\r\nSave/Ctrl+S writes chart drafts. Exit never auto-saves.\r\nSong picker remembers the last successful folder. What's new is available in Settings.\r\n");
                    File.WriteAllText(Path.Combine(output,"RELEASE_1.0.5.txt"),string.Join("\r\n\r\n",Array.ConvertAll(ReleaseNotesCatalog.Recent,n=>"v"+n.Version+"\r\n"+n.Chinese+"\r\n\r\n"+n.English)));
                    Debug.Log(gridChecks+"\n1.0.5 Windows build: "+exe);
                }
                finally{PlayerSettings.bundleVersion=oldVersion;PlayerSettings.productName=oldProduct;}
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
#endif
