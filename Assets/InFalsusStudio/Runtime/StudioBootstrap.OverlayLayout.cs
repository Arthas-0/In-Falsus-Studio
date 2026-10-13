using UnityEngine;
using InFalsusStudio.Core;
namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private GUIStyle overlayLabel,overlayBox;
        private float OverlayScale {get {Rect r=CameraGuiRect;return (float)OverlayMetrics.Scale(r.width,r.height);}}
        private GUIStyle OverlayStyle(bool box,float size=17,TextAnchor anchor=TextAnchor.MiddleLeft)
        {
            if(overlayLabel==null)overlayLabel=new GUIStyle(GUI.skin.label);
            if(overlayBox==null)overlayBox=new GUIStyle(GUI.skin.box);
            GUIStyle s=box?overlayBox:overlayLabel;float k=OverlayScale;
            s.fontSize=Mathf.Max(1,Mathf.RoundToInt(size*k));s.alignment=anchor;s.wordWrap=true;
            s.fixedWidth=0;s.fixedHeight=0;s.contentOffset=Vector2.zero;
            int p=Mathf.RoundToInt((box?6:2)*k);
            s.padding=new RectOffset(p,p,p,p);s.margin=new RectOffset(0,0,0,0);
            s.normal.textColor=new Color(.86f,.96f,1,1);return s;
        }
        private Rect OverlayMeasuredRect(Vector2 topLeft,string text,float designWidth,bool box,float font=17)
        {
            float k=OverlayScale;GUIStyle s=OverlayStyle(box,font);
            float width=Mathf.Min(designWidth*k,CameraGuiRect.width-8*k);
            float height=Mathf.Max((box?32:25)*k,s.CalcHeight(new GUIContent(text),width)+2*k);
            Rect r=new Rect(topLeft.x,topLeft.y,width,height);Rect bounds=CameraGuiRect;
            r.x=Mathf.Clamp(r.x,bounds.x+4*k,Mathf.Max(bounds.x+4*k,bounds.xMax-width-4*k));
            r.y=Mathf.Clamp(r.y,bounds.y+4*k,Mathf.Max(bounds.y+4*k,bounds.yMax-height-4*k));return r;
        }
        private void OverlayText(Vector2 pos,string text,float width=220,bool box=false,float font=17,TextAnchor anchor=TextAnchor.MiddleLeft)
        {
            Rect r=OverlayMeasuredRect(pos,text,width,box,font);GUIStyle s=OverlayStyle(box,font,anchor);
            if(box)GUI.Box(r,text,s);else GUI.Label(r,text,s);
        }
        private Vector2 ClampRadialCenter(Vector2 p)
        {
            Rect r=CameraGuiRect;float k=OverlayScale;
            // Leave room for the child menu around its parent's original button center.
            float mx=385*k,my=345*k;
            return new Vector2(Mathf.Clamp(p.x,r.xMin+mx,r.xMax-mx),Mathf.Clamp(p.y,r.yMin+my,r.yMax-my));
        }
    }
}
