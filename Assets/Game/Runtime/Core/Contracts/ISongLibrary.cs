using System;
using System.Collections.Generic;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// The difficulty column a chart occupies on the select screen. Charts that outgrow the scale
    /// sit in <see cref="Over"/>.
    /// </summary>
    public enum DifficultyTier
    {
        Easy,
        Normal,
        Hard,
        SuperHard,
        Over
    }

    /// <summary>One playable chart, as far as the select screen is concerned.</summary>
    public sealed class ChartSummary
    {
        public ChartSummary(string id, string songId, DifficultyTier tier, string difficultyName, int level, int keyCount, int noteCount)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            SongId = songId ?? throw new ArgumentNullException(nameof(songId));
            Tier = tier;
            DifficultyName = difficultyName ?? string.Empty;
            Level = level;
            KeyCount = keyCount;
            NoteCount = noteCount;
        }

        public string Id { get; }
        public string SongId { get; }
        public DifficultyTier Tier { get; }

        /// <summary>The chart's own name, which is not always the tier's name.</summary>
        public string DifficultyName { get; }

        public int Level { get; }
        public int KeyCount { get; }
        public int NoteCount { get; }
    }

    public sealed class SongSummary
    {
        public SongSummary(string id, string title, string artist, double bpm, string category, string jacketAddress, IReadOnlyList<ChartSummary> charts)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Title = title ?? string.Empty;
            Artist = artist ?? string.Empty;
            Bpm = bpm;
            Category = category ?? string.Empty;
            JacketAddress = jacketAddress;
            Charts = charts ?? throw new ArgumentNullException(nameof(charts));
        }

        public string Id { get; }
        public string Title { get; }
        public string Artist { get; }
        public double Bpm { get; }

        /// <summary>A short tag shown beside the jacket, such as the pack a song came from.</summary>
        public string Category { get; }

        public string JacketAddress { get; }
        public IReadOnlyList<ChartSummary> Charts { get; }

        public bool TryGetChart(DifficultyTier tier, out ChartSummary chart)
        {
            for (int index = 0; index < Charts.Count; index++)
            {
                if (Charts[index].Tier != tier) continue;
                chart = Charts[index];
                return true;
            }

            chart = null;
            return false;
        }

        public bool TryGetChart(DifficultyTier tier, PlayStyle style, out ChartSummary chart)
        {
            for (int index = 0; index < Charts.Count; index++)
            {
                ChartSummary candidate = Charts[index];
                if (candidate.Tier != tier || !PlayStyleChartCompatibility.IsCompatible(style, candidate)) continue;
                chart = candidate;
                return true;
            }

            chart = null;
            return false;
        }
    }

    /// <summary>Everything the select screen can list, already resolved from the content catalog.</summary>
    public interface ISongLibrary
    {
        IReadOnlyList<SongSummary> Songs { get; }

        bool TryGetSong(string songId, out SongSummary song);

        bool TryGetChart(string chartId, out ChartSummary chart);
    }
}
