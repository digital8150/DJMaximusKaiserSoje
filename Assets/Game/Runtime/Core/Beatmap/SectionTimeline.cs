using System;
using System.Collections.Generic;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// Divides a chart into equal, named time ranges. Equal ranges keep the result deterministic
    /// even when a chart has no section metadata of its own.
    /// </summary>
    public sealed class SectionTimeline
    {
        private static readonly string[] DefaultNames =
        {
            "Intro", "Verse", "Build", "Chorus", "Break", "Verse 2", "Finale", "Outro"
        };

        private readonly List<SectionMarker> sections;

        public SectionTimeline(double songLengthMs, int sectionCount = 8)
            : this(songLengthMs, CreateDefaultNames(sectionCount))
        {
        }

        public SectionTimeline(double songLengthMs, IReadOnlyList<string> names)
        {
            if (double.IsNaN(songLengthMs) || double.IsInfinity(songLengthMs) || songLengthMs < 0.0)
                throw new ArgumentOutOfRangeException(nameof(songLengthMs));
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (names.Count == 0) throw new ArgumentException("At least one section is required.", nameof(names));

            SongLengthMs = songLengthMs;
            sections = new List<SectionMarker>(names.Count);
            double sectionLength = names.Count == 0 ? 0.0 : songLengthMs / names.Count;
            for (int index = 0; index < names.Count; index++)
                sections.Add(new SectionMarker(index, names[index], sectionLength * index));
        }

        public SectionTimeline(Beatmap beatmap, int sectionCount = 8)
            : this(beatmap == null ? throw new ArgumentNullException(nameof(beatmap)) : beatmap.LastNoteTimeMs, sectionCount)
        {
        }

        public double SongLengthMs { get; }

        public IReadOnlyList<SectionMarker> Sections => sections;

        public SectionMarker Current(double songTimeMs) => GetSection(songTimeMs);

        public SectionMarker At(double songTimeMs) => GetSection(songTimeMs);

        public SectionMarker SectionAt(double songTimeMs) => GetSection(songTimeMs);

        public SectionMarker GetSection(double songTimeMs)
        {
            if (sections.Count == 0) throw new InvalidOperationException("The section timeline is empty.");
            if (songTimeMs <= sections[0].StartTimeMs || SongLengthMs <= 0.0) return sections[0];

            for (int index = sections.Count - 1; index >= 0; index--)
            {
                if (songTimeMs >= sections[index].StartTimeMs)
                    return sections[index];
            }

            return sections[0];
        }

        public bool TryGetSection(double songTimeMs, out SectionMarker section)
        {
            section = GetSection(songTimeMs);
            return true;
        }

        private static IReadOnlyList<string> CreateDefaultNames(int sectionCount)
        {
            if (sectionCount < 1) throw new ArgumentOutOfRangeException(nameof(sectionCount));
            var names = new string[sectionCount];
            for (int index = 0; index < sectionCount; index++)
                names[index] = index < DefaultNames.Length ? DefaultNames[index] : "Section " + (index + 1);
            return names;
        }
    }
}
