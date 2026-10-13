using System;

namespace InFalsusStudio.Core
{
    // Presentation only: no SPC mutation, physical judgement, or combo increments.
    public enum GroundSkin104 { HoldBlue,HoldPurple,HoldPink,TapBlue,TapWideMint,TapPurple,TapPink }
    public static class NoteSkinPolicy
    {
        public const double BodyU0=153.0/2048, BodyU1=870.0/2048;
        public const double CapU0=1177.0/2048, CapU1=1894.0/2048;
        public static bool Active(SpcEvent e,double time,bool autoplay)
        {return autoplay&&(e.Kind==EventKind.Hold||e.Kind==EventKind.SkyArea)&&e.DurationMs>0&&time>=e.TimeMs&&time<e.EndMs;}
        public static GroundSkin104 Ground(SpcEvent e)
        {
            if(e.Kind!=EventKind.Tap&&e.Kind!=EventKind.Hold)throw new ArgumentException("Ground note required.");
            if(e.Kind==EventKind.Hold)return e.Lane==0?GroundSkin104.HoldPurple:e.Lane==5?GroundSkin104.HoldPink:GroundSkin104.HoldBlue;
            return e.Lane==0?GroundSkin104.TapPurple:e.Lane==5?GroundSkin104.TapPink:e.GroundWidth>1?GroundSkin104.TapWideMint:GroundSkin104.TapBlue;
        }
    }
}
