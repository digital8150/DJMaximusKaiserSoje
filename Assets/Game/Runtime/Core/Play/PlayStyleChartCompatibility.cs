using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// Defines the chart column count each input layout accepts. Keeping this rule in the domain
    /// prevents the select screen and session creation from drifting apart.
    /// </summary>
    public static class PlayStyleChartCompatibility
    {
        public static int RequiredKeyCount(PlayStyle style)
        {
            switch (style)
            {
                case PlayStyle.FourKey: return 4;
                case PlayStyle.FourKeyFx:
                case PlayStyle.SixKey: return 6;
                case PlayStyle.SixKeyFx: return 8;
                default: throw new ArgumentOutOfRangeException(nameof(style), style, null);
            }
        }

        public static bool IsCompatible(PlayStyle style, ChartSummary chart) =>
            chart != null && chart.KeyCount == RequiredKeyCount(style);

        public static bool IsCompatible(PlayStyle style, SongSummary song)
        {
            if (song == null) return false;
            for (int index = 0; index < song.Charts.Count; index++)
                if (IsCompatible(style, song.Charts[index])) return true;
            return false;
        }
    }
}
