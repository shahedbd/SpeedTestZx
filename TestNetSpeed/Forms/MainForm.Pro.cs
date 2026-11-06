using DeviceDataModule;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;
using TestNetSpeed.Service.Monitor;

namespace TestNetSpeed.Forms
{
    /// <summary>
    /// MainForm — Pro and Connection Monitor wiring (Pro_plan.md Phases 0 / 2):
    ///   • Store entitlement: initialise with the window handle, check on
    ///     show, re-check once a day on activate; a tier change rebuilds the
    ///     active page so Pro parts lock or unlock at once.
    ///   • The upgrade overlay over the content panel (PromptManager).
    ///   • The Monitor: started at launch, alerts shown as tray
    ///     notifications, status in the tray tooltip (T58).
    ///   • Started by Windows at sign-in → go straight to the tray.
    /// </summary>
    public partial class MainForm
    {
        private UserTier _lastTier;
        private System.Windows.Forms.Timer? _trayStatusTimer;

        private void InitializePro()
        {
            var entitlements = EntitlementManager.Instance;
            _lastTier = entitlements.CurrentTier;
            entitlements.EntitlementChanged += OnEntitlementChanged;

            _ = new PromptManager(contentPanel);

            Activated += (s, e) =>
            {
                // Only after the first check, so this never races it.
                if (entitlements.LastCheckedUtc is null) return;
                _ = entitlements.RefreshIfStaleAsync(TimeSpan.FromHours(24));
            };
        }

        private void OnShownPro()
        {
            // Creating the Store context (~65 ms) and starting the Monitor wait
            // until the window has painted. Until then a purchase returns null
            // and the price falls back to AppConfig, as before Initialize.
            RunWhenIdle(() =>
            {
                if (IsDisposed) return;
                EntitlementManager.Instance.Initialize(Handle);
                _ = EntitlementManager.Instance.RefreshAsync(TimeSpan.FromSeconds(20));
                StartMonitor();
            });

            if (AppConfig.EnableSystemTray && WindowsStartup.IsStartupActivation())
                BeginInvoke(new Action(HideToTraySilently));
        }

        private void OnEntitlementChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;

            BeginInvoke(new Action(() =>
            {
                var tier = EntitlementManager.Instance.CurrentTier;
                if (tier == _lastTier) return;

                _lastTier = tier;
                _navigationManager.RefreshActivePage();
            }));
        }

        // ─────────────────────────────────────────────────────────────────────
        // CONNECTION MONITOR
        // ─────────────────────────────────────────────────────────────────────

        private void StartMonitor()
        {
            var monitor = MonitorService.Instance;
            monitor.AlertRaised += OnMonitorAlert;
            monitor.StatusChanged += (s, e) => UpdateTrayForMonitor();
            monitor.Start();

            // Ping in the tooltip moves every probe — refresh it on a slow timer.
            _trayStatusTimer = new System.Windows.Forms.Timer { Interval = 15000 };
            _trayStatusTimer.Tick += (s, e) => UpdateTrayForMonitor();
            _trayStatusTimer.Start();
            UpdateTrayForMonitor();
        }

        /// <summary>Keeps the tray icon up while the Monitor runs, with its status as the tooltip.</summary>
        private void UpdateTrayForMonitor()
        {
            if (!AppConfig.EnableSystemTray || IsDisposed) return;
            if (InvokeRequired)
            {
                if (IsHandleCreated) BeginInvoke(new Action(UpdateTrayForMonitor));
                return;
            }

            var monitor = MonitorService.Instance;
            if (!monitor.IsActive && _trayService == null) return;   // nothing to show or hide yet

            var tray = Tray!;
            tray.SetTooltip(monitor.TrayStatus());

            if (monitor.IsActive) tray.Show();
            else if (Visible) tray.Hide();
        }

        private void OnMonitorAlert(object? sender, (string Title, string Text) alert)
        {
            if (IsDisposed || !IsHandleCreated) return;

            BeginInvoke(new Action(() =>
            {
                var tray = Tray;
                if (tray == null)
                {
                    NotificationManager.Instance.ShowInfo(alert.Text, alert.Title);
                    return;
                }

                tray.Show();
                tray.ShowBalloonTip(alert.Title, alert.Text, 5000, ToolTipIcon.Info);
            }));
        }

        /// <summary>Launched by the StartupTask: keep monitoring from the tray, no window.</summary>
        private void HideToTraySilently()
        {
            if (!SettingsService.Current.MonitorEnabled && !SettingsService.Current.ScheduledTestsEnabled)
                return;

            Hide();
            Tray?.Show();
        }
    }
}
