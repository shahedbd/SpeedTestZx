namespace TestNetSpeed.Helper
{
    /// <summary>
    /// Small pure statistics used by the Report Card and the Monitor. The
    /// report grades on the median, not the average, so one bad test can't
    /// swing the result (Pro_plan.md "Grading rules").
    /// </summary>
    public static class Stats
    {
        /// <summary>Median rounded to 0.1, or null for an empty list.</summary>
        public static double? Median(IEnumerable<double> values) => Percentile(values, 50);

        /// <summary>
        /// Linear-interpolated percentile (0–100), rounded to 0.1; null when
        /// empty. Percentile(…, 10) is "the worst 10% of tests".
        /// </summary>
        public static double? Percentile(IEnumerable<double> values, double pct)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (sorted.Count == 0) return null;
            if (sorted.Count == 1) return Math.Round(sorted[0], 1);

            double rank = Math.Clamp(pct, 0, 100) / 100 * (sorted.Count - 1);
            int lo = (int)Math.Floor(rank);
            int hi = Math.Min(lo + 1, sorted.Count - 1);
            double value = sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
            return Math.Round(value, 1);
        }

        /// <summary>Share of values below <paramref name="threshold"/>, 0–100.</summary>
        public static double ShareBelow(IReadOnlyCollection<double> values, double threshold) =>
            values.Count == 0 ? 0 : Math.Round(100.0 * values.Count(v => v < threshold) / values.Count, 1);
    }
}
