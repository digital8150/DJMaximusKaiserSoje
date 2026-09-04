using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>Deterministic gauge changes shared by gameplay and tests.</summary>
    public sealed class HealthRules
    {
        public const double PerfectHighDelta = 0.010;
        public const double PerfectDelta = 0.008;
        public const double GreatDelta = 0.004;
        public const double GoodDelta = -0.010;
        public const double MissDelta = -0.200;

        public static HealthRules Default { get; } = new HealthRules();

        public HealthRules(double initialValue01 = 1.0)
        {
            StartingHealth = new HealthState(initialValue01);
        }

        public HealthState StartingHealth { get; }

        public HealthState Apply(HealthState current, JudgementGrade grade)
        {
            return new HealthState(current.Value01 + DeltaFor(grade));
        }

        public HealthState Update(HealthState current, JudgementGrade grade) => Apply(current, grade);

        public bool HasFailed(HealthState health) => health.IsEmpty;

        public bool IsRunFailed(HealthState health) => HasFailed(health);

        public static double DeltaFor(JudgementGrade grade)
        {
            switch (grade)
            {
                case JudgementGrade.PerfectHigh: return PerfectHighDelta;
                case JudgementGrade.Perfect: return PerfectDelta;
                case JudgementGrade.Great: return GreatDelta;
                case JudgementGrade.Good: return GoodDelta;
                case JudgementGrade.Miss: return MissDelta;
                default: throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
            }
        }
    }
}
