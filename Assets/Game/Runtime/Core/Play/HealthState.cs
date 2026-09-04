namespace DJMaximusKaiserSoje.Core
{
    /// <summary>The gauge beside the playfield. Empty ends the run.</summary>
    public readonly struct HealthState
    {
        public const double Maximum = 10.0;

        /// <summary>
        /// A gauge is drained by adding deltas, so the last drop lands a rounding error either side
        /// of zero. Anything this close to empty is empty.
        /// </summary>
        private const double EmptyEpsilon = 1e-6;

        public HealthState(double value)
        {
            Value = value < 0.0 ? 0.0 : value > Maximum ? Maximum : value;
        }

        public double Value { get; }

        public double Value01 => Value / Maximum;

        public bool IsEmpty => Value <= EmptyEpsilon;

        public static HealthState Full => new HealthState(Maximum);
    }
}
