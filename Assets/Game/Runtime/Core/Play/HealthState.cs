namespace DJMaximusKaiserSoje.Core
{
    /// <summary>The gauge beside the playfield. Empty ends the run.</summary>
    public readonly struct HealthState
    {
        /// <summary>
        /// A gauge is drained by adding deltas, so the last drop lands a rounding error either side
        /// of zero. Anything this close to empty is empty.
        /// </summary>
        private const double EmptyEpsilon = 1e-6;

        public HealthState(double value01)
        {
            Value01 = value01 < 0.0 ? 0.0 : value01 > 1.0 ? 1.0 : value01;
        }

        public double Value01 { get; }

        public bool IsEmpty => Value01 <= EmptyEpsilon;

        public static HealthState Full => new HealthState(1.0);
    }
}
