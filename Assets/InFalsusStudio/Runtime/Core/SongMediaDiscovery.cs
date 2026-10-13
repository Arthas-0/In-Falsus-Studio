using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace InFalsusStudio.Core
{
    // Scan on every open. Chart sources and working copies are not part of this inventory.
    public static class SongMediaDiscovery
    {
        private static bool IsImage(string path)
        {string ext=Path.GetExtension(path);return new[]{".png",".jpg",".jpeg"}.Any(e=>ext.Equals(e,StringComparison.OrdinalIgnoreCase));}
        private static IEnumerable<string> Images(string root)
        {
            var folders=new Stack<string>();folders.Push(root);
            while(folders.Count>0)
            {
                string folder=folders.Pop();foreach(string file in Directory.GetFiles(folder).Where(IsImage))yield return file;
                foreach(string child in Directory.GetDirectories(folder))
                    if(!Path.GetFileName(child).StartsWith(".",StringComparison.Ordinal)&&(File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)folders.Push(child);
            }
        }
        public static void RefreshJackets(SongFolderProject project)
        {
            var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string file in Images(project.Root))paths.Add(project.Resolve(project.Relative(file)));
            foreach(string file in new[]{project.JacketPath}.Concat(project.Difficulties.Select(d=>d.JacketPath)))
                if(!string.IsNullOrEmpty(file)&&File.Exists(file)&&IsImage(file))paths.Add(project.Resolve(project.Relative(file)));
            project.JacketCandidates=paths.OrderBy(p=>project.Relative(p),StringComparer.OrdinalIgnoreCase).ToArray();
            var inventory=new StringBuilder();
            foreach(string file in project.JacketCandidates)
                inventory.Append(project.Relative(file).ToLowerInvariant()).Append('\0').Append(SongFolderProject.FileHash(file)).Append('\n');
            string current=SongFolderProject.Sha256(Encoding.UTF8.GetBytes(inventory.ToString()));
            bool unchanged=current.Equals(project.JacketInventorySha256,StringComparison.Ordinal);
            bool remembered=project.JacketCandidates.Any(p=>p.Equals(project.JacketPath,StringComparison.OrdinalIgnoreCase));
            project.JacketInventorySha256=current;
            project.JacketChoiceRequired=project.JacketCandidates.Length>1&&(!unchanged||!remembered);
            if(project.JacketCandidates.Length==0)SetJacket(project,"");
            else if(project.JacketCandidates.Length==1)SetJacket(project,project.JacketCandidates[0]);
            else if(!project.JacketChoiceRequired)
                foreach(var d in project.Difficulties)
                    if(!project.JacketCandidates.Contains(d.JacketPath,StringComparer.OrdinalIgnoreCase))d.JacketPath=project.JacketPath;
        }
        private static void SetJacket(SongFolderProject project,string path)
        {project.JacketPath=path;foreach(var d in project.Difficulties)d.JacketPath=path;project.JacketChoiceRequired=false;}
        public static void ChooseJacket(SongFolderProject project,string path)
        {
            string chosen=project.Resolve(project.Relative(path));
            if(!project.JacketCandidates.Contains(chosen,StringComparer.OrdinalIgnoreCase))throw new IOException("Choose one of the discovered jacket images in this song folder.");
            SetJacket(project,chosen);
        }
        public static bool UseRememberedJacket(SongFolderProject project,string relative,string inventory)
        {
            if(string.IsNullOrEmpty(relative)||project.JacketInventorySha256!=inventory)return false;
            string path;
            try{path=project.Resolve(relative);}catch(IOException){return false;}
            if(!project.JacketCandidates.Contains(path,StringComparer.OrdinalIgnoreCase))return false;
            // Saved per-difficulty covers remain intact when the manifest already matches.
            if(project.JacketChoiceRequired)ChooseJacket(project,path);
            return true;
        }
        public static void ReapplyOpeningChoice(SongFolderProject opened,SongFolderProject checkedProject)
        {
            if(opened.JacketInventorySha256!=checkedProject.JacketInventorySha256)
                throw new IOException("Jacket files changed while opening. Reopen the folder to choose from the updated images.");
            if(checkedProject.JacketChoiceRequired)ChooseJacket(checkedProject,opened.JacketPath);
        }
        public static string PreferenceKey(string root)
        {return "InFalsusStudio.JacketChoice."+SongFolderProject.Sha256(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd('\\','/').ToLowerInvariant()));}
    }
}
