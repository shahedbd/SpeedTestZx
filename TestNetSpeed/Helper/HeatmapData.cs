using TestNetSpeed.Model;

namespace TestNetSpeed.Helper
{
    public enum HeatmapMode { Speed, Ping }

    /// <summary>A 7 × 24 grid (Monday first) of medians and their sample counts.</summary>
    public sealed class HeatmapGrid
    {
        public const int MinSamples = 2;

        public double?[,] Values { get; } = new double?[7, 24];
        public int[,] Counts { get; } = new int[7, 24];
        public HeatmapMode Mode { get; init; }

        /// <summary>Speed mode with a plan: values are % of plan; otherwise Mbps.</summary>
        public bool IsPct { get; init; }

        public int FilledCells
        {
            get
            {
                int n = 0;
                foreach (var c in Counts) if (c >= MinSamples) n++;
                return n;
            }
        }
    }

    /// <summary>
    /// Builds the Monitor's weekday × hour heatmap (T55). Speed comes from
    /// test results (manual and scheduled); ping comes from the hourly
    /// roll-up, so 90 days open without reading 90 probe files.
    /// </summary>
    public static class HeatmapData
    {
        public static HeatmapGrid Speed(IEnumerable<SpeedTestResult> history, DateTime from, IspPlan plan)
        {
            var grid = new HeatmapGrid { Mode = HeatmapMode.Speed, IsPct = plan.IsSet };
            var cells = new List<double>[7, 24];

            foreach (var r in history.Where(r => r.Success && r.TestedAt >= from))
            {
                double? value = plan.IsSet ? ReportStats.PctOfPlan(r, plan) : r.DownloadMbps;
                if (value is double v) Cell(cells, r.TestedAt).Add(v);
            }

            Fill(grid, cells);
            return grid;
        }

        public static HeatmapGrid Ping(IEnumerable<HourStat> hourly, DateTime from)
        {
            var grid = new HeatmapGrid { Mode = HeatmapMode.Ping };
            var cells = new List<double>[7, 24];

            foreach (var h in hourly.Where(h => h.Hour >= from && h.PingMs != null))
                Cell(cells, h.Hour).Add(h.PingMs!.Value);

            Fill(grid, cells);
            return grid;
        }

        /// <summary>Monday = 0 … Sunday = 6.</summary>
        public static int DayIndex(DateTime t) => ((int)t.DayOfWeek + 6) % 7;

        private static List<double> Cell(List<double>[,] cells, DateTime t)
        {
            int d = DayIndex(t), h = t.Hour;
            return cells[d, h] ??= new List<double>();
        }

        private static void Fill(HeatmapGrid grid, List<double>[,] cells)
        {
            for (int d = 0; d < 7; d++)
                for (int h = 0; h < 24; h++)
                {
                    var list = cells[d, h];
                    if (list == null) continue;
                    grid.Counts[d, h] = list.Count;
                    grid.Values[d, h] = Stats.Median(list);
                }
        }
    }
}
