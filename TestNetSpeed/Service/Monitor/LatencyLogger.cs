using System.Net;
using System.Net.NetworkInformation;
using TestNetSpeed.Helper;
using TestNetSpeed.Model;

namespace TestNetSpeed.Service.Monitor
{
    /// <summary>
    /// Latency and loss log (T54). Every 10 s it probes 1.1.1.1 (ICMP, TCP
    /// fallback — the same LatencyProbe as the Dashboard) and the router
    /// (ICMP to the default gateway) in parallel. Each probe feeds the
    /// OutageDetector live; every minute the samples are stored as one line
    /// (median, max, loss) and rolled into the hourly cache for the heatmap.
    /// </summary>
    public sealed class LatencyLogger
    {
        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
        private const int GatewayTimeoutMs = 1000;

        private readonly LatencyProbe _probe = new();
        private readonly OutageDetector _outages;

        private readonly List<double?> _internet = new();
        private readonly List<bool?> _router = new();
        private readonly List<double> _routerMs = new();
        private DateTime _minute;

        private readonly List<ProbeMinute> _hourMinutes = new();
        private DateTime _hour;

        private IPAddress? _gateway;
        private DateTime _gatewayCheckedAt;

        public LatencyLogger(OutageDetector outages) => _outages = outages;

        /// <summary>Raised after each probe with the internet RTT (null = lost). Off the UI thread.</summary>
        public event EventHandler<double?>? Probed;

        /// <summary>Raised after a minute is written. Off the UI thread.</summary>
        public event EventHandler<ProbeMinute>? MinuteRecorded;

        public async Task RunAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(Interval);
            try
            {
                do
                {
                    await ProbeOnceAsync();
                }
                while (await timer.WaitForNextTickAsync(ct));
            }
            catch (OperationCanceledException) { /* stopped */ }
            finally
            {
                FlushMinute();
                FlushHour();
            }
        }

        private async Task ProbeOnceAsync()
        {
            var now = DateTime.Now;
            RefreshGateway(now);

            var internetTask = _probe.ProbeAsync();
            var routerTask = PingGatewayAsync(_gateway);
            await Task.WhenAll(internetTask, routerTask);

            double? rtt = internetTask.Result;
            double? routerRtt = routerTask.Result;
            bool? routerOk = _gateway == null ? null : routerRtt != null;

            var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            if (minute != _minute)
            {
                FlushMinute();
                _minute = minute;
            }

            _internet.Add(rtt);
            _router.Add(routerOk);
            if (routerRtt is double r) _routerMs.Add(r);

            _outages.OnProbe(now, rtt != null, routerOk);
            Probed?.Invoke(this, rtt);
        }

        private void RefreshGateway(DateTime now)
        {
            if (_gateway != null && now - _gatewayCheckedAt < TimeSpan.FromMinutes(1)) return;
            _gateway = NetworkRoute.Current().Gateway;
            _gatewayCheckedAt = now;
        }

        private static async Task<double?> PingGatewayAsync(IPAddress? gateway)
        {
            if (gateway == null) return null;
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(gateway, GatewayTimeoutMs);
                return reply.Status == IPStatus.Success ? (double)reply.RoundtripTime : null;
            }
            catch
            {
                return null;
            }
        }

        // ── Minute → jsonl line ──────────────────────────────────────────────

        private void FlushMinute()
        {
            if (_internet.Count == 0) return;

            var replies = _internet.Where(v => v != null).Select(v => v!.Value).ToList();
            var routerKnown = _router.Where(v => v != null).ToList();

            var row = new ProbeMinute
            {
                Time = _minute,
                MedianMs = Stats.Median(replies),
                MaxMs = replies.Count > 0 ? replies.Max() : null,
                LossPct = Math.Round(100.0 * (_internet.Count - replies.Count) / _internet.Count, 1),
                GatewayMs = Stats.Median(_routerMs),
                GatewayLossPct = routerKnown.Count == 0 ? null
                    : Math.Round(100.0 * routerKnown.Count(v => v == false) / routerKnown.Count, 1),
                Count = _internet.Count,
            };

            _internet.Clear();
            _router.Clear();
            _routerMs.Clear();

            MonitorStore.AppendMinute(row);
            AddToHour(row);
            MinuteRecorded?.Invoke(this, row);
        }

        // ── Hour → hourly.json ───────────────────────────────────────────────

        private void AddToHour(ProbeMinute row)
        {
            var hour = row.Time.Date.AddHours(row.Time.Hour);
            if (hour != _hour)
            {
                FlushHour();
                _hour = hour;
            }
            _hourMinutes.Add(row);
        }

        private void FlushHour()
        {
            if (_hourMinutes.Count == 0) return;

            var medians = _hourMinutes.Where(m => m.MedianMs != null).Select(m => m.MedianMs!.Value).ToList();
            MonitorStore.SaveHour(new HourStat
            {
                Hour = _hour,
                PingMs = Stats.Median(medians),
                LossPct = Math.Round(_hourMinutes.Average(m => m.LossPct), 1),
                Minutes = _hourMinutes.Count,
            });
            _hourMinutes.Clear();
        }
    }
}
