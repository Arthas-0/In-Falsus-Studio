using System;
using System.Linq;

namespace InFalsusStudio.Core
{
    public struct DisplaySize
    {
        public int Width,Height;
        public DisplaySize(int width,int height){Width=width;Height=height;}
    }
    public static class DesktopPresentationPolicy
    {
        // Exclusive mode changes the OS scan-out resolution. Prefer the current monitor's
        // desktop aspect instead of forcing a 16:9 window preset onto an ultrawide panel.
        public static DisplaySize ExclusiveSize(DisplaySize desktop,DisplaySize[] supported)
        {
            if(desktop.Width<640||desktop.Height<480)throw new ArgumentException("No valid desktop display mode.");
            if(supported==null||supported.Length==0)throw new ArgumentException("No supported exclusive modes reported. Use borderless fullscreen.");
            if(supported.Any(x=>x.Width==desktop.Width&&x.Height==desktop.Height))return desktop;
            var sameAspect=supported.Where(x=>x.Width>=640&&x.Height>=480 &&
                Math.Abs((double)x.Width/x.Height-(double)desktop.Width/desktop.Height)<.015)
                .OrderBy(x=>Math.Abs((long)x.Width*x.Height-(long)desktop.Width*desktop.Height)).ToArray();
            if(sameAspect.Length==0)throw new ArgumentException("No supported exclusive resolution preserves the desktop aspect. Use borderless fullscreen.");
            return sameAspect[0];
        }
        // Design-space quad strictly inside SCORE's left-bottom recess.
        public const double PlayLeft=14, PlayTop=98, PlayBottom=143;
        public const double PlayRightTop=76, PlayRightBottom=96;
        public static readonly double[] PlayX={PlayLeft,PlayRightTop,PlayRightBottom,PlayLeft};
        public static readonly double[] PlayY={PlayTop,PlayTop,PlayBottom,PlayBottom};
        public static double PlayRightAt(double y)
        {return PlayRightTop+(y-PlayTop)*(PlayRightBottom-PlayRightTop)/(PlayBottom-PlayTop);}
        public static bool PlayContains(double x,double y)
        {
            if(y<PlayTop||y>PlayBottom||x<PlayLeft)return false;
            return x<=PlayRightAt(y);
        }
    }
    public static class NativeDialogFilter
    {
        public static string Make(string extensions)
        {
            var ext=(extensions??"").Split(new[]{',',';','|'},StringSplitOptions.RemoveEmptyEntries)
                .Select(x=>x.Trim().TrimStart('*','.')).Where(x=>x.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(ext.Any(x=>x.Length>16||x.Any(c=>!char.IsLetterOrDigit(c))))throw new ArgumentException("Invalid extension filter.");
            if(ext.Length==0)return "All files\0*.*\0\0";
            string pattern=string.Join(";",ext.Select(x=>"*."+x));
            return "Files ("+pattern+")\0"+pattern+"\0All files\0*.*\0\0";
        }
        public static string DefaultExtension(string extensions)
        {
            string s=(extensions??"").Trim().TrimStart('*','.');
            return s.Length>0&&s.All(char.IsLetterOrDigit)?s:null;
        }
    }
}
