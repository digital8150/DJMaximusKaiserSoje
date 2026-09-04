using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>The audio hardware clock, abstracted so the domain stays testable.</summary>
    public interface IDspTimeSource
    {
        double DspTime { get; }
    }

    /// <summary>
    /// The authoritative gameplay clock. Song position is derived from the audio device's own time,
    /// never from frame count or accumulated delta time.
    /// </summary>
    public sealed class DspSongClock
    {
        private readonly IDspTimeSource timeSource;
        private double startDspTime;
        private double pausedSongTimeMs;
        private bool isPaused;

        public DspSongClock(IDspTimeSource timeSource)
        {
            this.timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
        }

        public double StartDspTime => startDspTime;

        public double SongTimeMs => isPaused ? pausedSongTimeMs : (timeSource.DspTime - startDspTime) * 1000.0;

        public bool IsPaused => isPaused;

        public void Schedule(double delaySeconds)
        {
            startDspTime = timeSource.DspTime + Math.Max(0.0, delaySeconds);
            pausedSongTimeMs = 0.0;
            isPaused = false;
        }

        public void Pause()
        {
            if (isPaused) return;
            pausedSongTimeMs = SongTimeMs;
            isPaused = true;
        }

        public void Resume()
        {
            if (!isPaused) return;
            startDspTime = timeSource.DspTime - pausedSongTimeMs / 1000.0;
            isPaused = false;
        }
    }
}
