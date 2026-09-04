using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>What the select screen hands to gameplay when the player commits to a chart.</summary>
    public sealed class PlayRequest
    {
        public PlayRequest(string songId, string chartId, PlayStyle style, float scrollSpeed, double judgementOffsetMs,
            System.Collections.Generic.IReadOnlyList<string> keyBindings = null)
        {
            SongId = songId ?? throw new ArgumentNullException(nameof(songId));
            ChartId = chartId ?? throw new ArgumentNullException(nameof(chartId));
            Style = style;
            ScrollSpeed = ScrollSpeedRange.Clamp(scrollSpeed);
            JudgementOffsetMs = JudgementOffsetRange.Clamp(judgementOffsetMs);
            KeyBindings = GameOptionRules.NormalizeBindings(style, keyBindings);
        }

        public string SongId { get; }
        public string ChartId { get; }
        public PlayStyle Style { get; }
        public float ScrollSpeed { get; }
        public double JudgementOffsetMs { get; }
        public System.Collections.Generic.IReadOnlyList<string> KeyBindings { get; }
    }

    /// <summary>Screen transitions. Screens ask for the next one; they never load scenes themselves.</summary>
    public interface IGameFlow
    {
        void ShowTitle();

        void ShowSongSelect();

        void ShowOptions();

        void CloseOptions();

        void StartPlay(PlayRequest request);

        void ShowResult(PlayResult result);

        /// <summary>Replays the last request. False when there is nothing to replay.</summary>
        bool CanRetry { get; }

        void RetryLast();
    }
}
