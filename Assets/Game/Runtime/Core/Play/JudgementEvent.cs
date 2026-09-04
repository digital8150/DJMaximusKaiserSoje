namespace DJMaximusKaiserSoje.Core
{
    /// <summary>One judged note. The playfield turns these into text, bursts, and combo changes.</summary>
    public readonly struct JudgementEvent
    {
        public JudgementEvent(int lane, JudgementGrade grade, JudgementTiming timing, double offsetMs, int combo, bool isHoldRelease)
        {
            Lane = lane;
            Grade = grade;
            Timing = timing;
            OffsetMs = offsetMs;
            Combo = combo;
            IsHoldRelease = isHoldRelease;
        }

        public int Lane { get; }
        public JudgementGrade Grade { get; }
        public JudgementTiming Timing { get; }

        /// <summary>Signed: negative is early.</summary>
        public double OffsetMs { get; }

        /// <summary>Combo after this note, so the HUD does not have to read it back.</summary>
        public int Combo { get; }

        public bool IsHoldRelease { get; }
    }
}
