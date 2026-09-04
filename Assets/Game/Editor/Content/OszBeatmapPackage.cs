using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using DJMaximusKaiserSoje.Content;
using DJMaximusKaiserSoje.Core;

namespace DJMaximusKaiserSoje.Editor
{
    public sealed class OszImportException : Exception
    {
        public OszImportException(string message) : base(message) { }
        public OszImportException(string message, Exception innerException) : base(message, innerException) { }
    }

    public sealed class OszChartSource
    {
        public OszChartSource(string archivePath, string text, Beatmap beatmap, string difficultyName)
        {
            ArchivePath = archivePath;
            Text = text;
            Beatmap = beatmap;
            DifficultyName = difficultyName ?? beatmap.Header.DifficultyName;
        }

        public string ArchivePath { get; }
        public string Text { get; }
        public Beatmap Beatmap { get; }

        /// <summary>
        /// Difficulty as the player should see it. For a pack this is the marker left over after the
        /// track name is taken out of the Version line, not the raw line.
        /// </summary>
        public string DifficultyName { get; }
    }

    public sealed class OszBinaryAsset
    {
        public OszBinaryAsset(string filename, byte[] bytes)
        {
            Filename = filename;
            Bytes = bytes;
        }

        public string Filename { get; }
        public byte[] Bytes { get; }
    }

    /// <summary>One playable song: its charts plus the media they share.</summary>
    public sealed class OszBeatmapPackage
    {
        public OszBeatmapPackage(string title, string artist, IReadOnlyList<OszChartSource> charts,
            OszBinaryAsset audio, OszBinaryAsset jacket, OszBinaryAsset video)
        {
            Title = title;
            Artist = artist;
            Charts = charts;
            Audio = audio;
            Jacket = jacket;
            Video = video;
        }

        public string Title { get; }
        public string Artist { get; }
        public IReadOnlyList<OszChartSource> Charts { get; }
        public OszBinaryAsset Audio { get; }
        public OszBinaryAsset Jacket { get; }
        public OszBinaryAsset Video { get; }
    }

    /// <summary>
    /// Everything one archive yields. A normal beatmap set produces a single song; a pack produces
    /// one song per audio track. Songs that cannot be assembled are reported as warnings so a broken
    /// track never costs the rest of a pack.
    /// </summary>
    public sealed class OszArchiveContents
    {
        public OszArchiveContents(IReadOnlyList<OszBeatmapPackage> songs, IReadOnlyList<string> warnings)
        {
            Songs = songs;
            Warnings = warnings;
        }

        public IReadOnlyList<OszBeatmapPackage> Songs { get; }
        public IReadOnlyList<string> Warnings { get; }
    }

    /// <summary>
    /// Reads an osu! archive without extracting arbitrary paths to disk. ZipArchive is the
    /// maintained .NET implementation already shipped with Unity, so no custom archive format or
    /// third-party dependency is needed.
    /// </summary>
    public static class OszBeatmapPackageReader
    {
        private const long MaximumEntryBytes = 512L * 1024L * 1024L;

        /// <summary>
        /// Packs ship placeholder charts ("Delete upon download", "Not for Play") that hold a handful
        /// of notes and no song. Below this they are filler, not content.
        /// </summary>
        private const int MinimumPlayableNotes = 32;

        private static readonly int[] SupportedKeyCounts = { 4, 6, 8 };

        private sealed class ParsedChart
        {
            public string ArchivePath;
            public string Text;
            public Beatmap Beatmap;
        }

        public static OszArchiveContents Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new OszImportException("선택한 .osz 파일을 찾을 수 없습니다.");

            try
            {
                using (var stream = File.OpenRead(path))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
                    return ReadArchive(archive);
            }
            catch (OszImportException)
            {
                throw;
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is IOException)
            {
                throw new OszImportException(".osz 압축 파일을 읽을 수 없습니다: " + exception.Message, exception);
            }
        }

        private static OszArchiveContents ReadArchive(ZipArchive archive)
        {
            var warnings = new List<string>();
            List<ParsedChart> charts = ParseCharts(archive, warnings);
            if (charts.Count == 0)
                throw new OszImportException("4키, 6키 또는 8키 osu!mania 채보가 없습니다.");

            List<IGrouping<string, ParsedChart>> tracks = charts
                .Where(chart => !string.IsNullOrWhiteSpace(chart.Beatmap.Header.AudioFilename))
                .GroupBy(chart => Normalize(chart.Beatmap.Header.AudioFilename).ToLowerInvariant())
                .ToList();
            if (tracks.Count == 0) throw new OszImportException("채보에 AudioFilename이 없습니다.");

            bool isPack = tracks.Count > 1;
            var songs = new List<OszBeatmapPackage>();
            foreach (IGrouping<string, ParsedChart> track in tracks)
            {
                try
                {
                    songs.Add(BuildSong(archive, track.ToList(), isPack));
                }
                catch (OszImportException exception)
                {
                    warnings.Add(track.Key + ": " + exception.Message);
                }
            }

            if (songs.Count == 0)
                throw new OszImportException("가져올 수 있는 곡이 없습니다. " + string.Join(" / ", warnings.ToArray()));
            return new OszArchiveContents(songs, warnings);
        }

        private static List<ParsedChart> ParseCharts(ZipArchive archive, ICollection<string> warnings)
        {
            var charts = new List<ParsedChart>();
            foreach (ZipArchiveEntry entry in archive.Entries
                         .Where(value => value.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)))
            {
                string text = ReadText(entry);
                if (!IsMania(text)) continue;

                Beatmap beatmap;
                try
                {
                    beatmap = new OsuManiaBeatmapParser().Parse(text);
                }
                catch (BeatmapParseException exception)
                {
                    warnings.Add(entry.FullName + " 채보를 읽을 수 없습니다: " + exception.Message);
                    continue;
                }

                if (!SupportedKeyCounts.Contains(beatmap.Header.KeyCount)) continue;
                if (beatmap.Notes.Count < MinimumPlayableNotes)
                {
                    warnings.Add(entry.FullName + " 채보는 노트가 " + beatmap.Notes.Count + "개뿐이라 건너뜁니다.");
                    continue;
                }

                charts.Add(new ParsedChart { ArchivePath = entry.FullName, Text = text, Beatmap = beatmap });
            }
            return charts;
        }

        private static OszBeatmapPackage BuildSong(ZipArchive archive, IReadOnlyList<ParsedChart> track, bool isPack)
        {
            string title = null;
            string artist = null;
            var sources = new List<OszChartSource>(track.Count);
            foreach (ParsedChart chart in track)
            {
                string difficulty = chart.Beatmap.Header.DifficultyName;
                if (isPack)
                {
                    OszTrackLabel label = OszTrackLabel.Parse(difficulty);
                    if (!string.IsNullOrWhiteSpace(label.Title))
                    {
                        title = title ?? label.Title;
                        artist = artist ?? label.Artist;
                        // The line named the track, so anything left is the difficulty — often nothing.
                        difficulty = label.Difficulty ?? string.Empty;
                    }
                }

                sources.Add(new OszChartSource(chart.ArchivePath, chart.Text, chart.Beatmap, difficulty));
            }

            BeatmapHeader header = track[0].Beatmap.Header;
            var audio = ReadBinary(FindEntry(archive, header.AudioFilename), "오디오");
            string jacketFilename = track.Select(chart => chart.Beatmap.Header.BackgroundFilename)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && FindEntry(archive, value) != null);
            if (string.IsNullOrWhiteSpace(jacketFilename))
                throw new OszImportException("재킷으로 사용할 배경 이미지가 없습니다.");
            var jacket = ReadBinary(FindEntry(archive, jacketFilename), "배경 이미지");

            string videoFilename = track.Select(chart => chart.Beatmap.Header.VideoFilename)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && FindEntry(archive, value) != null);
            OszBinaryAsset video = string.IsNullOrWhiteSpace(videoFilename)
                ? null
                : ReadBinary(FindEntry(archive, videoFilename), "영상");

            return new OszBeatmapPackage(
                string.IsNullOrWhiteSpace(title) ? header.Title : title,
                string.IsNullOrWhiteSpace(artist) ? header.Artist : artist,
                sources, audio, jacket, video);
        }

        private static bool IsMania(string source)
        {
            string section = string.Empty;
            using (var reader = new StringReader(source))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                    {
                        section = line;
                        continue;
                    }
                    if (section != "[General]") continue;
                    int separator = line.IndexOf(':');
                    if (separator <= 0 || !line.Substring(0, separator).Trim().Equals("Mode", StringComparison.Ordinal)) continue;
                    return line.Substring(separator + 1).Trim() == "3";
                }
            }
            return false;
        }

        private static string ReadText(ZipArchiveEntry entry)
        {
            EnsureSafeSize(entry);
            using (Stream stream = entry.Open())
            using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), true))
                return reader.ReadToEnd();
        }

        private static OszBinaryAsset ReadBinary(ZipArchiveEntry entry, string description)
        {
            if (entry == null) throw new OszImportException(description + " 파일이 압축 안에 없습니다.");
            EnsureSafeSize(entry);
            using (Stream input = entry.Open())
            using (var output = new MemoryStream())
            {
                input.CopyTo(output);
                return new OszBinaryAsset(Path.GetFileName(entry.FullName), output.ToArray());
            }
        }

        private static void EnsureSafeSize(ZipArchiveEntry entry)
        {
            if (entry.Length < 0 || entry.Length > MaximumEntryBytes)
                throw new OszImportException(entry.FullName + " 파일이 너무 큽니다.");
        }

        private static ZipArchiveEntry FindEntry(ZipArchive archive, string referencedPath)
        {
            string normalized = Normalize(referencedPath);
            if (normalized == null) return null;
            ZipArchiveEntry exact = archive.Entries.FirstOrDefault(entry =>
                string.Equals(Normalize(entry.FullName), normalized, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            string filename = Path.GetFileName(normalized);
            ZipArchiveEntry[] matches = archive.Entries.Where(entry =>
                string.Equals(Path.GetFileName(entry.FullName), filename, StringComparison.OrdinalIgnoreCase)).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string value = path.Replace('\\', '/').TrimStart('/');
            if (value.Split('/').Any(part => part == "..")) return null;
            return value;
        }
    }
}
