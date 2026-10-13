using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace InFalsusStudio.Core
{
    /// <summary>Per-chart OFFSET companion. Chart Designer now belongs to native song metadata.
    /// Legacy v1 credits become a marked RAM migration; only explicit Save writes anything.</summary>
    public sealed class ChartCompanionSession
    {
        public const string RelativePath = ".ifstudio/chart-notes.json";
        private readonly SongFolderProject project;
        private readonly ManualSongSession session;
        private readonly double[] offsets = new double[4];
        private string savedCanonical;
        private bool upgradePending;
        private const string MigrationNotice = "1.0 Chart Designer credits loaded as an UNSAVED migration. Ctrl+S moves them into song.json; chart-notes.json will retain offsets only. Exit without saving changes no file.";
        public bool LegacyUpgradePending { get { return upgradePending; } }
        public ChartCompanionSession(SongFolderProject project, ManualSongSession session)
        {
            this.project = project ?? throw new ArgumentNullException("project");
            this.session = session ?? throw new ArgumentNullException("session");
            for (int i = 0; i < 4; i++) offsets[i] = project.AudioOffsetMs;
            byte[] bytes = session.RegisterChartCompanion();
            string[] legacy = null;
            if (bytes != null)
            {
                if (bytes.Length > 65536) throw new InvalidDataException("chart-notes.json exceeds 64 KiB.");
                var root = ProjectJson.Object(ProjectJson.Parse(new UTF8Encoding(false, true).GetString(bytes)));
                int version = ProjectJson.Integer(root, "version");
                if (ProjectJson.Text(root, "format") != "InFalsusStudioChartNotes" || version < 1 || version > 2)
                    throw new InvalidDataException("Unsupported chart-notes.json; original file was not changed.");
                legacy = version == 1 ? new string[4] : null;
                var rows = ProjectJson.Array(ProjectJson.Get(root, "charts")); var seen = new HashSet<int>();
                foreach (var value in rows)
                {
                    var row = ProjectJson.Object(value); int i = ProjectJson.Integer(row, "difficulty", -1); CheckIndex(i);
                    if (!seen.Add(i)) throw new InvalidDataException("Duplicate chart note difficulty.");
                    string expected = project.Relative(project.Difficulties[i].WorkingPath);
                    if (ProjectJson.Text(row, "chart") != expected) throw new InvalidDataException("Chart annotation path does not match its difficulty.");
                    offsets[i] = ValidateOffset(ProjectJson.Number(row, "audioOffsetMs"));
                    if (legacy != null && row.ContainsKey("chartDesigner"))
                        legacy[i] = ValidateDesigner(ProjectJson.Text(row, "chartDesigner"));
                }
                if (seen.Count != 4) throw new InvalidDataException("Chart notes must contain exactly four difficulty slots.");
            }
            savedCanonical = Serialize();
            if (legacy != null)
            {
                // Validate the whole old file before staging any credit. The baseline in
                // ManualSongSession still contains the native values and original bytes.
                for (int i = 0; i < 4; i++) if (legacy[i] != null) NativeChartDesigner.Stage(project, i, legacy[i]);
                upgradePending = true; Stage();
                project.Notices.Add(MigrationNotice);
            }
        }
        public static string ValidateDesigner(string text) { return NativeChartDesigner.Validate(text); }
        public static double ValidateOffset(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 3600000)
                throw new ArgumentOutOfRangeException("offset", "Audio offset must be finite and within ±3600000 ms.");
            return value;
        }
        private static void CheckIndex(int i) { if (i < 0 || i > 3) throw new ArgumentOutOfRangeException("difficulty"); }
        public string Designer(int index) { CheckIndex(index); return project.Difficulties[index].Designer ?? ""; }
        public double Offset(int index) { CheckIndex(index); return offsets[index]; }
        public void SetDesigner(int index, string value) { NativeChartDesigner.Stage(project, index, value); }
        public void SetOffset(int index, double value)
        {
            CheckIndex(index); ValidateOffset(value); if (offsets[index] == value) return;
            double old = offsets[index]; offsets[index] = value;
            try { Stage(); } catch { offsets[index] = old; throw; }
        }
        private void Stage()
        {
            string text = Serialize();
            session.StageChartCompanion(!upgradePending && text == savedCanonical ? null : new UTF8Encoding(false).GetBytes(text));
        }
        public void AcceptSaved() { savedCanonical = Serialize(); upgradePending = false; project.Notices.Remove(MigrationNotice); }
        private string Serialize()
        {
            var charts = new List<object>();
            for (int i = 0; i < 4; i++) charts.Add(new Dictionary<string, object> {
                {"difficulty",i}, {"chart",project.Relative(project.Difficulties[i].WorkingPath)}, {"audioOffsetMs",offsets[i]} });
            return ProjectJson.Write(new Dictionary<string, object> {
                {"format","InFalsusStudioChartNotes"}, {"version",2}, {"charts",charts} });
        }
    }
}
