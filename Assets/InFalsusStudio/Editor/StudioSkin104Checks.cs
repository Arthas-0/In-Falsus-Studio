#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio.EditorTools
{
    public static class StudioSkin104Checks
    {
        public static void ValidateAssets(Action<bool,string> check)
        {
            var shader=Resources.Load<Shader>("InFalsusStudio/NoteSkin104");
            check(shader!=null,"104 note skin shader exists");
            if(shader!=null)check(!ShaderUtil.ShaderHasError(shader),"104 shader has no CURRENTLY reported Unity compiler error");
            foreach(string name in new[]{"BlueInactive","BlueActive","PurpleInactive","PurpleActive","PinkInactive","PinkActive"})
            {
                var t=Resources.Load<Texture2D>("InFalsusStudio/NoteSkin104/"+name);
                check(t!=null&&t.width==2048&&t.height==2048,"104 supplied 2048 atlas "+name);
            }
        }
        [MenuItem("Tools/InFalsus Studio/Run 1.0.4 Scroll and Skin Checks")]
        public static void Run()
        {
            int count=0;Action<bool,string> check=(ok,label)=>{count++;if(!ok)throw new InvalidOperationException("FAILED: "+label);};
            Release104SelfTests.Run(check);
            Release104SelfTests.RunPair(Path.Combine(Application.dataPath,"InFalsusStudio/Editor/VisualRegression104"),check);
            ValidateAssets(check);
            Debug.Log("Actual C# 1.0.4 checks passed: "+count+". Shader error query is not a screenshot/render acceptance test.");
        }
        [MenuItem("Tools/InFalsus Studio/Inspect 1.0.4 Loaded Scroll")]
        public static void Inspect()
        {
            var app=UnityEngine.Object.FindFirstObjectByType<StudioBootstrap>();
            if(!EditorApplication.isPlaying||app==null||app.History==null){Debug.LogWarning("Enter Play and open a chart first.");return;}
            var e=app.History.Document.ReadEvents();
            var t=e.Where(n=>n.Kind==EventKind.Track&&Math.Abs(n.Get(1))>10000).ToArray();
            var timeline=new ScrollTimeline(e);
            string report=string.Join("\n",t.Select(n=>n.TimeMs+" ms / "+n.Get(1)+" / "+(AuthoringSafety.Reason(n)??"SUPPORTED")).ToArray());
            Debug.Log("1.0.4 high track events: "+t.Length+"\n"+report+
                "\n38282ms Flick Z @37592="+timeline.Delta(38282,37592)*.015+
                "; @37593="+timeline.Delta(38282,37593)*.015+" (reference speed1, 15 units/sec)");
        }
        [MenuItem("Tools/InFalsus Studio/Run 1.0.4 Generated Mesh Checks")]
        public static void Meshes()
        {
            if(!EditorApplication.isPlaying){Debug.LogWarning("Enter Play first for the real camera / runtime mesh checks.");return;}
            int count=0;Action<bool,string> check=(ok,label)=>{count++;if(!ok)throw new InvalidOperationException("FAILED: "+label);};
            GameObject parent=new GameObject("104 temporary mesh check");
            StudioProfile p=ScriptableObject.CreateInstance<StudioProfile>();StudioMaterials materials=null;NoteRenderer notes=null;MeshBatch clip=null;
            try
            {
                var cameraObject=new GameObject("104 check camera");cameraObject.transform.SetParent(parent.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;StageSpace.ApplyCamera(camera,p);
                materials=new StudioMaterials(p);clip=new MeshBatch(parent.transform,"104 UV clipping fixture",materials.TapBlue);
                clip.ClipX=true;clip.MinX=-2;clip.MaxX=2;clip.ClipZ=true;clip.MinZ=-1;clip.MaxZ=5;
                clip.QuadUv(new Vector3(-4,1,-100),new Vector3(4,1,-100),new Vector3(4,1,100),new Vector3(-4,1,100),
                    new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1),Color.white,new Vector2(1,2));
                check(clip.Vertices.Count>0,"104 clipped textured quad remains nonempty");
                for(int i=0;i<clip.Vertices.Count;i++)
                {
                    var v=clip.Vertices[i];
                    check(v.x>=-2.0001&&v.x<=2.0001&&v.z>=-1.0001&&v.z<=5.0001,"104 four-plane clipping bounds");
                    check(Math.Abs(clip.Uvs[i].x-(v.x+4)/8)<.0001&&Math.Abs(clip.Uvs[i].y-(v.z+100)/200)<.0001,"104 UV interpolation survives clipping");
                    check(clip.States[i]==new Vector2(1,2),"104 activation/cap state survives clipping");
                }
                clip.Dispose();clip=null;
                var doc=SpcDocument.Load(Path.Combine(Application.dataPath,"InFalsusStudio/Editor/VisualRegression104/Forbidden_codeleviathan3.spc.txt"));
                var events=doc.ReadEvents();var scroll=new ScrollTimeline(events);var beats=new BeatTimeline(events);var cursor=new AutoplayCursor();cursor.Rebuild(events);
                notes=new NoteRenderer(parent.transform,p,materials,camera);notes.UpdateConnections(events);
                foreach(double now in new[]{36810d,37592d,37592.5,37593d,38282d,40583d,41227.5,164908d,166379d})
                {
                    notes.Draw(events,scroll,beats,now,true,true,false,-1,.5f,null,null,4,cursor,false,false);
                    foreach(var filter in parent.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mesh=filter.sharedMesh;if(mesh==null||mesh.vertexCount==0)continue;
                        check(mesh.uv.Length==mesh.vertexCount&&mesh.uv2.Length==mesh.vertexCount,"104 every textured vertex has UV/state");
                        check(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.z)&&v.z>=p.stageNear-.002&&v.z<=p.stageFar+.002),"104 generated meshes finite and clipped @"+now);
                    }
                    check(notes.CheckGroundProjectionBounds(),"104 Sky/Flick floor footprints within central4");
                }
                Debug.Log("Actual Unity mesh C# checks passed: "+count+". Inspect materials visually in Play; no pixels were compared by this command.");
            }
            finally
            {
                parent.SetActive(false);if(clip!=null)clip.Dispose();if(notes!=null)notes.Dispose();if(materials!=null)materials.Dispose();
                UnityEngine.Object.Destroy(parent);UnityEngine.Object.Destroy(p);
            }
        }
    }
}
#endif
