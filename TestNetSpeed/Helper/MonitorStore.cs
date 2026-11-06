using System.Text;
using System.Text.Json;
using DeviceDataModule;
using TestNetSpeed.Model;

namespace TestNetSpeed.Helper
{
    /// <summary>
    /// Connection Monitor storage (Pro_plan.md T49, "Storage design"). JSON
    /// in the app data folder, each kind written the way it's produced:
    ///   • Monitor/probes/yyyy-MM-dd.jsonl — one line per minute, APPENDED,
    ///     never rewritten. A half-written last line is skipped on read.
    ///   • Monitor/outages.json — rewritten when an outage starts or ends.
    ///   • Monitor/hourly.json  — hourly roll-up for the heatmap.
    /// Retention deletes whole day files (90 days Pro, 1 day free).
    /// </summary>
    public static class MonitorStore
    {
        public const int ProRetentionDays = 90;
        public const int FreeRetentionDays = 1;

        private static readonly string Root = Path.Combine(
            ApplicationDataManager.Instance.ApplicationDataDirectory, "Monitor");
        private static readonly string ProbeDir = Path.Combine(Root, "probes");
        private static readonly string OutagesFile = Path.Combine(Root, "outages.json");
        private static readonly string HourlyFile = Path.Combine(Root, "hourly.json");

        private static readonly object Gate = new();
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        /// <summary>Raised after outages.json changes. May fire off the UI thread.</summary>
        public static event EventHandler? OutagesChanged;

        // ─────────────────────────────────────────────────────────────────────
        // PER-MINUTE LOG
        // ─────────────────────────────────────────────────────────────────────

        public static void AppendMinute(ProbeMinute minute)
        {
            try
            {
                string line = JsonSerializer.Serialize(minute) + "\n";
                lock (Gate)
                {
                    Directory.CreateDirectory(ProbeDir);
                    File.AppendAllText(DayFile(minute.Time), line, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Probe log write failed: {ex.Message}");
            }
        }

        /// <summary>Minutes in [from, to), oldest first. Reads only the day files the range covers.</summary>
        public static List<ProbeMinute> ReadMinutes(DateTime from, DateTime to)
        {
            var list = new List<ProbeMinute>();
            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                string path = DayFile(day);
                string[] lines;
                try
                {
                    lock (Gate)
                    {
                        if (!File.Exists(path)) continue;
                        lines = File.ReadAllLines(path);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Probe log read failed: {ex.Message}");
                    continue;
                }

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var m = JsonSerializer.Deserialize<ProbeMinute>(line, Json);
                        if (m != null && m.Time >= from && m.Time < to) list.Add(m);
                    }
                    catch (JsonException)
                    {
                        // Half-written line from a crash — skip it.
                    }
                }
            }
            return list;
        }

        private static string DayFile(DateTime day) => Path.Combine(ProbeDir, $"{day:yyyy-MM-dd}.jsonl");

        // ─────────────────────────────────────────────────────────────────────
        // OUTAGES
        // ─────────────────────────────────────────────────────────────────────

        public static List<Outage> LoadOutages()
        {
            lock (Gate)
                return ReadJson<List<Outage>>(OutagesFile) ?? new List<Outage>();
        }

        /// <summary>Adds or replaces (same Start) one outage.</summary>
        public static void SaveOutage(Outage outage)
        {
            lock (Gate)
            {
                var list = ReadJson<List<Outage>>(OutagesFile) ?? new List<Outage>();
                list.RemoveAll(o => o.Start == outage.Start);
                list.Add(outage);
                list.Sort((a, b) => a.Start.CompareTo(b.Start));
                WriteJson(OutagesFile, list);
            }
            OutagesChanged?.Invoke(null, EventArgs.Empty);
        }

        // ─────────────────────────────────────────────────────────────────────
        // HOURLY ROLL-UP
        // ─────────────────────────────────────────────────────────────────────

        public static List<HourStat> LoadHourly()
        {
            lock (Gate)
                return ReadJson<List<HourStat>>(HourlyFile) ?? new List<HourStat>();
        }

        /// <summary>
        /// Saves an hour's roll-up. When the hour already has one (the app
        /// restarted mid-hour) the two are merged — minutes added, ping
        /// weighted by minutes — so uptime never loses recorded time.
        /// </summary>
        public static void SaveHour(HourStat stat)
        {
            lock (Gate)
            {
                var list = ReadJson<List<HourStat>>(HourlyFile) ?? new List<HourStat>();
                var old = list.FirstOrDefault(h => h.Hour == stat.Hour);
                if (old != null)
                {
                    list.Remove(old);
                    int total = old.Minutes + stat.Minutes;
                    stat = new HourStat
                    {
                        Hour = stat.Hour,
                        Minutes = total,
                        PingMs = (old.PingMs, stat.PingMs) switch
                        {
                            (double a, double b) => Math.Round((a * old.Minutes + b * stat.Minutes) / Math.Max(1, total), 1),
                            (double a, null) => a,
                            (null, var b) => b,
                        },
                        LossPct = Math.Round((old.LossPct * old.Minutes + stat.LossPct * stat.Minutes) / Math.Max(1, total), 1),
                    };
                }
                list.Add(stat);
                WriteJson(HourlyFile, list);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // RETENTION — on startup and once a day
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Deletes probe day files past the tier's retention. Outages and the
        /// hourly roll-up are tiny, so they keep 90 days for everyone — Free
        /// only sees the last 24 h of them, and upgrading reveals the rest.
        /// </summary>
        public static void ApplyRetention(bool isPro)
        {
            try
            {
                lock (Gate)
                {
                    var keepFrom = DateTime.Today.AddDays(-(isPro ? ProRetentionDays : FreeRetentionDays));
                    if (Directory.Exists(ProbeDir))
                    {
                        foreach (var file in Directory.GetFiles(ProbeDir, "*.jsonl"))
                        {
                            if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd",
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.None, out var day) && day < keepFrom)
                                File.Delete(file);
                        }
                    }

                    var cutoff = DateTime.Now.AddDays(-ProRetentionDays);
                    var outages = ReadJson<List<Outage>>(OutagesFile);
                    if (outages != null && outages.RemoveAll(o => (o.End ?? DateTime.Now) < cutoff) > 0)
                        WriteJson(OutagesFile, outages);

                    var hourly = ReadJson<List<HourStat>>(HourlyFile);
                    if (hourly != null && hourly.RemoveAll(h => h.Hour < cutoff) > 0)
                        WriteJson(HourlyFile, hourly);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Monitor retention failed: {ex.Message}");
            }
        }

        /// <summary>Size of the Monitor folder in bytes (shown in Settings).</summary>
        public static long FolderSize()
        {
            try
            {
                return Directory.Exists(Root)
                    ? Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
                    : 0;
            }
            catch { return 0; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // SMALL JSON FILES — temp file + swap, like TestHistoryStore
        // ─────────────────────────────────────────────────────────────────────

        private static T? ReadJson<T>(string path) where T : class
        {
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Monitor file '{Path.GetFileName(path)}' unreadable: {ex.Message}");
                return null;
            }
        }

        private static void WriteJson<T>(string path, T value)
        {
            try
            {
                Directory.CreateDirectory(Root);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(value));
                File.Move(tmp, path, overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Log($"Monitor file '{Path.GetFileName(path)}' write failed: {ex.Message}");
            }
        }
    }
}
