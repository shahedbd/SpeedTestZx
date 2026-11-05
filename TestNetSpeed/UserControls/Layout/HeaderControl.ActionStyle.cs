using FontAwesome.Sharp;
using TestNetSpeed.Helper;

namespace TestNetSpeed.UserControls.Layout
{
    /// <summary>
    /// HeaderControl — colours of the action buttons on the right. Same
    /// pattern as Net Speed Meter Plus's header.
    ///
    /// Settings, About and Theme sit on a neutral surface (header-toned fill,
    /// thin border) so each icon can carry its own fixed colour. The colours
    /// are mid-saturation so they read on both themes.
    /// </summary>
    public partial class HeaderControl
    {
        private static readonly Color SettingsIconColor = Color.FromArgb(139, 92, 246); // purple
        private static readonly Color AboutIconColor = Color.FromArgb(33, 150, 243);    // blue
        private static readonly Color SunColor = Color.FromArgb(251, 191, 36);          // amber
        private static readonly Color MoonColor = Color.FromArgb(99, 102, 241);         // indigo

        // Theme toggle glyphs — Segoe MDL2 Assets (Brightness / QuietHours),
        // drawn as button text rather than a Font Awesome image.
        private const string SunGlyph = "";
        private const string MoonGlyph = "";
        private static readonly Font ThemeGlyphFont = new("Segoe MDL2 Assets", 12f, FontStyle.Regular);

        /// <summary>Neutral surface for an action button; the icon keeps <paramref name="iconColor"/>.</summary>
        private void StyleActionButton(Button btn, Color iconColor)
        {
            var theme = _themeManager.CurrentTheme;

            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = theme.HeaderBackground;
            btn.ForeColor = theme.TextColor;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = theme.BorderColor;
            btn.FlatAppearance.MouseOverBackColor = theme.HoverColor;
            btn.FlatAppearance.MouseDownBackColor = theme.SelectedColor;

            if (btn is IconButton icon)
                icon.IconColor = iconColor;
        }

        private void StyleActionButtons()
        {
            StyleActionButton(BtnSettings, SettingsIconColor);
            StyleActionButton(BtnAbout, AboutIconColor);
            StyleActionButton(BtnTheme, _themeManager.IsDarkTheme ? SunColor : MoonColor);
            UpdateThemeButtonIcon();
        }

        /// <summary>
        /// Sun in dark mode (switch to light), moon in light mode — each in its
        /// own colour.
        /// </summary>
        public void UpdateThemeButtonIcon()
        {
            bool isDark = _themeManager.IsDarkTheme;

            if (BtnTheme is IconButton iconBtn)
                iconBtn.IconChar = IconChar.None;

            BtnTheme.Font = ThemeGlyphFont;
            BtnTheme.Text = isDark ? SunGlyph : MoonGlyph;
            BtnTheme.ForeColor = isDark ? SunColor : MoonColor;
            BtnTheme.TextAlign = ContentAlignment.MiddleCenter;
            BtnTheme.Padding = Padding.Empty;

            _toolTip.SetToolTip(BtnTheme, isDark ? "Switch to Light Mode" : "Switch to Dark Mode");
        }
    }
}
