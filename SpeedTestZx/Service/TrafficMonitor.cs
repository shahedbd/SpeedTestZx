using System.Diagnostics;
using System.Net.NetworkInformation;

namespace SpeedTestZx.Service
{
    /// <summary>One reading from <see cref="TrafficMonitor"/>.</summary>
    /// <param name="DownMbps">Combined receive rate of the counted adapters.</param>
    /// <param name="UpMbps">Combined send rate of the counted adapters.</param>
    /// <param name="AdapterCount">How many adapters were counted (0 = no active physical adapter).</param>
    public readonly record struct TrafficSample(double DownMbps, double UpMbps, int AdapterCount);

    /// <summary>
    /// Live network traffic on this PC (Dashboard plan T41): how fast all
    /// apps together are downloading and uploading right now.
    ///
    /// Reads the byte counters Windows keeps per network adapter
    /// (<see cref="NetworkInterface.GetIPStatistics"/>) once per tick and
    /// turns the change into Mbps. It sends nothing over the network.
    ///
    /// Only physical Ethernet and Wi-Fi adapters that are up are summed.
    /// Loopback, tunnel, VPN and virtual adapters (Hyper-V, VMware,
    /// VirtualBox, WSL, TAP, WireGuard, Bluetooth PAN …) are skipped: VPN
    /// traffic still crosses the physical adapter, so counting the VPN
    /// adapter too would double it.
    ///
    /// Ticks come from a WinForms timer, so <see cref="Sample"/> is raised
    /// on the UI thread. Create, start and stop it from the UI thread.
    /// </summary>
    public sealed class TrafficMonitor : IDisposable
    {
        private const int DefaultIntervalMs = 1000;

        // Re-read the adapter list at least this often even without a
        // NetworkAddressChanged event (e.g. an adapter going up/down
        // without an address change).
        private const int AdapterRefreshTicks = 15;

        // Description / name fragments that mark a virtual or tunnel adapter.
        private static readonly string[] VirtualMarkers =
        [
            "virtual", "hyper-v", "vethernet", "vmware", "virtualbox", "vbox",
            "wsl", "tap-", "tap ", "tun", "wireguard", "wintun", "vpn", "tunnel",
            "loopback", "npcap", "bluetooth", "pseudo", "teredo", "isatap",
        ];

        private readonly System.Windows.Forms.Timer _timer;
        private readonly Stopwatch _clock = new();
        private readonly Dictionary<string, (long Rx, long Tx)> _last = new();

        private List<NetworkInterface> _adapters = new();
        private volatile bool _adaptersStale = true;
        private int _ticksSinceRefresh;
        private double _lastSec;
        private bool _disposed;
        private bool _wantRunning;   // Start() called and not stopped since
        private bool _baselining;    // baseline read in flight on a worker

        /// <summary>Raised once per tick with the combined rate.</summary>
        public event EventHandler<TrafficSample>? Sample;

        public bool IsRunning => _timer.Enabled;

        public TrafficMonitor(int intervalMs = DefaultIntervalMs)
        {
            _timer = new System.Windows.Forms.Timer { Interval = Math.Max(250, intervalMs) };
            _timer.Tick += (_, _) => Tick();
            NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        }

        /// <summary>
        /// Starts sampling. The baseline read (adapter list + counters, ~120 ms
        /// the first time) runs on a worker so it never holds up the UI; the
        /// timer starts when it is done and the first sample arrives one
        /// interval later. The timer is not running during the read, so
        /// nothing else touches the counters meanwhile.
        /// </summary>
        public void Start()
        {
            if (_disposed) return;
            _wantRunning = true;
            if (_timer.Enabled || _baselining) return;

            _baselining = true;
            _adaptersStale = true;
            _last.Clear();

            var ui = SynchronizationContext.Current is null
                ? TaskScheduler.Current
                : TaskScheduler.FromCurrentSynchronizationContext();

            Task.Run(() => ReadCounters(out _, out _)).ContinueWith(_ =>
            {
                _baselining = false;
                if (_disposed || !_wantRunning) return;

                _clock.Restart();
                _lastSec = 0;
                _timer.Start();
            }, ui);
        }

        /// <summary>Stops sampling. Start() later begins a fresh baseline.</summary>
        public void Stop()
        {
            _wantRunning = false;
            _timer.Stop();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SAMPLING
        // ══════════════════════════════════════════════════════════════════════

        private void Tick()
        {
            double now = _clock.Elapsed.TotalSeconds;
            double dt = now - _lastSec;
            _lastSec = now;

            ReadCounters(out long rxDelta, out long txDelta);

            // A tick that fired very late (system sleep, debugger) would
            // average over a long gap — still correct as a rate, but skip
            // absurdly short ones to avoid divide-by-tiny spikes.
            if (dt < 0.1) return;

            Sample?.Invoke(this, new TrafficSample(
                DownMbps: ToMbps(rxDelta, dt),
                UpMbps: ToMbps(txDelta, dt),
                AdapterCount: _adapters.Count));
        }

        /// <summary>
        /// Reads every counted adapter and returns the bytes moved since the
        /// previous read. Adapters seen for the first time, or whose counter
        /// went backwards (driver reset, Wi-Fi reconnect), add 0 this time.
        /// </summary>
        private void ReadCounters(out long rxDelta, out long txDelta)
        {
            rxDelta = 0;
            txDelta = 0;

            if (_adaptersStale || ++_ticksSinceRefresh >= AdapterRefreshTicks)
                RefreshAdapters();

            var seen = new HashSet<string>();

            foreach (var nic in _adapters)
            {
                long rx, tx;
                try
                {
                    var stats = nic.GetIPStatistics();
                    rx = stats.BytesReceived;
                    tx = stats.BytesSent;
                }
                catch
                {
                    // Adapter vanished between listing and reading.
                    _adaptersStale = true;
                    continue;
                }

                seen.Add(nic.Id);

                if (_last.TryGetValue(nic.Id, out var prev))
                {
                    long dRx = rx - prev.Rx;
                    long dTx = tx - prev.Tx;
                    if (dRx >= 0) rxDelta += dRx;
                    if (dTx >= 0) txDelta += dTx;
                }

                _last[nic.Id] = (rx, tx);
            }

            // Forget adapters that are gone, so a reconnect starts clean.
            foreach (var id in _last.Keys.Where(id => !seen.Contains(id)).ToList())
                _last.Remove(id);
        }

        private void RefreshAdapters()
        {
            _adaptersStale = false;
            _ticksSinceRefresh = 0;

            try
            {
                _adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(IsCounted)
                    .ToList();
            }
            catch
            {
                _adapters = new();
            }
        }

        private static bool IsCounted(NetworkInterface nic)
        {
            if (nic.OperationalStatus != OperationalStatus.Up) return false;

            bool physicalType = nic.NetworkInterfaceType is
                NetworkInterfaceType.Ethernet or
                NetworkInterfaceType.Wireless80211 or
                NetworkInterfaceType.GigabitEthernet or
                NetworkInterfaceType.FastEthernetT or
                NetworkInterfaceType.FastEthernetFx or
                NetworkInterfaceType.Ethernet3Megabit;
            if (!physicalType) return false;

            string text = (nic.Description + " " + nic.Name).ToLowerInvariant();
            return !VirtualMarkers.Any(text.Contains);
        }

        private void OnNetworkChanged(object? sender, EventArgs e) =>
            _adaptersStale = true;   // raised on a worker thread — just flag it

        private static double ToMbps(long bytes, double seconds) =>
            seconds > 0 ? bytes * 8.0 / seconds / 1_000_000 : 0;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
