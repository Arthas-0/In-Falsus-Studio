using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    /// <summary>
    /// Explicit-save boundary. Project fields and difficulty Documents are working memory.
    /// Only Save() writes existing project files. Switching slots/discarding/disposal do not.
    /// Byte snapshots are captured on open, not at Save time, so external edits are detected.
    /// </summary>
    public sealed class ManualSongSession
    {
        private readonly SongFolderProject project;
        private readonly Dictionary<string,byte[]> baseline=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,byte[]> pending=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        private string savedTitle,savedArtist;
        private readonly string[] savedRatings=new string[4];
        private readonly string[] savedDesigners=new string[4];
        private readonly string[] savedJacketDesigners=new string[4],savedJackets=new string[4];
        private string savedAudio,savedJacket,savedJacketInventory;
        private double savedPreviewStart,savedPreviewEnd;
        private double savedOffset;
        private sealed class Write {public string Path;public byte[] Before,After;public bool Attempted;}
        public ManualSongSession(SongFolderProject value)
        {
            project=value??throw new ArgumentNullException("value");
            Remember(project.Resolve(SongFolderProject.ManifestFile));Remember(project.Resolve("song.json"));
            foreach(var d in project.Difficulties)
            {
                Remember(d.WorkingPath);
                Remember(project.Resolve(".ifstudio/Score/"+d.Index+".json"));
                if(d.Document==null||!Same(d.Document.ToBytes(),baseline[d.WorkingPath]))
                    throw new IOException("Working chart changed while opening: "+d.WorkingPath);
            }
            NativeChartDesigner.ReadInto(project,baseline[project.Resolve("song.json")]);
            NativeSongDetails.ReadInto(project,baseline[project.Resolve("song.json")]);
            RememberMetadata();
            RememberOriginalMedia(); // Rediscovery is a RAM draft until explicit Save.
        }
        private static byte[] Read(string path){return File.Exists(path)?File.ReadAllBytes(path):null;}
        private static bool Same(byte[] a,byte[] b){return a==null?b==null:b!=null&&a.SequenceEqual(b);}
        private void Remember(string path){baseline[path]=Read(path);}
        private void RememberMetadata()
        {
            savedTitle=project.Title;savedArtist=project.Artist;savedOffset=project.AudioOffsetMs;
            savedPreviewStart=project.PreviewStartSeconds;savedPreviewEnd=project.PreviewEndSeconds;
            savedAudio=project.AudioPath;savedJacket=project.JacketPath;savedJacketInventory=project.JacketInventorySha256;
            for(int i=0;i<4;i++){savedRatings[i]=project.Difficulties[i].Rating;savedDesigners[i]=project.Difficulties[i].Designer??"";savedJacketDesigners[i]=project.Difficulties[i].JacketDesigner??"";savedJackets[i]=project.Difficulties[i].JacketPath;}
        }
        private void RememberOriginalMedia()
        {
            var bytes=baseline[project.Resolve(SongFolderProject.ManifestFile)];if(bytes==null)return;
            var manifest=ProjectJson.Object(ProjectJson.Parse(new UTF8Encoding(false,true).GetString(bytes)));
            savedAudio=project.Resolve(ProjectJson.Text(manifest,"audio"));savedJacket=project.Resolve(ProjectJson.Text(manifest,"jacket"));savedJacketInventory=ProjectJson.Text(manifest,"jacketInventorySha256");
            foreach(var row in ProjectJson.Array(ProjectJson.Get(manifest,"difficulties")))
            {var d=ProjectJson.Object(row);savedJackets[ProjectJson.Integer(d,"index")]=project.Resolve(ProjectJson.Text(d,"jacket"));}
        }
        public byte[] SavedChartBytes(int index)
        {return (byte[])baseline[project.Difficulties[index].WorkingPath].Clone();}
        public bool MetadataDirty
        {get{return project.Title!=savedTitle||project.Artist!=savedArtist||project.AudioOffsetMs!=savedOffset||project.PreviewStartSeconds!=savedPreviewStart||project.PreviewEndSeconds!=savedPreviewEnd||
            project.AudioPath!=savedAudio||project.JacketPath!=savedJacket||project.JacketInventorySha256!=savedJacketInventory||
            Enumerable.Range(0,4).Any(i=>project.Difficulties[i].Rating!=savedRatings[i]||(project.Difficulties[i].Designer??"")!=savedDesigners[i]||(project.Difficulties[i].JacketDesigner??"")!=savedJacketDesigners[i]||project.Difficulties[i].JacketPath!=savedJackets[i]);}}
        public bool ChartDirty(int index)
        {var d=project.Difficulties[index];return d.Document!=null&&!Same(d.Document.ToBytes(),baseline[d.WorkingPath]);}
        public bool HasChanges
        {get{return MetadataDirty||Enumerable.Range(0,4).Any(ChartDirty)||pending.Any(p=>!Same(p.Value,baseline[p.Key]));}}
        public int[] DirtyDifficulties {get{return Enumerable.Range(0,4).Where(ChartDirty).ToArray();}}
        // Measured evidence edits also stay in memory until the ordinary Save command.
        public byte[] EvidenceBytes(int index)
        {
            string path=project.Resolve(".ifstudio/Score/"+index+".json");byte[] bytes;
            return pending.TryGetValue(path,out bytes)?bytes:baseline[path];
        }
        public void StageEvidence(int index,byte[] bytes)
        {
            if(index<0||index>3||bytes==null)throw new ArgumentException("Invalid evidence draft.");
            ProjectJson.Parse(new UTF8Encoding(false,true).GetString(bytes));
            pending[project.Resolve(".ifstudio/Score/"+index+".json")]=(byte[])bytes.Clone();
        }
        // Fixed allowlisted companion path, captured at OPEN (not first Save).
        // Null Stage means revert this draft to the last saved baseline.
        public byte[] RegisterChartCompanion()
        {
            string path=project.Resolve(ChartCompanionSession.RelativePath);
            if(!baseline.ContainsKey(path))Remember(path);
            var b=baseline[path];return b==null?null:(byte[])b.Clone();
        }
        public void StageChartCompanion(byte[] bytes)
        {
            string path=project.Resolve(ChartCompanionSession.RelativePath);
            if(!baseline.ContainsKey(path))throw new InvalidOperationException("Register chart companion when opening the song.");
            if(bytes==null){pending.Remove(path);return;}
            if(bytes.Length>65536)throw new InvalidDataException("Chart companion exceeds 64 KiB.");
            var root=ProjectJson.Object(ProjectJson.Parse(new UTF8Encoding(false,true).GetString(bytes)));
            if(ProjectJson.Text(root,"format")!="InFalsusStudioChartNotes"||ProjectJson.Integer(root,"version")!=1&&ProjectJson.Integer(root,"version")!=2)
                throw new InvalidDataException("Invalid chart companion format.");
            pending[path]=(byte[])bytes.Clone();
        }
        private static void Match(string path,byte[] bytes)
        {if(!Same(Read(path),bytes))throw new IOException("File changed outside this session; nothing is silently overwritten: "+path);}
        private void Add(List<Write> writes,string path,byte[] bytes)
        {
            project.Resolve(project.Relative(path));byte[] before=baseline[path];
            if(!Same(before,bytes))writes.Add(new Write{Path=path,Before=before,After=bytes});
        }
        /// <summary>Only call in response to explicit Save/Ctrl+S or a Save confirmation.</summary>
        public string Save(Action<int,string> beforeReplaceForTests=null)
        {
            if(project.JacketChoiceRequired)throw new InvalidOperationException("Choose the jacket image before saving.");
            bool previewChange=project.PreviewStartSeconds!=savedPreviewStart||project.PreviewEndSeconds!=savedPreviewEnd;
            if(previewChange)NativeSongDetails.ValidatePreview(project.PreviewStartSeconds,project.PreviewEndSeconds);
            var writes=new List<Write>();
            foreach(var d in project.Difficulties)
            {
                if(d.Document==null)throw new InvalidOperationException("Missing in-memory chart "+d.Index);
                if(!d.Document.ReadEvents().Any(e=>e.Kind==EventKind.Chart))throw new InvalidDataException("Chart header missing in difficulty "+d.Index);
                var sorted=d.Document.Clone();sorted.SortTimedStatements();
                Add(writes,d.WorkingPath,sorted.ToBytes());
            }
            string source=project.Resolve("song.json");byte[] original=baseline[source];
            bool sourceChange=previewChange||project.Title!=savedTitle||project.Artist!=savedArtist||Enumerable.Range(0,4).Any(i=>project.Difficulties[i].Rating!=savedRatings[i]||(project.Difficulties[i].Designer??"")!=savedDesigners[i]||(project.Difficulties[i].JacketDesigner??"")!=savedJacketDesigners[i]);
            if(sourceChange&&original!=null)
            {
                var patch=new LosslessJsonPatch(original);
                if(project.Title!=savedTitle)patch.DisplayText("title",project.Title);
                if(project.Artist!=savedArtist)patch.DisplayText("artist",project.Artist);
                for(int i=0;i<4;i++)if(project.Difficulties[i].Rating!=savedRatings[i])patch.Rating(i,project.Difficulties[i].Rating);
                byte[] sourceBytes=patch.ToBytes();
                for(int i=0;i<4;i++)if((project.Difficulties[i].Designer??"")!=savedDesigners[i])
                    sourceBytes=NativeChartDesigner.Patch(sourceBytes,i,project.Difficulties[i].Designer);
                if(previewChange)sourceBytes=NativeSongDetails.PatchPreview(sourceBytes,project.PreviewStartSeconds,project.PreviewEndSeconds);
                for(int i=0;i<4;i++)if((project.Difficulties[i].JacketDesigner??"")!=savedJacketDesigners[i])
                    sourceBytes=NativeSongDetails.PatchJacketDesigner(sourceBytes,i,project.Difficulties[i].JacketDesigner);
                Add(writes,source,sourceBytes);
            }
            foreach(var entry in pending)Add(writes,entry.Key,entry.Value);
            string manifest=project.Resolve(SongFolderProject.ManifestFile);
            Add(writes,manifest,new UTF8Encoding(false).GetBytes(project.ManifestText()));
            // Preflight the entire batch before creating a recovery directory or replacing anything.
            foreach(var w in writes)Match(w.Path,w.Before);
            if(writes.Count==0){foreach(var d in project.Difficulties)d.Document.SortTimedStatements();RememberMetadata();pending.Clear();return "Already saved; no file writes needed.";}
            string backup=project.Resolve(".ifstudio/SaveHistory/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            var journal=new List<object>();
            for(int i=0;i<writes.Count;i++)
            {
                var w=writes[i];string prefix=i.ToString("D2",CultureInfo.InvariantCulture);
                if(w.Before!=null)File.WriteAllBytes(Path.Combine(backup,prefix+".before"),w.Before);
                File.WriteAllBytes(Path.Combine(backup,prefix+".after"),w.After);
                journal.Add(new Dictionary<string,object>{{"path",project.Relative(w.Path)},{"before",w.Before==null?null:prefix+".before"},{"after",prefix+".after"}});
            }
            File.WriteAllText(Path.Combine(backup,"files.json"),ProjectJson.Write(journal),new UTF8Encoding(false));
            try
            {
                for(int i=0;i<writes.Count;i++)
                {
                    var w=writes[i];if(beforeReplaceForTests!=null)beforeReplaceForTests(i,w.Path);
                    Match(w.Path,w.Before);Directory.CreateDirectory(Path.GetDirectoryName(w.Path));
                    w.Attempted=true;SongMetadataEdit.WriteBytesAtomic(w.Path,w.After);
                }
                foreach(var w in writes)Match(w.Path,w.After);
                File.WriteAllText(Path.Combine(backup,"COMMITTED.txt"),"Explicit project save. Multi-file recovery snapshots; not a power-loss-atomic transaction.");
            }
            catch(Exception error)
            {
                string failures="";
                foreach(var w in writes.AsEnumerable().Reverse().Where(w=>w.Attempted))
                {
                    try
                    {
                        byte[] current=Read(w.Path);if(Same(current,w.Before))continue;
                        if(!Same(current,w.After))throw new IOException("External modification; refusing rollback over it.");
                        if(w.Before==null)File.Delete(w.Path);else SongMetadataEdit.WriteBytesAtomic(w.Path,w.Before);
                    }
                    catch(Exception ex){failures+=" ["+project.Relative(w.Path)+": "+ex.Message+"]";}
                }
                throw new IOException("Save failed. Drafts remain unsaved in memory. Recovery: "+backup+". "+error.Message+failures,error);
            }
            foreach(var w in writes)baseline[w.Path]=(byte[])w.After.Clone();
            foreach(var d in project.Difficulties)d.Document.SortTimedStatements();
            pending.Clear();RememberMetadata();
            return "Saved project (all modified difficulties + metadata). Backup: "+backup;
        }
    }
}
