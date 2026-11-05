using TestNetSpeed.Helper;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.Pages.Settings
{
    /// <summary>
    /// Behavior — what the close button does. Moved here from the old
    /// Settings dialog; reads/writes SettingsService.Current the same way.
    /// </summary>
    internal sealed class BehaviorSettings : SettingsCategory
    {
        public override string Title => "Behavior";
        public override string Subtitle => "How the app window behaves";

        private ToggleSwitchControl _tglTray;

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

            LoadValues();
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
