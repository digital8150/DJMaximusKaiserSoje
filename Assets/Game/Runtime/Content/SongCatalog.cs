using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DJMaximusKaiserSoje.Core;
using UnityEngine;

namespace DJMaximusKaiserSoje.Content
{
    [Serializable]
    public sealed class SongCatalogDocument
    {
        public int schemaVersion;
        public SongCatalogEntry[] songs;

        [NonSerialized] public int OriginalSchemaVersion;
        [NonSerialized] public bool WasMigrated;

        public int SchemaVersion => schemaVersion;
        public IReadOnlyList<SongCatalogEntry> Songs => songs ?? Array.Empty<SongCatalogEntry>();
    }

    [Serializable]
    public sealed class SongCatalogEntry
    {
        public string id;
        public string title;
        public string artist;
        public string audioAddress;
        public string videoAddress;
        public string jacketAddress;
        public double bpm;
        public string category;
        public SongChartEntry[] charts;

        public string Id => id;
        public string Title => title;
        public string Artist => artist;
        public string AudioAddress => audioAddress;
        public string VideoAddress => videoAddress;
        public string JacketAddress => jacketAddress;
        public double Bpm => bpm;
        public string Category => category;
        public IReadOnlyList<SongChartEntry> Charts => charts ?? Array.Empty<SongChartEntry>();
    }

    [Serializable]
    public sealed class SongChartEntry
    {
        // The v1 field remains part of the document so old catalogs can be parsed unchanged.
        public string difficulty;
        public string tier;
        public int level;
        public int keyCount;
        public int noteCount;
        public string beatmapAddress;
        public string coverAddress;

        public string DifficultyName => difficulty;
        public string Tier => tier;
        public int Level => level;
        public int KeyCount => keyCount;
        public int NoteCount => noteCount;
        public string BeatmapAddress => beatmapAddress;
        public string CoverAddress => coverAddress;
    }

    public sealed class SongCatalogException : Exception
    {
        public SongCatalogException(string message) : base(message) { }
        public SongCatalogException(string message, Exception innerException) : base(message, innerException) { }
    }

    public static class SongCatalogParser
    {
        public const int SupportedSchemaVersion = 2;
        public const int LegacySchemaVersion = 1;

        private static readonly Regex LevelPattern = new Regex(@"(?<![A-Za-z])\d+(?:\.\d+)?", RegexOptions.Compiled);

        public static SongCatalogDocument Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new SongCatalogException("Song catalog is empty.");

            SongCatalogDocument catalog;
            try
            {
                catalog = JsonUtility.FromJson<SongCatalogDocument>(json);
            }
            catch (ArgumentException exception)
            {
                throw new SongCatalogException("Song catalog JSON is invalid: " + exception.Message, exception);
            }

            if (catalog == null)
                throw new SongCatalogException("Song catalog JSON did not contain a document.");
            if (catalog.schemaVersion != LegacySchemaVersion && catalog.schemaVersion != SupportedSchemaVersion)
                throw new SongCatalogException("Unsupported song catalog schema version " + catalog.schemaVersion + ". Supported versions are 1 and 2.");
            if (catalog.songs == null || catalog.songs.Length == 0)
                throw new SongCatalogException("Song catalog contains no songs.");

            catalog.OriginalSchemaVersion = catalog.schemaVersion;
            if (catalog.schemaVersion == LegacySchemaVersion)
                MigrateV1(catalog);

            catalog.schemaVersion = SupportedSchemaVersion;
            Validate(catalog);
            return catalog;
        }

        public static DifficultyTier InferTier(string difficultyName, int level = 0)
        {
            string value = (difficultyName ?? string.Empty).Trim().ToLowerInvariant();
            if (level <= 0)
            {
                Match levelMatch = LevelPattern.Match(value);
                if (levelMatch.Success)
                    int.TryParse(levelMatch.Value.Split('.')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out level);
            }
            if (value.Contains("easy") || value == "beginner" || value == "lv.1" || value == "1")
                return DifficultyTier.Easy;
            if (value.Contains("normal") || value.Contains("novice") || value == "lv.2" || value == "lv.3")
                return DifficultyTier.Normal;
            if (value.Contains("hard") || value.Contains("advanced") || value == "lv.20")
                return DifficultyTier.Hard;
            if (value.Contains("insane") || value.Contains("expert") || value.Contains("super"))
                return DifficultyTier.SuperHard;
            if (value.Contains("satellite") || value.Contains("stepmania") || value.Contains("challenge") || level >= 40)
                return DifficultyTier.Over;
            if (level > 0)
            {
                if (level <= 5) return DifficultyTier.Easy;
                if (level <= 12) return DifficultyTier.Normal;
                if (level <= 20) return DifficultyTier.Hard;
                if (level < 40) return DifficultyTier.SuperHard;
                return DifficultyTier.Over;
            }

            return DifficultyTier.Normal;
        }

        public static int InferLevel(string difficultyName, DifficultyTier tier)
        {
            Match match = LevelPattern.Match(difficultyName ?? string.Empty);
            if (match.Success && int.TryParse(match.Value.Split('.')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                return Math.Max(1, parsed);

            switch (tier)
            {
                case DifficultyTier.Easy: return 1;
                case DifficultyTier.Normal: return 5;
                case DifficultyTier.Hard: return 10;
                case DifficultyTier.SuperHard: return 15;
                default: return 20;
            }
        }

        private static void MigrateV1(SongCatalogDocument catalog)
        {
            catalog.WasMigrated = true;
            for (int songIndex = 0; songIndex < catalog.songs.Length; songIndex++)
            {
                SongCatalogEntry song = catalog.songs[songIndex];
                if (song == null) continue;
                song.category = string.IsNullOrWhiteSpace(song.category) ? "Prototype" : song.category;
                if (song.charts == null) continue;

                for (int chartIndex = 0; chartIndex < song.charts.Length; chartIndex++)
                {
                    SongChartEntry chart = song.charts[chartIndex];
                    if (chart == null) continue;
                    DifficultyTier inferredTier = InferTier(chart.difficulty, chart.level);
                    if (string.IsNullOrWhiteSpace(chart.tier)) chart.tier = inferredTier.ToString();
                    if (string.IsNullOrWhiteSpace(chart.difficulty)) chart.difficulty = chart.tier;
                    if (chart.level <= 0) chart.level = InferLevel(chart.difficulty, inferredTier);
                    if (chart.keyCount <= 0) chart.keyCount = 4;
                    if (string.IsNullOrWhiteSpace(song.jacketAddress) && !string.IsNullOrWhiteSpace(chart.coverAddress))
                        song.jacketAddress = chart.coverAddress;
                }
            }
        }

        private static void Validate(SongCatalogDocument catalog)
        {
            var songIds = new HashSet<string>(StringComparer.Ordinal);
            for (int songIndex = 0; songIndex < catalog.songs.Length; songIndex++)
            {
                SongCatalogEntry song = catalog.songs[songIndex];
                if (song == null || string.IsNullOrWhiteSpace(song.id) || string.IsNullOrWhiteSpace(song.title) ||
                    string.IsNullOrWhiteSpace(song.audioAddress) || song.charts == null || song.charts.Length == 0)
                    throw new SongCatalogException("Song catalog entry " + songIndex + " is incomplete.");
                if (!songIds.Add(song.id))
                    throw new SongCatalogException("Song id is duplicated: " + song.id);
                if (double.IsNaN(song.bpm) || double.IsInfinity(song.bpm) || song.bpm < 0.0)
                    throw new SongCatalogException("Song " + song.id + " has an invalid BPM.");

                var chartIds = new HashSet<DifficultyTier>();
                for (int chartIndex = 0; chartIndex < song.charts.Length; chartIndex++)
                {
                    SongChartEntry chart = song.charts[chartIndex];
                    if (chart != null && string.IsNullOrWhiteSpace(chart.difficulty) && !string.IsNullOrWhiteSpace(chart.tier))
                        chart.difficulty = chart.tier;
                    if (chart == null || string.IsNullOrWhiteSpace(chart.difficulty) ||
                        string.IsNullOrWhiteSpace(chart.tier) || string.IsNullOrWhiteSpace(chart.beatmapAddress) ||
                        (string.IsNullOrWhiteSpace(chart.coverAddress) && string.IsNullOrWhiteSpace(song.jacketAddress)))
                        throw new SongCatalogException("Chart " + chartIndex + " for song " + song.id + " is incomplete.");
                    if (!TryParseTier(chart.tier, out DifficultyTier tier))
                        throw new SongCatalogException("Chart " + chartIndex + " for song " + song.id + " has an unknown tier: " + chart.tier);
                    if (!chartIds.Add(tier))
                        throw new SongCatalogException("Song " + song.id + " has more than one chart in tier " + tier + ".");
                    if (chart.level < 1 || chart.noteCount < 0 || chart.keyCount < 0)
                        throw new SongCatalogException("Chart " + chartIndex + " for song " + song.id + " has invalid chart metadata.");
                }
            }
        }

        public static bool TryParseTier(string value, out DifficultyTier tier)
        {
            if (Enum.TryParse(value, true, out tier) && Enum.IsDefined(typeof(DifficultyTier), tier)) return true;
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
            switch (normalized)
            {
                case "ez": tier = DifficultyTier.Easy; return true;
                case "nm": tier = DifficultyTier.Normal; return true;
                case "hd": tier = DifficultyTier.Hard; return true;
                case "shd":
                case "expert":
                case "insane": tier = DifficultyTier.SuperHard; return true;
                case "ovr":
                case "challenge":
                case "satellite": tier = DifficultyTier.Over; return true;
                default: tier = default; return false;
            }
        }
    }

    public sealed class CatalogSongLibrary : ISongLibrary
    {
        private readonly List<SongSummary> songs;
        private readonly Dictionary<string, SongSummary> songsById;
        private readonly Dictionary<string, ChartSummary> chartsById;
        private readonly Dictionary<string, SongContentAddresses> contentByChartId;

        public CatalogSongLibrary(SongCatalogDocument catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            songs = new List<SongSummary>(catalog.songs.Length);
            songsById = new Dictionary<string, SongSummary>(StringComparer.Ordinal);
            chartsById = new Dictionary<string, ChartSummary>(StringComparer.Ordinal);
            contentByChartId = new Dictionary<string, SongContentAddresses>(StringComparer.Ordinal);

            for (int songIndex = 0; songIndex < catalog.songs.Length; songIndex++)
            {
                SongCatalogEntry entry = catalog.songs[songIndex];
                var chartSummaries = new List<ChartSummary>(entry.charts.Length);
                string jacket = entry.jacketAddress;
                for (int chartIndex = 0; chartIndex < entry.charts.Length; chartIndex++)
                {
                    SongChartEntry chart = entry.charts[chartIndex];
                    if (!SongCatalogParser.TryParseTier(chart.tier, out DifficultyTier tier))
                        throw new SongCatalogException("Unknown tier in catalog: " + chart.tier);
                    string chartId = entry.id + "." + tier.ToString().ToLowerInvariant();
                    string chartJacket = string.IsNullOrWhiteSpace(chart.coverAddress) ? jacket : chart.coverAddress;
                    if (string.IsNullOrWhiteSpace(jacket)) jacket = chartJacket;
                    var summary = new ChartSummary(chartId, entry.id, tier, chart.difficulty, chart.level, chart.keyCount, chart.noteCount);
                    chartSummaries.Add(summary);
                    chartsById.Add(chartId, summary);
                    contentByChartId.Add(chartId, new SongContentAddresses(
                        chartId, chart.beatmapAddress, entry.audioAddress, chartJacket, entry.videoAddress));
                }

                var song = new SongSummary(entry.id, entry.title, entry.artist, entry.bpm, entry.category, jacket, chartSummaries);
                songs.Add(song);
                songsById.Add(song.Id, song);
            }
        }

        public IReadOnlyList<SongSummary> Songs => songs;

        public bool TryGetSong(string songId, out SongSummary song)
        {
            song = null;
            return songId != null && songsById.TryGetValue(songId, out song);
        }

        public bool TryGetChart(string chartId, out ChartSummary chart)
        {
            chart = null;
            return chartId != null && chartsById.TryGetValue(chartId, out chart);
        }

        public bool TryGetContent(string chartId, out SongContentAddresses content)
        {
            content = default;
            return chartId != null && contentByChartId.TryGetValue(chartId, out content);
        }

        public SongContentAddresses GetContent(string chartId)
        {
            if (!TryGetContent(chartId, out SongContentAddresses content))
                throw new KeyNotFoundException("Chart is not in the song catalog: " + chartId);
            return content;
        }
    }

    public readonly struct SongContentAddresses
    {
        public SongContentAddresses(string chartId, string chartAddress, string audioAddress, string jacketAddress, string videoAddress)
        {
            ChartId = chartId;
            ChartAddress = chartAddress;
            AudioAddress = audioAddress;
            JacketAddress = jacketAddress;
            VideoAddress = videoAddress;
        }

        public string ChartId { get; }
        public string ChartAddress { get; }
        public string BeatmapAddress => ChartAddress;
        public string AudioAddress { get; }
        public string JacketAddress { get; }
        public string VideoAddress { get; }
        public bool HasVideo => !string.IsNullOrWhiteSpace(VideoAddress);
    }
}
