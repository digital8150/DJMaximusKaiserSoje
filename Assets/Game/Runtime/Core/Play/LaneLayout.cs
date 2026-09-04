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

    public enum LaneRole
    {
        Normal,
        LeftFx,
        RightFx
    }

    public readonly struct LaneSpec
    {
        public LaneSpec(string keyName, int baseLaneStart, int baseLaneSpan, LaneRole role, bool usesBlueColor = false)
        {
            KeyName = keyName;
            BaseLaneStart = baseLaneStart;
            BaseLaneSpan = baseLaneSpan;
            Role = role;
            UsesBlueColor = usesBlueColor;
        }

        public string KeyName { get; }
        public int BaseLaneStart { get; }
        public int BaseLaneSpan { get; }
        public LaneRole Role { get; }
        public bool IsFx => Role != LaneRole.Normal;
        public bool UsesBlueColor { get; }
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
                        Normal("D", 0), BlueNormal("F", 1), BlueNormal("J", 2), Normal("K", 3));
                case PlayStyle.FourKeyFx:
                    return new LaneLayout(style, "4K + 2 FX", 4,
                        Fx("S", 0, 2, LaneRole.LeftFx),
                        Normal("D", 0), BlueNormal("F", 1), BlueNormal("J", 2), Normal("K", 3),
                        Fx("L", 2, 2, LaneRole.RightFx));
                case PlayStyle.SixKey:
                    return new LaneLayout(style, "6K", 6,
                        Normal("S", 0), BlueNormal("D", 1), Normal("F", 2), Normal("J", 3), BlueNormal("K", 4), Normal("L", 5));
                case PlayStyle.SixKeyFx:
                    return new LaneLayout(style, "6K + 2 FX", 6,
                        Fx("A", 0, 3, LaneRole.LeftFx),
                        Normal("S", 0), BlueNormal("D", 1), Normal("F", 2), Normal("J", 3), BlueNormal("K", 4), Normal("L", 5),
                        Fx("Semicolon", 3, 3, LaneRole.RightFx));
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, null);
            }
        }

        private static LaneSpec Normal(string keyName, int baseLane) =>
            new LaneSpec(keyName, baseLane, 1, LaneRole.Normal);

        private static LaneSpec BlueNormal(string keyName, int baseLane) =>
            new LaneSpec(keyName, baseLane, 1, LaneRole.Normal, usesBlueColor: true);

        private static LaneSpec Fx(string keyName, int baseLane, int span, LaneRole role) =>
            new LaneSpec(keyName, baseLane, span, role);
    }
}
