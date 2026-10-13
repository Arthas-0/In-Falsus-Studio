using System;
using System.Runtime.InteropServices;

namespace InFalsusStudio
{
    internal static class WindowsDisplayInfo
    {
        public static bool TryDesktopSize(out int width,out int height)
        {
            width=height=0;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                var monitor=MonitorFromWindow(GetActiveWindow(),2);
                var info=new MonitorInfo{Size=(uint)Marshal.SizeOf(typeof(MonitorInfo)),Device=""};
                if(monitor==IntPtr.Zero||!GetMonitorInfoW(monitor,ref info))return false;
                var mode=new DevMode{DeviceName="",FormName="",Size=(ushort)Marshal.SizeOf(typeof(DevMode))};
                // ENUM_REGISTRY_SETTINGS, not current exclusive 16:9 mode. Do not request
                // a resolution merely because it was the last window-size preference.
                if(!EnumDisplaySettingsExW(info.Device,-2,ref mode,0))return false;
                if(mode.Width<640||mode.Height<480||mode.Width>32768||mode.Height>32768)return false;
                width=(int)mode.Width;height=(int)mode.Height;return true;
            }
            catch{return false;}
#else
            return false;
#endif
        }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)]private struct Rect {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct MonitorInfo
        {
            public uint Size;public Rect Monitor,Work;public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string DeviceName;
            public ushort SpecVersion,DriverVersion,Size,DriverExtra;public uint Fields;
            public int PositionX,PositionY;public uint Orientation,FixedOutput;
            public short Color,Duplex,YResolution,TTOption,Collate;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string FormName;
            public ushort LogPixels;public uint BitsPerPel,Width,Height,DisplayFlags,Frequency;
            public uint ICMMethod,ICMIntent,MediaType,DitherType,Reserved1,Reserved2,PanningWidth,PanningHeight;
        }
        [DllImport("user32.dll",ExactSpelling=true)]private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll",ExactSpelling=true)]private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
        [DllImport("user32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]
        [return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetMonitorInfoW(IntPtr monitor,ref MonitorInfo info);
        [DllImport("user32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]
        [return:MarshalAs(UnmanagedType.Bool)]private static extern bool EnumDisplaySettingsExW(string device,int mode,ref DevMode data,uint flags);
#endif
    }
}
