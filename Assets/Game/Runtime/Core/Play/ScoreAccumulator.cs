using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// Builds an immutable score snapshot from one judgement at a time.
    ///
    /// The point value intentionally keeps the prototype's feel: each note is worth 1,000
    /// weighted points and the combo multiplier grows from 1.00x to 2.00x over the first 100
    /// successful notes. Accuracy is the weighted mean of the judgements. Rating is a 0-100
    /// performance value that gives 70% weight to accuracy and 30% to combo consistency.
    /// </summary>
    public sealed class ScoreAccumulator
    {
        private readonly int totalNotes;
        private readonly JudgementEngine judgementEngine;
        private JudgementTally tally;
        private long score;
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
                score += (long)Math.Round(1000.0 * weight * (1.0 + Math.Min(combo, 100) / 100.0), MidpointRounding.AwayFromZero);
            }

            earnedAccuracy += weight;
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
