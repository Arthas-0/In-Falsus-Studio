#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio.EditorTools
{
    public static class StudioRelease110Checks
    {
        internal static string RunFlickReport(string samplesPath=null)
        {
            var report=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,label)=>{count++;report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);};
            var parent=new GameObject("110 temporary Flick checks");var profile=ScriptableObject.CreateInstance<StudioProfile>();
            StudioMaterials materials=null;NoteRenderer renderer=null;
            var samples=new StringBuilder("Width,Direction,Q,ScreenX,ScreenY,BaseX,BaseY\n");
            try
            {
                var cameraObject=new GameObject("110 geometry camera");cameraObject.transform.SetParent(parent.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;StageSpace.ApplyCamera(camera,profile);
                camera.pixelRect=new Rect(0,0,1920,1080);camera.aspect=16f/9;
                foreach(float width in new[]{4f,2f,1f,.3333333f,.1666667f})
                foreach(float z in new[]{0f,5f,20f})
                foreach(bool right in new[]{false,true})
                {
                    float y=profile.skyHeight+.019f,xl=-width*.5f,xr=width*.5f;
                    Vector3 high=new Vector3(right?xr:xl,y,z),tip=new Vector3(right?xl:xr,y,z);
                    Vector3 highScreen=camera.WorldToScreenPoint(high),tipScreen=camera.WorldToScreenPoint(tip);
                    Vector3[] points=Enumerable.Range(0,193).Select(i=>FlickGeometry.BackVertex(profile,camera,xl,xr,y,z,right,i/192f)).ToArray();
                    var screen=points.Select(camera.WorldToScreenPoint).ToArray();
                    string label="w="+width+" z="+z+" right="+right;
                    check(points.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)&&Math.Abs(p.y-y)<.00001&&p.z>=z-.0001),"110 finite flat receding Flick "+label);
                    check(Math.Abs(screen[0].y-highScreen.y-FlickGeometry.ScreenHeight(profile,camera,y,z))<.002f&&(points.Last()-tip).magnitude<.0001,"110 width-independent head height and exact tail endpoint "+label+
                        " height error="+Math.Abs(screen[0].y-highScreen.y-FlickGeometry.ScreenHeight(profile,camera,y,z))+" tail error="+(points.Last()-tip).magnitude);
                    check(Enumerable.Range(1,192).All(i=>screen[i].y<=screen[i-1].y+.002f&&(right?screen[i].x<=screen[i-1].x+.002f:screen[i].x>=screen[i-1].x-.002f)),"110 curve has no reversed taper or lateral fold "+label);
                    if(width==4)
                    {
                        float remaining=(screen[144].y-highScreen.y)/FlickGeometry.ScreenHeight(profile,camera,y,z);
                        check(remaining>=.04f&&remaining<=.07f,"110 tail fix retains projected thickness at three-quarter span "+label+" ratio="+remaining);
                    }
                    if(z==0)for(int i=0;i<screen.Length;i++)
                    {
                        float q=i/192f;Vector3 basePoint=camera.WorldToScreenPoint(Vector3.Lerp(high,tip,q));
                        samples.AppendLine(string.Join(",",new[]{width.ToString("R",CultureInfo.InvariantCulture),right?"R":"L",q.ToString("R",CultureInfo.InvariantCulture),
                            screen[i].x.ToString("R",CultureInfo.InvariantCulture),screen[i].y.ToString("R",CultureInfo.InvariantCulture),
                            basePoint.x.ToString("R",CultureInfo.InvariantCulture),basePoint.y.ToString("R",CultureInfo.InvariantCulture)}));
                    }
                }
                check(FlickGeometry.Taper(.25f,4,.4f)>.25f&&FlickGeometry.Taper(.5f,4,.4f)>.1f&&
                    FlickGeometry.Taper(.75f,4,.4f)>=.04f&&FlickGeometry.Taper(.9f,4,.4f)>=.015f&&FlickGeometry.Taper(1,4,.4f)==0,
                    "110 tail fix keeps the full-width final quarter and tapers to zero only at the endpoint");
                check(Math.Abs(FlickGeometry.Taper(.02f/4,4,.4f)-FlickGeometry.Taper(.02f/2,2,.4f))<.0002f,"110 nearby head shape uses the same physical falloff at different wide spans");
                foreach(float skew in new[]{0f,1f,2f})
                foreach(float width in new[]{.08f,.5f,1f,4f})
                {
                    profile.flickCapSkew=skew;float y=profile.skyHeight+.019f;
                    var points=Enumerable.Range(0,193).Select(i=>camera.WorldToScreenPoint(FlickGeometry.BackVertex(profile,camera,-width*.5f,width*.5f,y,0,true,i/192f))).ToArray();
                    check(Enumerable.Range(1,192).All(i=>points[i].x<=points[i-1].x+.002f),"110 cap skew stays monotone at configured extremes "+skew+"/"+width);
                }
                profile.flickCapSkew=1;
                materials=new StudioMaterials(profile);renderer=new NoteRenderer(parent.transform,profile,materials,camera);
                foreach(int direction in new[]{4,16})
                {
                    var doc=SpcDocument.Parse("chart(120,4)\nflick(0,50,100,100,"+direction+")\nflick(0,50,100,8,"+direction+")\n");
                    var events=doc.ReadEvents();renderer.Draw(events,new ScrollTimeline(events),new BeatTimeline(events),0,true,false,false,-1,.5f,null,null,4,null,false,false);
                    var mesh=parent.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Flick and cursor surfaces").sharedMesh;
                    check(mesh.vertexCount>0&&mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&v.x>=-2.00001f&&v.x<=2.00001f)&&mesh.triangles.All(i=>i>=0&&i<mesh.vertexCount),"110 actual full/narrow Flick mesh remains clipped, finite and indexed "+direction);
                    check(mesh.colors.All(c=>c.a<=.6501f)&&mesh.vertices.Select(v=>v.y).Distinct().Count()==1,"110 actual mesh retains soft material and flat air plane "+direction);
                }
                if(!string.IsNullOrEmpty(samplesPath))File.WriteAllText(samplesPath,samples.ToString());
            }
            finally
            {
                if(renderer!=null)renderer.Dispose();if(materials!=null)materials.Dispose();
                UnityEngine.Object.DestroyImmediate(parent);UnityEngine.Object.DestroyImmediate(profile);
            }
            report.AppendLine("TOTAL: "+count+" actual Unity Flick geometry/mesh checks passed.");return report.ToString();
        }
        private static string Reference110(string root)
        {
            var report=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,label)=>{count++;report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);};
            var files=Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(f=>f,SongFolderProject.FileHash);
            var project=SongFolderProject.Prepare(root,true);
            foreach(var difficulty in project.Difficulties)
            {
                var source=difficulty.Document;byte[] bytes=source.ToBytes();var draft=source.Clone();
                var automatic=source.Clone();SkySplitOperations.ResetGroups(automatic);var heads=SkyGroups.Heads(automatic.ReadEvents());
                var result=SkyAutoGrouping.Assign(draft);
                check(draft.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea).All(n=>SkyGroups.GroupOf(n)>=0)&&SkyGroups.Heads(draft.ReadEvents()).SetEquals(heads),"110 Enigma "+difficulty.Name+" explicit grouping preserves automatic heads; "+result.Notes+" SkyAreas / "+result.Groups+" groups");
                check(source.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea).All(n=>source.ArgumentTokens(n.SourceId).Take(10).SequenceEqual(draft.ArgumentTokens(n.SourceId).Take(10))),"110 Enigma "+difficulty.Name+" auto-grouping retains all first-ten raw tokens");
                draft.SortTimedStatements();var times=draft.ReadEvents().Where(n=>n.Kind!=EventKind.Chart&&n.Kind!=EventKind.Unknown&&n.Kind!=EventKind.Beam).Select(n=>n.TimeMs).ToArray();
                check(times.SequenceEqual(times.OrderBy(t=>t))&&source.ToBytes().SequenceEqual(bytes),"110 Enigma "+difficulty.Name+" sorting changes only clone; original chart unchanged");
                var issues=ChartHealthCheck.Run(source);report.AppendLine("INFO "+difficulty.Name+" existing short durations: "+issues.Count(i=>i.Kind==ChartIssueKind.HoldTooShort||i.Kind==ChartIssueKind.SkyAreaTooShort));
            }
            check(files.All(p=>File.Exists(p.Key)&&SongFolderProject.FileHash(p.Key)==p.Value)&&Directory.GetFiles(root,"*",SearchOption.AllDirectories).Length==files.Count,"110 all Enigma source/cache files unchanged");
            report.AppendLine("TOTAL: "+count+" actual Enigma 1.0.10 checks; no reference writes.");return report.ToString();
        }
        [MenuItem("Tools/InFalsus Studio/Run 1.0.10 Flick Shape Checks")]
        public static void CheckFlicks(){Debug.Log(RunFlickReport());}
        public static void VerifyAndBuildBatch()
        {
            try
            {
                string output=Environment.GetEnvironmentVariable("INFALSUS_BUILD_OUTPUT"),evidence=Environment.GetEnvironmentVariable("INFALSUS_BUILD_EVIDENCE");
                if(string.IsNullOrWhiteSpace(output)||Directory.Exists(output))throw new InvalidOperationException("Specify a NEW output folder with INFALSUS_BUILD_OUTPUT.");
                string grid=StudioGridSelectionChecks.RunReport();StudioRelease100.RunChecks();string feature=Release110SelfTests.LastReport;
                string meshes=StudioRelease106Checks.RunMeshReport();
                string flicks=RunFlickReport(string.IsNullOrWhiteSpace(evidence)?null:Path.Combine(evidence,"FlickShapeSamples.csv"));
                string reference=Environment.GetEnvironmentVariable("INFALSUS_REFERENCE_CHART");
                string references=string.IsNullOrWhiteSpace(reference)?"Reference checks not requested.":
                    StudioRelease107Checks.RunReferenceReport(reference)+"\n"+StudioRelease108Checks.RunCappedReferences(reference)+"\n"+
                    StudioRelease109Checks.ReferenceDetails(reference)+"\n"+Reference110(reference);
                string oldVersion=PlayerSettings.bundleVersion,oldProduct=PlayerSettings.productName;
                try
                {
                    StudioBranding101.EnsureConfigured();PlayerSettings.bundleVersion=ReleaseIdentity.Version;PlayerSettings.productName=ReleaseIdentity.ProductName;
                    string exe=InFalsusStudio.Editor.StudioBuild08.BuildTo(output);
                    if(!File.Exists(exe))throw new IOException("Build returned without an executable.");
                    File.WriteAllText(Path.Combine(output,"GridSelectionChecks.txt"),grid);
                    File.WriteAllText(Path.Combine(output,"FeatureChecks106.txt"),meshes);
                    File.WriteAllText(Path.Combine(output,"FeatureChecks110.txt"),feature);
                    File.WriteAllText(Path.Combine(output,"FlickGeometry110Checks.txt"),flicks);
                    File.WriteAllText(Path.Combine(output,"EnigmaReference110Checks.txt"),references);
                    File.WriteAllText(Path.Combine(output,"READ_ME.txt"),ReleaseIdentity.SettingsTitle+
                        "\r\nKeep the entire Data/DLL directory. Save/Ctrl+S writes drafts; exit never auto-saves.\r\n"+
                        "Sky joins allow a gap/overlap up to 2ms with spatial contact. Source timestamps stay unchanged.\r\n"+
                        "Tools: Sky auto-grouping assigns IDs explicitly, with undo. New SkyAreas still omit IDs.\r\n"+
                        "Chart Check reports Hold/Sky durations below 5ms and supports jump/selection.\r\n"+
                        "Explicit Save/export stably sorts known timed statements; equal-time order and opaque source are retained.\r\n"+
                        "Flick heads keep a fixed height/falloff scale; wider notes extend a thin tail.\r\n");
                    File.WriteAllText(Path.Combine(output,"RELEASE_1.0.10.txt"),string.Join("\r\n\r\n",Array.ConvertAll(ReleaseNotesCatalog.Recent,n=>"v"+n.Version+"\r\n"+n.Chinese+"\r\n\r\n"+n.English)));
                    Debug.Log(grid+"\n"+meshes+"\n"+feature+"\n"+flicks+"\n"+references+"\n1.0.10 Windows build: "+exe);
                }
                finally{PlayerSettings.bundleVersion=oldVersion;PlayerSettings.productName=oldProduct;}
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
#endif
