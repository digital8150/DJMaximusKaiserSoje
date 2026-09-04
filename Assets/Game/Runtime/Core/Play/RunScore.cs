using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>How many of each grade a run has collected, plus which side the misses fell on.</summary>
    public readonly struct JudgementTally
    {
        public JudgementTally(int perfectHigh, int perfect, int great, int good, int miss, int fast, int slow)
        {
            PerfectHigh = perfectHigh;
            Perfect = perfect;
            Great = great;
            Good = good;
            Miss = miss;
            Fast = fast;
            Slow = slow;
        }

        public int PerfectHigh { get; }
        public int Perfect { get; }
        public int Great { get; }
        public int Good { get; }
        public int Miss { get; }
        public int Fast { get; }
        public int Slow { get; }

        public int Judged => PerfectHigh + Perfect + Great + Good + Miss;

        public int CountOf(JudgementGrade grade)
        {
            switch (grade)
            {
                case JudgementGrade.PerfectHigh: return PerfectHigh;
                case JudgementGrade.Perfect: return Perfect;
                case JudgementGrade.Great: return Great;
                case JudgementGrade.Good: return Good;
                case JudgementGrade.Miss: return Miss;
                default: throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
            }
        }

        public JudgementTally Add(JudgementGrade grade, JudgementTiming timing)
        {
            int fast = Fast + (timing == JudgementTiming.Fast ? 1 : 0);
            int slow = Slow + (timing == JudgementTiming.Slow ? 1 : 0);
            switch (grade)
            {
                case JudgementGrade.PerfectHigh: return new JudgementTally(PerfectHigh + 1, Perfect, Great, Good, Miss, fast, slow);
                case JudgementGrade.Perfect: return new JudgementTally(PerfectHigh, Perfect + 1, Great, Good, Miss, fast, slow);
                case JudgementGrade.Great: return new JudgementTally(PerfectHigh, Perfect, Great + 1, Good, Miss, fast, slow);
                case JudgementGrade.Good: return new JudgementTally(PerfectHigh, Perfect, Great, Good + 1, Miss, fast, slow);
                case JudgementGrade.Miss: return new JudgementTally(PerfectHigh, Perfect, Great, Good, Miss + 1, fast, slow);
                default: throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
            }
        }
    }

    /// <summary>An immutable snapshot of a run in progress. The HUD renders one of these per change.</summary>
    public readonly struct RunScore
    {
        public RunScore(long score, double accuracy01, double rating, int combo, int maxCombo, int totalNotes, JudgementTally tally)
        {
            Score = score;
            Accuracy01 = accuracy01;
            Rating = rating;
            Combo = combo;
            MaxCombo = maxCombo;
            TotalNotes = totalNotes;
            Tally = tally;
        }

        public long Score { get; }

        /// <summary>0.0 to 1.0. The screens render it as a percentage.</summary>
        public double Accuracy01 { get; }

        /// <summary>The composite performance number the result screen leads with.</summary>
        public double Rating { get; }

        public int Combo { get; }
        public int MaxCombo { get; }
        public int TotalNotes { get; }
        public JudgementTally Tally { get; }

        public int JudgedCount => Tally.Judged;

        public double Progress01 => TotalNotes <= 0 ? 0.0 : Math.Min(1.0, (double)JudgedCount / TotalNotes);

        public bool IsFullCombo => JudgedCount > 0 && Tally.Miss == 0;

        public bool IsAllPerfect => JudgedCount > 0 && Tally.PerfectHigh == JudgedCount;

        public static RunScore Empty(int totalNotes) =>
            new RunScore(0L, 0.0, 0.0, 0, 0, totalNotes, default);
    }
}
