// Pending render-only modules 1 / 2 / 3 / 6. No chart or transport mutations.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
namespace InFalsusStudio.NativeVisual {
public sealed partial class NativeVisualPlugin {
 internal static BepInEx.Configuration.ConfigEntry<bool> HudEnabled,CenterEnabled,ComboEnabled;
 private void InitPresentation(){
  HudEnabled=Config.Bind("Visual","NativeHud",true,"Native score/song HUD; original input handlers retained.");
  CenterEnabled=Config.Bind("Visual","NativeCenter",true,"Native dynamic central hexagon, expanding outlines and triangle dispersion.");
  ComboEnabled=Config.Bind("Visual","NativeCombo",true,"Native combo font; original combo timeline retained.");
  InitProgressStyle();
  NativePresentation.Load(Path.Combine(folder,"Presentation"));
  harmony.Patch(T("StudioBootstrap").GetMethod("DrawSongHud",F),transpiler:new HarmonyMethod(typeof(NativeHudControls).GetMethod("PreserveControls",F)));
  foreach(var row in new[]{
   new[]{"StudioBootstrap","LayoutHud",null,"Layout"},
   new[]{"StudioBootstrap","LayoutChrome",null,"Layout"},
   new[]{"StudioBootstrap","DrawSongHud","Hud","EndHud"},
   new[]{"StudioBootstrap","HR",null,"MapRect"},
   new[]{"StudioBootstrap","DrawIntegratedPlay","Play",null},
   new[]{"StudioBootstrap","InPlayButton","PlayContains",null},
   new[]{"StudioBootstrap","DrawSongProgress","Progress",null},
   new[]{"StudioBootstrap","HudBlocksInput",null,"BlockProgress"},
   new[]{"StudioBootstrap","LateUpdate",null,"Backdrop"},
   new[]{"FloorComboRenderer","Draw","Combo","ComboUv"}}){
   var method=T(row[0]).GetMethod(row[1],F);if(method==null)throw new Exception("Presentation adapter interface missing: "+row[0]+"."+row[1]);
   harmony.Patch(method,row[2]==null?null:new HarmonyMethod(typeof(PresentationHooks).GetMethod(row[2],F)),row[3]==null?null:new HarmonyMethod(typeof(PresentationHooks).GetMethod(row[3],F)));
  }
  foreach(var name in new[]{"DrawGround","DrawSky","DrawFlick"})harmony.Patch(T("NoteRenderer").GetMethod(name,F),new HarmonyMethod(typeof(PresentationHooks).GetMethod("BeginSelected",F)),new HarmonyMethod(typeof(PresentationHooks).GetMethod("EndSelected",F)));
  harmony.Patch(T("NoteRenderer").GetMethod("Edge",F),new HarmonyMethod(typeof(PresentationHooks).GetMethod("SelectedEdge",F)));
  Logger.LogInfo("Pending native presentation modules 1, 2, 3, 6 loaded.");
 }
}
#pragma warning disable 0649 // Assigned by Unity JsonUtility from layout.json.
[Serializable] internal sealed class PresentationLayout {
 public float[] score,scoreFrame,scoreLabel,digits,play,song,songBacking,jacket,jacketFrame,title,artist,difficulty,difficultyText,level,speed,speedValue,designer,progress,scoreAdvances,comboCrop,comboAdvances;
 public string titleFace,artistFace;public string[] fallbackFaces;public float centerWidth,centerFarOverlap,centerOpaqueBottom;
}
#pragma warning restore 0649
internal static class NativePresentation {
 internal static PresentationLayout L;internal static bool Ready,Scope;
 internal static Dictionary<string,Texture2D> Images=new Dictionary<string,Texture2D>();
 private static string folder;private static List<string> fonts=new List<string>();
 private static Material selectionMaterial;
 private struct SelectedLine {internal Vector3 A,B;internal SelectedLine(Vector3 a,Vector3 b){A=a;B=b;}}
 sealed class OutlineBuffers {internal readonly List<Vector3> Vertices=new List<Vector3>();internal readonly List<int> Triangles=new List<int>();internal readonly List<Color> Colors=new List<Color>();internal readonly List<Vector2> Uvs=new List<Vector2>();}
 private static readonly Dictionary<object,OutlineBuffers> outlineBuffers=new Dictionary<object,OutlineBuffers>();
 private static readonly Dictionary<object,List<SelectedLine>> selectedLines=new Dictionary<object,List<SelectedLine>>();
 internal static void BeginNoteFrame(object notes){List<SelectedLine> lines;if(selectedLines.TryGetValue(notes,out lines))lines.Clear();}
 internal static void DisposeNotes(object notes){selectedLines.Remove(notes);outlineBuffers.Remove(notes);}
 internal static void SelectedOutlineLine(object notes,Vector3 a,Vector3 b){List<SelectedLine> lines;if(!selectedLines.TryGetValue(notes,out lines)){lines=new List<SelectedLine>();selectedLines.Add(notes,lines);}lines.Add(new SelectedLine(a,b));}
 internal static bool Hud{get{return Ready&&NativeVisualPlugin.Active&&NativeVisualPlugin.HudEnabled.Value;}}
 internal static object Value(object o,string n){var t=o.GetType();var p=t.GetProperty(n,NativeVisualPlugin.F);return p!=null?p.GetValue(o,null):NativeVisualPlugin.Get(o,n);}
 internal static void Load(string path){
  folder=path;L=JsonUtility.FromJson<PresentationLayout>(File.ReadAllText(Path.Combine(path,"layout.json")));
  var loadImage=typeof(ImageConversion).GetMethod("LoadImage",new[]{typeof(Texture2D),typeof(byte[]),typeof(bool)});
  foreach(var p in Directory.GetFiles(path,"*.png")){
  string imageName=Path.GetFileNameWithoutExtension(p);
  if(imageName.StartsWith("side-guide-",StringComparison.Ordinal)||imageName.StartsWith("key-mask-",StringComparison.Ordinal)||imageName.StartsWith("key-native-",StringComparison.Ordinal))continue;
// Combo pixels contain raw FastText buffer colors, not an sRGB image.
  // FastText.UpdateCommonParametersBuffer + SDFText2 pass these RGB values
  // directly to the fragment output. Avoid decoding them a second time.
  var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,Path.GetFileNameWithoutExtension(p)=="combo-digits");if(!(bool)loadImage.Invoke(null,new object[]{tex,File.ReadAllBytes(p),false}))throw new Exception("Presentation texture failed: "+p);tex.name=Path.GetFileNameWithoutExtension(p);tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;Images.Add(tex.name,tex);}
  NativeComboBlend.Init(path);CreateDifficultySkins();
  foreach(var p in Directory.GetFiles(Path.Combine(path,"Fonts"),"*.ttf")){if(GdiText.AddFontResourceEx(p,0x10,IntPtr.Zero)==0)throw new Exception("Native font registration failed: "+Path.GetFileName(p));fonts.Add(p);}
  Ready=true;
 }
 // Use FBD's transparent contour for every difficulty, including the jacket stripe
 // and short label plate. A filled rectangle would cover the title/artist panel.
 internal static void CreateDifficultySkins(){
  Texture2D source;
  if(!Images.TryGetValue("difficulty-fbd",out source))throw new Exception("FBD difficulty contour missing");
  string[] names={"min","evo","ult","unknown"};
  Color[] colors={new Color(.29f,.63f,.79f),new Color(21/255f,192/255f,179/255f),new Color(185/255f,103/255f,215/255f),new Color(.38f,.42f,.47f)};
  float baseHue,baseSat,baseValue;Color.RGBToHSV(new Color(.93f,.30f,.41f),out baseHue,out baseSat,out baseValue);
  var pixels=source.GetPixels();
  for(int n=0;n<names.Length;n++){
   string key="difficulty-"+names[n];if(Images.ContainsKey(key))continue;
   float targetHue,targetSat,targetValue;Color.RGBToHSV(colors[n],out targetHue,out targetSat,out targetValue);
   var recolored=new Color[pixels.Length];
   for(int i=0;i<pixels.Length;i++){
    var p=pixels[i];float hue,sat,value;Color.RGBToHSV(p,out hue,out sat,out value);
    var c=Color.HSVToRGB(Mathf.Repeat(hue+targetHue-baseHue,1),Mathf.Clamp01(sat*targetSat/baseSat),Mathf.Clamp01(value*targetValue/baseValue));
    c.a=p.a;recolored[i]=c;
   }
   var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
   texture.name=key;texture.SetPixels(recolored);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
   Images.Add(key,texture);
  }
 }
 internal static void RowText(Rect r,string text,string face,float size,Color color,float baseline,bool centered=false){
  if(string.IsNullOrEmpty(text))return;
  var glyphs=text.Select(c=>GdiText.Glyph(face,c,L.fallbackFaces)).ToArray();
  float natural=glyphs.Sum(g=>g.Advance),scale=Mathf.Min(size/72f,r.width/Mathf.Max(1,natural));
  int inkBottom=glyphs.Where(g=>g.HasInk).Select(g=>g.InkBottom).DefaultIfEmpty(0).Max();
  float x=r.x+(centered?(r.width-natural*scale)*.5f:0),y=baseline-inkBottom*scale;
  var old=GUI.color;GUI.color=color;
  foreach(var g in glyphs){GUI.DrawTexture(new Rect(x-8*scale,y,g.Texture.width*scale,96*scale),g.Texture,ScaleMode.StretchToFill,true);x+=g.Advance*scale;}
  GUI.color=old;
 }
 internal static Rect R(object boot,float[] a){
  float s=NativeVisualPlugin.N(boot,"hudScale");
  var camera=(Camera)Value(boot,"ViewCamera");
  if(!camera){var origin=(Vector2)NativeVisualPlugin.Get(boot,"hudOrigin");return new Rect(origin.x+a[0]*s,origin.y+a[1]*s,a[2]*s,a[3]*s);}
  var viewport=camera.pixelRect;float top=Screen.height-viewport.yMax;
  float x=a[0]>=1000?viewport.xMax-(1920-a[0])*s:viewport.x+a[0]*s;
  return new Rect(x,top+a[1]*s,a[2]*s,a[3]*s);
 }
 internal static void Layout(object boot){NativeVisualPlugin.Set(boot,"hudScoreRect",R(boot,L.score));NativeVisualPlugin.Set(boot,"hudSongRect",R(boot,L.song));NativeVisualPlugin.Set(boot,"playButtonRect",R(boot,L.play));NativeVisualPlugin.Set(boot,"progressRect",NativeProgress.Hit(boot));}
 internal static void Image(Rect r,string name){Texture2D tex;if(Images.TryGetValue(name,out tex))GUI.DrawTexture(r,tex,ScaleMode.StretchToFill,true);}
 internal static void Fill(Rect r,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
 internal static void Line(Vector2 a,Vector2 b,Color color,float thickness){var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);Fill(new Rect(a.x,a.y,(b-a).magnitude,thickness),color);GUI.matrix=matrix;}
 internal static void Text(Rect r,string text,string face,float size,Color color,bool centered=false){
  if(string.IsNullOrEmpty(text))return;var glyphs=text.Select(c=>GdiText.Glyph(face,c,L.fallbackFaces)).ToArray();float natural=glyphs.Sum(g=>g.Advance);float scale=Mathf.Min(size/72f,r.width/Mathf.Max(1,natural));float x=r.x+(centered?(r.width-natural*scale)*.5f:0),y=r.y+(r.height-96*scale)*.5f;
  var old=GUI.color;GUI.color=color;foreach(var g in glyphs){GUI.DrawTexture(new Rect(x-8*scale,y,g.Texture.width*scale,96*scale),g.Texture,ScaleMode.StretchToFill,true);x+=g.Advance*scale;}GUI.color=old;
 }
 // HUD label coordinates specify visible ink tops, not padded 96px glyph boxes.
 // Equal horizontal/vertical scaling retains the font's natural proportions.
 internal static void InkText(Rect r,string text,string face,float size,Color color,bool centered=false){
  if(string.IsNullOrEmpty(text))return;
  var glyphs=text.Select(c=>GdiText.Glyph(face,c,L.fallbackFaces)).ToArray();
  float natural=glyphs.Sum(g=>g.Advance),scale=Mathf.Min(size/72f,r.width/Mathf.Max(1,natural));
  int inkTop=glyphs.Where(g=>g.HasInk).Select(g=>g.InkTop).DefaultIfEmpty(0).Min();
  float x=r.x+(centered?(r.width-natural*scale)*.5f:0),y=r.y-inkTop*scale;
  var old=GUI.color;GUI.color=color;
  foreach(var g in glyphs){GUI.DrawTexture(new Rect(x-8*scale,y,g.Texture.width*scale,96*scale),g.Texture,ScaleMode.StretchToFill,true);x+=g.Advance*scale;}
  GUI.color=old;
 }
 private static string Score(object boot){
  string score=null;var timeline=NativeVisualPlugin.Get(boot,"comboTimeline");double now=NativeVisualPlugin.P(boot,"CurrentMs");
  if(timeline!=null&&(bool)Value(timeline,"Supported")){long current=Convert.ToInt64(Value(NativeVisualPlugin.Call(timeline,"At",now),"Total"));long total=Convert.ToInt64(Value(Value(timeline,"Totals"),"Total"));long value=(long)NativeVisualPlugin.T("Core.PreviewScore").GetMethod("At",NativeVisualPlugin.F).Invoke(null,new object[]{current,total});score=Format(value);}
  var evidence=NativeVisualPlugin.Get(boot,"scoreEvidence");if(evidence!=null&&(bool)NativeVisualPlugin.Get(boot,"evidenceMatches")){var observation=NativeVisualPlugin.Call(evidence,"At",now);if(observation!=null)score=Format(Convert.ToInt64(Value(observation,"Score")));}
  return score??"---'---'---";
 }
 private static string Format(long value){return (string)NativeVisualPlugin.T("Core.ScoreEvidence").GetMethod("Format",NativeVisualPlugin.F).Invoke(null,new object[]{value});}
 private static void Digits(Rect r,string text){
  const string chars="0123456789'-";float total=0;foreach(char c in text){int i=chars.IndexOf(c);total+=L.scoreAdvances[i<0?11:i];}float scale=Mathf.Min(r.height/80f,r.width/Mathf.Max(1,total));float x=r.x,top=r.y+(r.height-80*scale)*.5f;
  foreach(char c in text){int i=chars.IndexOf(c);if(i<0)i=11;GUI.DrawTextureWithTexCoords(new Rect(x-24*scale,top-30*scale,128*scale,128*scale),Images["score-digits"],new Rect((i%6)/6f,1-(i/6+1)/2f,1/6f,.5f),true);x+=L.scoreAdvances[i]*scale;}
 }
 internal static void DrawHud(object boot){
  if(!(bool)NativeVisualPlugin.Get(boot,"guiShowSongHud"))return;NativeVisualPlugin.Call(boot,"EnsureHudAssets");var old=GUI.color;GUI.color=Color.white;
  try{
   Image(R(boot,L.scoreFrame),"persistent-ui-topleft-freeplay");Image(R(boot,L.song),"persistent-ui-topright");Image(R(boot,L.songBacking),"song-backing-clean");
   InkText(R(boot,L.scoreLabel),"SCORE",L.titleFace,11*NativeVisualPlugin.N(boot,"hudScale"),new Color(.69f,.79f,.84f));Digits(R(boot,L.digits),Score(boot));
   float s=NativeVisualPlugin.N(boot,"hudScale");
   int index=(int)NativeVisualPlugin.Get(boot,"guiSongIndex");var codes=(string[])NativeVisualPlugin.T("Core.SongFolderProject").GetField("Codes",NativeVisualPlugin.F).GetValue(null);string code=index<0?"—":codes[Mathf.Clamp(index,0,codes.Length-1)];
   var badge=R(boot,L.difficulty);string skin="difficulty-"+code.ToLowerInvariant();Image(badge,Images.ContainsKey(skin)?skin:"difficulty-unknown");
   var jacket=NativeVisualPlugin.Get(boot,"songJacket") as Texture2D;var jr=R(boot,L.jacket);var jf=R(boot,L.jacketFrame);Fill(jf,new Color(.53f,.85f,.85f,.5f));if(jacket)GUI.DrawTexture(jr,jacket,ScaleMode.ScaleAndCrop,true);Image(jf,"cornerbrackets-xsmall-ingame-cards");
   InkText(R(boot,L.title),Convert.ToString(NativeVisualPlugin.Get(boot,"guiSongTitle")),L.titleFace,18.75f*s,new Color(.996f,.996f,.996f));InkText(R(boot,L.artist),Convert.ToString(NativeVisualPlugin.Get(boot,"guiSongArtist")),L.artistFace,12.8f*s,new Color(.749f,.749f,.749f));
   float rowBaseline=R(boot,L.difficultyText).y+10.825f*s;
   RowText(R(boot,L.difficultyText),code,L.titleFace,10.3f*s,new Color(.915f,.915f,.915f),rowBaseline);
   RowText(R(boot,L.level),Convert.ToString(NativeVisualPlugin.Get(boot,"guiSongRating")),L.titleFace,10.3f*s,new Color(.915f,.915f,.915f),rowBaseline,true);
   var profile=NativeVisualPlugin.Get(boot,"Profile");
   RowText(R(boot,L.speed),"SPEED",L.titleFace,9.25f*s,new Color(.56f,.62f,.66f),rowBaseline);
   RowText(R(boot,L.speedValue),NativeVisualPlugin.N(profile,"scrollMultiplier").ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),L.titleFace,10.3f*s,new Color(.788f,.875f,.890f,.9f),rowBaseline);

  }finally{GUI.color=old;}
 }
 internal static void DrawPlay(object boot){
  var r=R(boot,L.play);var old=GUI.color;GUI.color=new Color(.055f,.115f,.145f,.96f);var fill=NativeVisualPlugin.Get(boot,"playNookFill") as Texture2D;
  if(!fill){fill=Resources.Load<Texture2D>("InFalsusStudio/Hud/PlayNookFill");NativeVisualPlugin.Set(boot,"playNookFill",fill);}if(fill)GUI.DrawTexture(r,fill,ScaleMode.StretchToFill,true);GUI.color=Color.white;
  float t=NativeVisualPlugin.N(boot,"hudScale");var points=new[]{new Vector2(r.x,r.y),new Vector2(r.x+r.width*.78f,r.y),new Vector2(r.xMax,r.yMax),new Vector2(r.x,r.yMax)};for(int i=0;i<4;i++)Line(points[i],points[(i+1)%4],new Color(.5f,.82f,.86f),t);
  bool playing=(bool)Value(NativeVisualPlugin.Get(boot,"transport"),"Playing");var icon=new Rect(r.x+r.width*.29f,r.y+r.height*.22f,r.height*.5f,r.height*.5f);NativeVisualPlugin.Call(boot,"DrawSmallIcon",icon,playing?"pause":"play",new Color(.6f,.94f,.98f));GUI.color=old;
 }
 // Progress rendering belongs to the native visual module. Original seeking
 // and drag events continue through StudioBootstrap.DrawSongProgress.
 internal static void DrawProgress(object boot){
  if(NativeProgress.Embedded){NativeProgress.Draw(boot);return;}
  Rect rect=(Rect)NativeVisualPlugin.Get(boot,"progressRect");if(rect.width<=0||rect.height<=0)return;
  double length=Math.Max(1,Convert.ToDouble(NativeVisualPlugin.Get(boot,"durationMs"))),now=NativeVisualPlugin.P(boot,"CurrentMs");
  float ratio=(float)Math.Max(0,Math.Min(1,now/length)),unit=NativeVisualPlugin.N(boot,"hudScale")*.75f;
  Fill(rect,new Color(16/255f,46/255f,56/255f,235/255f));
  Fill(new Rect(rect.x,rect.y,rect.width*ratio,rect.height),new Color(97/255f,200/255f,211/255f));
  Fill(new Rect(rect.x,rect.yMax-unit,rect.width,unit),new Color(76/255f,119/255f,131/255f,94/255f));
  Fill(new Rect(Mathf.Clamp(rect.x+rect.width*ratio-1.5f*unit,rect.x,rect.xMax-3*unit),rect.y,3*unit,rect.height),new Color(196/255f,244/255f,244/255f));
  string text=(bool)Value(boot,"HasDocument")?now.ToString("0",System.Globalization.CultureInfo.InvariantCulture)+" / "+length.ToString("0",System.Globalization.CultureInfo.InvariantCulture)+" ms":"未打开谱面";
  // Dark ink outline preserves legibility over both cyan fill and dark track.
  float outline=Mathf.Max(1,1.25f*unit);
  for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)if(x!=0||y!=0)ProgressText(new Rect(rect.x+x*outline,rect.y+y*outline,rect.width,rect.height),text,14*unit,new Color(5/255f,21/255f,29/255f));
  ProgressText(rect,text,14*unit,new Color(239/255f,1,1));
 }
 internal static void ProgressText(Rect r,string text,float size,Color color){
  var glyphs=text.Select(c=>GdiText.Glyph("Bai Jamjuree Medium",c,L.fallbackFaces)).ToArray();
  float natural=glyphs.Sum(g=>g.Advance),scale=Mathf.Min(size/72f,r.width/Mathf.Max(1,natural));
  int top=glyphs.Where(g=>g.HasInk).Select(g=>g.InkTop).DefaultIfEmpty(0).Min(),bottom=glyphs.Where(g=>g.HasInk).Select(g=>g.InkBottom).DefaultIfEmpty(0).Max();
  float pen=r.x+(r.width-natural*scale)*.5f,y=r.center.y-(bottom-top)*scale*.5f-top*scale;var old=GUI.color;GUI.color=color;
  foreach(var glyph in glyphs){GUI.DrawTexture(new Rect(pen-8*scale,y,glyph.Texture.width*scale,96*scale),glyph.Texture);pen+=glyph.Advance*scale;}GUI.color=old;
 }
 internal static bool Contains(object boot,Vector2 point){var r=R(boot,L.play);if(!r.Contains(point))return false;float right=r.x+r.width*(.78f+.22f*(point.y-r.y)/r.height);return point.x<=right;}
 internal static Rect Map(object boot,float x,float y,float w,float h,Rect original){
  if(!Scope)return original;
  if(Mathf.Abs(x-1338)<.1f&&Mathf.Abs(y-11)<.1f)return R(boot,L.jacket);
  if(Mathf.Abs(x-1460)<.1f&&Mathf.Abs(y-19)<.1f)return NativeHudControls.TitleRect(R(boot,L.title),R(boot,L.artist),NativeVisualPlugin.N(boot,"hudScale"));
  if(Mathf.Abs(x-1460)<.1f&&Mathf.Abs(y-51)<.1f)return NativeHudControls.ArtistRect(R(boot,L.title),R(boot,L.artist),NativeVisualPlugin.N(boot,"hudScale"));
  if(Mathf.Abs(x-1483)<.1f&&(y==83||y==87))return R(boot,L.level);
  if(Mathf.Abs(x-1668)<.1f&&(y==86||y==87))return R(boot,L.designer);
  return original;
 }
 internal static void Backdrop(object boot){if(Ready){NativeCenter.Draw(boot);NativeComboBlend.View((Camera)Value(boot,"ViewCamera"));}}
 internal static void SelectionOutlines(object notes){
  if(!NativeVisualPlugin.Active||(bool)NativeVisualPlugin.Get(notes,"ghost"))return;
  List<SelectedLine> lines;if(!selectedLines.TryGetValue(notes,out lines)||lines.Count==0)return;
  OutlineBuffers buffers;if(!outlineBuffers.TryGetValue(notes,out buffers))outlineBuffers[notes]=buffers=new OutlineBuffers();var vertices=buffers.Vertices;var triangles=buffers.Triangles;vertices.Clear();triangles.Clear();buffers.Colors.Clear();buffers.Uvs.Clear();
  var camera=(Camera)NativeVisualPlugin.Get(notes,"camera");var profile=NativeVisualPlugin.Get(notes,"p");float near=NativeVisualPlugin.N(profile,"stageNear"),far=NativeVisualPlugin.N(profile,"stageFar");float halfPixels=Mathf.Max(.8f,camera.pixelHeight/1080f);
  foreach(var line in lines){
   var a=line.A;var b=line.B;if((a.z<near&&b.z<near)||(a.z>far&&b.z>far))continue;
   if(a.z<near)a=Vector3.Lerp(a,b,(near-a.z)/(b.z-a.z));if(a.z>far)a=Vector3.Lerp(a,b,(far-a.z)/(b.z-a.z));if(b.z<near)b=Vector3.Lerp(b,a,(near-b.z)/(a.z-b.z));if(b.z>far)b=Vector3.Lerp(b,a,(far-b.z)/(a.z-b.z));
   var sa=camera.WorldToScreenPoint(a);var sb=camera.WorldToScreenPoint(b);if(sa.z<=camera.nearClipPlane||sb.z<=camera.nearClipPlane)continue;var delta=new Vector2(sb.x-sa.x,sb.y-sa.y);if(delta.sqrMagnitude<.01f)continue;var normal=new Vector2(-delta.y,delta.x).normalized*halfPixels;int start=vertices.Count;
   vertices.Add(camera.ScreenToWorldPoint(new Vector3(sa.x+normal.x,sa.y+normal.y,sa.z)));vertices.Add(camera.ScreenToWorldPoint(new Vector3(sb.x+normal.x,sb.y+normal.y,sb.z)));vertices.Add(camera.ScreenToWorldPoint(new Vector3(sb.x-normal.x,sb.y-normal.y,sb.z)));vertices.Add(camera.ScreenToWorldPoint(new Vector3(sa.x-normal.x,sa.y-normal.y,sa.z)));triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
  }
  if(triangles.Count==0)return;
  if(!selectionMaterial){selectionMaterial=new Material(Shader.Find("UI/Default"));selectionMaterial.name="Selected bright orange outline";selectionMaterial.mainTexture=Texture2D.whiteTexture;selectionMaterial.SetColor("_Color",PresentationHooks.SelectedColor);selectionMaterial.SetFloat("unity_GUIZTestMode",(float)CompareFunction.Always);selectionMaterial.renderQueue=3100;}
  var slot=NativeVisualPlugin.Visuals(notes).Take("selectionoutline",selectionMaterial);slot.Mesh.Clear();slot.Mesh.SetVertices(vertices);slot.Mesh.SetTriangles(triangles,0);for(int i=0;i<vertices.Count;i++){buffers.Colors.Add(Color.white);buffers.Uvs.Add(Vector2.zero);}slot.Mesh.SetColors(buffers.Colors);slot.Mesh.SetUVs(0,buffers.Uvs);slot.Mesh.RecalculateBounds();
 }
 internal static void Dispose(){NativeComboBlend.Dispose();Ready=false;selectedLines.Clear();outlineBuffers.Clear();foreach(var p in fonts)GdiText.RemoveFontResourceEx(p,0x10,IntPtr.Zero);fonts.Clear();GdiText.Dispose();foreach(var t in Images.Values)UnityEngine.Object.Destroy(t);Images.Clear();NativeCenter.Dispose();if(selectionMaterial)UnityEngine.Object.Destroy(selectionMaterial);}
}
internal static class PresentationHooks {
 internal static readonly Color SelectedColor=new Color(1f,.55f,.035f,1f);
 private static readonly HashSet<object> selecting=new HashSet<object>();
 private static Shader comboOriginalShader;
 internal static void BeginSelected(object __instance,object __0,ref bool __state){__state=selecting.Contains(__instance);if(NativeVisualPlugin.Active&&(bool)NativeVisualPlugin.Call(__instance,"IsSelected",(int)NativeVisualPlugin.Get(__0,"SourceId")))selecting.Add(__instance);}
 internal static void EndSelected(object __instance,bool __state){if(!__state)selecting.Remove(__instance);}
 internal static void SelectedEdge(object __instance,ref Color __2){if(selecting.Contains(__instance))__2=SelectedColor;}
 internal static void Layout(object __instance){if(NativePresentation.Hud)NativePresentation.Layout(__instance);}
 internal static bool Hud(object __instance){if(!NativePresentation.Hud)return true;NativePresentation.Scope=true;if(Event.current.type!=EventType.Repaint)return true;NativePresentation.DrawHud(__instance);return true;}
 internal static void EndHud(){NativePresentation.Scope=false;}
 internal static void MapRect(object __instance,float __0,float __1,float __2,float __3,ref Rect __result){if(NativePresentation.Hud)__result=NativePresentation.Map(__instance,__0,__1,__2,__3,__result);}
 internal static bool Play(object __instance){if(!NativePresentation.Hud)return true;NativeVisualPlugin.Set(__instance,"playButtonRect",NativePresentation.R(__instance,NativePresentation.L.play));if(Event.current.type!=EventType.Repaint)return true;NativeVisualPlugin.Set(__instance,"playControlId",GUIUtility.GetControlID("IfStudio.Play1".GetHashCode(),FocusType.Passive));NativePresentation.DrawPlay(__instance);return false;}
 internal static bool PlayContains(object __instance,Vector2 __0,ref bool __result){if(!NativePresentation.Hud)return true;__result=NativePresentation.Contains(__instance,__0);return false;}
 internal static bool Progress(object __instance){
  if(!NativePresentation.Hud)return true;
  NativeVisualPlugin.Set(__instance,"progressRect",NativeProgress.Hit(__instance));
  if(Event.current.type!=EventType.Repaint)return true;
  NativeVisualPlugin.Set(__instance,"progressControlId",GUIUtility.GetControlID("InFalsusStudio.Progress07".GetHashCode(),FocusType.Passive));
  NativePresentation.DrawProgress(__instance);return false;
 }
 internal static void BlockProgress(object __instance,Vector2 __0,ref bool __result){if(NativePresentation.Hud)__result=__result||NativeProgress.Hit(__instance).Contains(__0)||NativePresentation.Contains(__instance,__0);}
 internal static void Backdrop(object __instance){if(NativePresentation.Ready)NativePresentation.Backdrop(__instance);}
 internal static void Combo(object __instance,int __0,bool __1,float __2,ref bool __state){
  __state=false;if(!NativePresentation.Ready)return;var material=(Material)NativeVisualPlugin.Get(__instance,"material");bool active=NativeVisualPlugin.Active&&NativeVisualPlugin.ComboEnabled.Value;
  if(!comboOriginalShader)comboOriginalShader=material.shader;var shader=active?NativeComboBlend.Shader:comboOriginalShader;NativeComboBlend.Request(active&&__1&&__0>0);
  if(material.shader!=shader){material.shader=shader;material.renderQueue=2940;NativeVisualPlugin.Set(__instance,"previousCount",-1);}
  if(active)NativeComboBlend.Bind(material);
  Texture texture=active?NativePresentation.Images["combo-digits"]:Resources.Load<Texture2D>("InFalsusStudio/Hud/ComboDigits");if(material.GetTexture("_MainTex")!=texture){material.SetTexture("_MainTex",texture);NativeVisualPlugin.Set(__instance,"previousCount",-1);}
  __state=active&&__1&&__0>0&&((int)NativeVisualPlugin.Get(__instance,"previousCount")!=__0||NativeVisualPlugin.N(__instance,"previousHalfWidth")!=__2);
 }
 internal static void ComboUv(object __instance,int __0,bool __state){
  if(!__state)return;var mesh=(Mesh)NativeVisualPlugin.Get(__instance,"mesh");var uv=mesh.uv;string digits=__0.ToString(System.Globalization.CultureInfo.InvariantCulture);
  for(int j=0;j<digits.Length;j++){int digit=digits[j]-'0',column=digit%5;for(int k=0;k<4;k++){int i=j*4+k;float local=uv[i].x*5-column;uv[i].x=(column+Mathf.Lerp(NativePresentation.L.comboCrop[0],NativePresentation.L.comboCrop[1],local))/5;}}
  mesh.uv=uv;
  // The original layout gives every digit the same advance. Native text
  // uses the font's proportional advances (particularly narrow for 1).
  var v=mesh.vertices;float pixelScale=Mathf.Abs(v[1].x-v[0].x)/(256*(NativePresentation.L.comboCrop[1]-NativePresentation.L.comboCrop[0]));
  float total=digits.Sum(c=>NativePresentation.L.comboAdvances[c-'0'])*pixelScale;float fit=Mathf.Min(1,NativeVisualPlugin.N(__instance,"previousHalfWidth")*1.56f/Mathf.Max(.0001f,total));pixelScale*=fit;total*=fit;float pen=-total*.5f;
  for(int j=0;j<digits.Length;j++){float advance=NativePresentation.L.comboAdvances[digits[j]-'0']*pixelScale,center=pen+advance*.5f,oldCenter=(v[j*4].x+v[j*4+1].x)*.5f;for(int k=0;k<4;k++){int i=j*4+k;v[i].x=center+(v[i].x-oldCenter)*fit;}pen+=advance;}
  mesh.vertices=v;mesh.colors=Enumerable.Repeat(Color.white,v.Length).ToArray();mesh.RecalculateBounds();
 }
}
// Private process fonts + GDI grayscale coverage. No OS font installation.
internal static class GdiText {
 [StructLayout(LayoutKind.Sequential)]struct Size {internal int X,Y;}
 [StructLayout(LayoutKind.Sequential)]struct Header {internal uint Size;internal int Width,Height;internal ushort Planes,Bits;internal uint Compression,ImageSize;internal int XPels,YPels;internal uint Colors,Important;}
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]internal static extern int AddFontResourceEx(string file,uint flags,IntPtr reserved);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]internal static extern bool RemoveFontResourceEx(string file,uint flags,IntPtr reserved);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr hdc);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr hdc);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateFont(int h,int w,int e,int o,int weight,uint italic,uint underline,uint strike,uint charset,uint output,uint clip,uint quality,uint pitch,string face);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern bool GetTextExtentPoint32(IntPtr dc,string text,int count,out Size size);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern uint GetGlyphIndices(IntPtr dc,string text,int count,[Out]ushort[] indices,uint flags);
 [DllImport("gdi32.dll")]static extern IntPtr CreateDIBSection(IntPtr dc,ref Header info,uint usage,out IntPtr bits,IntPtr section,uint offset);
 [DllImport("gdi32.dll")]static extern uint SetTextColor(IntPtr dc,uint color);
 [DllImport("gdi32.dll")]static extern int SetBkMode(IntPtr dc,int mode);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern bool TextOut(IntPtr dc,int x,int y,string text,int count);
 [DllImport("gdi32.dll")]static extern bool GdiFlush();
 internal sealed class Letter {internal Texture2D Texture;internal float Advance;internal int InkTop,InkBottom;internal bool HasInk;}
 private static Dictionary<string,Letter> letters=new Dictionary<string,Letter>();
 internal static Letter Glyph(string face,char c,string[] fallback){
  string key=face+":"+c;Letter result;if(letters.TryGetValue(key,out result))return result;
  IntPtr dc=CreateCompatibleDC(IntPtr.Zero),font=IntPtr.Zero,bmp=IntPtr.Zero,oldFont=IntPtr.Zero,oldBmp=IntPtr.Zero;
  try{
   foreach(string choice in new[]{face}.Concat(fallback)){if(font!=IntPtr.Zero){SelectObject(dc,oldFont);DeleteObject(font);}font=CreateFont(-72,0,0,0,400,0,0,0,1,0,0,4,0,choice);if(font==IntPtr.Zero)throw new Exception("CreateFont failed");oldFont=SelectObject(dc,font);var indexes=new ushort[1];GetGlyphIndices(dc,c.ToString(),1,indexes,1);if(indexes[0]!=0xffff)break;}
   Size size;if(!GetTextExtentPoint32(dc,c.ToString(),1,out size))throw new Exception("Native glyph measurement failed");int width=Math.Max(16,size.X+16),height=96;var header=new Header{Size=40,Width=width,Height=-height,Planes=1,Bits=32,ImageSize=(uint)(width*height*4)};IntPtr bits;bmp=CreateDIBSection(dc,ref header,0,out bits,IntPtr.Zero,0);if(bmp==IntPtr.Zero)throw new Exception("Native glyph DIB failed");oldBmp=SelectObject(dc,bmp);var data=new byte[width*height*4];Marshal.Copy(data,0,bits,data.Length);SetBkMode(dc,1);SetTextColor(dc,0xffffff);TextOut(dc,8,0,c.ToString(),1);GdiFlush();Marshal.Copy(bits,data,0,data.Length);
   int inkTop=96,inkBottom=0;bool hasInk=false;
   for(int y=0;y<96;y++)for(int x=0;x<width;x++){int j=(y*width+x)*4;if(Math.Max(data[j],Math.Max(data[j+1],data[j+2]))>0){inkTop=Math.Min(inkTop,y);inkBottom=Math.Max(inkBottom,y+1);hasInk=true;}}
   for(int i=0;i<data.Length;i+=4){byte alpha=Math.Max(data[i],Math.Max(data[i+1],data[i+2]));data[i]=data[i+1]=data[i+2]=255;data[i+3]=alpha;}
   // DIB is top-down, Unity raw texture rows are bottom-up.
   var rgba=new byte[data.Length];int stride=width*4;for(int y=0;y<height;y++)Buffer.BlockCopy(data,y*stride,rgba,(height-1-y)*stride,stride);
   var tex=new Texture2D(width,height,TextureFormat.RGBA32,false);tex.LoadRawTextureData(rgba);tex.Apply();tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;result=new Letter{Texture=tex,Advance=size.X,InkTop=inkTop,InkBottom=inkBottom,HasInk=hasInk};letters.Add(key,result);return result;
  }finally{if(oldBmp!=IntPtr.Zero)SelectObject(dc,oldBmp);if(bmp!=IntPtr.Zero)DeleteObject(bmp);if(oldFont!=IntPtr.Zero)SelectObject(dc,oldFont);if(font!=IntPtr.Zero)DeleteObject(font);DeleteDC(dc);}
 }
 internal static void Dispose(){foreach(var g in letters.Values)UnityEngine.Object.Destroy(g.Texture);letters.Clear();}
}
}
