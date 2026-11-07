using DeviceDataModule;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;

namespace SpeedTestZx.Service.Monitor
{
    /// <summary>
    /// Connection Monitor engine (Pro_plan.md Phase 2). Runs in the app
    /// process — which stays alive in the tray when the window is closed —
    /// never as a Windows service and never with admin rights. If the app
    /// isn't running, nothing is recorded and the page says so.
    ///
    /// Two independent loops, each switched by its own setting:
    ///   • LatencyLogger (MonitorEnabled) → latency log, outages, alerts;
    ///   • TestScheduler (ScheduledTestsEnabled) → scheduled light tests.
    /// Call <see cref="ApplySettings"/> after changing either setting.
    /// </summary>
    public sealed class MonitorService
    {
        private static readonly Lazy<MonitorService> _instance = new(() => new MonitorService());
        public static MonitorService Instance => _instance.Value;

        private readonly OutageDetector _outages = new();
        private readonly AlertEngine _alerts = new();
        private readonly TestScheduler _scheduler = new();
        private readonly LatencyLogger _logger;

        private CancellationTokenSource? _logCts, _testCts;
        private DateTime _retentionDay;

        /// <summary>Latest internet probe RTT (null = lost or not probed yet).</summary>
        public double? LastProbeMs { get; private set; }

        public bool IsLogging => _logCts != null;
        public bool IsScheduling => _testCts != null;
        public bool IsActive => IsLogging || IsScheduling;

        public Outage? CurrentOutage => _outages.Current;
        public string LastSkip => _scheduler.LastSkip;

        /// <summary>Alert to show as a tray notification. Off the UI thread.</summary>
        public event EventHandler<(string Title, string Text)>? AlertRaised;

        /// <summary>Anything the page or tray tooltip shows has changed. Off the UI thread.</summary>
        public event EventHandler? StatusChanged;

        /// <summary>A minute of probe data was recorded. Off the UI thread.</summary>
        public event EventHandler<ProbeMinute>? MinuteRecorded;

        private MonitorService()
        {
            _logger = new LatencyLogger(_outages);

            _logger.Probed += (s, ms) => { LastProbeMs = ms; };
            _logger.MinuteRecorded += OnMinute;
            _outages.Started += (s, o) => { _alerts.OnOutageStarted(o); Changed(); };
            _outages.Ended += (s, o) => { _alerts.OnOutageEnded(o); Changed(); };
            _alerts.Raised += (s, a) => AlertRaised?.Invoke(this, a);
            _scheduler.StateChanged += (s, e) => Changed();
            _scheduler.TestCompleted += (s, r) => _alerts.OnScheduledTest(r);
            EntitlementManager.Instance.EntitlementChanged += (s, e) => Changed();
        }

        /// <summary>Call once at startup: tidies old data, then starts what the settings ask for.</summary>
        public void Start()
        {
            _ = Task.Run(async () =>
            {
                OutageDetector.CloseLeftOpen();

                // Retention depends on the tier — wait for the first Store answer.
                var em = EntitlementManager.Instance;
                await Task.WhenAny(em.FirstCheck, Task.Delay(TimeSpan.FromSeconds(30)));
                ApplyRetention();
            });
            ApplySettings();
        }

        /// <summary>Starts or stops each loop to match the current settings.</summary>
        public void ApplySettings()
        {
            var s = SettingsService.Current;

            if (s.MonitorEnabled && _logCts == null)
            {
                _logCts = new CancellationTokenSource();
                var token = _logCts.Token;
                _ = Task.Run(() => _logger.RunAsync(token));
            }
            else if (!s.MonitorEnabled && _logCts != null)
            {
                _logCts.Cancel();
                _logCts = null;
                _outages.Reset();
            }

            if (s.ScheduledTestsEnabled && _testCts == null)
            {
                _testCts = new CancellationTokenSource();
                var token = _testCts.Token;
                _ = Task.Run(() => _scheduler.RunAsync(token));
            }
            else if (!s.ScheduledTestsEnabled && _testCts != null)
            {
                _testCts.Cancel();
                _testCts = null;
            }

            Changed();
        }

        public void Stop()
        {
            _logCts?.Cancel();
            _testCts?.Cancel();
            _logCts = _testCts = null;
        }

        /// <summary>One line for the tray tooltip (≤ 63 chars).</summary>
        public string TrayStatus()
        {
            if (!IsActive) return AppConfig.AppName;
            if (CurrentOutage is { } o) return $"Internet down since {o.Start:HH:mm}";

            string ping = LastProbeMs is double ms ? $"ping {ms:0} ms" : "monitoring";
            string next = TestScheduler.NextDue is DateTime due ? $" · next test {due:HH:mm}" : "";
            return $"Monitor: {ping}{next}";
        }

        private void OnMinute(object? sender, ProbeMinute minute)
        {
            _alerts.OnMinute(minute);
            MinuteRecorded?.Invoke(this, minute);

            if (DateTime.Today != _retentionDay) ApplyRetention();
            Changed();
        }

        private void ApplyRetention()
        {
            _retentionDay = DateTime.Today;

            // Free trims to 24 h only on a Store-verified answer: an offline
            // Pro user past the cache's grace window must never lose 90 days.
            var em = EntitlementManager.Instance;
            MonitorStore.ApplyRetention(em.HasAccess(ProFeature.LatencyLog) || !em.Current.IsVerified);
        }

        private void Changed() => StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
