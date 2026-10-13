using System;
using System.Collections.Generic;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private readonly RadialMenuState radial=new RadialMenuState();
        private readonly EditModifierGate editModifier=new EditModifierGate();
        private struct RadialButton { public RadialItem Item; public Rect Rect; public string Text; }
        private readonly List<RadialButton> radialButtons=new List<RadialButton>();
        private Vector2 radialCenter,radialPointer;
        private Rect radialViewport;
        private int radialControlId;
        private GUIStyle radialButtonStyle,radialTextStyle;
        private Font radialFont;
        private bool radialFontReady;
        private bool EditHeld {get{return editModifier.Held;}}
        private bool PointerEditGesture {get{return gesture==Gesture.Move||gesture==Gesture.Resize||gesture==Gesture.TimelineMove;}}
        private int tapWidth=1,holdWidth=1,lastFlickDirection=16;

        private void ObserveEditorModifiers(Event e)
        {
            editModifier.Observe(e);
            if(!EditHeld&&PointerEditGesture)
            {
                // Releasing LCtrl ends the edit immediately at the last valid preview.
                // Mouse movement after release cannot keep modifying the source.
                ReleaseGesture();status="Alt released: last valid preview committed once.";
            }
        }
        private void CancelRadial()
        {
            radial.Cancel();
            if(GUIUtility.hotControl==radialControlId)GUIUtility.hotControl=0;
        }
        private void UseTool(Tool next)
        {
            CancelGesture();tool=next;pointPlacement.Reset(PlacementKind);hoverValid=false;
            if(next!=Tool.Select){radial.InPointMode=true;showNotes=true;}
            if(next==Tool.FlickLeft)lastFlickDirection=16;
            if(next==Tool.FlickRight)lastFlickDirection=4;
            status=next==Tool.Select?"No creation tool. Select normally; hold Alt to move or use handles.":PlacementInstruction;
        }
        private bool HandleRadialEvent(Event e)
        {
            if(!HasDocument){if(radial.Pressed)CancelRadial();return false;}
            double now=Time.realtimeSinceStartupAsDouble;
            if(transport.Playing){if(radial.Pressed)CancelRadial();return false;}
            if(e.rawType==EventType.MouseDown&&e.button==1&&(WorkflowActive||pointPlacement.Pending))
            {CancelGesture();CancelRadial();status=L("已取消未完成操作，原谱和选择不变。","Pending operation cancelled; source retained.");e.Use();return true;}
            if(radial.Pressed&&e.rawType==EventType.KeyDown&&e.keyCode==KeyCode.Escape)
            {CancelRadial();e.Use();return true;}
            if(e.type==EventType.MouseDown&&e.button==1&&!radial.Pressed&&AllowedCanvas(e.mousePosition)&&gesture==Gesture.None)
            {
                if(!CommitPendingNoteNow()){e.Use();return true;}GUI.FocusControl(null);radial.Press(now,selection.Count>0&&tool==Tool.Select);radialPointer=e.mousePosition;
                radialCenter=ClampRadialCenter(radialPointer);radialViewport=CameraGuiRect;
                BuildRadialButtons();GUIUtility.hotControl=radialControlId;e.Use();return true;
            }
            if(!radial.Pressed)return false;
            if(e.isMouse||e.type==EventType.ScrollWheel)radialPointer=e.mousePosition;
            BuildRadialButtons();RadialItem hit=HitRadial(radialPointer);Vector2 childCenter=radialCenter;
            foreach(var b in radialButtons)if(b.Item==hit)childCenter=b.Rect.center;
            if(radial.Tick(now,hit))
            {
                // Exact parent button center; no new press and no mouse release is required.
                radialCenter=childCenter;BuildRadialButtons();hit=HitRadial(radialPointer);
            }
            if(e.rawType==EventType.MouseUp&&e.button==1)
            {
                RadialChoice choice=radial.Release(hit);
                if(GUIUtility.hotControl==radialControlId)GUIUtility.hotControl=0;
                if(choice.Valid)QueueGui(()=>ApplyRadialChoice(choice));
                // Blank release is a true no-op: keep tool/defaults/draft/mode as they were.
                e.Use();return true;
            }
            // No left button / scroll / keyboard action may fall through a held RMB menu.
            if(e.isMouse||e.type==EventType.ScrollWheel||e.type==EventType.KeyDown||e.type==EventType.KeyUp)e.Use();
            return true;
        }
        private void ApplyRadialChoice(RadialChoice choice)
        {
            switch(choice.Item)
            {
                case RadialItem.Delete:DeleteSelected();break;
                case RadialItem.Mirror:MirrorSelection();break;
                case RadialItem.Copy:CopySelection(false);break;
                case RadialItem.Cut:CopySelection(true);break;
                case RadialItem.Align:AlignSelection();break;
                case RadialItem.Grid:SetBeatGrid(!showGrid);status="Beat grid "+(showGrid?"ON: time placement and dragging must snap.":"OFF: free integer-ms mouse placement.");break;
                case RadialItem.EnterPoint:if(choice.Level==RadialLevel.Selection)SetSelection(new int[0]);status="Point mode entered. Press RMB AGAIN for note types; release RMB on a button to choose.";break;
                case RadialItem.NoCreate:UseTool(Tool.Select);radial.InPointMode=true;break;
                case RadialItem.Exit:UseTool(Tool.Select);radial.InPointMode=false;status="Point mode exited. No creation tool is active.";break;
                case RadialItem.Tap:UseTool(Tool.Tap);break;
                case RadialItem.Hold:UseTool(Tool.Hold);break;
                case RadialItem.Sky:UseTool(Tool.Sky);break;
                case RadialItem.Flick:UseTool(lastFlickDirection==4?Tool.FlickRight:Tool.FlickLeft);break;
                case RadialItem.Left:UseTool(Tool.FlickLeft);break;
                case RadialItem.Right:UseTool(Tool.FlickRight);break;
                case RadialItem.Width1:case RadialItem.Width2:case RadialItem.Width3:case RadialItem.Width4:
                    int width=(int)choice.Item-(int)RadialItem.Width1+1;
                    if(choice.Level==RadialLevel.TapWidth){tapWidth=width;UseTool(Tool.Tap);}
                    else{holdWidth=width;UseTool(Tool.Hold);}
                    status="Default "+(choice.Level==RadialLevel.TapWidth?"Tap":"Hold")+" width = "+width+". Side lanes 0/5 always use width 1.";break;
            }
        }
        private void BuildRadialButtons()
        {
            Rect view=CameraGuiRect;
            if(radialViewport.width>0&&radialViewport.height>0&&view!=radialViewport)
            {radialCenter=new Vector2(view.x+(radialCenter.x-radialViewport.x)*view.width/radialViewport.width,
                view.y+(radialCenter.y-radialViewport.y)*view.height/radialViewport.height);}
            radialViewport=view;
            radialButtons.Clear();float radius=(float)OverlayMetrics.RadialRadius*OverlayScale;
            if(radial.Level==RadialLevel.Selection)
            {
                var items=new List<RadialItem>{RadialItem.EnterPoint,RadialItem.Mirror,RadialItem.Cut,RadialItem.Copy,RadialItem.Delete,RadialItem.Grid};
                var texts=new List<string>{L("点立得","Quick Place"),L("镜像","Mirror"),L("剪贴","Cut"),L("复制","Copy"),L("删除","Delete"),L("网格线","Grid")};
                if(showGrid){items.Add(RadialItem.Align);texts.Add(L("对齐网格线","Align to grid"));}
                for(int i=0;i<items.Count;i++){float a=(-90+i*360f/items.Count)*Mathf.Deg2Rad;AddRadial(items[i],texts[i],new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius));}
                return;
            }
            if(radial.Level==RadialLevel.Home)
            {AddRadial(RadialItem.EnterPoint,"点立得\nQuick Place",new Vector2(-radius,0));AddRadial(RadialItem.Grid,showGrid?"关闭网格线\nGrid OFF":"开启网格线\nGrid ON",new Vector2(radius,0));return;}
            if(radial.Level==RadialLevel.Point)
            {
                RadialItem[] items={RadialItem.Sky,RadialItem.Flick,RadialItem.Exit,RadialItem.Hold,RadialItem.Tap,RadialItem.NoCreate,RadialItem.Grid};
                string[] texts={"SkyArea", "Flick", "退出\nExit", "Hold  ["+holdWidth+"]", "Tap  ["+tapWidth+"]", "不创建\nNo creation",showGrid?"关闭网格线\nGrid OFF":"开启网格线\nGrid ON"};
                for(int i=0;i<items.Length;i++){float a=(-90+i*360f/items.Length)*Mathf.Deg2Rad;AddRadial(items[i],texts[i],new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius));}
                return;
            }
            if(radial.Level==RadialLevel.FlickDirection)
            {AddRadial(RadialItem.Left,"向左 / Left\nYellow",new Vector2(-radius,0));AddRadial(RadialItem.Right,"向右 / Right\nGreen",new Vector2(radius,0));return;}
            for(int i=0;i<4;i++){float a=(-90+90*i)*Mathf.Deg2Rad;AddRadial((RadialItem)((int)RadialItem.Width1+i),(i+1)+" 轨宽",new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius));}
        }
        private void AddRadial(RadialItem item,string text,Vector2 offset)
        {
            Vector2 p=radialCenter+offset;float w=(float)OverlayMetrics.ButtonWidth*OverlayScale,h=(float)OverlayMetrics.ButtonHeight*OverlayScale;
            radialButtons.Add(new RadialButton{Item=item,Text=text,Rect=new Rect(p.x-w*.5f,p.y-h*.5f,w,h)});
        }
        private RadialItem HitRadial(Vector2 pointer)
        {foreach(var b in radialButtons)if(b.Rect.Contains(pointer))return b.Item;return RadialItem.Blank;}
        private void EnsureRadialStyles()
        {
            if(!radialFontReady)
            {
                radialFontReady=true;
                try{radialFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","PingFang SC","Arial"},17);}
                catch(Exception){radialFont=null;}
            }
            if(radialButtonStyle==null)radialButtonStyle=new GUIStyle(GUI.skin.box);
            if(radialTextStyle==null)radialTextStyle=new GUIStyle(GUI.skin.label);
            foreach(var style in new[]{radialButtonStyle,radialTextStyle})
            {
                style.alignment=TextAnchor.MiddleCenter;style.wordWrap=true;
                style.fixedHeight=0;style.fixedWidth=0;style.contentOffset=Vector2.zero;
                int p=Mathf.RoundToInt(4*OverlayScale);style.padding=new RectOffset(p,p,p,p);
                style.fontSize=Mathf.Max(1,Mathf.RoundToInt((style==radialButtonStyle?17:15)*OverlayScale));
                if(radialFont!=null)style.font=radialFont;
            }
        }

        private void DrawRadialOverlay()
        {
            if(!radial.Pressed)return;EnsureRadialStyles();
            RadialItem hovered=HitRadial(radialPointer);float radius=(float)OverlayMetrics.RadialRadius*OverlayScale;
            Color ring=new Color(.35f,.8f,1,.65f);
            for(int i=0;i<64;i++)
            {float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;LineGui(radialCenter+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,radialCenter+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,ring,2*OverlayScale);}
            GUI.Box(new Rect(radialCenter.x-58*OverlayScale,radialCenter.y-30*OverlayScale,116*OverlayScale,60*OverlayScale),L("右键松开\n空白不操作","Release RMB\nBlank = cancel"),radialButtonStyle);
            foreach(var button in radialButtons)
            {
                Color old=GUI.color;bool hit=button.Item==hovered;
                GUI.color=hit?new Color(.5f,1,.85f,1):new Color(.78f,.89f,1,1);
                // Decorative boxes only: GUI.Button would accidentally accept LEFT clicks.
                GUI.Box(button.Rect,button.Text,radialButtonStyle);GUI.color=old;
                if(hit)
                {
                    double progress=radial.HoverProgress(Time.realtimeSinceStartupAsDouble);
                    if(progress>0){GUI.color=new Color(.2f,1,.65f,1);GUI.DrawTexture(new Rect(button.Rect.x+3*OverlayScale,button.Rect.yMax-4*OverlayScale,(button.Rect.width-6*OverlayScale)*(float)progress,2*OverlayScale),Texture2D.whiteTexture);GUI.color=old;}
                }
            }
            string title=radial.Level==RadialLevel.Selection?L("选区操作：在按钮上松开右键","Selection: release RMB on a button"):radial.Level==RadialLevel.Home?"一级菜单":radial.Level==RadialLevel.Point?"点立得：停留 0.5 秒展开；在类型上松开直接选择":radial.Level==RadialLevel.FlickDirection?"Flick 方向":"默认宽度：仅中央四轨生效";
            OverlayText(new Vector2(radialCenter.x-265*OverlayScale,radialCenter.y-radius-82*OverlayScale),title,530,true,15,TextAnchor.MiddleCenter);
        }
    }
}
