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
        public OszChartSource(string archivePath, string text, Beatmap beatmap)
        {
            ArchivePath = archivePath;
            Text = text;
            Beatmap = beatmap;
        }

        public string ArchivePath { get; }
        public string Text { get; }
        public Beatmap Beatmap { get; }
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

    public sealed class OszBeatmapPackage
    {
        public OszBeatmapPackage(IReadOnlyList<OszChartSource> charts, OszBinaryAsset audio,
            OszBinaryAsset jacket, OszBinaryAsset video)
        {
            Charts = charts;
            Audio = audio;
            Jacket = jacket;
            Video = video;
        }

        public IReadOnlyList<OszChartSource> Charts { get; }
        public OszBinaryAsset Audio { get; }
        public OszBinaryAsset Jacket { get; }
        public OszBinaryAsset Video { get; }
        public string Title => Charts[0].Beatmap.Header.Title;
        public string Artist => Charts[0].Beatmap.Header.Artist;
    }

    /// <summary>
    /// Reads an osu! archive without extracting arbitrary paths to disk. ZipArchive is the
    /// maintained .NET implementation already shipped with Unity, so no custom archive format or
    /// third-party dependency is needed.
    /// </summary>
    public static class OszBeatmapPackageReader
    {
        private const long MaximumEntryBytes = 512L * 1024L * 1024L;
        private static readonly int[] SupportedKeyCounts = { 4, 6, 8 };

        public static OszBeatmapPackage Read(string path)
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

        private static OszBeatmapPackage ReadArchive(ZipArchive archive)
        {
            var charts = new List<OszChartSource>();
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
                    throw new OszImportException(entry.FullName + " 채보를 읽을 수 없습니다: " + exception.Message, exception);
                }

                if (!SupportedKeyCounts.Contains(beatmap.Header.KeyCount)) continue;
                charts.Add(new OszChartSource(entry.FullName, text, beatmap));
            }

            if (charts.Count == 0)
                throw new OszImportException("4키, 6키 또는 8키 osu!mania 채보가 없습니다.");

            string audioFilename = charts[0].Beatmap.Header.AudioFilename;
            if (string.IsNullOrWhiteSpace(audioFilename))
                throw new OszImportException("채보에 AudioFilename이 없습니다.");
            if (charts.Any(chart => !string.Equals(chart.Beatmap.Header.AudioFilename, audioFilename,
                    StringComparison.OrdinalIgnoreCase)))
                throw new OszImportException("한 .osz 안에서 여러 오디오 파일을 사용하는 채보는 한 곡으로 가져올 수 없습니다.");

            var audio = ReadBinary(FindEntry(archive, audioFilename), "오디오");
            string jacketFilename = charts.Select(chart => chart.Beatmap.Header.BackgroundFilename)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && FindEntry(archive, value) != null);
            if (string.IsNullOrWhiteSpace(jacketFilename))
                throw new OszImportException("재킷으로 사용할 배경 이미지가 없습니다.");
            var jacket = ReadBinary(FindEntry(archive, jacketFilename), "배경 이미지");

            string videoFilename = charts.Select(chart => chart.Beatmap.Header.VideoFilename)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && FindEntry(archive, value) != null);
            OszBinaryAsset video = string.IsNullOrWhiteSpace(videoFilename)
                ? null
                : ReadBinary(FindEntry(archive, videoFilename), "영상");
            return new OszBeatmapPackage(charts, audio, jacket, video);
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
