using System;
using System.Collections.Generic;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed class NoteRenderer : IDisposable
    {
        private readonly MeshBatch ground, air, edges, grid, shadows, skySkin;
        private readonly MeshBatch[] groundSkins;
        public bool ShowGroundProjections=true;
        private readonly Camera camera;
        private readonly bool ghost;
        private SkyConnections connections;
        public void UpdateConnections(IList<SpcEvent> source){connections=new SkyConnections(source);}
        private readonly StudioProfile p;
        private readonly List<HitTriangle> hits = new List<HitTriangle>();
        private readonly List<SpcEvent> visible = new List<SpcEvent>();
        private struct HitTriangle { public Vector3 A,B,C; public int Id; public bool Air; }
        public int RenderedNotes {get;private set;}
        public int InvalidNotes {get;private set;}
        public int OutOfBoundsNotes {get;private set;}
        public int CrossingSegments {get;private set;}
        public int TriangleCount {get{int n=ground.Triangles.Count+air.Triangles.Count+edges.Triangles.Count+skySkin.Triangles.Count;foreach(var b in groundSkins)n+=b.Triangles.Count;return n/3;}}
        private ScrollTimeline scroll;
        private double now;
        private bool hidePast;
        private AutoplayCursor visualCursor;
        private AuthoringGrid authoringGrid;
        private int gridDivision=4;
        private double gridEndTime;
        public int DrawnGridLines {get;private set;}
        public int DrawnMeasureLines {get;private set;}
        private int selected;
        private ISet<int> multiSelected;
        private bool IsSelected(int id) { return id==selected || multiSelected!=null && multiSelected.Contains(id); }

        public NoteRenderer(Transform parent, StudioProfile profile, StudioMaterials materials, Camera cam,bool ghostPreview=false)
        {
            p=profile;camera=cam;ghost=ghostPreview;
            ground=new MeshBatch(parent,"Tap and Hold surfaces",ghost?materials.Transparent:materials.Opaque,ghost?30:0);
            air=new MeshBatch(parent,"Flick and cursor surfaces",materials.Transparent,ghost?32:11);
            edges=new MeshBatch(parent,"Note luminous borders",materials.Glow,ghost?32:20);
            grid=new MeshBatch(parent,"Beat grid and preview cursor",materials.Transparent,2);
            shadows=new MeshBatch(parent,"Air note footprint on central floor",materials.Transparent,-5);
            skySkin=new MeshBatch(parent,"SkyArea translucent field / active state",materials.SkySkin,ghost?31:10);
            var skins=new[]{materials.HoldBlue,materials.HoldPurple,materials.HoldPink,materials.TapBlue,materials.TapWideMint,materials.TapPurple,materials.TapPink};
            groundSkins=new MeshBatch[skins.Length];
            for(int i=0;i<skins.Length;i++)groundSkins[i]=new MeshBatch(parent,"Note skin / "+((GroundSkin104)i),skins[i],ghost?30:3);
            foreach(var batch in AllBatches()){batch.ClipZ=true;batch.MinZ=p.stageNear;batch.MaxZ=p.stageFar;}
        }
        private IEnumerable<MeshBatch> AllBatches()
        {
            yield return ground;yield return air;yield return edges;yield return grid;yield return shadows;yield return skySkin;
            foreach(var b in groundSkins)yield return b;

        }
        private float Z(double t){return (float)(scroll.Delta(t,now)*p.UnitsPerMs);}
        private Vector3 SkyPoint(double normalized,double time){return new Vector3(StageSpace.AirX(p,normalized),p.skyHeight+.006f,Z(time));}
        private static Color Alpha(Color c,float a){c.a=a;return c;}
        private void PickQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int id,bool isAir)
        {
            if(isAir)return; // Air hits come from the ACTUAL clipped rendered triangles.
            hits.Add(new HitTriangle{A=a,B=b,C=c,Id=id,Air=isAir});hits.Add(new HitTriangle{A=a,B=c,C=d,Id=id,Air=isAir});
        }
        public void Draw(IList<SpcEvent> events, ScrollTimeline timeline, BeatTimeline beats, double timeMs, bool showNotes, bool removePast, bool showGrid, int selectedId, float cursorNorm=.5f, ISet<int> selection=null, AuthoringGrid editingGrid=null, int subdivision=4, AutoplayCursor autoMotion=null,bool persistentBars=true,bool drawCursor=true,ISet<int> ignored=null)
        {
            scroll=timeline;now=timeMs;hidePast=removePast;visualCursor=autoMotion;selected=selectedId;multiSelected=selection;
            foreach(var batch in AllBatches()){batch.Clear();batch.MinZ=p.stageNear;batch.MaxZ=p.stageFar;}
            SetAirClip(false);hits.Clear();RenderedNotes=0;InvalidNotes=0;CrossingSegments=0;OutOfBoundsNotes=0;
            authoringGrid=editingGrid??new AuthoringGrid(beats);gridDivision=Math.Max(1,subdivision);
            gridEndTime=now+Math.Max(10000,p.stageFar/Math.Max(.0001f,p.UnitsPerMs)*2);
            foreach(var e in events)if(e.IsNote&&AuthoringSafety.Reason(e)==null)gridEndTime=Math.Max(gridEndTime,e.EndMs+3000);
            DrawnGridLines=0;DrawnMeasureLines=0;if(showGrid||persistentBars)DrawBeats(showGrid);
            if(showNotes)
            {
                visible.Clear();
                foreach(var e in events)
                {
                    if(!e.IsNote||ignored!=null&&ignored.Contains(e.SourceId))continue;
                    if(ChartMath.InvalidReason(e)!=null){InvalidNotes++;continue;}
                    if(AirReachBounds.Violation(e)!=null)OutOfBoundsNotes++;
                    if(Visible(e))visible.Add(e);
                }
                visible.Sort((a,b)=>Z((b.TimeMs+b.EndMs)*.5).CompareTo(Z((a.TimeMs+a.EndMs)*.5)));
                foreach(var e in visible)
                {
                    bool clipping=e.Kind==EventKind.SkyArea||e.Kind==EventKind.Flick;
                    SetAirClip(clipping);MeshBatch hitSurface=e.Kind==EventKind.SkyArea?skySkin:
                        e.Kind==EventKind.Flick?air:groundSkins[(int)NoteSkinPolicy.Ground(e)];
                    int firstTriangle=hitSurface.Triangles.Count;
                    switch(e.Kind)
                    {
                        case EventKind.Tap:case EventKind.Hold:DrawGround(e);break;
                        case EventKind.SkyArea:DrawSky(e);break;
                        case EventKind.Flick:DrawFlick(e);break;
                    }
                    if(clipping&&ShowGroundProjections&&!ghost)DrawGroundProjection(firstTriangle,e,hitSurface);
                    // Pick the same clipped surface vertices, never offscreen or hidden full-length polygons.
                    for(int i=firstTriangle;i<hitSurface.Triangles.Count;i+=3)
                        hits.Add(new HitTriangle{A=hitSurface.Vertices[hitSurface.Triangles[i]],B=hitSurface.Vertices[hitSurface.Triangles[i+1]],C=hitSurface.Vertices[hitSurface.Triangles[i+2]],Id=e.SourceId,Air=clipping});
                    SetAirClip(false);RenderedNotes++;
                }
            }
            if(drawCursor)DrawCursor(cursorNorm);
            if(ghost)foreach(var batch in AllBatches())for(int i=0;i<batch.Colors.Count;i++){Color c=batch.Colors[i];c.a*=.28f;batch.Colors[i]=c;}
            foreach(var batch in AllBatches())batch.Commit();
        }
        private void DrawGroundProjection(int first,SpcEvent note,MeshBatch surface)
        {
            // Vertical projection: preserve X and Z, replace Y. The air reach x=±2
            // therefore lands exactly on the four-track floor edge, not the side lanes.
            Color tint=note.Kind==EventKind.Flick?(note.Get(4)==4?new Color(.06f,.16f,.12f,.25f):new Color(.18f,.15f,.04f,.25f)):new Color(.12f,.07f,.2f,.27f);
            shadows.ClipX=true;shadows.MinX=-p.centralHalfWidth;shadows.MaxX=p.centralHalfWidth;
            for(int i=first;i<surface.Triangles.Count;i+=3)
            {
                Vector3 a=surface.Vertices[surface.Triangles[i]],b=surface.Vertices[surface.Triangles[i+1]],c=surface.Vertices[surface.Triangles[i+2]];
                a.y=b.y=c.y=.004f;shadows.Triangle(a,b,c,tint,tint,tint);
            }
        }
        public int GroundProjectionVertices {get{return shadows.Vertices.Count;}}
        public bool CheckGroundProjectionBounds()
        {
            foreach(var v in shadows.Vertices)
                if(float.IsNaN(v.x)||float.IsNaN(v.z)||Mathf.Abs(v.y-.004f)>.00001f||v.x<-p.centralHalfWidth-.00001f||v.x>p.centralHalfWidth+.00001f)return false;
            return true;
        }
        private bool Visible(SpcEvent e)
        {
            double expire=e.Kind==EventKind.Flick&&visualCursor!=null?visualCursor.VisualContactTime(e):e.TimeMs;
            if(hidePast&&(e.DurationMs>0?e.EndMs<now:expire<now))return false;
            if(e.DurationMs>0)
            {
                double begin=hidePast?Math.Max(now,e.TimeMs):e.TimeMs;
                return ScrollVisibility.Intersects(scroll,begin,e.EndMs,now,p.UnitsPerMs,p.stageNear,p.stageFar);
            }
            double z=e.Kind==EventKind.Flick?FlickZ(e):scroll.Delta(e.TimeMs,now)*p.UnitsPerMs;
            // Cull before camera-dependent Flick decoration: a 99999 impulse must NOT
            // make later groups tiny but visible outside the distant stage horizon.
            return AuthoringSafety.Finite(z)&&z>=p.stageNear-p.tapDepth&&z<=p.stageFar;
        }
        private bool Active(SpcEvent e){return !ghost&&NoteSkinPolicy.Active(e,now,visualCursor!=null);}
        private Color SurfaceTint(SpcEvent e){return IsSelected(e.SourceId)?new Color(1.18f,1.18f,1.18f,1):Color.white;}
        private void DrawGround(SpcEvent e)
        {
            float width=(float)e.GroundWidth;int lane=e.Lane;
            float inset=(1-p.tapWidthRatio)*width*.5f;
            Func<bool,float,Vector3> point=(right,z)=>StageSpace.FloorBound(p,lane,width,right,z,inset);
            MeshBatch batch=groundSkins[(int)NoteSkinPolicy.Ground(e)];
            bool active=Active(e);Color tint=SurfaceTint(e);
            if(e.Kind==EventKind.Hold&&e.DurationMs>0)
            {
                double begin=hidePast?Math.Max(e.TimeMs,now):e.TimeMs;
                var spans=ScrollVisibility.Between(scroll,begin,e.EndMs,now,p.UnitsPerMs,p.stageNear,p.stageFar);
                foreach(var span in spans)
                {
                    float z0=Mathf.Clamp(Z(span.Start),p.stageNear,p.stageFar),z1=Mathf.Clamp(Z(span.End),p.stageNear,p.stageFar);
                    if(Mathf.Abs(z1-z0)<.0000001f)continue;
                    // Periodic body strip is one texture across the WHOLE event width.
                    // Keep the original phase while clipping an active head or a long pulse.
                    double phase=scroll.Delta(span.Start,e.TimeMs)*p.UnitsPerMs/5.0;
                    float v0=(float)(phase-2*Math.Floor(phase*.5)),v1=v0+(z1-z0)/5f;
                    batch.QuadUv(point(false,z0),point(true,z0),point(true,z1),point(false,z1),
                        new Vector2(0,v0),new Vector2(1,v0),new Vector2(1,v1),new Vector2(0,v1),tint,new Vector2(active?1:0,0));
                }
                // Only REAL endpoints get caps. Clipping at the horizon never invents one.
                double head=scroll.Delta(begin,now)*p.UnitsPerMs;
                if(head>=p.stageNear-p.tapDepth&&head<=p.stageFar)GroundCap(e,batch,point,(float)head,tint,active,1);
                double tail=scroll.Delta(e.EndMs,now)*p.UnitsPerMs;
                if(tail>=p.stageNear&&tail<=p.stageFar)GroundCap(e,batch,point,(float)tail-.06f,tint,active,2);
                if(active)
                {
                    Color c=lane==0?new Color(.78f,.48f,1):lane==5?new Color(1,.32f,.64f):new Color(.32f,.92f,1);
                    Edge(point(false,.012f),point(true,.012f),new Color(c.r,c.g,c.b,.82f),.032f);
                }
            }
            else GroundCap(e,batch,point,Z(e.TimeMs),tint,false,0);
        }
        private void GroundCap(SpcEvent e,MeshBatch batch,Func<bool,float,Vector3> point,float z,Color tint,bool active,int part)
        {
            float depth=e.Kind==EventKind.Hold?(part==2?.09f:.15f):p.tapDepth;
            Vector3 lift=Vector3.up*.001f;
            batch.QuadUv(point(false,z)+lift,point(true,z)+lift,point(true,z+depth)+lift,point(false,z+depth)+lift,
                new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1),tint,new Vector2(active?1:0,part));
        }
        private void Edge(Vector3 a,Vector3 b,Color color,float width)
        {
            edges.Strip(a,b,width*3.5f,Alpha(color,color.a*.14f));edges.Strip(a,b,width,color);
        }
        private void DrawSky(SpcEvent e)
        {
            if(connections!=null&&connections.Starts.Contains(e.SourceId)&&(!hidePast||now<e.TimeMs))DrawSkyBrackets(e);
            double begin=hidePast?Math.Max(e.TimeMs,now):e.TimeMs,end=e.EndMs;
            bool active=Active(e);
            if(end<=begin)
            {
                if(ScrollVisibility.Point(scroll,e.TimeMs,now,p.UnitsPerMs,p.stageNear,p.stageFar))
                {var r=ChartMath.SkyAt(e,e.TimeMs);Edge(SkyPoint(r.Left,e.TimeMs),SkyPoint(r.Right,e.TimeMs),p.skyEdge,p.skyBorderWidth);}
                return;
            }
            var spans=ScrollVisibility.Between(scroll,begin,end,now,p.UnitsPerMs,p.stageNear,p.stageFar);
            foreach(var span in spans)
            {
                // Recurse ONLY over the visible part of each linear scroll section.
                var samples=new List<double>{span.Start};Sample(e,span.Start,span.End,0,samples);
                for(int i=0;i<samples.Count-1;i++)
                {
                    double ta=samples[i],tb=samples[i+1];var ra=ChartMath.SkyAt(e,ta);var rb=ChartMath.SkyAt(e,tb);
                    if(ra.Width < -1e-6||rb.Width < -1e-6){CrossingSegments++;continue;}
                    Vector3 a=SkyPoint(ra.Left,ta),b=SkyPoint(ra.Right,ta),c=SkyPoint(rb.Right,tb),d=SkyPoint(rb.Left,tb);
                    float va=(float)((ta-e.TimeMs)/400.0),vb=(float)((tb-e.TimeMs)/400.0);
                    skySkin.QuadUv(a,b,c,d,new Vector2(0,va),new Vector2(1,va),new Vector2(1,vb),new Vector2(0,vb),SurfaceTint(e),new Vector2(active?1:0,0));
                    Color border=Alpha(p.skyEdge,active?.98f:.90f);
                    Edge(a,d,border,p.skyBorderWidth);Edge(b,c,border,p.skyBorderWidth);
                }
            }
            // Original time ribs, with endpoints culled before float mesh generation.
            int ribs=Math.Min(120,Math.Max(4,(int)(e.DurationMs/90)));
            for(int i=1;i<ribs;i++)
            {
                double t=e.TimeMs+e.DurationMs*i/ribs;
                if(t<begin||!ScrollVisibility.Point(scroll,t,now,p.UnitsPerMs,p.stageNear,p.stageFar))continue;
                var r=ChartMath.SkyAt(e,t);if(r.Width<0)continue;
                grid.Strip(SkyPoint(r.Left,t)+Vector3.up*.003f,SkyPoint(r.Right,t)+Vector3.up*.003f,.005f,new Color(.79f,.56f,1,active?.43f:.30f));
            }
            if(active)
            {
                var r=ChartMath.SkyAt(e,now);
                if(r.Width>=0)
                {
                    Vector3 a=SkyPoint(r.Left,now)+Vector3.up*.009f,b=SkyPoint(r.Right,now)+Vector3.up*.009f;
                    edges.Strip(a,b,.105f,new Color(.64f,.24f,1,.16f));
                    edges.Strip(a,b,.026f,new Color(.9f,.66f,1,.7f));
                    edges.Strip(a,b,.008f,new Color(1,.94f,1,.94f));
                }
            }
            if(IsSelected(e.SourceId))foreach(double t in new[]{begin,end})
            {
                if(!ScrollVisibility.Point(scroll,t,now,p.UnitsPerMs,p.stageNear,p.stageFar))continue;
                var r=ChartMath.SkyAt(e,t);Edge(SkyPoint(r.Left,t),SkyPoint(r.Right,t),Color.white,.009f);
            }
        }
        private void Sample(SpcEvent e,double a,double b,int depth,List<double> target)
        {
            double mid=(a+b)*.5;var ra=ChartMath.SkyAt(e,a);var rb=ChartMath.SkyAt(e,b);var rm=ChartMath.SkyAt(e,mid);
            float error=Mathf.Max(ScreenDeviation(SkyPoint(rm.Left,mid),SkyPoint(ra.Left,a),SkyPoint(rb.Left,b)),ScreenDeviation(SkyPoint(rm.Right,mid),SkyPoint(ra.Right,a),SkyPoint(rb.Right,b)));
            // A world-space length cap also samples fog and degenerate/edge-on segments.
            bool split=error>p.curvePixelTolerance || Mathf.Abs(Z(b)-Z(a))>2f || b-a>80;
            if(split&&depth<10&&b-a>.2&&target.Count<4096){Sample(e,a,mid,depth+1,target);Sample(e,mid,b,depth+1,target);}else target.Add(b);
        }
        private float ScreenDeviation(Vector3 q,Vector3 a,Vector3 b)
        {
            Vector3 qp=camera.WorldToScreenPoint(q),ap=camera.WorldToScreenPoint(a),bp=camera.WorldToScreenPoint(b);
            if(qp.z<=camera.nearClipPlane||ap.z<=camera.nearClipPlane||bp.z<=camera.nearClipPlane)return 0;
            Vector2 v=new Vector2(bp.x-ap.x,bp.y-ap.y),u=new Vector2(qp.x-ap.x,qp.y-ap.y);
            float f=v.sqrMagnitude>1e-8f?Mathf.Clamp01(Vector2.Dot(u,v)/v.sqrMagnitude):0;
            return (u-v*f).magnitude;
        }
        private float FlickZ(SpcEvent e)
        {
            if(hidePast&&visualCursor!=null&&now>=e.TimeMs&&now<=visualCursor.VisualContactTime(e))return 0;
            return Z(e.TimeMs);
        }
        private void DrawFlick(SpcEvent e)
        {
            var range=ChartMath.FlickRange(e);bool right=e.Get(4)==4;
            float xl=StageSpace.AirX(p,range.Left),xr=StageSpace.AirX(p,range.Right),z=FlickZ(e),y=p.skyHeight+.019f;
            Color color=right?p.rightFlick:p.leftFlick;if(IsSelected(e.SourceId))color=Color.Lerp(color,Color.white,.5f);
            const int count=48;
            // High/wide edge on the motion side. It recedes in +Z; ALL vertices share the same Y.
            // 'height' in the old 2D editor is screen height, NOT a world-Y extrusion.
            Func<float,float> x=q=>Mathf.Lerp(right?xr:xl,right?xl:xr,q);
            Func<float,Vector3> back=q=>FlickGeometry.BackVertex(p,camera,xl,xr,y,z,right,q);
            for(int i=0;i<count;i++)
            {
                float u=i/(float)count,v=(i+1)/(float)count;
                Vector3 a=new Vector3(x(u),y,z),b=new Vector3(x(v),y,z),c=back(v),d=back(u);
                Color baseC=color;baseC.a=.54f;Color rim=Color.Lerp(color,Color.white,.45f);rim.a=.65f;
                air.Quad(a,b,c,d,baseC,baseC,rim,rim);PickQuad(a,b,c,d,e.SourceId,true);
                Edge(c,d,Alpha(Color.Lerp(color,Color.white,.62f),.72f),.006f);
                if(i%4==0)grid.Strip(a,d,.008f,Alpha(Color.Lerp(color,Color.black,.45f),.20f));
            }
            Vector3 highBase=new Vector3(x(0),y,z),highBack=back(0);
            Edge(highBase,highBack,new Color(1,1,.92f,.80f),.045f);
            Edge(highBase,new Vector3(x(1),y,z),Alpha(color,.76f),.008f);
        }
        private void DrawBeats(bool snapGrid)
        {
            // Time candidates are generated once, not separately for six lanes. Every
            // timing/BPM event resets LOCAL beat/bar phase; track only maps time to Z.
            // Retain every distinct visible position: no world-distance/pixel thinning.
            double low=p.stageNear/p.UnitsPerMs;
            double high=p.stageFar/p.UnitsPerMs;
            var times=new HashSet<double>();
            Dictionary<double,BeatGridPoint> positions=new Dictionary<double,BeatGridPoint>();
            for(int i=0;i<scroll.Points.Count;i++)
            {
                var point=scroll.Points[i];double a=Math.Max(0,point.Time),relative=scroll.Delta(point.Time,now);
                double b=i+1<scroll.Points.Count?scroll.Points[i+1].Time:gridEndTime;
                if(Math.Abs(point.Speed)<1e-12)
                {if(relative<low||relative>high)continue;}
                else
                {
                    double t0=point.Time+(low-relative)/point.Speed,t1=point.Time+(high-relative)/point.Speed;
                    a=Math.Max(a,Math.Min(t0,t1));
                    // Last nonzero scroll segment can extend to the visible horizon.
                    b=i+1<scroll.Points.Count?Math.Min(b,Math.Max(t0,t1)):Math.Max(t0,t1);
                }
                if(b<a)continue;
                foreach(var line in snapGrid?authoringGrid.Between(a,b,gridDivision):authoringGrid.BarsBetween(a,b))
                {
                    if(!times.Add(line.TimeMs))continue;
                    double z=scroll.Delta(line.TimeMs,now)*p.UnitsPerMs;
                    if(z<p.stageNear-.01f||z>p.stageFar+.01f)continue;
                    // A stop/reversal can place multiple times at exactly the same Z.
                    // Draw one strongest guide there, instead of hundreds of alpha layers.
                    double key=Math.Round(z,7);BeatGridPoint previous;
                    if(!positions.TryGetValue(key,out previous)||line.Priority>previous.Priority||
                       line.Priority==previous.Priority&&line.Segment>previous.Segment)positions[key]=line;
                }
            }
            var depths=new List<double>(positions.Keys);depths.Sort((a,b)=>b.CompareTo(a));
            foreach(double key in depths)
            {
                var line=positions[key];float z=Z(line.TimeMs);
                Color c=new Color(.55f,.55f,.55f,.65f);
                if(snapGrid)switch(line.Kind)
                {
                    case BeatGridKind.Bar:c=new Color(.80f,.50f,1,1);break;
                    case BeatGridKind.Quarter:c=new Color(.16f,.55f,1,1);break;
                    case BeatGridKind.Eighth:c=new Color(.25f,1,.43f,.93f);break;
                    case BeatGridKind.Sixteenth:c=new Color(1,.38f,.45f,.93f);break;
                    default:c=new Color(1,1,1,.88f);break;
                }
                float pixels=!snapGrid?1.15f:line.IsBar?3.5f:line.IsBeat?2.5f:1.65f;
                if(line.IsBar)DrawnMeasureLines++;
                // Do not use .004 world units: perspective made most 1/6 and 1/8 lines
                // subpixel and almost invisible, amplified by old alpha=.20.
                for(int lane=0;lane<6;lane++)
                    grid.ScreenStrip(camera,
                        BeatGuideGeometry.Point(p,lane,0,z),
                        BeatGuideGeometry.Point(p,lane,1,z),pixels,c);
                DrawnGridLines++;
            }
        }
        private void SetAirClip(bool enabled)
        {
            foreach(var batch in new[]{air,skySkin,edges,grid})
            {batch.ClipX=enabled;batch.MinX=-p.centralHalfWidth;batch.MaxX=p.centralHalfWidth;}
        }
        private void DrawCursor(float norm)
        {
            float x=StageSpace.AirX(p,AirReachBounds.Clamp(norm));Color col=p.skyEdge;
            Vector3 a=new Vector3(x-.20f,p.skyHeight+.014f,.15f),b=new Vector3(x+.20f,p.skyHeight+.014f,.15f),c=new Vector3(x,p.skyHeight+.014f,.55f);
            air.Triangle(a,b,c,Alpha(col,.96f),Alpha(col,.96f),new Color(.95f,.85f,1,1));
            Edge(a,c,Alpha(col,.8f),.01f);Edge(b,c,Alpha(col,.8f),.01f);
            // Visual cursor X is evaluated at the current chart time. Yellow ray starts at
            // the arrow's decorative forward tip, on its SAME air plane (not floor Y).
            // Keep the needle within the upper quarter of the calibrated viewport.
            // Its endpoint is derived from the SAME horizontal plane, not a world-Y wall.
            Vector3 projected=camera.WorldToScreenPoint(c);float endZ=8;
            Ray ray=camera.ScreenPointToRay(new Vector3(projected.x,camera.pixelRect.y+camera.pixelRect.height*.75f,0));
            Plane plane=new Plane(Vector3.up,new Vector3(0,c.y,0));float distance;
            if(plane.Raycast(ray,out distance))endZ=ray.GetPoint(distance).z;
            endZ=Mathf.Clamp(endZ,c.z+.05f,p.stageFar);
            grid.Strip(c,new Vector3(x,c.y,endZ),.012f,new Color(1,.84f,.12f,.10f));
            grid.Strip(c,new Vector3(x,c.y,endZ),.005f,new Color(1,.94f,.35f,.88f));
        }
        private void DrawSkyBrackets(SpcEvent e)
        {
            var range=ChartMath.SkyAt(e,e.TimeMs);
            for(int side=0;side<2;side++)
            {
                Vector3 point=SkyPoint(side==0?range.Left:range.Right,e.TimeMs);Vector3 screen=camera.WorldToScreenPoint(point);
                if(screen.z<=camera.nearClipPlane||point.z>p.stageFar||point.z<p.stageNear)return;
                float size=Mathf.Clamp(90/Mathf.Max(.5f,screen.z),2,12),inward=side==0?1:-1;
                Func<float,float,Vector3> at=(dx,dy)=>camera.ScreenToWorldPoint(new Vector3(screen.x+dx,screen.y+dy,screen.z-.002f));
                Color c=new Color(.9f,.64f,1,.94f);
                grid.ScreenStrip(camera,at(0,-size),at(0,size),2,c);
                grid.ScreenStrip(camera,at(0,size),at(inward*size*.7f,size),2,c);
                grid.ScreenStrip(camera,at(0,-size),at(inward*size*.7f,-size),2,c);
            }
        }
        public int Pick(Ray ray,int filter=0)
        {
            int id=-1;float best=float.PositiveInfinity;
            foreach(var h in hits)
            {
                if(filter==1&&h.Air||filter==2&&!h.Air)continue;
                float d;if(RayTriangle(ray,h.A,h.B,h.C,out d)&&d<best){best=d;id=h.Id;}
            }
            return id;
        }
        public List<int> PickAll(Ray ray,int filter=0)
        {
            Dictionary<int,float> distances=new Dictionary<int,float>();
            foreach(var h in hits)
            {
                if(filter==1&&h.Air||filter==2&&!h.Air)continue;
                float d,old;
                if(RayTriangle(ray,h.A,h.B,h.C,out d)&&(!distances.TryGetValue(h.Id,out old)||d<old))distances[h.Id]=d;
            }
            var ids=new List<int>(distances.Keys);ids.Sort((a,b)=>distances[a].CompareTo(distances[b]));return ids;
        }
        // Marquee intersects actual projected note triangles, not only head centers or giant AABBs.
        // GUI rectangle has top-left origin; camera projections have bottom-left origin.
        public HashSet<int> BoxPick(Rect guiRect,int filter=0)
        {
            var ids=new HashSet<int>();
            foreach(var h in hits)
            {
                if(ids.Contains(h.Id)||filter==1&&h.Air||filter==2&&!h.Air)continue;
                Vector3 a=camera.WorldToScreenPoint(h.A),b=camera.WorldToScreenPoint(h.B),c=camera.WorldToScreenPoint(h.C);
                if(a.z<=camera.nearClipPlane||b.z<=camera.nearClipPlane||c.z<=camera.nearClipPlane)continue;
                Vector2 aa=new Vector2(a.x,Screen.height-a.y),bb=new Vector2(b.x,Screen.height-b.y),cc=new Vector2(c.x,Screen.height-c.y);
                if(TriangleRect(aa,bb,cc,guiRect))ids.Add(h.Id);
            }
            return ids;
        }
        private static bool TriangleRect(Vector2 a,Vector2 b,Vector2 c,Rect r)
        {
            if(r.Contains(a)||r.Contains(b)||r.Contains(c))return true;
            Vector2[] corners={new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax)};
            foreach(var p in corners)if(InsideTriangle(p,a,b,c))return true;
            for(int i=0;i<4;i++)if(Segments(a,b,corners[i],corners[(i+1)%4])||Segments(b,c,corners[i],corners[(i+1)%4])||Segments(c,a,corners[i],corners[(i+1)%4]))return true;
            return false;
        }
        private static float Cross2(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
        private static bool InsideTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {
            if(Mathf.Abs(Cross2(b-a,c-a))<1e-5f)return false;
            float d1=Cross2(b-a,p-a),d2=Cross2(c-b,p-b),d3=Cross2(a-c,p-c);
            return !(d1<0||d2<0||d3<0)||!(d1>0||d2>0||d3>0);
        }
        private static bool Segments(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            Vector2 v=b-a,w=d-c;float den=Cross2(v,w);if(Mathf.Abs(den)<1e-6f)return false;
            float t=Cross2(c-a,w)/den,u=Cross2(c-a,v)/den;return t>=0&&t<=1&&u>=0&&u<=1;
        }
        public static bool RayTriangle(Ray ray,Vector3 a,Vector3 b,Vector3 c,out float distance)
        {
            Vector3 e1=b-a,e2=c-a,pvec=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,pvec);distance=0;
            if(Mathf.Abs(det)<1e-8f)return false;float inv=1/det;Vector3 tvec=ray.origin-a;float u=Vector3.Dot(tvec,pvec)*inv;
            if(u<0||u>1)return false;Vector3 q=Vector3.Cross(tvec,e1);float v=Vector3.Dot(ray.direction,q)*inv;
            if(v<0||u+v>1)return false;distance=Vector3.Dot(e2,q)*inv;return distance>=0;
        }
        public void Dispose(){foreach(var batch in AllBatches())batch.Dispose();}
    }
}
