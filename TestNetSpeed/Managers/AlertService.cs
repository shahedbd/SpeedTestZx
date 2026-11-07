using SpeedTestZx.Forms;

namespace SpeedTestZx.Managers
{
    /// <summary>
    /// Reusable modal alert/confirm service — replaces raw MessageBox.Show
    /// calls with a theme-aware dialog matching the rest of the app
    /// (AlertDialog). Call from anywhere; each call blocks like MessageBox
    /// did, so existing call sites don't need to change shape, just the
    /// method they call.
    /// </summary>
    public static class AlertService
    {
        public static void ShowInfo(string message, string title = "Info") =>
            Show(title, message, AlertType.Info, isConfirm: false, "OK", null);

        public static void ShowSuccess(string message, string title = "Success") =>
            Show(title, message, AlertType.Success, isConfirm: false, "OK", null);

        public static void ShowWarning(string message, string title = "Warning") =>
            Show(title, message, AlertType.Warning, isConfirm: false, "OK", null);

        public static void ShowError(string message, string title = "Error") =>
            Show(title, message, AlertType.Error, isConfirm: false, "OK", null);

        /// <summary>
        /// Blocking Yes/No confirmation. Returns true only if the user chose
        /// the affirmative (primary) button.
        /// </summary>
        public static bool Confirm(
            string message,
            string title = "Confirm",
            AlertType type = AlertType.Warning,
            string yesText = "Yes",
            string noText = "No")
        {
            return Show(title, message, type, isConfirm: true, yesText, noText) == DialogResult.Yes;
        }

        private static DialogResult Show(
            string title, string message, AlertType type,
            bool isConfirm, string primaryText, string secondaryText)
        {
            using var dialog = new AlertDialog(title, message, type, isConfirm, primaryText, secondaryText);
            return dialog.ShowDialog();
        }
    }
}
