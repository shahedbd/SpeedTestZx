using SpeedTestZx.Model;

namespace SpeedTestZx.Helper
{
    /// <summary>Everything the ISP Report Card and its PDF show for one range.</summary>
    public sealed class ReportData
    {
        public required DateTime From { get; init; }
        public required DateTime To { get; init; }
        public required IspPlan Plan { get; init; }
        public required bool WiredOnly { get; init; }

        /// <summary>Successful tests in range, oldest first.</summary>
        public required List<SpeedTestResult> Tests { get; init; }

        public double? MedianDown, MedianUp, MedianPct, P10Pct, MedianUpPct, P10UpPct;
        public double Below50, Below80;

        /// <summary>Lowest-median 3-hour window, or null with too little data.</summary>
        public (int Start, int End)? WorstHours;

        /// <summary>Median % of plan per hour of day (or median Mbps with no plan); null = no tests.</summary>
        public double?[] ByHour = new double?[24];

        /// <summary>Median % of plan (or Mbps) per day in range.</summary>
        public List<(DateTime Day, double? Value)> Daily = new();

        public double? UptimePct;
        public TimeSpan OutageTime;
        public List<Outage> Outages = new();

        /// <summary>Both kinds of adapter appear in the range — offer "wired tests only".</summary>
        public bool MixedAdapters;

        public bool HasPlan => Plan.IsSet;

        /// <summary>Estimated monthly cost of undelivered speed (T52), or null without a priced plan.</summary>
        public double? UndeliveredCost =>
            HasPlan && Plan.MonthlyPrice > 0 && MedianDown is double down
                ? Math.Round(Plan.MonthlyPrice * Math.Max(0, 1 - down / Plan.DownMbps), 2, MidpointRounding.AwayFromZero)
                : null;
    }

    /// <summary>
    /// Builds <see cref="ReportData"/> from Test History and the Monitor's
    /// files (T51). Pure apart from the Monitor file reads; grading follows
    /// Pro_plan.md: medians, the 10th percentile, share below 50% / 80%.
    /// </summary>
    public static class ReportStats
    {
        /// <summary>Minimum tests in a 3-hour window before it can be called the worst.</summary>
        private const int MinWorstWindowTests = 3;

        public static ReportData Build(IEnumerable<SpeedTestResult> history, DateTime from, DateTime to,
            bool wiredOnly, IspPlan plan)
        {
            var all = history.Where(r => r.Success && r.TestedAt >= from && r.TestedAt < to).ToList();
            bool hasWired = all.Any(r => r.IsWired);
            bool hasWifi = all.Any(r => r.AdapterType == "Wi-Fi");

            var tests = (wiredOnly ? all.Where(r => r.IsWired) : all).OrderBy(r => r.TestedAt).ToList();

            var data = new ReportData
            {
                From = from, To = to, Plan = plan, WiredOnly = wiredOnly, Tests = tests,
                MixedAdapters = hasWired && hasWifi,
            };

            var pct = tests.Select(r => PctOfPlan(r, plan)).Where(p => p != null).Select(p => p!.Value).ToList();
            var upPct = tests.Select(r => UpPctOfPlan(r, plan)).Where(p => p != null).Select(p => p!.Value).ToList();

            data.MedianDown = Stats.Median(tests.Select(r => r.DownloadMbps));
            data.MedianUp = Stats.Median(tests.Select(r => r.UploadMbps));
            data.MedianPct = Stats.Median(pct);
            data.P10Pct = Stats.Percentile(pct, 10);
            data.MedianUpPct = Stats.Median(upPct);
            data.P10UpPct = Stats.Percentile(upPct, 10);
            data.Below50 = Stats.ShareBelow(pct, 50);
            data.Below80 = Stats.ShareBelow(pct, 80);

            Func<SpeedTestResult, double?> score = plan.IsSet ? r => PctOfPlan(r, plan) : r => r.DownloadMbps;

            for (int h = 0; h < 24; h++)
                data.ByHour[h] = Stats.Median(tests.Where(r => r.TestedAt.Hour == h)
                    .Select(score).Where(v => v != null).Select(v => v!.Value));

            data.Daily = tests.GroupBy(r => r.TestedAt.Date)
                .Select(g => (g.Key, Stats.Median(g.Select(score).Where(v => v != null).Select(v => v!.Value))))
                .ToList();

            data.WorstHours = FindWorstHours(tests, score);

            data.Outages = MonitorStats.OutagesIn(from, to);
            data.OutageTime = MonitorStats.OutageTime(data.Outages, from, to);
            data.UptimePct = MonitorStats.UptimePct(from, to, data.Outages);

            return data;
        }

        /// <summary>Stamped % of plan, or computed from the current plan for tests made before it was set.</summary>
        public static double? PctOfPlan(SpeedTestResult r, IspPlan plan) =>
            r.PctOfPlan ?? (plan.IsSet ? Math.Round(r.DownloadMbps / plan.DownMbps * 100, 1) : null);

        public static double? UpPctOfPlan(SpeedTestResult r, IspPlan plan) =>
            r.UpPctOfPlan ?? (plan.UpMbps > 0 ? Math.Round(r.UploadMbps / plan.UpMbps * 100, 1) : null);

        /// <summary>The 3-hour window (wrapping past midnight) whose tests have the lowest median.</summary>
        private static (int, int)? FindWorstHours(List<SpeedTestResult> tests, Func<SpeedTestResult, double?> score)
        {
            (int, int)? worst = null;
            double worstValue = double.MaxValue;

            for (int start = 0; start < 24; start++)
            {
                var hours = new[] { start, (start + 1) % 24, (start + 2) % 24 };
                var values = tests.Where(r => hours.Contains(r.TestedAt.Hour))
                    .Select(score).Where(v => v != null).Select(v => v!.Value).ToList();
                if (values.Count < MinWorstWindowTests) continue;

                double median = Stats.Median(values)!.Value;
                if (median < worstValue)
                {
                    worstValue = median;
                    worst = Trim(start, tests);
                }
            }
            return worst is { } w ? Extend(w, worstValue, tests, score) : null;
        }

        /// <summary>Longest slow stretch the worst window is part of (max 8 h).</summary>
        private const int MaxWorstHours = 8;

        /// <summary>Within this factor of the worst window's median, a neighbouring hour counts as "just as slow".</summary>
        private const double SameSlowness = 1.15;

        /// <summary>
        /// Grows the worst window over neighbouring hours that are about as
        /// slow, so a slump from 7 pm to midnight reads "7 pm–12 am" rather
        /// than whichever 3 hours of it happened to score lowest.
        /// </summary>
        private static (int, int) Extend((int Start, int End) w, double worstValue, List<SpeedTestResult> tests,
            Func<SpeedTestResult, double?> score)
        {
            double? HourMedian(int hour) => Stats.Median(tests.Where(r => r.TestedAt.Hour == ((hour % 24) + 24) % 24)
                .Select(score).Where(v => v != null).Select(v => v!.Value));
            bool Slow(int hour) => HourMedian(hour) is double m && m <= worstValue * SameSlowness;

            int start = w.Start, end = w.End;
            while (end - start < MaxWorstHours && Slow(start - 1)) start--;
            while (end - start < MaxWorstHours && Slow(end)) end++;

            // Keep Start in 0–23 for Format.HourRange; End may pass 24 (wraps past midnight).
            int shift = start < 0 ? 24 : 0;
            return (start + shift, end + shift);
        }

        /// <summary>
        /// Shrinks a 3-hour window to the hours that actually have tests, so
        /// tests only at 8 pm read "8–9 pm", not "6–9 pm".
        /// </summary>
        private static (int, int) Trim(int start, List<SpeedTestResult> tests)
        {
            var withData = Enumerable.Range(0, 3).Where(i => tests.Any(r => r.TestedAt.Hour == (start + i) % 24)).ToList();
            return (start + withData.First(), start + withData.Last() + 1);
        }
    }
}
