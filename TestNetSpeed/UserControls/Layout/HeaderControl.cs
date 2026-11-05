// UserControls/Layout/HeaderControl.cs
using TestNetSpeed.Forms;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using DpiCommon.Helper;
using FontAwesome.Sharp;

namespace TestNetSpeed.UserControls.Layout
{
    /// <summary>
    /// Part 02 — header bar, laid out like Net Speed Meter Plus's. Left: PC
    /// name, processor, RAM, OS and uptime as coloured icon + text segments
    /// (HeaderControl.Summary.cs; data loaded by <see cref="LoadSystemSummary"/>
    /// in HeaderControl.SystemSummary.cs). Right: Settings + About + Theme
    /// icon-only action buttons (colours in HeaderControl.ActionStyle.cs),
    /// plus the Go Pro pill (HeaderControl.Pro.cs).
    /// Only spans the region to the right of the sidebar — MainForm nests
    /// this inside the content-side panel, not directly under the title bar.
    /// </summary>
    public partial class HeaderControl : UserControl
    {
        // ── Public controls (MainForm reads/wires these) ───────────────────────
        public Button BtnSettings { get; private set; }
        public Button BtnTheme { get; private set; }
        public Button BtnAbout { get; private set; }

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

            Disposed += (s, e) => DisposeProButton();
        }

        private void BuildLayout()
        {
            // Right-side panel sized to its button group (PositionButtons) —
            // its own Width/Height are known immediately, so button positioning
            // is deterministic at construction time and never depends on
            // resize-event timing.
            _buttonBar = new Panel
            {
                Dock = DockStyle.Right,
                Height = Height // seed from HeaderControl's own Height — Dock
                                 // hasn't parented this yet.
            };

            BtnSettings = HeaderButtonService.CreateIconOnlyButton(
                IconChar.ScrewdriverWrench, "Settings", BtnSettings_Click, _toolTip);
            BtnAbout = HeaderButtonService.CreateIconOnlyButton(
                IconChar.CircleInfo, $"About {AppConfig.AppName}", BtnAbout_Click, _toolTip);
            BtnTheme = HeaderButtonService.CreateIconOnlyButton(
                _themeManager.IsDarkTheme ? IconChar.Sun : IconChar.Moon,
                _themeManager.IsDarkTheme ? "Switch to Light Mode" : "Switch to Dark Mode",
                BtnTheme_Click,
                _toolTip);

            _buttonBar.Controls.Add(BtnSettings);
            _buttonBar.Controls.Add(BtnAbout);
            _buttonBar.Controls.Add(BtnTheme);

            // Go Pro pill, left of Settings — HeaderControl.Pro.cs.
            CreateProButton();
            _buttonBar.Controls.Add(_btnGoPro);
            PositionButtons();

            _summaryStrip = BuildSummaryStrip();
            RenderSummary();

            _leftAccent = new Panel { Dock = DockStyle.Left, Width = DpiAwareService.Scale(3) };

            // Add order (first → last): Fill content first (innermost), then
            // Left accent, then Right button bar, then Bottom border — each
            // later addition claims its edge from the full remaining area
            // first. The Fill strip and Right button bar never overlap in
            // bounds, so z-order can't hide either one behind the other.
            Controls.Add(_summaryStrip);
            Controls.Add(_leftAccent);
            Controls.Add(_buttonBar);
            Controls.Add(_bottomBorder = new Panel { Dock = DockStyle.Bottom, Height = DpiAwareService.Scale(1) });
        }

        private void PositionButtons()
        {
            // Right-to-left: the last entry ends up left-most.
            var buttons = BarButtons;
            int rightMargin = DpiAwareService.Scale(12);

            _buttonBar.Width = HeaderButtonService.GroupWidth(buttons, rightMargin) + DpiAwareService.Scale(8);

            HeaderButtonService.PositionButtonsRightAligned(
                parentWidth: _buttonBar.Width,
                parentHeight: _buttonBar.Height,
                rightMargin: rightMargin,
                buttons: buttons);
        }

        private void BtnAbout_Click(object sender, EventArgs e)
        {
            using var about = new AboutForm();
            about.ShowDialog(FindForm());
        }

        private void BtnTheme_Click(object sender, EventArgs e)
        {
            _themeManager.ToggleTheme();
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            // Settings is a sidebar page (not a separate dialog) — jump to it
            // the same way clicking the sidebar's "Settings" entry would.
            NavigationManager.Instance.NavigateTo("Settings");
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

            // Segment labels take their colour at build time — rebuild them.
            RenderSummary();

            // Colourful icons on a neutral surface — HeaderControl.ActionStyle.cs.
            StyleActionButtons();
            StyleProButton();
        }

        // ─────────────────────────────────────────────────────────────────────
        // DPI REFRESH
        // ─────────────────────────────────────────────────────────────────────

        public void RefreshForDpi()
        {
            Height = DpiAwareService.Scale(AppConfig.HeaderHeight);
            _buttonBar.Height = Height;
            _summaryStrip.Padding = SummaryPadding();
            RenderSummary();

            int btnH = HeaderButtonService.ButtonHeight;
            BtnSettings.Size = new Size(HeaderButtonService.IconOnlyWidth, btnH);
            BtnAbout.Size = new Size(HeaderButtonService.IconOnlyWidth, btnH);
            BtnTheme.Size = new Size(HeaderButtonService.IconOnlyWidth, btnH);
            RefreshProButtonForDpi();

            PositionButtons();
        }
    }
}
