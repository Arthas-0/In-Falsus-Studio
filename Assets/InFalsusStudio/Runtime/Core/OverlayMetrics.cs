using System;
namespace InFalsusStudio.Core
{
    // Design units shared by decoration, text, and pointer hit-testing. No screen pixels
    // are silently treated as design units, including when Windows/Unity changes DPI.
    public static class OverlayMetrics
    {
        public const double ReferenceWidth=1920,ReferenceHeight=1080;
        public static double Scale(double width,double height)
        {return Math.Max(.05,Math.Min(width/ReferenceWidth,height/ReferenceHeight));}
        public const double RadialRadius=150,ButtonWidth=134,ButtonHeight=50,FontSize=17;
    }
}
