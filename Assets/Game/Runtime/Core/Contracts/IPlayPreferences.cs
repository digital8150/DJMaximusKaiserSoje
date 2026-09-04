using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// The settings the player changes between runs. Writes persist; the change event lets every
    /// screen showing a value update without polling.
    /// </summary>
    public interface IPlayPreferences
    {
        /// <summary>Clamped to <see cref="ScrollSpeedRange"/>.</summary>
        float ScrollSpeed { get; set; }

        /// <summary>Clamped to <see cref="JudgementOffsetRange"/>. Positive delays judgement.</summary>
        double JudgementOffsetMs { get; set; }

        PlayStyle PlayStyle { get; set; }

        event Action Changed;
    }
}
