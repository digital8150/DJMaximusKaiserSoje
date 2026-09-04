using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DJMaximusKaiserSoje.Core;

namespace DJMaximusKaiserSoje.Content
{
    public sealed class BeatmapParseException : Exception
    {
        public BeatmapParseException(string message) : base(message) { }
        public BeatmapParseException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>Maps osu!mania text into the engine's canonical beatmap model.</summary>
    public sealed class OsuManiaBeatmapParser
    {
        public Beatmap Parse(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new BeatmapParseException("Beatmap text is empty.");

            string section = string.Empty;
            string title = "Untitled";
            string artist = "Unknown Artist";
            string difficulty = "Normal";
            int keyCount = 0;
            double previewTimeMs = -1.0;
            double bpm = 0.0;
            string backgroundFilename = null;
            string videoFilename = null;
            double videoStartTimeMs = 0.0;
            var rawObjects = new List<string>();

            using (var reader = new StringReader(source))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        section = line;
                        continue;
                    }

                    if (section == "[Difficulty]" && TryProperty(line, "CircleSize", out string circleSize))
                    {
                        if (!double.TryParse(circleSize, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedKeys))
                            throw new BeatmapParseException("CircleSize must be a number.");
                        keyCount = (int)Math.Round(parsedKeys, MidpointRounding.AwayFromZero);
                    }
                    else if (section == "[Metadata]")
                    {
                        if (TryProperty(line, "Title", out string value)) title = value;
                        else if (TryProperty(line, "Artist", out value)) artist = value;
                        else if (TryProperty(line, "Version", out value)) difficulty = value;
                    }
                    else if (section == "[General]" && TryProperty(line, "PreviewTime", out string preview))
                    {
                        if (!double.TryParse(preview, NumberStyles.Float, CultureInfo.InvariantCulture, out previewTimeMs) || previewTimeMs < -1.0)
                            throw new BeatmapParseException("PreviewTime must be -1 or a non-negative number.");
                        if (previewTimeMs < 0.0) previewTimeMs = -1.0;
                    }
                    else if (section == "[TimingPoints]")
                    {
                        if (TryReadUninheritedBpm(line, out double parsedBpm) && bpm <= 0.0)
                            bpm = parsedBpm;
                    }
                    else if (section == "[HitObjects]")
                    {
                        rawObjects.Add(line);
                    }
                    else if (section == "[Events]")
                    {
                        ReadEvent(line, ref backgroundFilename, ref videoFilename, ref videoStartTimeMs);
                    }
                }
            }

            if (keyCount < 1 || keyCount > 18)
                throw new BeatmapParseException("CircleSize must define between 1 and 18 mania keys.");

            var notes = new List<BeatmapNote>(rawObjects.Count);
            for (int index = 0; index < rawObjects.Count; index++)
                notes.Add(ParseHitObject(rawObjects[index], keyCount, index + 1));
            notes.Sort((left, right) => left.StartTimeMs.CompareTo(right.StartTimeMs));

            var header = new BeatmapHeader(title, artist, difficulty, keyCount, bpm, previewTimeMs,
                backgroundFilename, videoFilename, videoStartTimeMs);
            return new Beatmap(header, notes);
        }

        private static bool TryReadUninheritedBpm(string line, out double bpm)
        {
            bpm = 0.0;
            string[] values = line.Split(',');
            if (values.Length < 7) return false;
            if (!double.TryParse(values[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double beatLength))
                throw new BeatmapParseException("Timing point beat length must be a number.");
            if (!int.TryParse(values[6].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int uninherited))
                throw new BeatmapParseException("Timing point inheritance flag must be 0 or 1.");
            if (uninherited != 1 || beatLength <= 0.0) return false;
            bpm = 60000.0 / beatLength;
            return true;
        }

        private static void ReadEvent(string line, ref string backgroundFilename, ref string videoFilename, ref double videoStartTimeMs)
        {
            string[] values = line.Split(',');
            if (values.Length < 3) return;
            if (values[0].Equals("Video", StringComparison.OrdinalIgnoreCase) || values[0] == "1")
            {
                if (!double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out videoStartTimeMs))
                    throw new BeatmapParseException("Video start time must be a number.");
                videoFilename = Unquote(values[2]);
            }
            else if (values[0] == "0")
            {
                backgroundFilename = Unquote(values[2]);
            }
        }

        private static BeatmapNote ParseHitObject(string line, int keyCount, int objectIndex)
        {
            string[] values = line.Split(',');
            if (values.Length < 5 ||
                !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
                !double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double startMs) ||
                !int.TryParse(values[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type))
                throw new BeatmapParseException("Hit object " + objectIndex + " is malformed.");
            if (startMs < 0.0) throw new BeatmapParseException("Hit object " + objectIndex + " has a negative start time.");

            int lane = Math.Min(keyCount - 1, Math.Max(0, x * keyCount / 512));
            double endMs = startMs;
            if ((type & 128) != 0)
            {
                if (values.Length < 6) throw new BeatmapParseException("Hold object " + objectIndex + " has no end time.");
                string endToken = values[5].Split(':')[0];
                if (!double.TryParse(endToken, NumberStyles.Float, CultureInfo.InvariantCulture, out endMs) || endMs < startMs)
                    throw new BeatmapParseException("Hold object " + objectIndex + " has an invalid end time.");
            }

            return new BeatmapNote(lane, startMs, endMs);
        }

        private static bool TryProperty(string line, string property, out string value)
        {
            int separator = line.IndexOf(':');
            if (separator <= 0 || !line.Substring(0, separator).Trim().Equals(property, StringComparison.Ordinal))
            {
                value = null;
                return false;
            }

            value = line.Substring(separator + 1).Trim();
            return true;
        }

        private static string Unquote(string value)
        {
            value = value.Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2);
            return value;
        }
    }
}
