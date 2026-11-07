using DeviceDataModule;
using Microsoft.Win32;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;

namespace SpeedTestZx.Service.Monitor
{
    /// <summary>
    /// Scheduled light tests (T53). Free: once a day. Pro: every 1, 3 or 6 h
    /// (default 3, decision 3). Checks every 30 s whether a test is due —
    /// due = last test + interval — so a PC that slept all night runs ONE
    /// catch-up test on wake, never a burst. A skipped test (metered,
    /// battery saver, busy) retries 10 minutes later.
    /// </summary>
    public sealed class TestScheduler
    {
        private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RetryAfterSkip = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan WakeGrace = TimeSpan.FromSeconds(60);

        private DateTime _retryAt = DateTime.MinValue;

        /// <summary>Why the last due test didn't run, with when — shown on the Monitor page.</summary>
        public string LastSkip { get; private set; } = string.Empty;

        /// <summary>Raised after a scheduled test finished (success or not). Off the UI thread.</summary>
        public event EventHandler<SpeedTestResult>? TestCompleted;

        /// <summary>Raised when NextDue or LastSkip changes. Off the UI thread.</summary>
        public event EventHandler? StateChanged;

        /// <summary>Hours between tests for the current tier (Pro: 1, 3, 6 or daily; Free: daily).</summary>
        public static int IntervalHours
        {
            get
            {
                if (!EntitlementManager.Instance.HasAccess(ProFeature.FrequentTests)) return 24;
                int hours = SettingsService.Current.TestIntervalHours;
                return hours is 1 or 3 or 6 or 24 ? hours : 3;
            }
        }

        /// <summary>When the next test is due, or null when scheduled tests are off.</summary>
        public static DateTime? NextDue
        {
            get
            {
                var s = SettingsService.Current;
                if (!s.ScheduledTestsEnabled) return null;
                var last = s.LastScheduledTestAt ?? DateTime.MinValue;
                var due = last == DateTime.MinValue ? DateTime.Now : last.AddHours(IntervalHours);
                return due < DateTime.Now ? DateTime.Now : due;
            }
        }

        public async Task RunAsync(CancellationToken ct)
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            try
            {
                using var timer = new PeriodicTimer(CheckEvery);
                while (await timer.WaitForNextTickAsync(ct))
                    await CheckAsync(ct);
            }
            catch (OperationCanceledException) { /* stopped */ }
            finally
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            }
        }

        /// <summary>After resume, give the network a minute to reconnect before the catch-up test.</summary>
        private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
                _retryAt = DateTime.Now + WakeGrace;
        }

        private async Task CheckAsync(CancellationToken ct)
        {
            var settings = SettingsService.Current;
            if (!settings.ScheduledTestsEnabled) return;

            var now = DateTime.Now;
            if (now < _retryAt) return;

            var last = settings.LastScheduledTestAt ?? DateTime.MinValue;
            if (last != DateTime.MinValue && now < last.AddHours(IntervalHours)) return;

            string? skip = await TestConditions.SkipReasonAsync();
            if (skip != null)
            {
                LastSkip = $"Skipped at {now:HH:mm}: {skip}. Retrying in 10 min.";
                Logger.Log($"Scheduled test skipped: {skip}");
                _retryAt = now + RetryAfterSkip;
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            var result = await SpeedTestRunner.Instance.RunAsync(null, scheduled: true, ct);
            if (result == null)
            {
                _retryAt = now + RetryAfterSkip;   // a manual test started meanwhile
                return;
            }

            if (result.Success)
            {
                settings.LastScheduledTestAt = now;
                LastSkip = string.Empty;
                TrySaveSettings();
            }
            else
            {
                LastSkip = $"Test at {now:HH:mm} failed: {result.ErrorMessage}";
                _retryAt = now + RetryAfterSkip;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
            TestCompleted?.Invoke(this, result);
        }

        private static void TrySaveSettings()
        {
            try { SettingsService.Save(); }
            catch (Exception ex) { Logger.Log($"Couldn't save scheduled test time: {ex.Message}"); }
        }
    }
}
