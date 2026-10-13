using System;
using UnityEngine;
namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        public string RuntimeStabilityReport010()
        {
            if(!initialized)return "v0.10 not initialized; check the first Console error.";
            return "InFalsus Studio v0.10 | Unity "+Application.unityVersion+
                "\nHasDocument="+HasDocument+" | events="+events.Count+" | time="+CurrentMs+" | playing="+transport.Playing+
                "\nViewport="+ViewCamera.pixelRect+" | overlay scale="+OverlayScale+
                " | radial button="+(134*OverlayScale)+"x"+(50*OverlayScale)+" | font="+Mathf.RoundToInt(17*OverlayScale)+
                "\nHold heads="+groundHoldAttacks+" | single-note max=300000ms | no startup chart loading"+
                "\n"+(keySounds!=null?keySounds.Diagnostic:"No key audio engine")+
                "\nMissing PCM: "+(keySounds!=null?keySounds.MissingClips:"")+
                "\nThis runtime report is evidence from the running build, NOT a substitute for listening/visual inspection.";
        }
    }
}
