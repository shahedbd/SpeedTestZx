using TestNetSpeed.Model;

namespace TestNetSpeed.Helper
{
    /// <summary>
    /// Uptime and outage figures over a time range, from the Monitor's files.
    /// Uptime only counts time the Monitor actually recorded: hours when the
    /// PC was off or the app closed are "not recorded", not downtime.
    /// </summary>
    public static class MonitorStats
    {
        /// <summary>Outages that overlap [from, to), oldest first.</summary>
        public static List<Outage> OutagesIn(DateTime from, DateTime to) =>
            MonitorStore.LoadOutages()
                .Where(o => o.Start < to && (o.End ?? DateTime.Now) > from)
                .OrderBy(o => o.Start)
                .ToList();

        /// <summary>Total outage time inside [from, to).</summary>
        public static TimeSpan OutageTime(IEnumerable<Outage> outages, DateTime from, DateTime to) =>
            TimeSpan.FromTicks(outages.Sum(o =>
            {
                var start = o.Start < from ? from : o.Start;
                var end = (o.End ?? DateTime.Now) > to ? to : (o.End ?? DateTime.Now);
                return Math.Max(0, (end - start).Ticks);
            }));

        /// <summary>
        /// Minutes the Monitor recorded in [from, to). Short ranges read the
        /// per-minute files; long ones use the hourly roll-up so a 90-day
        /// report doesn't parse 90 files.
        /// </summary>
        public static int RecordedMinutes(DateTime from, DateTime to)
        {
            if (to - from <= TimeSpan.FromDays(2))
                return MonitorStore.ReadMinutes(from, to).Count;

            int minutes = MonitorStore.LoadHourly()
                .Where(h => h.Hour >= from && h.Hour < to)
                .Sum(h => h.Minutes);

            // The current hour isn't rolled up yet.
            var hourStart = DateTime.Now.Date.AddHours(DateTime.Now.Hour);
            if (hourStart < to)
                minutes += MonitorStore.ReadMinutes(hourStart, to).Count;

            return minutes;
        }

        /// <summary>Uptime in percent, or null when nothing was recorded in the range.</summary>
        public static double? UptimePct(DateTime from, DateTime to, IEnumerable<Outage>? outages = null)
        {
            int recorded = RecordedMinutes(from, to);
            if (recorded == 0) return null;

            double down = OutageTime(outages ?? OutagesIn(from, to), from, to).TotalMinutes;
            double total = Math.Max(recorded, down);
            return Math.Round(100 * (1 - down / total), 2);
        }
    }
}
