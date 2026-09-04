using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>Why a run ended. The result screen reads differently for each.</summary>
    public enum PlayOutcome
    {
        Cleared,
        Failed,
        Abandoned
    }

    /// <summary>
    /// A finished run, carried from gameplay to the result screen. It holds the previous best so the
    /// screen can show what improved without going back to storage.
    /// </summary>
    public sealed class PlayResult
    {
        public PlayResult(
            string songId,
            string chartId,
            PlayStyle style,
            RunScore score,
            Rank rank,
            PlayOutcome outcome,
            ChartRecord previousBest,
            DateTime playedAtUtc)
        {
            SongId = songId ?? throw new ArgumentNullException(nameof(songId));
            ChartId = chartId ?? throw new ArgumentNullException(nameof(chartId));
            Style = style;
            Score = score;
            Rank = rank;
            Outcome = outcome;
            PreviousBest = previousBest ?? ChartRecord.Empty(chartId);
            PlayedAtUtc = playedAtUtc;
        }

        public string SongId { get; }
        public string ChartId { get; }
        public PlayStyle Style { get; }
        public RunScore Score { get; }
        public Rank Rank { get; }
        public PlayOutcome Outcome { get; }

        /// <summary>Never null; an unplayed chart yields <see cref="ChartRecord.Empty"/>.</summary>
        public ChartRecord PreviousBest { get; }

        public DateTime PlayedAtUtc { get; }

        public bool IsNewScoreRecord => Score.Score > PreviousBest.BestScore;
        public bool IsNewAccuracyRecord => Score.Accuracy01 > PreviousBest.BestAccuracy01;
        public bool IsNewRatingRecord => Score.Rating > PreviousBest.BestRating;
        public bool IsNewComboRecord => Score.MaxCombo > PreviousBest.BestCombo;

        public long ScoreDelta => Score.Score - PreviousBest.BestScore;
        public double AccuracyDelta => Score.Accuracy01 - PreviousBest.BestAccuracy01;
        public double RatingDelta => Score.Rating - PreviousBest.BestRating;
    }
}
