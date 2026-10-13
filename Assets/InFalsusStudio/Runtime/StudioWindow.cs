using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public enum StudioWindowMode { Windowed=0, BorderlessWindow=1, Fullscreen=2, BorderlessFullscreen=3 }
    public sealed class StudioWindow : MonoBehaviour
    {
        public StudioWindowMode Mode {get;private set;}
        public bool Busy {get;private set;}
        public string Message {get;private set;}="";
        private int version;
        private readonly NativeBorderlessWindow native=new NativeBorderlessWindow();
        private DisplaySize desktop;
        public void Apply(int width,int height,StudioWindowMode mode)
        {
            if(width<960||height<540||width>7680||height>4320)throw new ArgumentOutOfRangeException("resolution","Use 960..7680 width and 540..4320 height.");
            if(!Enum.IsDefined(typeof(StudioWindowMode),mode))throw new ArgumentOutOfRangeException("mode");
#if UNITY_EDITOR
            Message="窗口设置用于独立 exe；Editor 内请调整 Game 视图。 / Standalone only.";
#else
            int token=++version;StartCoroutine(Change(width,height,mode,token));
#endif
        }
        private IEnumerator Change(int width,int height,StudioWindowMode mode,int token)
        {
            Busy=true;Message="Applying display settings...";
            string error=null;
            int requestedW=width,requestedH=height;
            try
            {
                int dw,dh;
                if(WindowsDisplayInfo.TryDesktopSize(out dw,out dh))desktop=new DisplaySize(dw,dh);
                else if(desktop.Width==0||Screen.fullScreenMode!=FullScreenMode.ExclusiveFullScreen)
                {var res=Screen.currentResolution;desktop=new DisplaySize(res.width,res.height);}
                if(mode==StudioWindowMode.Fullscreen||mode==StudioWindowMode.BorderlessFullscreen)
                {
                    var size=mode==StudioWindowMode.Fullscreen
                        ?DesktopPresentationPolicy.ExclusiveSize(desktop,Screen.resolutions.Select(x=>new DisplaySize(x.width,x.height)).ToArray())
                        :desktop;
                    width=size.Width;height=size.Height;
                }
                var full=mode==StudioWindowMode.Fullscreen?FullScreenMode.ExclusiveFullScreen:
                    mode==StudioWindowMode.BorderlessFullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
                native.Restore();Screen.SetResolution(width,height,full);
            }
            catch(Exception ex){error=ex.Message;}
            if(error!=null){Busy=false;Message="Display change refused: "+error;yield break;}
            // GPU/mode changes can take several frames; never apply native window styles
            // based on the old backbuffer dimensions. A newer request invalidates this one.
            int stable=0,lastW=-1,lastH=-1;double deadline=Time.realtimeSinceStartupAsDouble+2;
            do
            {
                yield return null;
                if(token!=version)yield break;
                if(Screen.width==lastW&&Screen.height==lastH)stable++;else stable=0;
                lastW=Screen.width;lastH=Screen.height;
            } while((stable<2||Screen.width!=width||Screen.height!=height)&&Time.realtimeSinceStartupAsDouble<deadline);
            yield return new WaitForEndOfFrame();if(token!=version)yield break;
            try
            {
                if(mode==StudioWindowMode.BorderlessWindow)native.Enable(width,height);
                Mode=mode;
                Message="Display: "+Screen.width+"x"+Screen.height+" / "+Screen.fullScreenMode+
                    "; desktop "+desktop.Width+"x"+desktop.Height+"; 16:9 calibrated content";
                if(mode==StudioWindowMode.Fullscreen||mode==StudioWindowMode.BorderlessFullscreen)
                    Message+=". Window preset "+requestedW+"x"+requestedH+" is retained for windowed mode.";
            }
            catch(Exception ex)
            {
                try{native.Restore();}catch{}
                Mode=StudioWindowMode.Windowed;Screen.SetResolution(1280,720,FullScreenMode.Windowed);
                Message="Recovered to 1280x720 windowed: "+ex.Message;
            }
            Busy=false;
        }
        public void RecoverWindowed(){Apply(1280,720,StudioWindowMode.Windowed);}
        private void OnDestroy(){version++;native.Dispose();}
    }
}
