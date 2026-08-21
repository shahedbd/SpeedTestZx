// UserControls/Layout/HeaderControl.cs
using TestNetSpeed.Forms;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using DpiCommon.Helper;
using FontAwesome.Sharp;

namespace TestNetSpeed.UserControls.Layout
{
    /// <summary>
    /// Part 02 — header bar. Left: PC name | Processor | RAM (via
    /// SystemInfoService). Right: Theme + Settings action buttons.
    /// Only spans the region to the right of the sidebar (see
    /// Doc/UI layout.md) — MainForm nests this inside the content-side panel,
    /// not directly under the title bar.
    /// </summary>
    public class HeaderControl : UserControl
    {
        // ── Public controls (MainForm reads/wires these) ───────────────────────
        public Label LblSystemSummary { get; private set; }
        public Button BtnSettings { get; private set; }
        public Button BtnTheme { get; private set; }

        private readonly ThemeManager _themeManager;
        private readonly ToolTip _toolTip;
        private Panel _bottomBorder;
        private Panel _leftAccent;
        private Panel _buttonBar;

        public HeaderControl()
        {
            _themeManager = ThemeManager.Instance;
            _toolTip = new ToolTip { AutoPopDelay = 3000, InitialDelay = 400, ReshowDelay = 200 };

            Dock = DockStyle.Top;
            Height = DpiAwareService.Scale(AppConfig.HeaderHeight);

            BuildLayout();
            ApplyTheme();
        }

        private void BuildLayout()
        {
            // Fixed-width right-side panel — its own Width/Height are known
            // immediately (not inherited from the not-yet-laid-out parent),
            // so button positioning inside it is deterministic at construction
            // time and never depends on resize-event timing.
            _buttonBar = new Panel
            {
                Dock = DockStyle.Right,
                Width = DpiAwareService.Scale(170),
                Height = Height // seed from HeaderControl's own Height (already
                                 // set) — Dock hasn't parented this yet, so its
                                 // own Height would otherwise still be the
                                 // Panel default, not the real header height.
            };

            BtnSettings = HeaderButtonService.CreateLabeledButton("Settings", IconChar.Gear, BtnSettings_Click);
            BtnTheme = HeaderButtonService.CreateIconOnlyButton(
                _themeManager.IsDarkTheme ? IconChar.Sun : IconChar.Moon,
                _themeManager.IsDarkTheme ? "Switch to Light Mode" : "Switch to Dark Mode",
                BtnTheme_Click,
                _toolTip);

            _buttonBar.Controls.Add(BtnSettings);
            _buttonBar.Controls.Add(BtnTheme);
            PositionButtons();

            LblSystemSummary = new Label
            {
                Text = "Loading system information...",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Padding = new Padding(DpiAwareService.Scale(16), 0, 0, 0)
            };

            _leftAccent = new Panel { Dock = DockStyle.Left, Width = DpiAwareService.Scale(3) };

            // Add order (first → last): Fill content first (innermost), then
            // Left accent, then Right button bar, then Bottom border — each
            // later addition claims its edge from the full remaining area
            // first. The Fill label and Right button bar never overlap in
            // bounds, so z-order can't hide either one behind the other.
            Controls.Add(LblSystemSummary);
            Controls.Add(_leftAccent);
            Controls.Add(_buttonBar);
            Controls.Add(_bottomBorder = new Panel { Dock = DockStyle.Bottom, Height = DpiAwareService.Scale(1) });
        }

        private void PositionButtons()
        {
            HeaderButtonService.PositionButtonsRightAligned(
                parentWidth: _buttonBar.Width,
                parentHeight: _buttonBar.Height,
                rightMargin: DpiAwareService.Scale(12),
                buttons: new Button[] { BtnTheme, BtnSettings });
        }

        private void BtnTheme_Click(object sender, EventArgs e)
        {
            _themeManager.ToggleTheme();
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            using var dlg = new SettingsDialog();
            dlg.ShowDialog(this.FindForm());
        }

        // ─────────────────────────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────────────────────────

        public void ApplyTheme()
        {
            BackColor = _themeManager.CurrentTheme.HeaderBackground;
            _bottomBorder.BackColor = _themeManager.CurrentTheme.BorderColor;
            _leftAccent.BackColor = HeaderButtonService.BrandColor;
            _buttonBar.BackColor = _themeManager.CurrentTheme.HeaderBackground;

            LblSystemSummary.BackColor = Color.Transparent;
            LblSystemSummary.ForeColor = _themeManager.CurrentTheme.TextColor;

            foreach (var btn in new[] { BtnSettings, BtnTheme })
                HeaderButtonService.ApplyBrandStyle(btn);

            UpdateThemeButtonIcon();
        }

        public void UpdateThemeButtonIcon()
        {
            if (BtnTheme is not IconButton iconBtn) return;

            bool isDark = _themeManager.IsDarkTheme;
            iconBtn.IconChar = isDark ? IconChar.Sun : IconChar.Moon;

            _toolTip.SetToolTip(BtnTheme, isDark ? "Switch to Light Mode" : "Switch to Dark Mode");
        }

        // ─────────────────────────────────────────────────────────────────────
        // DPI REFRESH
        // ─────────────────────────────────────────────────────────────────────

        public void RefreshForDpi()
        {
            Height = DpiAwareService.Scale(AppConfig.HeaderHeight);
            _buttonBar.Width = DpiAwareService.Scale(170);
            _buttonBar.Height = Height;

            int btnH = HeaderButtonService.ButtonHeight;
            BtnSettings.Size = new Size(HeaderButtonService.MeasureButtonWidth("Settings"), btnH);
            BtnTheme.Size = new Size(HeaderButtonService.IconOnlyWidth, btnH);

            PositionButtons();
        }
    }
}
