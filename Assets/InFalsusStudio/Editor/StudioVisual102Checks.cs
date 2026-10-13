#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio.EditorTools
{
    public static class StudioVisual102Checks
    {
        [MenuItem("Tools/InFalsus Studio/Run 1.0.2 Visual Geometry Checks")]
        public static void Run()
        {
            int count=0;
            Action<bool,string> check=(ok,name)=>{count++;if(!ok)throw new InvalidOperationException("1.0.2 visual check failed: "+name);};
            Visual102SelfTests.Run(check);
            var atlas=Resources.Load<Texture2D>(FloorComboRenderer.AtlasResource);
            check(atlas!=null&&atlas.width==1280&&atlas.height==640,"numeral atlas imported at native size");
            check(Resources.Load<Shader>(FloorComboRenderer.ShaderResource)!=null,"floor combo shader included in Resources");
            var path="Assets/InFalsusStudio/Resources/InFalsusStudio/Hud/ComboDigits.png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            check(importer!=null&&!importer.sRGBTexture,"mask texture uses linear data, not sRGB");
            check(importer!=null&&importer.mipmapEnabled,"digit mask has mipmaps for angled minification");
            using(var hash=SHA256.Create())
            {
                var actual=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(StudioBranding101.IconPath))).Replace("-","").ToLowerInvariant();
                check(actual=="d76ece59e29dccd3e991b06029d5bec47fd2527533192062baa11beb37ec098d","exact new user icon bytes");
            }
            Debug.Log("1.0.2 actual C# visual geometry/resource checks passed: "+count+". This is not a GPU or Windows executable rendering certification.");
        }
        [MenuItem("Tools/InFalsus Studio/Inspect 1.0.2 Running Track and Combo")]
        public static void Inspect()
        {
            var app=UnityEngine.Object.FindFirstObjectByType<StudioBootstrap>();
            if(!EditorApplication.isPlaying||app==null){Debug.LogWarning("Open Demo and enter Play first.");return;}
            int count=0;
            Action<bool,string> check=(ok,name)=>{count++;if(!ok)throw new InvalidOperationException("1.0.2 running mesh check failed: "+name);};
            var filters=app.GetComponentsInChildren<MeshFilter>(true);
            var stage=filters.FirstOrDefault(x=>x.name=="Stage - four floor lanes and two joined slopes");
            check(stage!=null&&stage.sharedMesh!=null,"generated stage mesh exists");
            var mesh=stage.sharedMesh;var v=mesh.vertices;var indices=mesh.triangles;
            float half=app.Profile.centralHalfWidth;
            check(v.Where(p=>Mathf.Abs(p.x)<=half+.000001f).All(p=>Mathf.Abs(p.y)<.00001f),"central surface has no lifted depth-writing facet decals");
            foreach(float xf in new[]{-.887f,-.31f,.003f,.381f,.879f})
            foreach(float zf in new[]{.113f,.531f,.915f})
            {
                Vector2 point=new Vector2(xf*half,Mathf.Lerp(app.Profile.stageNear,app.Profile.stageFar,zf));int hits=0;
                for(int i=0;i<indices.Length;i+=3)
                    if(Inside(point,v[indices[i]],v[indices[i+1]],v[indices[i+2]]))hits++;
                check(hits==1,"single opaque surface at "+point);
            }
            var combo=filters.FirstOrDefault(x=>x.name==FloorComboRenderer.ObjectName);
            check(combo!=null,"floor combo mesh object exists");
            var renderer=combo.GetComponent<MeshRenderer>();
            check(combo.GetComponent<Collider>()==null,"combo has no collider or input target");
            check(renderer.sharedMaterial.shader.name=="InFalsusStudio/FloorCombo","dedicated translucent combo material");
            check(renderer.sharedMaterial.renderQueue<3000,"combo drawn below note transparent passes");
            if(combo.sharedMesh.vertexCount>0)
                check(combo.sharedMesh.vertices.All(p=>Mathf.Abs(p.y-(float)(FloorComboLayout.FloorLift*half/2))<.00001f),"all numeral corners lie on one ground-parallel plane");
            Debug.Log("1.0.2 running Unity mesh checks passed: "+count+"; current combo visibility="+renderer.enabled+". Inspect the far road and translucent digits visually as well.");
        }
        private static bool Inside(Vector2 p,Vector3 a,Vector3 b,Vector3 c)
        {
            double x=p.x,z=p.y;
            double u=(b.x-a.x)*(z-a.z)-(b.z-a.z)*(x-a.x);
            double v=(c.x-b.x)*(z-b.z)-(c.z-b.z)*(x-b.x);
            double w=(a.x-c.x)*(z-c.z)-(a.z-c.z)*(x-c.x);
            const double epsilon=1e-9;
            return u>epsilon&&v>epsilon&&w>epsilon||u<-epsilon&&v<-epsilon&&w<-epsilon;
        }
    }
}
#endif
