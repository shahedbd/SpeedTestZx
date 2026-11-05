using TestNetSpeed.Forms;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;

namespace TestNetSpeed.UserControls.Pages.Settings
{
    /// <summary>
    /// Test History — clearing saved speed test results. Moved here from the
    /// old Settings dialog's Data section; same confirm + TestHistoryStore.Clear.
    /// </summary>
    internal sealed class DataSettings : SettingsCategory
    {
        public override string Title => "Test History";
        public override string Subtitle => "Speed test results saved on this PC";
        public override bool CanReset => false;

        protected override void BuildContent()
        {
            AddSection("Saved results");
            var card = AddCard();

            var button = MakeButton("Clear history…", (s, e) => ClearHistory());
            AddRow(card, "Clear test history", button,
                "Deletes every saved speed test result. This cannot be undone.",
                "delete", "remove", "results", "data", "reset", "wipe");
            // After AddRow: theming the row ghost-styles its input, which would reset the color.
            button.ForeColor = Theme.ErrorColor;
        }

        private static void ClearHistory()
        {
            bool confirmed = AlertService.Confirm(
                "This deletes every saved speed test result.\n\nThis cannot be undone.",
                "Clear Test History", AlertType.Warning, "Clear", "Cancel");
            if (!confirmed) return;

            TestHistoryStore.Clear();
            NotificationManager.Instance.ShowSuccess("Test history cleared.");
        }
    }
}
