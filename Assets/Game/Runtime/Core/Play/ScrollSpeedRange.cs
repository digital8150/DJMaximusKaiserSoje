using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>The bounds the note speed setting is kept inside, wherever it is stored.</summary>
    public static class ScrollSpeedRange
    {
        public const float Minimum = 1f;
        public const float Maximum = 20f;
        public const float Step = 0.5f;
        public const float Default = 5f;

        public static float Clamp(float value) => Math.Max(Minimum, Math.Min(Maximum, value));

        public static float Stepped(float value, int steps) => Clamp(value + steps * Step);
    }

    /// <summary>The judgement offset the player can nudge when their setup runs early or late.</summary>
    public static class JudgementOffsetRange
    {
        public const double MinimumMs = -200.0;
        public const double MaximumMs = 200.0;
        public const double StepMs = 5.0;

        public static double Clamp(double value) => Math.Max(MinimumMs, Math.Min(MaximumMs, value));
    }
}
