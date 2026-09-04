using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// Converts between an audio device's sample counter and seconds. Mixers that schedule playback
    /// count output samples while <see cref="DspSongClock"/> works in seconds, so every crossing
    /// between the two goes through here rather than being open-coded at each call site.
    /// </summary>
    public static class SampleClock
    {
        public static double ToSeconds(ulong samples, int sampleRate)
        {
            RequireSampleRate(sampleRate);
            return samples / (double)sampleRate;
        }

        /// <summary>
        /// Rounds to the nearest sample so a scheduled start never lands a sample early, and clamps
        /// negative times to zero because a sample counter cannot run before the device started.
        /// </summary>
        public static ulong ToSamples(double seconds, int sampleRate)
        {
            RequireSampleRate(sampleRate);
            if (double.IsNaN(seconds) || seconds <= 0.0) return 0UL;
            double samples = Math.Round(seconds * sampleRate, MidpointRounding.AwayFromZero);
            return samples >= ulong.MaxValue ? ulong.MaxValue : (ulong)samples;
        }

        private static void RequireSampleRate(int sampleRate)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate,
                    "The audio device reported no sample rate.");
        }
    }
}
