#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio.EditorTools
{
    /// <summary>Wraps the user's repaired existing build entry. Does not overwrite it or
    /// bypass its checks. The settings changes are scoped to this one real Unity build.</summary>
    public static class StudioRelease100
    {
        [MenuItem("Tools/InFalsus Studio/Run " + ReleaseIdentity.DisplayVersion + " Release Checks")]
        public static void RunChecks()
        {
            int count=0;Action<bool,string> check=(ok,name)=>{count++;if(!ok)throw new InvalidOperationException("FAILED: "+name);};
            Release010SelfTests.Run(check);Release100SelfTests.Run(check);Release101SelfTests.Run(check);Visual102SelfTests.Run(check);Release103SelfTests.Run(check);Release104SelfTests.Run(check);Release105SelfTests.Run(check);Release106SelfTests.Run(check);Release107SelfTests.Run(check);Release108SelfTests.Run(check);Release109SelfTests.Run(check);Release109SkyGroupSelfTests.Run(check);Release110SelfTests.Run(check);StudioSkin104Checks.ValidateAssets(check);
            Debug.Log("InFalsus Studio "+ReleaseIdentity.DisplayVersion+": actual C# release checks passed: "+count+". Native UI, rendering and audio-device tests remain separate.");
        }
        [MenuItem("Tools/InFalsus Studio/Inspect " + ReleaseIdentity.DisplayVersion + " Runtime")]
        public static void Inspect()
        {
            var app=UnityEngine.Object.FindFirstObjectByType<StudioBootstrap>();
            if(!EditorApplication.isPlaying||app==null){Debug.LogWarning("Enter Play first.");return;}
            Debug.Log(app.RuntimeReport100());
        }
        [MenuItem("Tools/InFalsus Studio/Use Selected Texture as Application Icon")]
        public static void SetSelectedIcon()
        {
            var texture=Selection.activeObject as Texture2D;
            if(texture==null&&Selection.activeObject is Sprite)texture=((Sprite)Selection.activeObject).texture;
            if(texture==null||!AssetDatabase.GetAssetPath(texture).StartsWith("Assets/",StringComparison.Ordinal))
            {EditorUtility.DisplayDialog("Application icon","Import your own square PNG into Assets, select it in the Project window, then run this command.","OK");return;}
            if(texture.width!=texture.height||texture.width<256)
            {EditorUtility.DisplayDialog("Application icon","Use a square image at least 256 x 256 (512 or 1024 recommended).","OK");return;}
            var target=NamedBuildTarget.Standalone;int[] sizes=PlayerSettings.GetIconSizes(target,IconKind.Application);
            if(sizes.Length==0){EditorUtility.DisplayDialog("Application icon","No Standalone icon slots. Set Edit / Project Settings / Player / Icon manually.","OK");return;}
            if(!EditorUtility.DisplayDialog("Set application icon?",AssetDatabase.GetAssetPath(texture)+"\nThis updates your Unity project's Standalone icon settings. Rebuild the exe to embed it.","Set icon","Cancel"))return;
            PlayerSettings.SetIcons(target,Enumerable.Repeat(texture,sizes.Length).ToArray(),IconKind.Application);
            AssetDatabase.SaveAssets();Debug.Log("Standalone icons assigned: "+string.Join(",",sizes.Select(x=>x.ToString()))+". Rebuild to update the executable.");
        }
        [MenuItem("Tools/InFalsus Studio/Build Windows Editor " + ReleaseIdentity.DisplayVersion + "...")]
        public static void Build()
        {
            if(EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)
            {EditorUtility.DisplayDialog("Build " + ReleaseIdentity.DisplayVersion,"Stop Play and wait for compilation/import.","OK");return;}
            string parent=EditorUtility.OpenFolderPanel("New Windows " + ReleaseIdentity.DisplayVersion + " output directory",Path.GetDirectoryName(Application.dataPath),"");
            if(string.IsNullOrEmpty(parent))return;
            string folder=Path.Combine(parent,"InFalsusStudio_Windows_"+ReleaseIdentity.Version+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            string oldVersion=PlayerSettings.bundleVersion,oldProduct=PlayerSettings.productName;
            try
            {
                StudioBranding101.EnsureConfigured();RunChecks();PlayerSettings.bundleVersion=ReleaseIdentity.Version;PlayerSettings.productName=ReleaseIdentity.ProductName;
                string exe=InFalsusStudio.Editor.StudioBuild08.BuildTo(folder);
                if(!File.Exists(exe))throw new IOException("Build returned without an executable.");
                File.WriteAllText(Path.Combine(folder,"RELEASE_"+ReleaseIdentity.DisplayVersion+".txt"),ReleaseIdentity.SettingsTitle+"\r\nUnity "+Application.unityVersion+"\r\n"+
                    "Keep the entire folder with the exe. Save/Ctrl+S writes drafts; exit never auto-saves.\r\n"+
                    "Chart Designer: song.json DisplayChartDesigner (main manifest mirror). Per-chart offsets: .ifstudio/chart-notes.json v2.\r\n"+
                    "Release number is not certification of native dialog, fullscreen or audio-device testing.\r\n");
                File.WriteAllText(Path.Combine(folder,"READ_ME.txt"),File.ReadAllText(Path.Combine(folder,"RELEASE_"+ReleaseIdentity.DisplayVersion+".txt")));
                EditorUtility.RevealInFinder(exe);EditorUtility.DisplayDialog(ReleaseIdentity.DisplayVersion+" build succeeded",exe+"\nKeep all Data/DLL files. Run the native-dialog and fullscreen acceptance checks in this exe.","OK");
            }
            catch(Exception ex){Debug.LogException(ex);EditorUtility.DisplayDialog(ReleaseIdentity.DisplayVersion+" build failed",ex.Message+"\nSee Console / Editor.log. Existing tests were NOT bypassed.","OK");}
            finally{PlayerSettings.bundleVersion=oldVersion;PlayerSettings.productName=oldProduct;}
        }
    }
}
#endif
