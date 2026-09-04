using System;

namespace DJMaximusKaiserSoje.Core
{
    public static class PreviewPointResolver
    {
        public static double Resolve(BeatmapHeader chart, double songLengthMs)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            if (double.IsNaN(songLengthMs) || double.IsInfinity(songLengthMs) || songLengthMs < 0.0)
                throw new ArgumentOutOfRangeException(nameof(songLengthMs));

            double point = chart.HasPreviewPoint
                ? chart.PreviewTimeMs
                : songLengthMs * MusicTiming.PreviewFallbackPosition01;
            return Math.Max(0.0, Math.Min(songLengthMs, point));
        }

        public static double Resolve(Beatmap chart, double songLengthMs)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            return Resolve(chart.Header, songLengthMs);
        }

        public static double ResolveMilliseconds(BeatmapHeader chart, double songLengthMs) => Resolve(chart, songLengthMs);
    }
}
