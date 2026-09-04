using System;
using System.Collections.Generic;

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

        /// <summary>
        /// How much audio the mixer works on at a time, in samples. Smaller is more responsive and
        /// more likely to crackle; see <see cref="GameOptionRules.AudioBufferSizes"/>.
        /// </summary>
        int AudioBufferSize { get; set; }

        int QualityLevel { get; set; }

        bool VSync { get; set; }

        DisplayMode DisplayMode { get; set; }

        int ResolutionWidth { get; }

        int ResolutionHeight { get; }

        void SetResolution(int width, int height);

        IReadOnlyList<string> GetKeyBindings(PlayStyle style);

        void SetKeyBinding(PlayStyle style, int lane, string keyName);

        void ResetKeyBindings(PlayStyle style);

        event Action Changed;
    }

    /// <summary>
    /// The mixer, as far as the options need to steer it. Keeping this narrow lets preferences be
    /// tested without an audio device and keeps the audio backend out of the settings code.
    /// </summary>
    public interface IAudioDevice
    {
        /// <summary>What the device actually settled on, which may not be what was asked for.</summary>
        int BufferLength { get; }

        void SetBufferLength(int samples);
    }
}
