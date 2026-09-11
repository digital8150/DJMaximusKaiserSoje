using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// Builds an immutable score snapshot from one judgement at a time.
    ///
    /// The scoring system uses a 1,000,000 maximum score standard. Each note contributes
    /// an equal fraction of the 1,000,000 points multiplied by the judgement accuracy weight.
    /// Accuracy is the weighted mean of the judgements. Rating is a 0-100 performance value
    /// that gives 70% weight to accuracy and 30% to combo consistency.
    /// </summary>
    public sealed class ScoreAccumulator
    {
        public const long MaxPossibleScore = 1_000_000L;

        private readonly int totalNotes;
        private readonly JudgementEngine judgementEngine;
        private JudgementTally tally;
        private long score;
        private double earnedScore;
        private double earnedAccuracy;
        private int combo;
        private int maxCombo;

        public ScoreAccumulator(int totalNotes) : this(totalNotes, new JudgementEngine())
        {
        }

        public ScoreAccumulator(int totalNotes, JudgementEngine judgementEngine)
        {
            if (totalNotes < 0) throw new ArgumentOutOfRangeException(nameof(totalNotes));
            this.totalNotes = totalNotes;
            this.judgementEngine = judgementEngine ?? throw new ArgumentNullException(nameof(judgementEngine));
            Current = RunScore.Empty(totalNotes);
        }

        public RunScore Current { get; private set; }

        public RunScore Feed(JudgementGrade grade, JudgementTiming timing)
        {
            double weight = JudgementEngine.AccuracyWeight(grade);
            tally = tally.Add(grade, timing);

            if (grade == JudgementGrade.Miss)
            {
                combo = 0;
            }
            else
            {
                combo++;
                maxCombo = Math.Max(maxCombo, combo);
            }

            earnedAccuracy += weight;
            if (totalNotes > 0)
            {
                earnedScore += (MaxPossibleScore / (double)totalNotes) * weight;
                score = (long)Math.Round(earnedScore, MidpointRounding.AwayFromZero);
                if (tally.Judged == totalNotes && tally.PerfectHigh == totalNotes)
                {
                    score = MaxPossibleScore;
                }
                score = Math.Min(MaxPossibleScore, Math.Max(0L, score));
            }

            double accuracy = tally.Judged == 0 ? 0.0 : earnedAccuracy / tally.Judged;
            double comboRatio = totalNotes <= 0 ? 0.0 : Math.Min(1.0, (double)maxCombo / totalNotes);
            double rating = accuracy * 100.0 * (0.7 + 0.3 * comboRatio);
            Current = new RunScore(score, accuracy, rating, combo, maxCombo, totalNotes, tally);
            return Current;
        }

        public RunScore Add(JudgementGrade grade, JudgementTiming timing) => Feed(grade, timing);

        public RunScore Record(JudgementGrade grade, JudgementTiming timing) => Feed(grade, timing);

        public static double RatingFor(RunScore score)
        {
            double comboRatio = score.TotalNotes <= 0
                ? 0.0
                : Math.Min(1.0, (double)score.MaxCombo / score.TotalNotes);
            return score.Accuracy01 * 100.0 * (0.7 + 0.3 * comboRatio);
        }
    }
}
