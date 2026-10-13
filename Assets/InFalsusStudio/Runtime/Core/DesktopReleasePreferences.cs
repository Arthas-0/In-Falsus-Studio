using System;
using System.IO;

namespace InFalsusStudio.Core
{
    public static class DesktopReleasePreferences
    {
        public const string LastSongFolderKey="InFalsusStudio.LastSongFolder";
        public static string ReleaseSeenKey(string version)
        {
            Version parsed;
            if(!Version.TryParse(version,out parsed))throw new ArgumentException("A release version is required.","version");
            return "InFalsusStudio.ReleaseNotesSeen."+parsed;
        }
        // The active song is the most recently opened one. Deleted/moved locations
        // never prevent opening the system picker; its normal location is the fallback.
        public static string InitialSongFolder(string active,string remembered)
        {return ExistingFolder(active)??ExistingFolder(remembered)??"";}
        private static string ExistingFolder(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||!Directory.Exists(path))return null;
            try{return Path.GetFullPath(path);}
            catch(ArgumentException){return null;}
            catch(NotSupportedException){return null;}
            catch(PathTooLongException){return null;}
        }
    }
}
