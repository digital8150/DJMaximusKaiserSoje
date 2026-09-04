using System;
using System.Collections.Generic;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// A note the playfield still has to draw. This is read-only view data: the session owns note
    /// state, the view only positions a sprite from it.
    /// </summary>
    public readonly struct ActiveNote
    {
        public ActiveNote(int id, int lane, double startTimeMs, double endTimeMs, bool headJudged)
        {
            Id = id;
            Lane = lane;
            StartTimeMs = startTimeMs;
            EndTimeMs = endTimeMs;
            HeadJudged = headJudged;
        }

        /// <summary>Stable for the life of the note, so the view can keep a sprite attached to it.</summary>
        public int Id { get; }

        /// <summary>Index into <see cref="LaneLayout.Lanes"/>, not the chart's own column.</summary>
        public int Lane { get; }

        public double StartTimeMs { get; }
        public double EndTimeMs { get; }
        public bool IsHold => EndTimeMs > StartTimeMs;

        /// <summary>A hold whose head has been hit and is being held down.</summary>
        public bool HeadJudged { get; }
    }

    public enum PlaySessionState
    {
        Loading,
        Ready,
        Playing,
        Paused,
        Resuming,
        Finished
    }

    /// <summary>
    /// One run in progress. The gameplay screen binds to this and to nothing else: it never reads
    /// input, audio time, or the chart directly.
    /// </summary>
    public interface IPlaySession
    {
        /// <summary>The catalog ids this run was started from, so screens can look the song up.</summary>
        string SongId { get; }

        string ChartId { get; }

        PlaySessionState State { get; }
        RunScore Score { get; }
        HealthState Health { get; }
        SectionMarker CurrentSection { get; }
        LaneLayout Layout { get; }
        BeatmapHeader Chart { get; }

        /// <summary>Song position; negative during the lead-in count.</summary>
        double SongTimeMs { get; }

        double SongLengthMs { get; }

        /// <summary>Time left before a paused run becomes playable again; zero otherwise.</summary>
        double ResumeCountdownRemainingMs { get; }

        double Progress01 { get; }

        /// <summary>
        /// Notes that have not finished yet, ordered by start time. The playfield walks it from the
        /// front and stops once the notes are further out than it can draw.
        /// </summary>
        IReadOnlyList<ActiveNote> PendingNotes { get; }

        event Action<JudgementEvent> Judged;
        event Action<HoldTickEvent> HoldTicked;
        event Action<RunScore> ScoreChanged;
        event Action<HealthState> HealthChanged;
        event Action<SectionMarker> SectionChanged;
        event Action<PlaySessionState> StateChanged;

        /// <summary>Raised on the frame a lane is pressed, so the playfield can flash it.</summary>
        event Action<int> LanePressed;

        event Action<int> LaneReleased;

        event Action<PlayResult> Finished;

        void Pause();
        void Resume();
        void Restart();
        void Abort();
    }
}
