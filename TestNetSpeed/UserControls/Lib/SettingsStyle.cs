using SpeedTestZx.Managers;
using DpiCommon.Helper;

namespace SpeedTestZx.UserControls.Lib
{
    /// <summary>
    /// Shared styling helpers for the Settings page (ported from Net Speed
    /// Meter Plus): DPI-scaled dimensions and theme-aware flat inputs, so
    /// stock WinForms light-mode chrome doesn't leak into the dark theme.
    /// </summary>
    public static class SettingsStyle
    {
        public static int Scale(int value) => (int)Math.Round(value * DpiAwareService.ScaleFactor);

        private static AppTheme Theme => ThemeManager.Instance.CurrentTheme;

        /// <summary>
        /// Themes a row input to match the page's card look. Unknown control
        /// types (self-painting ones like ToggleSwitchControl) are left alone.
        /// </summary>
        public static void ThemeInput(Control input)
        {
            if (input is Button button)
                ThemeGhostButton(button);
        }

        /// <summary>Bordered card-colored flat button (secondary action).</summary>
        public static void ThemeGhostButton(Button button)
        {
            var theme = Theme;
            bool dark = ThemeManager.Instance.IsDarkTheme;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = dark ? Color.FromArgb(45, 45, 45) : Color.FromArgb(240, 241, 243);
            button.ForeColor = theme.TextColor;
            button.FlatAppearance.BorderColor = theme.BorderColor;
            button.FlatAppearance.MouseOverBackColor = theme.HoverColor;
        }
    }
}
