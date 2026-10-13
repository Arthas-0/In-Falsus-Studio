using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace InFalsusStudio.Core
{
    /// <summary>
    /// Decodes ONLY the timing schema established by the paired Coldsea/Chronophobia
    /// JSON and plaintext: type 1, value=BPM, transition=beats per bar. Type 0 remains
    /// track speed. This is not a general binary SPC decoder.
    /// Old icp_event wrappers contain the complete original JSON in base64, so they
    /// can be read without touching the file or reimporting any edited notes.
    /// </summary>
    public static class IcpTimingEvents
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public const int MaxRetainedPayloadCharacters = 65536;

        private static bool Number(Dictionary<string, object> row, string key, out double value)
        {
            object v = ProjectJson.Get(row, key);
            value = v is double ? (double)v : double.NaN;
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
        private static bool Fail(string message, out string reason)
        { reason = message; return false; }

        public static bool TryDecode(Dictionary<string, object> row, out double[] args, out string reason)
        {
            args = null; reason = "";
            double time, type;
            if (!Number(row, "timestamp_ms", out time) || time < 0 || time > AuthoringSafety.MaxTimelineMs)
                return Fail("Invalid ICP timing timestamp.", out reason);
            if (!Number(row, "type", out type) || type != Math.Truncate(type))
                return Fail("Missing/inexact ICP event type.", out reason);

            // Keep the earlier explicit named-schema import contract. Unknown type IDs
            // without explicitly named BPM/meter are never assumed to be timing.
            bool named = ProjectJson.Get(row, "bpm") != null || ProjectJson.Get(row, "beats_per_bar") != null;
            double bpm, meter;
            if (named)
            {
                if (!Number(row, "bpm", out bpm) || !Number(row, "beats_per_bar", out meter))
                    return Fail("Incomplete explicitly named BPM/meter fields.", out reason);
                if (type == 1 && (ProjectJson.Get(row, "value") != null || ProjectJson.Get(row, "transition") != null))
                {
                    double value, transition;
                    if (!Number(row, "value", out value) || !Number(row, "transition", out transition) || value != bpm || transition != meter)
                        return Fail("Conflicting named and type-1 BPM/meter fields.", out reason);
                }
            }
            else
            {
                if (type != 1) return Fail("ICP event type is not a supported BPM record.", out reason);
                if (!Number(row, "value", out bpm) || !Number(row, "transition", out meter))
                    return Fail("Type-1 timing needs numeric value (BPM) and transition (meter).", out reason);
            }
            if (bpm < .01 || bpm > 10000 || meter <= 0 || meter > 1000000)
                return Fail("BPM/meter outside the supported positive range.", out reason);

            // Optional corroborating binary payload: little-endian double BPM, float meter,
            // four zero reserved bytes. If present, inconsistent/unknown data stays opaque.
            object payload = ProjectJson.Get(row, "payload_hex");
            if (type == 1 && payload != null)
            {
                byte[] bytes;
                if (!(payload is string) || !TryHex((string)payload, out bytes) || bytes.Length != 16)
                    return Fail("Invalid type-1 payload_hex (expected 16 bytes).", out reason);
                var b = new byte[8]; var m = new byte[4];
                Buffer.BlockCopy(bytes, 0, b, 0, 8); Buffer.BlockCopy(bytes, 8, m, 0, 4);
                if (!BitConverter.IsLittleEndian) { Array.Reverse(b); Array.Reverse(m); }
                double payloadBpm = BitConverter.ToDouble(b, 0);
                float payloadMeter = BitConverter.ToSingle(m, 0);
                if (payloadBpm != bpm || payloadMeter != (float)meter || float.IsNaN(payloadMeter) || float.IsInfinity(payloadMeter))
                    return Fail("Type-1 payload does not agree with decoded BPM/meter.", out reason);
                for (int i = 12; i < 16; i++)
                    if (bytes[i] != 0) return Fail("Unverified nonzero type-1 reserved payload.", out reason);
            }
            args = new[] { time, bpm, meter };
            return true;
        }

        public static bool TryReadRetained(string arguments, out double[] args, out string reason)
        {
            args = null; reason = "";
            if (arguments == null || arguments.Length > MaxRetainedPayloadCharacters + 128)
                return Fail("Retained timing payload exceeds safety limit.", out reason);
            string[] tokens = arguments.Split(',');
            double time, type;
            if (tokens.Length != 3 || !double.TryParse(tokens[0], NumberStyles.Float, CI, out time) ||
                !double.TryParse(tokens[1], NumberStyles.Float, CI, out type))
                return Fail("Malformed icp_event wrapper.", out reason);
            string encoded = tokens[2].Trim();
            if (encoded.Length == 0 || encoded.Length > MaxRetainedPayloadCharacters)
                return Fail("Empty/oversize retained timing record.", out reason);
            try
            {
                var row = ProjectJson.Object(ProjectJson.Parse(Utf8.GetString(Convert.FromBase64String(encoded))));
                double actualTime, actualType;
                if (!Number(row, "timestamp_ms", out actualTime) || !Number(row, "type", out actualType) || actualTime != time || actualType != type)
                    return Fail("Wrapper timestamp/type disagrees with retained JSON.", out reason);
                return TryDecode(row, out args, out reason);
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidDataException || ex is DecoderFallbackException || ex is OverflowException)
            { return Fail("Retained timing record could not be decoded: " + ex.Message, out reason); }
        }
        public static string CanonicalCall(double[] args)
        {
            if (args == null || args.Length != 3) throw new ArgumentException("Expected time, BPM and meter.");
            return "bpm(" + args[0].ToString("G17", CI) + "," + args[1].ToString("G17", CI) + "," + args[2].ToString("G17", CI) + ")";
        }
        private static bool TryHex(string text, out byte[] bytes)
        {
            bytes = null;
            if (text == null || text.Length != 32) return false;
            bytes = new byte[16];
            for (int i = 0; i < bytes.Length; i++)
                if (!byte.TryParse(text.Substring(i * 2, 2), NumberStyles.HexNumber, CI, out bytes[i]))
                { bytes = null; return false; }
            return true;
        }
    }
}
