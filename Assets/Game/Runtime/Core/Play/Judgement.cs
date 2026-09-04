using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>Judgement tiers, best first. Display strings belong to the presentation layer.</summary>
    public enum JudgementGrade
    {
        PerfectHigh,
        Perfect,
        Great,
        Good,
        Miss
    }

    /// <summary>Which side of the note the player landed on.</summary>
    public enum JudgementTiming
    {
        Exact,
        Fast,
        Slow
    }

    /// <summary>
    /// Timing windows in milliseconds, injectable so charts, modifiers, and tests can tighten them.
    /// </summary>
    public sealed class JudgementWindows
    {
        public JudgementWindows(double perfectHighMs, double perfectMs, double greatMs, double goodMs, double missMs)
        {
            if (perfectHighMs <= 0.0) throw new ArgumentOutOfRangeException(nameof(perfectHighMs));
            if (perfectMs < perfectHighMs) throw new ArgumentOutOfRangeException(nameof(perfectMs));
            if (greatMs < perfectMs) throw new ArgumentOutOfRangeException(nameof(greatMs));
            if (goodMs < greatMs) throw new ArgumentOutOfRangeException(nameof(goodMs));
            if (missMs < goodMs) throw new ArgumentOutOfRangeException(nameof(missMs));

            PerfectHighMs = perfectHighMs;
            PerfectMs = perfectMs;
            GreatMs = greatMs;
            GoodMs = goodMs;
            MissMs = missMs;
        }

        public static JudgementWindows Default { get; } = new JudgementWindows(22.5, 45.0, 90.0, 135.0, 180.0);

        public double PerfectHighMs { get; }
        public double PerfectMs { get; }
        public double GreatMs { get; }
        public double GoodMs { get; }

        /// <summary>Outside this, a press is not attributed to the note at all.</summary>
        public double MissMs { get; }

        public double WindowFor(JudgementGrade grade)
        {
            switch (grade)
            {
                case JudgementGrade.PerfectHigh: return PerfectHighMs;
                case JudgementGrade.Perfect: return PerfectMs;
                case JudgementGrade.Great: return GreatMs;
                case JudgementGrade.Good: return GoodMs;
                case JudgementGrade.Miss: return MissMs;
                default: throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
            }
        }
    }

    public sealed class JudgementEngine
    {
        private readonly JudgementWindows windows;

        public JudgementEngine() : this(JudgementWindows.Default)
        {
        }

        public JudgementEngine(JudgementWindows windows)
        {
            this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
        }

        public JudgementWindows Windows => windows;

        /// <summary>
        /// Grades a press against a note. Returns false when the press is too far away to belong to
        /// the note, which leaves the note alive for a later press.
        /// </summary>
        public bool TryJudge(double signedOffsetMs, out JudgementGrade grade)
        {
            double offset = Math.Abs(signedOffsetMs);
            if (offset <= windows.PerfectHighMs) { grade = JudgementGrade.PerfectHigh; return true; }
            if (offset <= windows.PerfectMs) { grade = JudgementGrade.Perfect; return true; }
            if (offset <= windows.GreatMs) { grade = JudgementGrade.Great; return true; }
            if (offset <= windows.GoodMs) { grade = JudgementGrade.Good; return true; }
            if (offset <= windows.MissMs) { grade = JudgementGrade.Miss; return true; }

            grade = JudgementGrade.Miss;
            return false;
        }

        /// <summary>How much of a note's accuracy a grade earns, from 1.0 down to 0.0.</summary>
        public static double AccuracyWeight(JudgementGrade grade)
        {
            switch (grade)
            {
                case JudgementGrade.PerfectHigh: return 1.0;
                case JudgementGrade.Perfect: return 0.98;
                case JudgementGrade.Great: return 0.75;
                case JudgementGrade.Good: return 0.4;
                default: return 0.0;
            }
        }

        /// <summary>
        /// Fast/slow only counts once the player is outside the tightest window; inside it there is
        /// nothing to correct.
        /// </summary>
        public JudgementTiming TimingOf(double signedOffsetMs)
        {
            if (Math.Abs(signedOffsetMs) <= windows.PerfectHighMs) return JudgementTiming.Exact;
            return signedOffsetMs < 0.0 ? JudgementTiming.Fast : JudgementTiming.Slow;
        }
    }
}
