using System;
using System.IO;
using System.Linq;

namespace InFalsusStudio.Core
{
    public static class Release105SelfTests
    {
        public static void Run(Action<bool,string> check)
        {
            check(new Version(ReleaseIdentity.Version)>=new Version("1.0.5")&&ReleaseIdentity.DisplayVersion==ReleaseIdentity.Version,"105+ application and displayed version");
            var recent=ReleaseNotesCatalog.Recent;
            check(recent.Length==3&&recent.Select(n=>new Version(n.Version)).SequenceEqual(recent.Select(n=>new Version(n.Version)).OrderByDescending(n=>n)),"105+ announcement contains three releases in descending order");
            check(recent[0].Version==ReleaseIdentity.Version,"105 announcement begins with the running release");
            check(recent.All(n=>!string.IsNullOrWhiteSpace(n.Chinese)&&!string.IsNullOrWhiteSpace(n.English)),"105 announcement has both UI languages");
            recent[0]=null;check(ReleaseNotesCatalog.Recent[0]!=null,"105 reopening receives intact release notes");
            string currentKey=DesktopReleasePreferences.ReleaseSeenKey("1.0.5"),previousKey=DesktopReleasePreferences.ReleaseSeenKey("1.0.4");
            check(currentKey!=previousKey,"105 a previously read release cannot suppress a new release");
            check(currentKey!=DesktopReleasePreferences.LastSongFolderKey,"105 announcement and folder memories are separate");
            string root=Path.Combine(Path.GetTempPath(),"ifstudio-105-"+Guid.NewGuid().ToString("N"));
            string active=Path.Combine(root,"当前 歌曲"),remembered=Path.Combine(root,"上次 Enigma");
            try
            {
                Directory.CreateDirectory(active);Directory.CreateDirectory(remembered);
                check(DesktopReleasePreferences.InitialSongFolder("",remembered)==remembered,"105 a fresh session restores the last song folder including Unicode/spaces");
                check(DesktopReleasePreferences.InitialSongFolder(active,remembered)==active,"105 the current successfully opened song is the picker location");
                check(DesktopReleasePreferences.InitialSongFolder(Path.Combine(root,"missing"),remembered)==remembered,"105 stale current location falls back to remembered folder");
                Directory.Delete(remembered);
                check(DesktopReleasePreferences.InitialSongFolder("",remembered)=="","105 removed remembered folder falls back to the system location");
                check(DesktopReleasePreferences.InitialSongFolder(null,null)=="","105 first launch without a stored folder is valid");
                check(DesktopReleasePreferences.InitialSongFolder("\0","\0")=="","105 invalid stored paths cannot prevent opening the picker");
            }
            finally
            {
                if(Directory.Exists(active))Directory.Delete(active);
                if(Directory.Exists(remembered))Directory.Delete(remembered);
                if(Directory.Exists(root))Directory.Delete(root);
            }
        }
    }
}
