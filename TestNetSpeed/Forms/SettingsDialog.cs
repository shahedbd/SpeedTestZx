using DeviceDataModule;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using DpiCommon.Helper;

namespace TestNetSpeed.Forms
{
    /// <summary>
    /// App settings dialog — Dark/Light mode and minimize-to-tray-on-close.
    /// Reads/writes through SettingsService.Current, same as ThemeManager
    /// and SidebarControl already do for these same two preferences.
    /// A "Clear Test History" data action belongs here too once the Test
    /// History tab exists (ported from WiFiPasswordViewer's Settings
    /// dialog, which had a "Clear WiFi Profile Cache" action in the same
    /// slot) — omitted for now since there's no history to clear yet.
    /// </summary>
    public class SettingsDialog : Form
    {
        private readonly ThemeManager _themeManager;

        // Tracked for ApplyTheme — avoids a control-tree walk.
        private Label _lblTitle;
        private CheckBox _chkDarkMode;
        private CheckBox _chkMinimizeToTray;
        private Button _btnClose;
        private readonly List<Panel> _dividers = new();
        private readonly List<Label> _sectionLabels = new();

        public SettingsDialog()
        {
            _themeManager = ThemeManager.Instance;

            InitializeComponent();
            BuildLayout();
            ApplyTheme();

            _themeManager.ThemeChanged += OnThemeChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        // INIT
        // ─────────────────────────────────────────────────────────────────────

        private void InitializeComponent()
        {
            SuspendLayout();

            ClientSize = DpiAwareService.ScaledSize(360, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = File.Exists(AppConfig.FaviconPath) ? new Icon(AppConfig.FaviconPath) : null;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Settings";
            AutoScaleMode = AutoScaleMode.Dpi;

            ResumeLayout(false);
        }

        private void BuildLayout()
        {
            int width = ClientSize.Width;
            int contentWidth = DpiAwareService.Scale(300);
            int contentX = (width - contentWidth) / 2;
            int y = DpiAwareService.Scale(20);

            _lblTitle = new Label
            {
                Text = "Settings",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            CenterHorizontally(_lblTitle, width, y);
            Controls.Add(_lblTitle);
            y += DpiAwareService.Scale(36);

            y = AddDivider(contentX, contentWidth, y);

            // ── Appearance ────────────────────────────────────────────────────
            y = AddSectionLabel("Appearance", contentX, y);

            _chkDarkMode = CreateCheckBox("Dark Mode", contentX, y, _themeManager.IsDarkTheme);
            _chkDarkMode.CheckedChanged += (s, e) => _themeManager.SetTheme(_chkDarkMode.Checked);
            Controls.Add(_chkDarkMode);
            y += DpiAwareService.Scale(30);

            y = AddDivider(contentX, contentWidth, y);

            // ── Behavior ──────────────────────────────────────────────────────
            y = AddSectionLabel("Behavior", contentX, y);

            _chkMinimizeToTray = CreateCheckBox(
                "Minimize to tray when closing",
                contentX, y,
                SettingsService.Current.MinimizeToTrayOnClose);
            _chkMinimizeToTray.Enabled = AppConfig.EnableSystemTray;
            _chkMinimizeToTray.CheckedChanged += (s, e) =>
            {
                SettingsService.Current.MinimizeToTrayOnClose = _chkMinimizeToTray.Checked;
                SettingsService.Save();
            };
            Controls.Add(_chkMinimizeToTray);
            y += DpiAwareService.Scale(30);

            y = AddDivider(contentX, contentWidth, y);

            // TODO(Test History tab): add a "Data" section here with a
            // "Clear Test History" action, same slot the WiFi build used
            // for "Clear WiFi Profile Cache".

            // ── Close button ──────────────────────────────────────────────────
            _btnClose = HeaderButtonService.CreateCloseButton((s, e) => Close(), DpiAwareService.Scale(90));
            _btnClose.Location = new Point((width - _btnClose.Width) / 2, y);
            Controls.Add(_btnClose);
            y += _btnClose.Height + DpiAwareService.Scale(20);

            ClientSize = new Size(width, y);
        }

        // ─────────────────────────────────────────────────────────────────────
        // FACTORY HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private int AddSectionLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text.ToUpperInvariant(),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Location = new Point(x, y),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _sectionLabels.Add(lbl);
            Controls.Add(lbl);
            return y + DpiAwareService.Scale(22);
        }

        private CheckBox CreateCheckBox(string text, int x, int y, bool isChecked)
        {
            return new CheckBox
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true,
                Checked = isChecked,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
        }

        private Button CreateActionButton(string text)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5f),
                Size = DpiAwareService.ScaledSize(100, 30),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private int AddDivider(int x, int w, int y)
        {
            var divider = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, DpiAwareService.Scale(1))
            };
            _dividers.Add(divider);
            Controls.Add(divider);
            return y + DpiAwareService.Scale(14);
        }

        private static void CenterHorizontally(Label label, int containerWidth, int y)
        {
            int textWidth = TextRenderer.MeasureText(label.Text, label.Font).Width;
            label.Location = new Point((containerWidth - textWidth) / 2, y);
        }

        // ─────────────────────────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyTheme()
        {
            var theme = _themeManager.CurrentTheme;

            BackColor = theme.ContentBackground;
            _lblTitle.ForeColor = theme.HeadingColor;

            foreach (var lbl in _sectionLabels)
                lbl.ForeColor = theme.SecondaryTextColor;

            _chkDarkMode.ForeColor = theme.TextColor;
            _chkMinimizeToTray.ForeColor = theme.TextColor;

            HeaderButtonService.ApplyBrandStyle(_btnClose);

            foreach (var divider in _dividers)
                divider.BackColor = theme.BorderColor;

            Invalidate(true);
        }

        // Keeps this dialog's own checkbox in sync if the theme is toggled
        // from elsewhere (e.g. the header button) while this is open. Safe
        // to just re-set Checked here — it re-fires CheckedChanged, but
        // ThemeManager.SetTheme already no-ops when the value hasn't
        // actually changed, so there's no feedback loop.
        private void OnThemeChanged(object sender, EventArgs e)
        {
            _chkDarkMode.Checked = _themeManager.IsDarkTheme;
            ApplyTheme();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _themeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
