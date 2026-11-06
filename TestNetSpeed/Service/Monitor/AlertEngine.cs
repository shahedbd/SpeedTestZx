using DeviceDataModule;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;

namespace TestNetSpeed.Service.Monitor
{
    /// <summary>
    /// Smart alerts (T56, extends T23). Each alert fires once per incident
    /// and stays quiet during the user's quiet hours (the data is still
    /// logged). Free: internet down and back online. Pro adds:
    ///   • slow — 2+ tests in a row, over at least N hours, all below X% of plan;
    ///   • high latency — per-minute median above the limit for 10 minutes.
    /// MainForm shows the alerts as tray notifications.
    /// </summary>
    public sealed class AlertEngine
    {
        private const int HighLatencyMinutes = 10;

        private bool _slowActive;
        private int _highStreak, _normalStreak;
        private bool _highActive;

        /// <summary>(title, text). Off the UI thread.</summary>
        public event EventHandler<(string Title, string Text)>? Raised;

        private static AppSettingsVm Settings => SettingsService.Current;
        private static bool HasSmartAlerts => EntitlementManager.Instance.HasAccess(ProFeature.SmartAlerts);

        public void OnOutageStarted(Outage outage)
        {
            if (Settings.AlertDown)
                Raise("Internet is down", outage.Cause == Outage.CauseRouter
                    ? "Your router isn't answering either. Check the router or Wi-Fi."
                    : "Your router is fine, so the problem is with your ISP or the internet.");
        }

        public void OnOutageEnded(Outage outage)
        {
            if (Settings.AlertBackOnline)
                Raise("Back online",
                    $"Internet was down {Format.Duration(outage.Duration)} ({ShortCause(outage.Cause)}).");
        }

        /// <summary>Slow-speed check after each scheduled test.</summary>
        public void OnScheduledTest(SpeedTestResult result)
        {
            if (!result.Success || !HasSmartAlerts || !Settings.AlertSlow || !IspPlanStore.Current.IsSet) return;

            double limit = Settings.SlowPctOfPlan;

            // Look back at least one schedule interval (+30 min slack): on the
            // default 3 h schedule a 3 h window held only the latest test, so
            // the "2 tests in a row" rule could never be met.
            double hours = Math.Max(Math.Max(1, Settings.SlowHours), TestScheduler.IntervalHours) + 0.5;
            var since = DateTime.Now.AddHours(-hours);
            var recent = TestHistoryStore.Load()
                .Where(r => r.Success && r.TestedAt >= since && r.PctOfPlan != null)
                .ToList();

            bool slow = recent.Count >= 2 && recent.All(r => r.PctOfPlan < limit);
            if (slow && !_slowActive)
                Raise("Internet is slow",
                    $"Your last {recent.Count} tests were all below {limit:0}% of your plan " +
                    $"(latest {result.PctOfPlan:0}%).");
            _slowActive = slow;
        }

        /// <summary>High-latency check per recorded minute.</summary>
        public void OnMinute(ProbeMinute minute)
        {
            if (!HasSmartAlerts || !Settings.AlertHighLatency) return;

            bool high = minute.MedianMs is double ms && ms > Settings.HighLatencyMs;
            if (high) { _highStreak++; _normalStreak = 0; }
            else { _normalStreak++; _highStreak = 0; }

            if (!_highActive && _highStreak >= HighLatencyMinutes)
            {
                _highActive = true;
                Raise("High latency", $"Ping has been above {Settings.HighLatencyMs} ms for {HighLatencyMinutes} minutes.");
            }
            else if (_highActive && _normalStreak >= HighLatencyMinutes)
            {
                _highActive = false;
            }
        }

        private void Raise(string title, string text)
        {
            if (InQuietHours(DateTime.Now)) return;
            Raised?.Invoke(this, (title, text));
        }

        private static bool InQuietHours(DateTime now)
        {
            var s = Settings;
            if (!s.QuietHoursEnabled) return false;

            int h = now.Hour, start = s.QuietStartHour, end = s.QuietEndHour;
            return start <= end ? h >= start && h < end : h >= start || h < end;
        }

        private static string ShortCause(string cause) => cause == Outage.CauseRouter ? "router / Wi-Fi" : "ISP";
    }
}
