namespace DJMaximusKaiserSoje.Core
{
    /// <summary>The badge a finished run earns, worst first.</summary>
    public enum Rank
    {
        F,
        D,
        C,
        B,
        A,
        S,
        SS,
        SSS
    }

    public static class RankCalculator
    {
        public const double SssThreshold = 0.99;
        public const double SsThreshold = 0.98;
        public const double SThreshold = 0.96;
        public const double AThreshold = 0.93;
        public const double BThreshold = 0.90;
        public const double CThreshold = 0.85;
        public const double DThreshold = 0.80;

        public static Rank FromAccuracy(double accuracy01)
        {
            if (accuracy01 >= SssThreshold) return Rank.SSS;
            if (accuracy01 >= SsThreshold) return Rank.SS;
            if (accuracy01 >= SThreshold) return Rank.S;
            if (accuracy01 >= AThreshold) return Rank.A;
            if (accuracy01 >= BThreshold) return Rank.B;
            if (accuracy01 >= CThreshold) return Rank.C;
            if (accuracy01 >= DThreshold) return Rank.D;
            return Rank.F;
        }
    }
}
