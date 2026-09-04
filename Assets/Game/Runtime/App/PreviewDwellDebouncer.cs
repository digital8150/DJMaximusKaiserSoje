using System;

namespace DJMaximusKaiserSoje.App
{
    public interface IPreviewClock
    {
        double Now { get; }
    }

    /// <summary>Pure debounce gate used by MusicDirector and deterministic EditMode tests.</summary>
    public sealed class PreviewDwellDebouncer
    {
        private readonly IPreviewClock clock;
        private readonly double dwellSeconds;
        private string pendingSongId;
        private double requestedAt;

        public PreviewDwellDebouncer(IPreviewClock clock, double dwellSeconds = DJMaximusKaiserSoje.Core.MusicTiming.PreviewDwellSeconds)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (dwellSeconds < 0.0) throw new ArgumentOutOfRangeException(nameof(dwellSeconds));
            this.dwellSeconds = dwellSeconds;
        }

        public string PendingSongId => pendingSongId;
        public bool HasPendingRequest => pendingSongId != null;

        public void Request(string songId)
        {
            if (string.IsNullOrWhiteSpace(songId))
            {
                Cancel();
                return;
            }
            pendingSongId = songId;
            requestedAt = clock.Now;
        }

        public void Cancel()
        {
            pendingSongId = null;
            requestedAt = 0.0;
        }

        public bool TryTake(out string songId)
        {
            songId = null;
            if (pendingSongId == null || clock.Now - requestedAt < dwellSeconds) return false;
            songId = pendingSongId;
            Cancel();
            return true;
        }

        public bool Poll(out string songId) => TryTake(out songId);
    }

    public sealed class UnityPreviewClock : IPreviewClock
    {
        public double Now => UnityEngine.Time.unscaledTime;
    }
}
