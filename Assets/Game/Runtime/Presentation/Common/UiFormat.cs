using System;
using System.Globalization;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>Number and time formatting, kept identical across the three screens.</summary>
    public static class UiFormat
    {
        public static string Score(long score) => score.ToString("N0", CultureInfo.InvariantCulture);

        public static string PaddedCount(int value) => value.ToString("0000", CultureInfo.InvariantCulture);

        public static string Combo(int combo) => combo.ToString(CultureInfo.InvariantCulture);

        /// <summary>Four decimals, the way a player compares two near-identical runs.</summary>
        public static string AccuracyPrecise(double accuracy01) =>
            (accuracy01 * 100.0).ToString("0.0000", CultureInfo.InvariantCulture) + "%";

        public static string AccuracyShort(double accuracy01) =>
            (accuracy01 * 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "%";

        public static string Rating(double rating) => rating.ToString("0.00", CultureInfo.InvariantCulture);

        public static string Speed(float speed) => speed.ToString("0.0", CultureInfo.InvariantCulture);

        public static string Bpm(double bpm) => "BPM " + bpm.ToString("0", CultureInfo.InvariantCulture);

        public static string Offset(double milliseconds) =>
            milliseconds.ToString("+0;-0;0", CultureInfo.InvariantCulture) + " ms";

        public static string OffsetPrecise(double milliseconds) =>
            milliseconds.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " ms";

        public static string Time(double seconds)
        {
            int total = Math.Max(0, (int)seconds);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", total / 60, total % 60);
        }

        public static string SignedScore(long delta) =>
            delta.ToString("+#,##0;-#,##0;0", CultureInfo.InvariantCulture);

        public static string SignedAccuracy(double delta01) =>
            (delta01 * 100.0).ToString("+0.0000;-0.0000;0.0000", CultureInfo.InvariantCulture) + "%";

        public static string SignedRating(double delta) =>
            delta.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

        public static string Level(int level) => level.ToString("00", CultureInfo.InvariantCulture);
    }
}
