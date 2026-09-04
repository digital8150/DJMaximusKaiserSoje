using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// A named stretch of a chart. The gameplay screen names the part of the song the player is in
    /// so a long chart does not read as one undifferentiated scroll.
    /// </summary>
    public readonly struct SectionMarker : IEquatable<SectionMarker>
    {
        public SectionMarker(int index, string name, double startTimeMs)
        {
            Index = index;
            Name = name ?? string.Empty;
            StartTimeMs = startTimeMs;
        }

        public int Index { get; }
        public string Name { get; }
        public double StartTimeMs { get; }

        public bool Equals(SectionMarker other) => Index == other.Index && StartTimeMs.Equals(other.StartTimeMs);

        public override bool Equals(object obj) => obj is SectionMarker other && Equals(other);

        public override int GetHashCode() => unchecked((Index * 397) ^ StartTimeMs.GetHashCode());
    }
}
