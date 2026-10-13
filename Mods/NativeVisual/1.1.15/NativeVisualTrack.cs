using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.NativeVisual {
// Original lane meshes and atlas UVs. Only the visible stage is rebuilt;
// StageSpace and the editor's selection/input geometry remain unchanged.
internal static class NativeTrack {
 private struct Vertex {internal Vector3 P;internal Vector2 U;internal Vertex(Vector3 p,Vector2 u){P=p;U=u;}}
 internal static void ClipMesh(Mesh mesh,Vector3[] vertices,Vector2[] uv,int[] indices,float near,float far){
  var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
  for(int i=0;i<indices.Length;i+=3){var poly=new List<Vertex>();for(int j=0;j<3;j++){int k=indices[i+j];poly.Add(new Vertex(vertices[k],uv[k]));}poly=Clip(poly,near,true);poly=Clip(poly,far,false);if(poly.Count<3)continue;int b=v.Count;foreach(var p in poly){v.Add(p.P);u.Add(p.U);}for(int j=1;j<poly.Count-1;j++){t.Add(b);t.Add(b+j);t.Add(b+j+1);}}
  mesh.Clear();mesh.SetVertices(v);mesh.SetUVs(0,u);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
 }
 private static List<Vertex> Clip(List<Vertex> poly,float z,bool lower){
  var result=new List<Vertex>();if(poly.Count==0)return result;var a=poly[poly.Count-1];bool ai=lower?a.P.z>=z:a.P.z<=z;
  foreach(var b in poly){bool bi=lower?b.P.z>=z:b.P.z<=z;if(ai!=bi){float f=(z-a.P.z)/(b.P.z-a.P.z);result.Add(new Vertex(Vector3.LerpUnclamped(a.P,b.P,f),Vector2.LerpUnclamped(a.U,b.U,f)));}if(bi)result.Add(b);a=b;ai=bi;}return result;
 }
 private static void Opaque(Material m){
  // A floor is opaque. Reusing a transparent Tap material in 1.0.0 let the
  // floor draw after distant notes and completely overwrite their pixels.
  m.renderQueue=2000;m.SetOverrideTag("RenderType","Opaque");m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
  foreach(var k in new[]{"_Surface","_SrcBlend","_DstBlend","_SrcBlendAlpha","_DstBlendAlpha","_ZWrite","_ZWriteControl"})if(m.HasProperty(k))m.SetFloat(k,k=="_SrcBlend"||k=="_SrcBlendAlpha"||k=="_ZWrite"||k=="_ZWriteControl"?1:0);
 }
 internal static void Build(object renderer,object p){
  if(!NativeVisualPlugin.Ready)return;var set=NativeVisualPlugin.Visuals(renderer);set.Begin();bool active=NativeVisualPlugin.Active&&NativeVisualPlugin.StageEnabled.Value;
  if(!active){set.End();return;}
  try{
   float near=NativeVisualPlugin.N(p,"stageNear"),far=NativeVisualPlugin.N(p,"stageFar"),half=NativeVisualPlugin.N(p,"centralHalfWidth"),side=NativeVisualPlugin.N(p,"sideRun");
   var surface=NativeVisualPlugin.T("StageSpace").GetMethod("Surface",NativeVisualPlugin.F);
   // Native global uniforms: six lanes, sixteen transition entries per lane.
   // Baseline stage is enabled; this adapter does not change chart track state.
   Shader.SetGlobalFloat("lowiro_LaneEnableStateDetails_DistanceFromJudgementToTrackEnd",far);
   Shader.SetGlobalFloatArray("lowiro_LaneEnableStateDetails_RelativeVisualTimes",Enumerable.Repeat(1e8f,96).ToArray());
   Shader.SetGlobalFloatArray("lowiro_LaneEnableStateDetails_NormalizedEnabled",Enumerable.Repeat(1f,96).ToArray());
   Backing(set,p,near,far,half,side);
   for(int lane=0;lane<6;lane++){
    string meshName=lane==0?"shift_2":lane==5?"space_2":"lane_"+lane;
    var source=NativeVisualPlugin.Meshes[meshName];var s=set.Take("lane"+lane,NativeVisualPlugin.Mat(lane==0?"matid_trackwall1":lane==5?"matid_trackwall1_alt":"matid_trackfloor1"));
    Opaque(s.Material);s.Material.SetFloat("_LaneIndex",lane);s.Material.SetFloat("_IsFloorLane",lane==0||lane==5?0:1);
    var native=source.vertices;var rotation=lane==0?new Quaternion(-.270598054f,.65328151f,.65328151f,-.270598054f):lane==5?new Quaternion(.65328151f,-.270598054f,-.270598054f,.65328145f):Quaternion.identity;
    for(int i=0;i<native.Length;i++)native[i]=rotation*native[i];
    float minX=native.Min(v=>v.x),maxX=native.Max(v=>v.x),minY=native.Min(v=>v.y),maxY=native.Max(v=>v.y);
    var vertices=new Vector3[native.Length];float scale=far/20.181811f;
    for(int i=0;i<native.Length;i++){
     float f=(native[i].x-minX)/(maxX-minX),x=lane==0?-half-side+f*side:lane==5?half+f*side:-half+(lane-1+Mathf.Lerp(.015f,.985f,f))*half*.5f;
     // Native floor source is centered at z=9.411811; judgment is z=-1.5.
     // Side source is centered at z=0. Preserve original atlas longitudinal UV.
     float z=(native[i].z+(lane==0||lane==5?1.5f:10.911811f))*scale;
     float expectedY=lane==0?Mathf.Lerp(maxY,minY,f):lane==5?Mathf.Lerp(minY,maxY,f):0;
     float relief=(native[i].y-expectedY)*half/.8f;
     vertices[i]=(Vector3)surface.Invoke(null,new object[]{p,x,z,-.009f+relief});
    }
    ClipMesh(s.Mesh,vertices,source.uv,source.triangles,near,far);
    var judge=(Vector3)surface.Invoke(null,new object[]{p,lane==0?-half-side*.5f:lane==5?half+side*.5f:-half+(lane-.5f)*half*.5f,0f,0f});
    var end=judge;end.z=far;s.Material.SetVector("_JudgementLineWorldPosition",judge);s.Material.SetVector("_EndOfTrackWorldPosition",end);
    KeyPanel(set,p,lane);
   }
   Hooks.Judge(set,p,"judge-main","judge_main","matid_judgement",1);
   Hooks.Judge(set,p,"judge-left","judge_shift","matid_judgement",0);
   Hooks.Judge(set,p,"judge-right","judge_space","judgement_line_alt",5);
   KeepSkyGuide(set,renderer,p);
   // Near longitudinal authoring details are omitted; beat grid and judgement guides remain.
   foreach(var n in new[]{"floor","detail","glow"})NativeVisualPlugin.Show(NativeVisualPlugin.Get(renderer,n),false);
  }catch(Exception e){NativeVisualPlugin.Instance.Error(e);foreach(var n in new[]{"floor","detail","glow"})NativeVisualPlugin.Show(NativeVisualPlugin.Get(renderer,n),true);}
  set.End();
 }
 private static void Backing(VisualSet set,object p,float near,float far,float half,float side){
  var s=set.Take("backing",NativeVisualPlugin.Mat("matid_notesingle"));Opaque(s.Material);s.Material.SetTexture("_BaseMap",Texture2D.whiteTexture);s.Material.SetTexture("_MainTex",Texture2D.whiteTexture);s.Material.SetColor("_BaseColor",Color.black);s.Material.SetColor("_Color",Color.black);
  var surface=NativeVisualPlugin.T("StageSpace").GetMethod("Surface",NativeVisualPlugin.F);var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();var x=new[]{-half-side,-half,half,half+side};
  for(int i=0;i<3;i++){int b=v.Count;foreach(var point in new[]{new Vector2(x[i],near),new Vector2(x[i+1],near),new Vector2(x[i+1],far),new Vector2(x[i],far)}){v.Add((Vector3)surface.Invoke(null,new object[]{p,point.x,point.y,-.04f}));uv.Add(Vector2.zero);}t.AddRange(new[]{b,b+2,b+1,b,b+3,b+2});}
  s.Mesh.Clear();s.Mesh.SetVertices(v);s.Mesh.SetUVs(0,uv);s.Mesh.SetTriangles(t,0);s.Mesh.RecalculateNormals();s.Mesh.RecalculateBounds();
 }
 private static void KeyPanel(VisualSet set,object p,int lane){
  Texture2D texture;if(!NativeVisualPlugin.Textures.TryGetValue("keybind-indicators-v2-default",out texture))throw new Exception("Native key panel texture missing");
  var s=set.Take("key"+lane,NativeVisualPlugin.Mat("matid_notesingle"));s.Material.renderQueue=2990;s.Material.SetTexture("_BaseMap",texture);s.Material.SetTexture("_MainTex",texture);s.Material.SetColor("_BaseColor",new Color(1,1,1,.22f));s.Material.SetColor("_Color",new Color(1,1,1,.22f));
  var bound=NativeVisualPlugin.T("StageSpace").GetMethod("FloorBound",NativeVisualPlugin.F);var v=new Vector3[4];v[0]=(Vector3)bound.Invoke(null,new object[]{p,lane,1f,false,.10f,.025f});v[1]=(Vector3)bound.Invoke(null,new object[]{p,lane,1f,true,.10f,.025f});v[2]=(Vector3)bound.Invoke(null,new object[]{p,lane,1f,true,1.1f,.025f});v[3]=(Vector3)bound.Invoke(null,new object[]{p,lane,1f,false,1.1f,.025f});for(int i=0;i<v.Length;i++)v[i].y+=.003f;
  s.Mesh.Clear();s.Mesh.vertices=v;s.Mesh.uv=new[]{new Vector2(0,.30f),new Vector2(1,.30f),new Vector2(1,1),new Vector2(0,1)};s.Mesh.triangles=new[]{0,2,1,0,3,2};s.Mesh.RecalculateNormals();s.Mesh.RecalculateBounds();
 }
 private static void KeepFrontLines(VisualSet set,object renderer,object p){
  var batch=NativeVisualPlugin.Get(renderer,"detail");var vertices=(List<Vector3>)NativeVisualPlugin.Get(batch,"Vertices");var triangles=(List<int>)NativeVisualPlugin.Get(batch,"Triangles");var colors=(List<Color>)NativeVisualPlugin.Get(batch,"Colors");var uv=(List<Vector2>)NativeVisualPlugin.Get(batch,"Uvs");float half=NativeVisualPlugin.N(p,"centralHalfWidth");var indices=new List<int>();
  for(int i=0;i<triangles.Count;i+=3){var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];if(Mathf.Min(a.z,b.z,c.z)>=.01f&&Mathf.Max(a.z,b.z,c.z)<=3.81f&&Mathf.Max(a.x,b.x,c.x)-Mathf.Min(a.x,b.x,c.x)<.02f&&Mathf.Max(Mathf.Abs(a.x),Mathf.Abs(b.x),Mathf.Abs(c.x))<half)indices.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});}
  var s=set.Take("front-lines",NativeVisualPlugin.Obj(batch).GetComponent<MeshRenderer>().sharedMaterial);s.Mesh.Clear();s.Mesh.SetVertices(vertices);s.Mesh.SetTriangles(indices,0);if(colors.Count==vertices.Count)s.Mesh.SetColors(colors);if(uv.Count==vertices.Count)s.Mesh.SetUVs(0,uv);s.Mesh.RecalculateBounds();
 }
 private static void KeepSkyGuide(VisualSet set,object renderer,object p){
  var batch=NativeVisualPlugin.Get(renderer,"glow");var vertices=(List<Vector3>)NativeVisualPlugin.Get(batch,"Vertices");var triangles=(List<int>)NativeVisualPlugin.Get(batch,"Triangles");var colors=(List<Color>)NativeVisualPlugin.Get(batch,"Colors");var uv=(List<Vector2>)NativeVisualPlugin.Get(batch,"Uvs");
  var s=set.Take("sky-guide",NativeVisualPlugin.Obj(batch).GetComponent<MeshRenderer>().sharedMaterial);float height=NativeVisualPlugin.N(p,"skyHeight")-.06f;var indices=new List<int>();for(int i=0;i<triangles.Count;i+=3)if(vertices[triangles[i]].y>=height&&vertices[triangles[i+1]].y>=height&&vertices[triangles[i+2]].y>=height){indices.Add(triangles[i]);indices.Add(triangles[i+1]);indices.Add(triangles[i+2]);}
  s.Mesh.Clear();s.Mesh.SetVertices(vertices);s.Mesh.SetTriangles(indices,0);if(colors.Count==vertices.Count)s.Mesh.SetColors(colors);if(uv.Count==vertices.Count)s.Mesh.SetUVs(0,uv);s.Mesh.RecalculateBounds();
 }
}
}
