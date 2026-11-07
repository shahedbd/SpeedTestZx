using SpeedTestZx.Model;

namespace SpeedTestZx.Helper
{
    /// <summary>Average / best / worst over a set of tests.</summary>
    public record HistorySummary(
        int Count,
        double AvgDownMbps,
        double BestDownMbps,
        double WorstDownMbps,
        double AvgUpMbps,
        double AvgPingMs);

    /// <summary>
    /// Pure helpers over Test History, shared by the Dashboard's Recent tests
    /// card (T47) and, later, the History charts (T20). No I/O — pass in what
    /// <see cref="TestHistoryStore.Load"/> returned.
    /// </summary>
    public static class HistoryStats
    {
        /// <summary>
        /// The newest <paramref name="count"/> successful tests, returned
        /// oldest first (left-to-right chart order).
        /// </summary>
        /// <param name="newestFirst">History as TestHistoryStore returns it.</param>
        public static List<SpeedTestResult> Recent(IEnumerable<SpeedTestResult> newestFirst, int count)
        {
            var list = newestFirst.Where(r => r.Success).Take(Math.Max(0, count)).ToList();
            list.Reverse();
            return list;
        }

        /// <summary>Null when there are no tests to summarise.</summary>
        public static HistorySummary? Summarise(IReadOnlyCollection<SpeedTestResult> tests)
        {
            if (tests.Count == 0) return null;

            return new HistorySummary(
                Count: tests.Count,
                AvgDownMbps: tests.Average(r => r.DownloadMbps),
                BestDownMbps: tests.Max(r => r.DownloadMbps),
                WorstDownMbps: tests.Min(r => r.DownloadMbps),
                AvgUpMbps: tests.Average(r => r.UploadMbps),
                AvgPingMs: tests.Average(r => r.PingMs));
        }
    }
}
