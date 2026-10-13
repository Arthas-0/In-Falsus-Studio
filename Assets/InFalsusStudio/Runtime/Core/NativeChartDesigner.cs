using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    /// <summary>Native per-difficulty credit. Reads source fields, but only ManualSongSession.Save
    /// calls Patch. Unrelated JSON bytes (including 64-bit IDs) are never reserialized.</summary>
    public static class NativeChartDesigner
    {
        private const string Key = "DisplayChartDesigner";
        private static readonly string[] Lists = { "/charts", "/song_info/ChartInfos" };

        public static string Validate(string value)
        {
            value = (value ?? "").Trim();
            if (value.Length > 128 || value.Any(char.IsControl))
                throw new ArgumentException("Chart Designer: up to 128 characters, one line only.");
            return value;
        }
        public static void Stage(SongFolderProject project, int difficulty, string value)
        {
            if (project == null || difficulty < 0 || difficulty > 3)
                throw new ArgumentException("Open a song project and select a difficulty first.");
            project.Difficulties[difficulty].Designer = Validate(value); // RAM only.
        }
        private static List<string> Rows(LosslessJsonPatch patch, int difficulty)
        {
            if (difficulty < 0 || difficulty > 3) throw new ArgumentOutOfRangeException("difficulty");
            var result = new List<string>();
            foreach (string list in Lists)
            {
                if (!patch.Contains(list) || patch.Get(list) == null) continue;
                if (!(patch.Get(list) is List<object>))
                    throw new InvalidDataException("Expected a native chart array: " + list);
                int hits = 0;
                for (int i = 0; patch.Contains(list + "/" + i); i++)
                {
                    string row = list + "/" + i;
                    object flag = patch.Get(row + "/Difficulty");
                    if (!(flag is double) || (double)flag != (1 << difficulty)) continue;
                    if (++hits > 1) throw new InvalidDataException("Duplicate native difficulty entries in " + list);
                    result.Add(row);
                }
            }
            return result;
        }
        // Native source wins over a stale designer cache in infalsus.studio.json.
        // The v1 companion migration (if present) is an explicitly marked RAM draft afterwards.
        public static void ReadInto(SongFolderProject project, byte[] source)
        {
            if (source == null) return;
            var patch = new LosslessJsonPatch(source);
            for (int i = 0; i < 4; i++)
            {
                foreach (string row in Rows(patch, i))
                {
                    if (!patch.Contains(row + "/" + Key)) continue;
                    object value = patch.Get(row + "/" + Key);
                    if (value == null || value is string)
                    {
                        project.Difficulties[i].Designer = value as string ?? "";
                        break; // /charts is primary; ChartInfos is its mirror.
                    }
                }
            }
        }
        public static byte[] Patch(byte[] source, int difficulty, string value)
        {
            value = Validate(value);
            var patch = new LosslessJsonPatch(source);
            var rows = Rows(patch, difficulty);
            if (rows.Count == 0)
                throw new InvalidDataException("Chart Designer cannot be saved: song.json has no recognized entry for difficulty " + difficulty + ". No files changed.");
            var missing = new List<string>();
            foreach (string row in rows)
            {
                string path = row + "/" + Key;
                if (!patch.Contains(path)) { missing.Add(row); continue; }
                object old = patch.Get(path);
                if (old != null && !(old is string))
                    throw new InvalidDataException("Chart Designer is not a text field: " + path);
                patch.Set(path, value);
            }
            byte[] result = patch.ToBytes();
            // Some valid source versions omit optional credits. Add just the known member
            // in a verified difficulty row, not a fabricated chart or rewritten object.
            foreach (string row in missing) result = InsertMember(result, row, value);
            return result;
        }
        private static byte[] InsertMember(byte[] source, string parentPath, string value)
        {
            string text = new UTF8Encoding(false, true).GetString(source);
            var index = new ObjectEnds(text);
            int end;
            if (!index.Ends.TryGetValue(parentPath, out end))
                throw new InvalidDataException("Native chart row is not an object: " + parentPath);
            var existing = new LosslessJsonPatch(source);
            var row = ProjectJson.Object(existing.Get(parentPath));
            if (row.ContainsKey(Key)) throw new InvalidDataException("Credit member already exists.");
            string addition = (row.Count == 0 ? "" : ",") + "\"" + Key + "\":" + ProjectJson.Write(value).TrimEnd('\r', '\n');
            string output = text.Insert(end, addition);
            ProjectJson.Parse(output);
            return new UTF8Encoding(false, true).GetBytes(output);
        }
        // Index only object closing braces. The original text has already passed the strict
        // JSON parser. This scanner preserves BOM, CRLF, escapes, and number lexemes.
        private sealed class ObjectEnds
        {
            private readonly string text;
            private int p;
            public readonly Dictionary<string, int> Ends = new Dictionary<string, int>(StringComparer.Ordinal);
            public ObjectEnds(string value)
            {
                text = value; ProjectJson.Parse(text);
                p = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
                Scan("", 0);
            }
            private void Space() { while (p < text.Length && char.IsWhiteSpace(text[p])) p++; }
            private string String()
            {
                int start = p++;
                while (p < text.Length)
                {
                    char c = text[p++];
                    if (c == '\\') { p++; continue; }
                    if (c == '"') return (string)ProjectJson.Parse(text.Substring(start, p - start));
                }
                throw new InvalidDataException("Unterminated JSON string.");
            }
            private static string Child(string parent, string key)
            { return parent + "/" + key.Replace("~", "~0").Replace("/", "~1"); }
            private void Scan(string path, int depth)
            {
                if (depth > 64) throw new InvalidDataException("Metadata nesting too deep.");
                Space(); char c = text[p];
                if (c == '{')
                {
                    p++; Space();
                    if (text[p] != '}') while (true)
                    {
                        string key = String(); Space(); p++; // colon
                        Scan(Child(path, key), depth + 1); Space();
                        if (text[p] != ',') break;
                        p++; Space();
                    }
                    Ends.Add(path, p); p++;
                }
                else if (c == '[')
                {
                    p++; Space(); int i = 0;
                    if (text[p] != ']') while (true)
                    {
                        Scan(Child(path, (i++).ToString(CultureInfo.InvariantCulture)), depth + 1); Space();
                        if (text[p] != ',') break;
                        p++; Space();
                    }
                    p++;
                }
                else if (c == '"') String();
                else while (p < text.Length && text[p] != ',' && text[p] != ']' && text[p] != '}' && !char.IsWhiteSpace(text[p])) p++;
            }
        }
    }
}
