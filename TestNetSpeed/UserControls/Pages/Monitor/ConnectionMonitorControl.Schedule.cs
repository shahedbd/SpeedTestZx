using DeviceDataModule;
using TestNetSpeed.Forms;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;
using TestNetSpeed.Service.Monitor;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.Pages.Monitor
{
    /// <summary>
    /// ConnectionMonitorControl — the "Schedule &amp; alerts" card: the
    /// switches for the whole Monitor (T53 / T56 / T58). Everything is off
    /// until the user turns it on here. Turning on automatic tests the first
    /// time offers "Start with Windows" once (decision 4). Pro-only options
    /// stay visible and open the upgrade prompt when picked.
    /// </summary>
    public partial class ConnectionMonitorControl
    {
        /// <summary>Interval choices, in combo order.</summary>
        private static readonly int[] IntervalHours = { 24, 6, 3, 1 };

        /// <summary>Approximate data per light test (≈ 25 MB each way).</summary>
        private const int MbPerTest = 50;

        private CheckBox _chkMonitor = null!, _chkTests = null!, _chkMetered = null!, _chkBattery = null!;
        private CheckBox _chkDown = null!, _chkSlow = null!, _chkLatency = null!, _chkQuiet = null!;
        private ComboBox _cmbInterval = null!;
        private Label _lblUsage = null!;
        private bool _loadingSchedule;

        private static AppSettingsVm Settings => SettingsService.Current;

        private void BuildScheduleCard()
        {
            _scheduleCard = PageCards.Card();
            var title = PageCards.Title("Schedule & alerts");

            var body = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
            };

            string star = IsPro ? "" : "  ★ Pro";
            _chkMonitor = Check(body, "Monitor latency & outages (a tiny probe every 10 s)");
            _chkTests = Check(body, "Run automatic speed tests");

            _cmbInterval = PageCards.Combo("Once a day", "Every 6 hours" + star, "Every 3 hours" + star, "Every 1 hour" + star);
            _cmbInterval.Width = Scale(190);
            _cmbInterval.Margin = new Padding(Scale(20), Scale(2), 0, Scale(2));
            body.Controls.Add(_cmbInterval);

            _lblUsage = Note(body, "");
            _chkMetered = Check(body, "Also test on metered connections", indent: true);
            _chkBattery = Check(body, "Also test with battery saver on", indent: true);

            var alerts = PageCards.Title("Alerts");
            alerts.Size = new Size(Scale(300), Scale(24));
            alerts.Margin = new Padding(0, Scale(8), 0, 0);
            body.Controls.Add(alerts);

            _chkDown = Check(body, "Internet down and back online");
            _chkSlow = Check(body, $"Slow: below {Settings.SlowPctOfPlan}% of plan for {Settings.SlowHours} h{star}");
            _chkLatency = Check(body, $"High latency: above {Settings.HighLatencyMs} ms for 10 min{star}");
            _chkQuiet = Check(body, $"Quiet hours {Settings.QuietStartHour:00}:00–{Settings.QuietEndHour:00}:00");
            Note(body, "Runs while the app is open or in the tray. Start with Windows is in Settings → Behavior.");

            _scheduleCard.Controls.AddRange(new Control[] { title, body });
            _scheduleCard.SizeChanged += (s, e) =>
            {
                var rect = LayoutTitled(_scheduleCard, title, null);
                body.Bounds = rect;
            };
            Controls.Add(_scheduleCard);

            LoadSchedule();
            WireSchedule();
        }

        private CheckBox Check(FlowLayoutPanel body, string text, bool indent = false)
        {
            var chk = PageCards.Check(text);
            chk.Margin = new Padding(indent ? Scale(20) : 0, Scale(2), 0, Scale(2));
            body.Controls.Add(chk);
            return chk;
        }

        private Label Note(FlowLayoutPanel body, string text)
        {
            var lbl = PageCards.Info(text);
            lbl.AutoSize = true;
            lbl.MaximumSize = new Size(Scale(380), 0);
            lbl.Margin = new Padding(Scale(20), 0, 0, Scale(4));
            body.Controls.Add(lbl);
            return lbl;
        }

        private void LoadSchedule()
        {
            _loadingSchedule = true;
            var s = Settings;
            _chkMonitor.Checked = s.MonitorEnabled;
            _chkTests.Checked = s.ScheduledTestsEnabled;
            _cmbInterval.SelectedIndex = IsPro ? Math.Max(0, Array.IndexOf(IntervalHours, TestScheduler.IntervalHours)) : 0;
            _chkMetered.Checked = s.TestOnMetered;
            _chkBattery.Checked = s.TestOnBatterySaver;
            _chkDown.Checked = s.AlertDown;
            _chkSlow.Checked = IsPro && s.AlertSlow;
            _chkLatency.Checked = IsPro && s.AlertHighLatency;
            _chkQuiet.Checked = s.QuietHoursEnabled;
            _loadingSchedule = false;
            UpdateScheduleState();
        }

        private void WireSchedule()
        {
            _chkMonitor.CheckedChanged += (s, e) => Commit(v => v.MonitorEnabled = _chkMonitor.Checked);
            _chkTests.CheckedChanged += (s, e) => OnTestsToggled();
            _cmbInterval.SelectedIndexChanged += (s, e) => OnIntervalChanged();
            _chkMetered.CheckedChanged += (s, e) => Commit(v => v.TestOnMetered = _chkMetered.Checked);
            _chkBattery.CheckedChanged += (s, e) => Commit(v => v.TestOnBatterySaver = _chkBattery.Checked);
            _chkDown.CheckedChanged += (s, e) => Commit(v => v.AlertDown = v.AlertBackOnline = _chkDown.Checked);
            _chkSlow.CheckedChanged += (s, e) => ProCommit(_chkSlow, v => v.AlertSlow = _chkSlow.Checked);
            _chkLatency.CheckedChanged += (s, e) => ProCommit(_chkLatency, v => v.AlertHighLatency = _chkLatency.Checked);
            _chkQuiet.CheckedChanged += (s, e) => Commit(v => v.QuietHoursEnabled = _chkQuiet.Checked);
        }

        private void OnTestsToggled()
        {
            Commit(v => v.ScheduledTestsEnabled = _chkTests.Checked);
            if (_chkTests.Checked && !Settings.StartWithWindowsOffered)
                _ = OfferStartWithWindowsAsync();
        }

        private void OnIntervalChanged()
        {
            if (_loadingSchedule) return;

            if (_cmbInterval.SelectedIndex > 0 && !EntitlementManager.Instance.HasAccess(ProFeature.FrequentTests))
            {
                _loadingSchedule = true;
                _cmbInterval.SelectedIndex = 0;
                _loadingSchedule = false;
                PromptManager.Current?.Show(ProFeature.FrequentTests, () => { });
                return;
            }

            Commit(v => v.TestIntervalHours = IntervalHours[_cmbInterval.SelectedIndex]);
        }

        /// <summary>A Pro-only switch: free users get the prompt and the box un-ticks itself.</summary>
        private void ProCommit(CheckBox box, Action<AppSettingsVm> write)
        {
            if (_loadingSchedule) return;
            if (box.Checked && !EntitlementManager.Instance.HasAccess(ProFeature.SmartAlerts))
            {
                _loadingSchedule = true;
                box.Checked = false;
                _loadingSchedule = false;
                PromptManager.Current?.Show(ProFeature.SmartAlerts, () => { });
                return;
            }
            Commit(write);
        }

        private void Commit(Action<AppSettingsVm> write)
        {
            if (_loadingSchedule) return;

            write(Settings);
            try { SettingsService.Save(); }
            catch { NotificationManager.Instance.ShowError("Couldn't save the Monitor settings."); }

            MonitorService.Instance.ApplySettings();
            UpdateScheduleState();
            _ = ReloadAsync();
        }

        private void UpdateScheduleState()
        {
            bool tests = _chkTests.Checked;
            _cmbInterval.Enabled = _chkMetered.Enabled = _chkBattery.Enabled = tests;

            int hours = IsPro ? IntervalHours[Math.Max(0, _cmbInterval.SelectedIndex)] : 24;
            int perDay = 24 / hours;
            _lblUsage.Text = $"About {MbPerTest} MB per test · about {perDay * MbPerTest:N0} MB a day";
            RenderStatus();
        }

        /// <summary>Asked once, the first time automatic tests are turned on (decision 4).</summary>
        private async Task OfferStartWithWindowsAsync()
        {
            Settings.StartWithWindowsOffered = true;
            try { SettingsService.Save(); } catch { /* asked again next time — harmless */ }

            if (!WindowsStartup.IsSupported || await WindowsStartup.IsEnabledAsync()) return;

            bool yes = AlertService.Confirm(
                "Start with Windows so automatic tests keep running after a restart?\n" +
                "The app starts in the tray. You can change this later in Settings → Behavior.",
                "Start with Windows", AlertType.Info, "Start with Windows", "Not now");
            if (!yes) return;

            if (!await WindowsStartup.EnableAsync())
                NotificationManager.Instance.ShowWarning(
                    "Windows didn't allow it. Turn it on in Task Manager → Startup apps.", "Start with Windows");
        }
    }
}
