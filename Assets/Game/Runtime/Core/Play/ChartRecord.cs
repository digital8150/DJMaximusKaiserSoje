using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>The player's best on one chart. Absent until the chart has been finished once.</summary>
    public sealed class ChartRecord
    {
        public ChartRecord(string chartId, long bestScore, double bestAccuracy01, double bestRating, int bestCombo, Rank bestRank, bool cleared, int playCount)
        {
            ChartId = chartId ?? throw new ArgumentNullException(nameof(chartId));
            BestScore = bestScore;
            BestAccuracy01 = bestAccuracy01;
            BestRating = bestRating;
            BestCombo = bestCombo;
            BestRank = bestRank;
            Cleared = cleared;
            PlayCount = playCount;
        }

        public string ChartId { get; }
        public long BestScore { get; }
        public double BestAccuracy01 { get; }
        public double BestRating { get; }
        public int BestCombo { get; }
        public Rank BestRank { get; }
        public bool Cleared { get; }
        public int PlayCount { get; }

        public static ChartRecord Empty(string chartId) =>
            new ChartRecord(chartId, 0L, 0.0, 0.0, 0, Rank.F, false, 0);

        public bool IsEmpty => PlayCount == 0;
    }
}
