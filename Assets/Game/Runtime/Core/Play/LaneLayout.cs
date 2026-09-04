using System;
using System.Collections.Generic;

namespace DJMaximusKaiserSoje.Core
{
    public enum PlayStyle
    {
        FourKey,
        FourKeyFx,
        SixKey,
        SixKeyFx
    }

    public readonly struct LaneSpec
    {
        public LaneSpec(string keyName, int baseLaneStart, int baseLaneSpan, bool isFx)
        {
            KeyName = keyName;
            BaseLaneStart = baseLaneStart;
            BaseLaneSpan = baseLaneSpan;
            IsFx = isFx;
        }

        public string KeyName { get; }
        public int BaseLaneStart { get; }
        public int BaseLaneSpan { get; }
        public bool IsFx { get; }
    }

    /// <summary>
    /// The columns a play style puts on screen and how a chart's columns map onto them.
    /// </summary>
    public sealed class LaneLayout
    {
        private LaneLayout(PlayStyle style, string displayName, int baseLaneCount, params LaneSpec[] lanes)
        {
            Style = style;
            DisplayName = displayName;
            BaseLaneCount = baseLaneCount;
            Lanes = lanes;
        }

        public PlayStyle Style { get; }
        public string DisplayName { get; }
        public int BaseLaneCount { get; }
        public IReadOnlyList<LaneSpec> Lanes { get; }
        public int ExpectedChartColumns => Lanes.Count;

        public int MapChartLane(int chartLane, int chartKeyCount)
        {
            if (chartKeyCount < 1) throw new ArgumentOutOfRangeException(nameof(chartKeyCount));
            chartLane = Math.Min(chartKeyCount - 1, Math.Max(0, chartLane));
            if (chartKeyCount == ExpectedChartColumns)
                return chartLane;

            int coreOffset = Style == PlayStyle.FourKeyFx || Style == PlayStyle.SixKeyFx ? 1 : 0;
            int mappedCore = Math.Min(BaseLaneCount - 1, chartLane * BaseLaneCount / chartKeyCount);
            return mappedCore + coreOffset;
        }

        public static LaneLayout Create(PlayStyle style)
        {
            switch (style)
            {
                case PlayStyle.FourKey:
                    return new LaneLayout(style, "4K", 4,
                        Normal("D", 0), Normal("F", 1), Normal("J", 2), Normal("K", 3));
                case PlayStyle.FourKeyFx:
                    return new LaneLayout(style, "4K + 2 FX", 4,
                        Fx("S", 0, 2), Normal("D", 0), Normal("F", 1), Normal("J", 2), Normal("K", 3), Fx("L", 2, 2));
                case PlayStyle.SixKey:
                    return new LaneLayout(style, "6K", 6,
                        Normal("S", 0), Normal("D", 1), Normal("F", 2), Normal("J", 3), Normal("K", 4), Normal("L", 5));
                case PlayStyle.SixKeyFx:
                    return new LaneLayout(style, "6K + 2 FX", 6,
                        Fx("A", 0, 3), Normal("S", 0), Normal("D", 1), Normal("F", 2), Normal("J", 3), Normal("K", 4), Normal("L", 5), Fx("Semicolon", 3, 3));
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, null);
            }
        }

        private static LaneSpec Normal(string keyName, int baseLane) => new LaneSpec(keyName, baseLane, 1, false);

        private static LaneSpec Fx(string keyName, int baseLane, int span) => new LaneSpec(keyName, baseLane, span, true);
    }
}
