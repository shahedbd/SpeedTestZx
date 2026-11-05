using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TestNetSpeed.Service
{
    /// <summary>
    /// Small, cheap round-trip probe used for idle latency, packet loss and
    /// latency under load (T11 / T12).
    ///
    /// HTTP can't see packet loss — TCP quietly re-sends lost packets — so
    /// this uses ICMP echo (System.Net.NetworkInformation.Ping). Some
    /// networks block ICMP; if the first burst gets no replies at all the
    /// probe switches to timing a TCP connect to port 443 instead, and
    /// <see cref="Method"/> becomes "TCP" so the UI can mark loss as
    /// approximate.
    ///
    /// One instance per test run: the ICMP / TCP decision is made once in
    /// <see cref="MeasureIdleAsync"/> and reused for the loaded phases, so
    /// all three stages are measured the same way.
    ///
    /// The Dashboard's live latency uses <see cref="ProbeAsync"/> instead,
    /// which makes the same ICMP → TCP decision on the fly.
    /// </summary>
    public class LatencyProbe
    {
        // Cloudflare's resolver — anycast, close to almost everyone, and it
        // answers both ICMP and TCP 443.
        private static readonly IPAddress Target = IPAddress.Parse("1.1.1.1");
        private const int TcpPort = 443;
        private const int TimeoutMs = 1000;

        private bool _useTcp;

        /// <summary>"ICMP" or "TCP".</summary>
        public string Method => _useTcp ? "TCP" : "ICMP";

        // ══════════════════════════════════════════════════════════════════════
        //  IDLE — latency + packet loss
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sends <paramref name="count"/> probes, <paramref name="intervalMs"/>
        /// apart, without waiting for each reply before sending the next — so
        /// the phase takes about count × interval + one timeout, not count ×
        /// timeout when the line is lossy.
        /// </summary>
        /// <returns>Median RTT (null if nothing answered) and loss in percent.</returns>
        public async Task<(double? MedianMs, double LossPct)> MeasureIdleAsync(
            int count, int intervalMs, Action<double?>? onSample, CancellationToken ct)
        {
            // Decide ICMP vs TCP with a short burst first.
            var burst = await SendBurstAsync(4, intervalMs, useTcp: false, onSample: null, ct);
            if (burst.All(r => r == null))
            {
                var tcpBurst = await SendBurstAsync(4, intervalMs, useTcp: true, onSample: null, ct);
                if (tcpBurst.Any(r => r != null))
                    _useTcp = true;
            }

            var results = await SendBurstAsync(count, intervalMs, _useTcp, onSample, ct);

            var replies = results.Where(r => r != null).Select(r => r!.Value).ToList();
            double lossPct = results.Count == 0 ? 0 : 100.0 * (results.Count - replies.Count) / results.Count;

            return (Median(replies), Math.Round(lossPct, 1));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LOADED — runs alongside a download / upload until cancelled
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Probes every <paramref name="intervalMs"/> until <paramref name="ct"/>
        /// is cancelled, then returns the median RTT of the replies (null if
        /// none). Cancellation is the normal way to stop it and never throws.
        /// </summary>
        public async Task<double?> MeasureLoadedAsync(
            int intervalMs, Action<double?>? onSample, CancellationToken ct)
        {
            var pending = new List<Task<double?>>();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    pending.Add(ProbeOnceAsync(_useTcp, onSample));
                    await Task.Delay(intervalMs, ct);
                }
            }
            catch (OperationCanceledException) { /* normal stop */ }

            // Replies still in flight are at most one timeout away.
            var results = await Task.WhenAll(pending);
            return Median(results.Where(r => r != null).Select(r => r!.Value).ToList());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SINGLE — one probe at a time (Dashboard live latency, T42)
        // ══════════════════════════════════════════════════════════════════════

        // After this many ICMP misses in a row, check whether TCP answers —
        // if it does, ICMP is blocked here and the probe switches to TCP.
        private const int IcmpMissesBeforeTcpCheck = 3;
        private int _icmpMisses;

        /// <summary>
        /// Sends one probe and returns its round-trip in ms, or null when it
        /// was lost (no reply within the timeout). Starts with ICMP and
        /// switches to TCP for the rest of this instance's life once ICMP
        /// misses <see cref="IcmpMissesBeforeTcpCheck"/> times in a row while
        /// TCP still answers. Never throws; takes at most about two timeouts.
        /// Callers should await each call before starting the next.
        /// </summary>
        public async Task<double?> ProbeAsync()
        {
            if (_useTcp)
                return await TcpConnectAsync();

            double? rtt = await IcmpAsync();
            if (rtt != null)
            {
                _icmpMisses = 0;
                return rtt;
            }

            if (++_icmpMisses < IcmpMissesBeforeTcpCheck)
                return null;

            // ICMP has gone quiet. If TCP answers, the network blocks ICMP
            // rather than being down: switch, and report this TCP reply.
            _icmpMisses = 0;
            double? tcp = await TcpConnectAsync();
            if (tcp != null)
                _useTcp = true;

            return tcp;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LOW LEVEL
        // ══════════════════════════════════════════════════════════════════════

        private static async Task<List<double?>> SendBurstAsync(
            int count, int intervalMs, bool useTcp, Action<double?>? onSample, CancellationToken ct)
        {
            var tasks = new List<Task<double?>>(count);
            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                tasks.Add(ProbeOnceAsync(useTcp, onSample));
                if (i < count - 1)
                    await Task.Delay(intervalMs, ct);
            }

            return (await Task.WhenAll(tasks)).ToList();
        }

        /// <summary>One probe: RTT in ms, or null for no reply within the timeout.</summary>
        private static async Task<double?> ProbeOnceAsync(bool useTcp, Action<double?>? onSample)
        {
            double? rtt = useTcp ? await TcpConnectAsync() : await IcmpAsync();
            onSample?.Invoke(rtt);
            return rtt;
        }

        private static async Task<double?> IcmpAsync()
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(Target, TimeoutMs);
                return reply.Status == IPStatus.Success ? (double)reply.RoundtripTime : null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<double?> TcpConnectAsync()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };
                using var timeout = new CancellationTokenSource(TimeoutMs);

                var sw = Stopwatch.StartNew();
                await socket.ConnectAsync(Target, TcpPort, timeout.Token);
                sw.Stop();

                return Math.Round(sw.Elapsed.TotalMilliseconds, 1);
            }
            catch
            {
                return null;
            }
        }

        private static double? Median(List<double> values)
        {
            if (values.Count == 0) return null;

            values.Sort();
            int mid = values.Count / 2;
            double median = values.Count % 2 == 1
                ? values[mid]
                : (values[mid - 1] + values[mid]) / 2;

            return Math.Round(median, 1);
        }
    }
}
