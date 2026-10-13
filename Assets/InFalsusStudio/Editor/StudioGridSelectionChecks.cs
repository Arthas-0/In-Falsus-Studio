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
    public static class StudioGridSelectionChecks
    {
        private static readonly Color Purple=new Color(.80f,.50f,1,1);
        private static readonly Color Blue=new Color(.16f,.55f,1,1);
        private static readonly Color Green=new Color(.25f,1,.43f,.93f);
        private static readonly Color Red=new Color(1,.38f,.45f,.93f);
        private static readonly Color MeasureGray=new Color(.55f,.55f,.55f,.65f);
        private static bool SameRgb(Color a,Color b)
        {return Mathf.Abs(a.r-b.r)<.00001f&&Mathf.Abs(a.g-b.g)<.00001f&&Mathf.Abs(a.b-b.b)<.00001f;}
        private static void CheckGeneratedGuides(Action<bool,string> check)
        {
            var parent=new GameObject("Temporary grid selection regression");
            var profile=ScriptableObject.CreateInstance<StudioProfile>();
            StudioMaterials materials=null;NoteRenderer renderer=null;
            try
            {
                var cameraObject=new GameObject("Grid check camera");cameraObject.transform.SetParent(parent.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
                StageSpace.ApplyCamera(camera,profile);camera.rect=new Rect(0,0,1,1);
                camera.pixelRect=new Rect(0,0,1920,1080);camera.aspect=16f/9;
                materials=new StudioMaterials(profile);renderer=new NoteRenderer(parent.transform,profile,materials,camera);
                var doc=SpcDocument.Parse("chart(120,4)\n");var events=doc.ReadEvents();
                var scroll=new ScrollTimeline(events);var beats=new BeatTimeline(events);
                var gridMesh=parent.GetComponentsInChildren<MeshFilter>().Single(f=>f.gameObject.name=="Beat grid and preview cursor").sharedMesh;
                renderer.Draw(events,scroll,beats,0,false,false,true,-1,.5f,null,null,4,null,true,false);
                var colors=gridMesh.colors;
                check(colors.Length>0,"actual generated grid mesh is nonempty");
                foreach(var color in new[]{Purple,Blue,Green,Red})
                    check(colors.Any(c=>SameRgb(c,color)),"actual 1/4 guide mesh contains "+color);
                check(colors.All(c=>new[]{Purple,Blue,Green,Red}.Any(expected=>SameRgb(c,expected))),"1/4 guide mesh has no alternate beat or fallback colors");
                renderer.Draw(events,scroll,beats,0,false,false,true,-1,.5f,null,null,8,null,true,false);
                check(gridMesh.colors.Any(c=>SameRgb(c,Color.white)),"actual finer non-binary musical positions use white");
                renderer.Draw(events,scroll,beats,0,false,false,false,-1,.5f,null,null,4,null,true,false);
                check(gridMesh.vertexCount>0&&gridMesh.colors.All(c=>SameRgb(c,MeasureGray)),"persistent measure guides are neutral gray when subdivisions are hidden");
                var stopped=SpcDocument.Parse("chart(120,4)\ntrack(0,0)\n").ReadEvents();
                renderer.Draw(stopped,new ScrollTimeline(stopped),new BeatTimeline(stopped),0,false,false,true,-1,.5f,null,null,8,null,true,false);
                check(renderer.DrawnGridLines==1&&gridMesh.colors.All(c=>SameRgb(c,Purple)),"overlapping stopped guides draw one highest-priority purple line");
            }
            finally
            {
                if(renderer!=null)renderer.Dispose();if(materials!=null)materials.Dispose();
                UnityEngine.Object.DestroyImmediate(parent);UnityEngine.Object.DestroyImmediate(profile);
            }
        }
        internal static string RunReport()
        {
            int count=0;var report=new StringBuilder();
            Action<bool,string> check=(ok,label)=>{count++;if(!ok)throw new InvalidOperationException("FAILED: "+label);report.AppendLine("PASS "+label);};
            GridSelectionSelfTests.Run(check);CheckGeneratedGuides(check);
            report.AppendLine("TOTAL: "+count+" actual C#/Unity mesh checks passed.");return report.ToString();
        }
        [MenuItem("Tools/InFalsus Studio/Run Grid Color and Range Selection Checks")]
        public static void Run(){Debug.Log(RunReport());}
        // Uses the existing repaired Build08 and release checks; never overwrites an old build.
        public static void VerifyAndBuildBatch()
        {
            try
            {
                string output=Environment.GetEnvironmentVariable("INFALSUS_BUILD_OUTPUT");
                if(string.IsNullOrWhiteSpace(output))throw new InvalidOperationException("Set INFALSUS_BUILD_OUTPUT to a new output directory.");
                string checks=RunReport();StudioRelease100.RunChecks();
                string version=PlayerSettings.bundleVersion,product=PlayerSettings.productName;
                try
                {
                    StudioBranding101.EnsureConfigured();PlayerSettings.bundleVersion=ReleaseIdentity.Version;PlayerSettings.productName=ReleaseIdentity.ProductName;
                    string exe=InFalsusStudio.Editor.StudioBuild08.BuildTo(output);
                    if(!File.Exists(exe))throw new IOException("Build returned without an executable.");
                    File.WriteAllText(Path.Combine(output,"GridSelectionChecks.txt"),checks);
                    File.WriteAllText(Path.Combine(output,"READ_ME.txt"),ReleaseIdentity.SettingsTitle+"\r\nGrid color and complete range selection fix.\r\nKeep the whole Data/DLL directory with this exe. Save/Ctrl+S writes; exit never auto-saves.\r\n");
                    Debug.Log(checks+"\nFixed Windows build: "+exe);
                }
                finally{PlayerSettings.bundleVersion=version;PlayerSettings.productName=product;}
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
#endif
