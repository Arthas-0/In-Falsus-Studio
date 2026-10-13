using System;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private Vector2 easeSelectorCenter;
        private int easeInitialCode;
        private SkyEaseGeometry easeGeometry;
        private static bool IsEaseHandle(NoteHandle handle)
        {return handle==NoteHandle.LeftEase||handle==NoteHandle.RightEase;}
        private void BeginEaseSelector()
        {
            bool right=dragHandle==NoteHandle.RightEase;
            // Re-evaluate the current endpoints every time; no sticky wall/straight flags.
            easeGeometry=GeometricSkyEase.Describe(dragEvent,right);
            easeInitialCode=(int)dragEvent.Get(right?8:7);
            float offset=(float)(easeGeometry.Position(easeInitialCode)*SkyEaseHandle.NotchPixels*OverlayScale);
            easeSelectorCenter=pointerDown-new Vector2(offset,0);guideActive=false;
        }
        private void PreviewEaseSelector()
        {
            if(dragEvent==null||dragEvent.Kind!=EventKind.SkyArea)return;
            bool right=dragHandle==NoteHandle.RightEase;
            if(!easeGeometry.CanCurve){status=easeGeometry.StraightReason;return;}
            int code=easeGeometry.FromDrag(easeInitialCode,(pointerNow.x-pointerDown.x)/OverlayScale);
            try
            {
                History.Preview(d=>EditOperations.SetEase(d,new[]{dragPrimary},right,code));
                AfterDocumentEdit();guideActive=false;
                status=(right?"Right":"Left")+" curve follows the mouse: "+SkyEaseHandle.Name(code)+
                    ". Endpoint direction determines SineOut/SineIn; opposite edge and all endpoints unchanged.";
            }
            catch(Exception ex){status=ex.Message+" Last valid easing preview retained.";}
        }
        private void DrawEaseSelector()
        {
            if(gesture!=Gesture.Resize||!IsEaseHandle(dragHandle)||dragEvent==null||!EditHeld||!easeGeometry.CanCurve)return;
            bool right=dragHandle==NoteHandle.RightEase;
            var current=events.FirstOrDefault(n=>n.SourceId==dragPrimary);
            int code=current!=null?(int)current.Get(right?8:7):easeInitialCode;
            float k=OverlayScale,span=(float)SkyEaseHandle.NotchPixels*k;Color track=new Color(.55f,.92f,1,.95f);
            ClippedGuiLine(easeSelectorCenter-new Vector2(span+8*k,0),easeSelectorCenter+new Vector2(span+8*k,0),track,2*k);
            for(int value=0;value<3;value++)
            {
                Vector2 tick=easeSelectorCenter+new Vector2((float)easeGeometry.Position(value)*span,0);
                Color color=code==value?new Color(1,.85f,.15f,1):track;
                ClippedGuiLine(tick-new Vector2(0,7*k),tick+new Vector2(0,7*k),color,(code==value?3:1)*k);
                OverlayText(tick+new Vector2(-50*k,(value==0?-38:15)*k),SkyEaseHandle.Name(value),100,false,17,TextAnchor.MiddleCenter);
            }
            OverlayText(easeSelectorCenter+new Vector2(-180*k,51*k),
                (right?"Right":"Left")+" curve | "+SkyEaseHandle.Name(code)+" | release to commit",360,true,16,TextAnchor.MiddleCenter);
        }
    }
}
