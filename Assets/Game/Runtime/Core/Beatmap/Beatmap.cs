using System;
using System.Collections.Generic;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// A chart in the game's own representation. Parsers map their formats onto this type and never
    /// leak their own DTOs past it.
    /// </summary>
    public sealed class Beatmap
    {
        public Beatmap(BeatmapHeader header, IReadOnlyList<BeatmapNote> notes)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Notes = notes ?? throw new ArgumentNullException(nameof(notes));

            double last = 0.0;
            for (int index = 0; index < notes.Count; index++)
                last = Math.Max(last, notes[index].EndTimeMs);
            LastNoteTimeMs = last;
        }

        public BeatmapHeader Header { get; }

        public IReadOnlyList<BeatmapNote> Notes { get; }

        /// <summary>End of the last note, which is where a run can stop waiting for input.</summary>
        public double LastNoteTimeMs { get; }
    }

    /// <summary>
    /// Everything about a chart that can be shown before its notes are loaded.
    /// </summary>
    public sealed class BeatmapHeader
    {
        public BeatmapHeader(
            string title,
            string artist,
            string difficultyName,
            int keyCount,
            double bpm = 0.0,
            double previewTimeMs = -1.0,
            string backgroundFilename = null,
            string videoFilename = null,
            double videoStartTimeMs = 0.0,
            string audioFilename = null)
        {
            if (keyCount < 1) throw new ArgumentOutOfRangeException(nameof(keyCount));

            Title = title ?? string.Empty;
            Artist = artist ?? string.Empty;
            DifficultyName = difficultyName ?? string.Empty;
            KeyCount = keyCount;
            Bpm = bpm;
            PreviewTimeMs = previewTimeMs;
            BackgroundFilename = backgroundFilename;
            VideoFilename = videoFilename;
            VideoStartTimeMs = videoStartTimeMs;
            AudioFilename = audioFilename;
        }

        public string Title { get; }
        public string Artist { get; }
        public string DifficultyName { get; }
        public int KeyCount { get; }
        public double Bpm { get; }

        /// <summary>
        /// Where the select-screen preview should start. Negative when the chart declares none, in
        /// which case the caller picks a fallback.
        /// </summary>
        public double PreviewTimeMs { get; }

        public bool HasPreviewPoint => PreviewTimeMs >= 0.0;

        public string BackgroundFilename { get; }
        public string VideoFilename { get; }
        public double VideoStartTimeMs { get; }
        public string AudioFilename { get; }
    }

    public readonly struct BeatmapNote
    {
        public BeatmapNote(int lane, double startTimeMs, double endTimeMs)
        {
            Lane = lane;
            StartTimeMs = startTimeMs;
            EndTimeMs = Math.Max(startTimeMs, endTimeMs);
        }

        public int Lane { get; }
        public double StartTimeMs { get; }
        public double EndTimeMs { get; }
        public bool IsHold => EndTimeMs > StartTimeMs;
    }
}
