using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    /// <summary>Local dialogs. Editor uses EditorUtility; Windows Player uses system dialogs.
    /// No editor assemblies, external helper exe, shell commands or runtime downloads in Player.</summary>
    internal static class StudioFileDialogs
    {
        public static string LastDiagnostic {get;private set;}="No native dialog requested.";
        public static string Folder(string title,string directory="")
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFolderPanel(title,directory??"","");
#elif UNITY_STANDALONE_WIN
            return OnSta(()=>NativeFolder(title,directory));
#else
            throw new PlatformNotSupportedException("Native folder selection is implemented for Windows. Use the folder path field on other platforms.");
#endif
        }
        public static string Open(string title,string directory,string extension)
        {
#if UNITY_EDITOR
            if(!string.IsNullOrEmpty(extension)&&(extension.Contains(",")||extension.Contains(";")||extension.Contains("|")))
                return UnityEditor.EditorUtility.OpenFilePanelWithFilters(title,directory??"",new[]{"Files",extension.Replace(';',',').Replace('|',','),"All files","*"});
            return UnityEditor.EditorUtility.OpenFilePanel(title,directory??"",extension??"");
#elif UNITY_STANDALONE_WIN
            return OnSta(()=>NativeFile(false,title,directory,"",extension));
#else
            throw new PlatformNotSupportedException("Native file selection is available on Windows.");
#endif
        }
        public static string Save(string title,string directory,string name,string extension)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.SaveFilePanel(title,directory??"",name??"",extension??"");
#elif UNITY_STANDALONE_WIN
            return OnSta(()=>NativeFile(true,title,directory,name,extension));
#else
            throw new PlatformNotSupportedException("Native file save selection is available on Windows.");
#endif
        }
        public static bool Confirm(string title,string text,string yes="OK",string no="Cancel")
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.DisplayDialog(title,text,yes,no);
#elif UNITY_STANDALONE_WIN
            return MessageBoxW(GetActiveWindow(),text+"\n\n"+yes+" = Yes / "+no+" = No",title,0x124)==6;
#else
            return false;
#endif
        }
        // 0 Save, 1 Cancel, 2 Discard. Default is CANCEL, never silently save/discard.
        public static int SaveDecision(string text)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.DisplayDialogComplex("Unsaved changes",text,"Save","Cancel","Discard");
#elif UNITY_STANDALONE_WIN
            int result=MessageBoxW(GetActiveWindow(),text+"\n\n是/Yes = 保存 Save\n否/No = 放弃 Discard\n取消/Cancel = 返回", "未保存 / Unsaved changes",0x223);
            return result==6?0:result==7?2:1;
#else
            return 1;
#endif
        }
        public static void Error(string title,string text)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayDialog(title,text,"OK");
#elif UNITY_STANDALONE_WIN
            MessageBoxW(GetActiveWindow(),text,title,0x10);
#endif
        }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")]private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int MessageBoxW(IntPtr hwnd,string text,string title,uint flags);
        [DllImport("comdlg32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]
        [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileNameW(ref OpenFileName ofn);
        [DllImport("comdlg32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]
        [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetSaveFileNameW(ref OpenFileName ofn);
        [DllImport("comdlg32.dll",ExactSpelling=true)]private static extern uint CommDlgExtendedError();
        [DllImport("ole32.dll",ExactSpelling=true)]private static extern int OleInitialize(IntPtr reserved);
        [DllImport("ole32.dll",ExactSpelling=true)]private static extern void OleUninitialize();
        // Blittable OPENFILENAMEW. In particular, lpstrFile is NOT a StringBuilder field
        // embedded in a formatted class (unsupported by some Unity/Mono marshal paths).
        [StructLayout(LayoutKind.Sequential)]
        private struct OpenFileName
        {
            public uint size; public IntPtr owner,instance,filter,customFilter;
            public uint maxCustomFilter,filterIndex; public IntPtr file; public uint maxFile;
            public IntPtr fileTitle; public uint maxFileTitle; public IntPtr initialDirectory,title;
            public uint flags; public ushort fileOffset,fileExtension;
            public IntPtr defaultExtension,userData,hook,templateName,reserved;
            public uint reserved2,flagsEx;
        }
        private sealed class NativeBuffers : IDisposable
        {
            private readonly System.Collections.Generic.List<IntPtr> blocks=new System.Collections.Generic.List<IntPtr>();
            public IntPtr Allocate(int bytes)
            {
                var p=Marshal.AllocCoTaskMem(bytes);blocks.Add(p);
                Marshal.Copy(new byte[bytes],0,p,bytes);return p;
            }
            public IntPtr Text(string value)
            {
                if(value==null)return IntPtr.Zero;
                byte[] bytes=Encoding.Unicode.GetBytes(value+"\0");var p=Allocate(bytes.Length);
                Marshal.Copy(bytes,0,p,bytes.Length);return p;
            }
            public void Dispose(){foreach(var p in blocks)Marshal.FreeCoTaskMem(p);blocks.Clear();}
        }
        [DllImport("ole32.dll",ExactSpelling=true)]
        private static extern int CoCreateInstance(ref Guid clsid,IntPtr outer,uint context,ref Guid iid,out IntPtr instance);
        [DllImport("shell32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(string path,IntPtr bindContext,ref Guid iid,out IntPtr item);
        // Call the documented IFileOpenDialog/IShellItem ABI directly. Unity Mono
        // does not need a COM RCW, a WinForms assembly or an external helper.
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int ComShow(IntPtr self,IntPtr owner);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int ComGetOptions(IntPtr self,out uint options);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int ComSetOptions(IntPtr self,uint options);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int ComSetText(IntPtr self,[MarshalAs(UnmanagedType.LPWStr)]string text);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int ComSetItem(IntPtr self,IntPtr item);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int ComGetItem(IntPtr self,out IntPtr item);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int ComDisplayName(IntPtr self,uint kind,out IntPtr text);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate uint ComRelease(IntPtr self);
        private static T ComMethod<T>(IntPtr instance,int slot) where T:class
        {
            if(instance==IntPtr.Zero)throw new InvalidOperationException("Missing native dialog interface.");
            IntPtr method=Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance),slot*IntPtr.Size);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(method,typeof(T));
        }
        private static void CheckCom(int hr,string operation)
        {if(hr<0)throw new COMException(operation+" failed (0x"+hr.ToString("X8")+").",hr);}
        private static void ReleaseCom(IntPtr instance)
        {if(instance!=IntPtr.Zero)ComMethod<ComRelease>(instance,2)(instance);}
        private static string OnSta(Func<string> work)
        {
            string result="";Exception error=null;
            // Only native work runs on this STA. An unowned native dialog avoids a
            // synchronous cross-thread SendMessage to Unity while the main thread joins.
            var t=new Thread(()=>
            {
                bool ole=false;
                try { int hr=OleInitialize(IntPtr.Zero);if(hr<0)Marshal.ThrowExceptionForHR(hr);ole=true;result=work()??""; }
                catch(Exception e){error=e;}
                finally {if(ole)OleUninitialize();}
            });
            t.IsBackground=true;t.SetApartmentState(ApartmentState.STA);t.Start();t.Join();
            if(error!=null)
            {
                LastDiagnostic=error.GetType().Name+": "+error.Message;
                throw new IOException("Windows dialog failed: "+LastDiagnostic,error);
            }
            return result;
        }
        private static string NativeFile(bool save,string title,string directory,string name,string extension)
        {
            using(var mem=new NativeBuffers())
            {
                const int capacity=32768;
                int size=Marshal.SizeOf(typeof(OpenFileName));
                int expected=IntPtr.Size==8?152:88;
                if(size!=expected)throw new InvalidOperationException("OPENFILENAMEW ABI mismatch: "+size+" vs "+expected);
                string filter=NativeDialogFilter.Make(extension);
                var f=new OpenFileName{size=(uint)size,file=mem.Allocate(capacity*2),maxFile=capacity,
                    owner=IntPtr.Zero,filter=mem.Text(filter),filterIndex=1,
                    initialDirectory=mem.Text(!string.IsNullOrEmpty(directory)&&Directory.Exists(directory)?Path.GetFullPath(directory):null),
                    title=mem.Text(title??"Select file"),defaultExtension=mem.Text(NativeDialogFilter.DefaultExtension(extension)),
                    flags=0x80000u|0x8u|0x800u|(save?0x2u:0x1000u)};
                if(!string.IsNullOrEmpty(name))
                {
                    string initial=Path.GetFileName(name);
                    if(initial.Length>=capacity)throw new ArgumentException("Initial filename too long.");
                    byte[] bytes=Encoding.Unicode.GetBytes(initial+"\0");Marshal.Copy(bytes,0,f.file,bytes.Length);
                }
                bool ok=save?GetSaveFileNameW(ref f):GetOpenFileNameW(ref f);
                if(ok)
                {
                    LastDiagnostic=(save?"Save":"Open")+"FileNameW succeeded; struct="+size+"; Unicode; STA.";
                    return Marshal.PtrToStringUni(f.file)??"";
                }
                uint err=CommDlgExtendedError(); // Same native thread, immediately after FALSE.
                if(err==0){LastDiagnostic="Native file dialog cancelled (not an error).";return "";}
                throw new IOException("Get"+(save?"Save":"Open")+"FileNameW: CommDlgExtendedError=0x"+err.ToString("X8")+
                    ", struct="+size+", ptr="+(IntPtr.Size*8)+"-bit; filter="+(extension??"all"));
            }
        }
        private static string NativeFolder(string title,string directory)
        {
            Guid dialogClass=new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
            Guid dialogInterface=new Guid("D57C7288-D4AD-4768-BE02-9D969532D960");
            Guid shellItemInterface=new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
            IntPtr dialog=IntPtr.Zero,initial=IntPtr.Zero,selected=IntPtr.Zero,pathText=IntPtr.Zero;
            try
            {
                CheckCom(CoCreateInstance(ref dialogClass,IntPtr.Zero,1,ref dialogInterface,out dialog),"Create folder picker");
                uint options;CheckCom(ComMethod<ComGetOptions>(dialog,10)(dialog,out options),"GetOptions");
                // FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_NOCHANGEDIR.
                CheckCom(ComMethod<ComSetOptions>(dialog,9)(dialog,options|0x20u|0x40u|0x800u|0x8u),"SetOptions");
                CheckCom(ComMethod<ComSetText>(dialog,17)(dialog,title??"Select folder"),"SetTitle");
                if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))
                {
                    CheckCom(SHCreateItemFromParsingName(Path.GetFullPath(directory),IntPtr.Zero,ref shellItemInterface,out initial),"Initial folder");
                    // Explicitly restore OUR last successfully opened song, rather
                    // than Windows' shared last location (which may be unrelated).
                    CheckCom(ComMethod<ComSetItem>(dialog,12)(dialog,initial),"SetFolder");
                }
                int result=ComMethod<ComShow>(dialog,3)(dialog,IntPtr.Zero);
                if(result==unchecked((int)0x800704C7)){LastDiagnostic="Explorer folder picker cancelled (not an error).";return "";}
                CheckCom(result,"Show folder picker");
                CheckCom(ComMethod<ComGetItem>(dialog,20)(dialog,out selected),"GetResult");
                CheckCom(ComMethod<ComDisplayName>(selected,5)(selected,0x80058000u,out pathText),"Folder filesystem path");
                string path=Marshal.PtrToStringUni(pathText);
                if(string.IsNullOrEmpty(path)||!Directory.Exists(path))throw new IOException("Select an existing filesystem directory.");
                LastDiagnostic="IFileOpenDialog folder picker succeeded; Explorer; Unicode; STA.";
                return path;
            }
            finally
            {
                if(pathText!=IntPtr.Zero)Marshal.FreeCoTaskMem(pathText);
                ReleaseCom(selected);ReleaseCom(initial);ReleaseCom(dialog);
            }
        }
#endif
    }
}
