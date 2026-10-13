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
    public static class StudioRelease106Checks
    {
        internal static string RunMeshReport()
        {
            var report=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,label)=>{count++;report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);};
            var shader=Resources.Load<Shader>("InFalsusStudio/NoteSkin106");
            check(shader!=null&&!ShaderUtil.ShaderHasError(shader),"106 skin resource has no currently reported compiler errors");
            var parent=new GameObject("106 temporary mesh regression");var profile=ScriptableObject.CreateInstance<StudioProfile>();
            StudioMaterials materials=null;NoteRenderer renderer=null;
            try
            {
                var cameraObject=new GameObject("106 regression camera");cameraObject.transform.SetParent(parent.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;StageSpace.ApplyCamera(camera,profile);
                camera.pixelRect=new Rect(0,0,1920,1080);camera.aspect=16f/9;
                materials=new StudioMaterials(profile);renderer=new NoteRenderer(parent.transform,profile,materials,camera);
                check(new[]{materials.HoldBlue,materials.HoldPurple,materials.HoldPink,materials.SkySkin}.All(m=>m.shader==shader),"106 all three Hold lanes and Sky use the new shader");
                foreach(int lane in new[]{0,1,5})
                {
                    var d=SpcDocument.Parse("chart(120,4)\ntrack(400,2)\nhold(0,"+lane+",1,1000)\n");var events=d.ReadEvents();
                    renderer.Draw(events,new ScrollTimeline(events),new BeatTimeline(events),0,true,false,false,-1,.5f,null,null,4,null,false,false);
                    string name="Note skin / "+NoteSkinPolicy.Ground(events.Last());
                    var mesh=parent.GetComponentsInChildren<MeshFilter>().Single(n=>n.name==name).sharedMesh;
                    check(mesh.uv.Any(uv=>Math.Abs(uv.y-1.2f)<.0001),"106 Hold keeps mirror parity across a scroll breakpoint in lane "+lane);
                    check(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"106 Hold vertices remain finite in lane "+lane);
                }
                var flick=SpcDocument.Parse("chart(120,4)\nflick(1000,50,100,20,16)\n");var f=flick.ReadEvents();
                renderer.Draw(f,new ScrollTimeline(f),new BeatTimeline(f),0,true,false,false,-1,.5f,null,null,4,null,false,false);
                var air=parent.GetComponentsInChildren<MeshFilter>().Single(n=>n.name=="Flick and cursor surfaces").sharedMesh;
                check(air.vertexCount>0&&air.colors.All(c=>c.a<=.6501f),"106 Flick surface brightness is reduced");
                check(air.vertices.Select(v=>v.y).Distinct().Count()==1,"106 Flick remains on its original flat air plane");
                check(renderer.CheckGroundProjectionBounds(),"106 air footprints remain in the central four lanes");
            }
            finally
            {
                if(renderer!=null)renderer.Dispose();if(materials!=null)materials.Dispose();
                UnityEngine.Object.DestroyImmediate(parent);UnityEngine.Object.DestroyImmediate(profile);
            }
            report.AppendLine("TOTAL: "+count+" actual Unity 1.0.6 mesh/material checks passed.");return report.ToString();
        }
        [MenuItem("Tools/InFalsus Studio/Run 1.0.6 Generated Mesh Checks")]
        public static void RunMeshes(){Debug.Log(RunMeshReport());}
        public static void VerifyAndBuildBatch()
        {
            try
            {
                string output=Environment.GetEnvironmentVariable("INFALSUS_BUILD_OUTPUT");
                if(string.IsNullOrWhiteSpace(output)||Directory.Exists(output))throw new InvalidOperationException("Specify a NEW output folder with INFALSUS_BUILD_OUTPUT.");
                string grid=StudioGridSelectionChecks.RunReport();StudioRelease100.RunChecks();string features=RunMeshReport();
                string oldVersion=PlayerSettings.bundleVersion,oldProduct=PlayerSettings.productName;
                try
                {
                    StudioBranding101.EnsureConfigured();PlayerSettings.bundleVersion=ReleaseIdentity.Version;PlayerSettings.productName=ReleaseIdentity.ProductName;
                    string exe=InFalsusStudio.Editor.StudioBuild08.BuildTo(output);
                    if(!File.Exists(exe))throw new IOException("Build returned without an executable.");
                    File.WriteAllText(Path.Combine(output,"GridSelectionChecks.txt"),grid);
                    File.WriteAllText(Path.Combine(output,"FeatureChecks106.txt"),features);
                    File.WriteAllText(Path.Combine(output,"READ_ME.txt"),ReleaseIdentity.SettingsTitle+"\r\nKeep the entire Data/DLL directory with the exe.\r\nSave/Ctrl+S writes drafts. Exit never auto-saves.\r\nTools: equal/grid SkyArea split, SkyArea/Flick output and reset automatic groups.\r\nChart Check reports unknown source and note conflicts. Camera/Help are in Settings.\r\nSlow music preserves pitch; key sounds retain their original independent timing.\r\n");
                    File.WriteAllText(Path.Combine(output,"RELEASE_1.0.6.txt"),string.Join("\r\n\r\n",Array.ConvertAll(ReleaseNotesCatalog.Recent,n=>"v"+n.Version+"\r\n"+n.Chinese+"\r\n\r\n"+n.English)));
                    Debug.Log(grid+"\n"+features+"\n1.0.6 Windows build: "+exe);
                }
                finally{PlayerSettings.bundleVersion=oldVersion;PlayerSettings.productName=oldProduct;}
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
#endif
