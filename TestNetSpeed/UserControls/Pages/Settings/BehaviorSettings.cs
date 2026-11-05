using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;
using TestNetSpeed.Service.Monitor;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.Pages.Settings
{
    /// <summary>
    /// Behavior — what the close button does, Start with Windows (T58) and,
    /// in Debug builds only, the Pro access override for testing (T08).
    /// Reads/writes SettingsService.Current the same way as before.
    /// </summary>
    internal sealed class BehaviorSettings : SettingsCategory
    {
        public override string Title => "Behavior";
        public override string Subtitle => "How the app window behaves";

        private ToggleSwitchControl _tglTray;
        private ToggleSwitchControl _tglStartup;

        protected override void BuildContent()
        {
            AddSection("Window");
            var card = AddCard();

            _tglTray = new ToggleSwitchControl();
            _tglTray.CheckedChanged += (s, e) => Commit(c => c.MinimizeToTrayOnClose = _tglTray.Checked);

            var row = AddRow(card, "Minimize to tray when closing", _tglTray,
                AppConfig.EnableSystemTray
                    ? "The close button hides the window to the system tray instead of exiting."
                    : "The system tray is turned off in this build.",
                "tray", "close", "exit", "hide", "background", "notification");
            row.SetEnabled(AppConfig.EnableSystemTray);

            // Start with Windows — the MSIX StartupTask; never on unless the user says so.
            _tglStartup = new ToggleSwitchControl();
            _tglStartup.CheckedChanged += async (s, e) =>
            {
                if (IsLoading) return;
                if (_tglStartup.Checked && !await WindowsStartup.EnableAsync())
                {
                    NotificationManager.Instance.ShowWarning(
                        "Windows didn't allow it. Turn it on in Task Manager → Startup apps.", "Start with Windows");
                    await LoadStartupAsync();
                }
                else if (!_tglStartup.Checked)
                {
                    await WindowsStartup.DisableAsync();
                }
            };
            var startupRow = AddRow(card, "Start with Windows", _tglStartup,
                WindowsStartup.IsSupported
                    ? "Starts in the tray at sign-in, so the Connection Monitor keeps recording after a restart."
                    : "Available in the installed (Microsoft Store) app.",
                "startup", "windows", "boot", "sign-in", "monitor", "tray");
            startupRow.SetEnabled(WindowsStartup.IsSupported);

#if DEBUG
            AddSection("Testing (debug build only)");
            var debugCard = AddCard();
            var cmbAccess = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(160) };
            cmbAccess.Items.AddRange(new object[] { "Off (real licence)", "Full access (Pro)", "Gated (Free)" });
            cmbAccess.SelectedIndex = (int)AppConfig.TestAccess;
            SettingsStyle.ThemeInput(cmbAccess);
            cmbAccess.SelectedIndexChanged += (s, e) =>
            {
                AppConfig.TestAccess = (TestAccessMode)cmbAccess.SelectedIndex;
                EntitlementManager.Instance.NotifyTestAccessChanged();
            };
            AddRow(debugCard, "Pro access (debug)", cmbAccess,
                "Overrides the Store licence for this session to test both sides of the paywall.",
                "pro", "premium", "license", "debug", "test");
#endif

            LoadValues();
            _ = LoadStartupAsync();
        }

        private async Task LoadStartupAsync()
        {
            bool enabled = await WindowsStartup.IsEnabledAsync();
            if (IsDisposed) return;
            IsLoading = true;
            try { _tglStartup.Checked = enabled; }
            finally { IsLoading = false; }
        }

        public override void ResetToDefaults()
        {
            Current.MinimizeToTrayOnClose = true;
            Save();
        }

        private void LoadValues()
        {
            IsLoading = true;
            try
            {
                _tglTray.Checked = Current.MinimizeToTrayOnClose;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
