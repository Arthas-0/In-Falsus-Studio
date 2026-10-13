using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InFalsusStudio
{
    public sealed class MeshBatch : IDisposable
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<Vector2> States = new List<Vector2>();
        public readonly GameObject Object;
        private readonly Mesh mesh;
        public bool ClipX;
        public float MinX,MaxX;
        public bool ClipZ;
        public float MinZ,MaxZ;
        private struct ClipVertex
        {
            public Vector3 P;public Color C;public Vector2 U,S;
            public ClipVertex(Vector3 p,Color c,Vector2 u,Vector2 s){P=p;C=c;U=u;S=s;}
        }
        public MeshBatch(Transform parent, string name, Material material, int order = 0)
        {
            Object = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); Object.layer = 30; Object.transform.SetParent(parent, false);
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 }; mesh.MarkDynamic();
            Object.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = Object.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }
        public void Clear() { Vertices.Clear(); Colors.Clear(); Triangles.Clear();Uvs.Clear();States.Clear(); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) { Quad(a,b,c,d,color,color,color,color); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd)
        { QuadUv(a,b,c,d,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero,ca,cb,cc,cd,Vector2.zero); }
        public void QuadUv(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud,Color color,Vector2 state)
        {QuadUv(a,b,c,d,ua,ub,uc,ud,color,color,color,color,state);}
        public void QuadUv(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud,Color ca,Color cb,Color cc,Color cd,Vector2 state)
        {
            if(ClipX||ClipZ)
            {TriangleUv(a,c,b,ca,cc,cb,ua,uc,ub,state,state,state);TriangleUv(a,d,c,ca,cd,cc,ua,ud,uc,state,state,state);return;}
            int i=Vertices.Count;
            Add(new ClipVertex(a,ca,ua,state));Add(new ClipVertex(b,cb,ub,state));Add(new ClipVertex(c,cc,uc,state));Add(new ClipVertex(d,cd,ud,state));
            Triangles.Add(i);Triangles.Add(i+2);Triangles.Add(i+1);Triangles.Add(i);Triangles.Add(i+3);Triangles.Add(i+2);
        }
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color ca,Color cb,Color cc)
        {TriangleUv(a,b,c,ca,cb,cc,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);}
        private static bool Finite(Vector3 p)
        {return !float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);}
        public void TriangleUv(Vector3 a,Vector3 b,Vector3 c,Color ca,Color cb,Color cc,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 sa,Vector2 sb,Vector2 sc)
        {
            if(!Finite(a)||!Finite(b)||!Finite(c))return;
            bool outsideX=ClipX&&(a.x<MinX||a.x>MaxX||b.x<MinX||b.x>MaxX||c.x<MinX||c.x>MaxX);
            bool outsideZ=ClipZ&&(a.z<MinZ||a.z>MaxZ||b.z<MinZ||b.z>MaxZ||c.z<MinZ||c.z>MaxZ);
            if(outsideX||outsideZ)
            {
                var polygon=new List<ClipVertex>{new ClipVertex(a,ca,ua,sa),new ClipVertex(b,cb,ub,sb),new ClipVertex(c,cc,uc,sc)};
                if(outsideX){polygon=ClipPlane(polygon,MinX,true,0);polygon=ClipPlane(polygon,MaxX,false,0);}
                if(outsideZ){polygon=ClipPlane(polygon,MinZ,true,2);polygon=ClipPlane(polygon,MaxZ,false,2);}
                for(int n=1;n+1<polygon.Count;n++)RawTriangle(polygon[0],polygon[n],polygon[n+1]);
                return;
            }
            RawTriangle(new ClipVertex(a,ca,ua,sa),new ClipVertex(b,cb,ub,sb),new ClipVertex(c,cc,uc,sc));
        }
        private void Add(ClipVertex v){Vertices.Add(v.P);Colors.Add(v.C);Uvs.Add(v.U);States.Add(v.S);}
        private void RawTriangle(ClipVertex a,ClipVertex b,ClipVertex c)
        {int i=Vertices.Count;Add(a);Add(b);Add(c);Triangles.Add(i);Triangles.Add(i+1);Triangles.Add(i+2);}
        private static List<ClipVertex> ClipPlane(List<ClipVertex> input,float plane,bool keepGreater,int axis)
        {
            var output=new List<ClipVertex>();if(input.Count==0)return output;
            ClipVertex a=input[input.Count-1];bool aIn=keepGreater?a.P[axis]>=plane:a.P[axis]<=plane;
            foreach(var b in input)
            {
                bool bIn=keepGreater?b.P[axis]>=plane:b.P[axis]<=plane;
                if(aIn!=bIn)
                {
                    float t=(plane-a.P[axis])/(b.P[axis]-a.P[axis]);Vector3 point=Vector3.Lerp(a.P,b.P,t);point[axis]=plane;
                    output.Add(new ClipVertex(point,Color.Lerp(a.C,b.C,t),Vector2.Lerp(a.U,b.U,t),Vector2.Lerp(a.S,b.S,t)));
                }
                if(bIn)output.Add(b);a=b;aIn=bIn;
            }
            return output;
        }
        public void Strip(Vector3 a, Vector3 b, float width, Color color)
        {
            // Offset in the horizontal plane. For a transverse line this offsets Z.
            Vector3 side = Vector3.Cross(Vector3.up, b-a).normalized * width * .5f;
            if (side.sqrMagnitude < 1e-12f) side = Vector3.right * width * .5f;
            Quad(a-side,a+side,b+side,b-side,color);
        }
        /// <summary>Camera-facing ribbon with a stable pixel width, only for authoring guides.
        /// Centerline screen coordinates still come from the actual shared stage/time geometry.
        /// Bias slightly toward the camera so subpixel edges do not disappear inside the floor.
        /// No sorting layer, camera, track surface or note geometry is changed.</summary>
        public void ScreenStrip(Camera camera,Vector3 a,Vector3 b,float pixels,Color color)
        {
            Vector3 pa=camera.WorldToScreenPoint(a),pb=camera.WorldToScreenPoint(b);
            if(pa.z<=camera.nearClipPlane||pb.z<=camera.nearClipPlane)return;
            Vector2 direction=new Vector2(pb.x-pa.x,pb.y-pa.y);
            if(direction.sqrMagnitude<.01f)return;
            Vector2 offset=new Vector2(-direction.y,direction.x).normalized*(pixels*.5f);
            float factor=2*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)/Mathf.Max(1,camera.pixelRect.height);
            float za=Mathf.Max(camera.nearClipPlane+.001f,pa.z-pa.z*factor*(pixels+1));
            float zb=Mathf.Max(camera.nearClipPlane+.001f,pb.z-pb.z*factor*(pixels+1));
            Quad(camera.ScreenToWorldPoint(new Vector3(pa.x-offset.x,pa.y-offset.y,za)),
                 camera.ScreenToWorldPoint(new Vector3(pa.x+offset.x,pa.y+offset.y,za)),
                 camera.ScreenToWorldPoint(new Vector3(pb.x+offset.x,pb.y+offset.y,zb)),
                 camera.ScreenToWorldPoint(new Vector3(pb.x-offset.x,pb.y-offset.y,zb)),color);
        }
        public void Commit()
        {
            mesh.Clear(); if (Vertices.Count == 0) { Object.SetActive(false); return; }
            Object.SetActive(true); mesh.SetVertices(Vertices); mesh.SetColors(Colors);mesh.SetUVs(0,Uvs);mesh.SetUVs(1,States); mesh.SetTriangles(Triangles,0); mesh.RecalculateBounds();
        }
        public void Dispose() { if (Application.isPlaying) { UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(Object); } else { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(Object); } }
    }
    public sealed class StudioMaterials : IDisposable
    {
        public readonly Material Opaque, Transparent, Glow;
        public readonly Material HoldBlue,HoldPurple,HoldPink,TapBlue,TapWideMint,TapPurple,TapPink,SkySkin;
        public StudioMaterials(StudioProfile p)
        {
            Opaque = Make("VertexOpaque",p); Transparent = Make("VertexTransparent",p); Glow = Make("VertexGlow",p);
            HoldBlue=Skin(p,"BlueHold",0,Color.white,"BlueInactive","BlueActive");
            HoldPurple=Skin(p,"PurpleHold",0,Color.white,"PurpleInactive","PurpleActive");
            HoldPink=Skin(p,"PinkHold",0,Color.white,"PinkInactive","PinkActive");
            TapBlue=Skin(p,"SingleTap",1,new Color(.16f,.68f,.91f,1));
            TapWideMint=Skin(p,"WideTap",2,new Color(.36f,.91f,.72f,1));
            TapPurple=Skin(p,"LeftTap",3,new Color(.62f,.30f,.98f,1));
            TapPink=Skin(p,"RightTap",3,new Color(.96f,.18f,.50f,1));
            SkySkin=Skin(p,"Sky field",4,new Color(.56f,.25f,.98f,1));
        }
        private static Material Make(string shaderName, StudioProfile p)
        {
            var shader = Resources.Load<Shader>("InFalsusStudio/" + shaderName);
            if (shader == null) throw new InvalidOperationException("Missing InFalsusStudio shader: " + shaderName + ". Copy the complete Assets/InFalsusStudio folder.");
            var m = new Material(shader) { name = "IF " + shaderName };
            m.SetColor("_FogColor",p.background); m.SetFloat("_FogStart",p.fogStart);m.SetFloat("_FogEnd",p.fogEnd);return m;
        }
        private static Material Skin(StudioProfile p,string name,int mode,Color tint,string inactive=null,string active=null)
        {
            var m=Make("NoteSkin106",p);m.name="IF 1.0.6 "+name;m.SetFloat("_Mode",mode);m.SetColor("_Tint",tint);
            if(inactive!=null)
            {
                var a=Resources.Load<Texture2D>("InFalsusStudio/NoteSkin104/"+inactive);
                var b=Resources.Load<Texture2D>("InFalsusStudio/NoteSkin104/"+active);
                if(a==null||b==null)throw new InvalidOperationException("Missing Hold texture pair: "+inactive+" / "+active+". Merge all 1.0.4 assets.");
                m.SetTexture("_InactiveTex",a);m.SetTexture("_ActiveTex",b);
            }
            return m;
        }
        public void Dispose() { foreach (var m in new[]{Opaque,Transparent,Glow,HoldBlue,HoldPurple,HoldPink,TapBlue,TapWideMint,TapPurple,TapPink,SkySkin}) { if (Application.isPlaying) UnityEngine.Object.Destroy(m); else UnityEngine.Object.DestroyImmediate(m); } }
    }
}
