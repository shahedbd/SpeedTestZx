// UserControls/Layout/TitleBarControl.cs
using System.Runtime.InteropServices;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using DpiCommon.Helper;

namespace TestNetSpeed.UserControls.Layout
{
    /// <summary>
    /// Part 01 — custom-drawn title bar. Shows AppConfig.AppName + AppSubtitle
    /// on the left and Minimize/Close buttons on the right. No maximize button —
    /// fixed-layout template design.
    /// </summary>
    public class TitleBarControl : UserControl
    {
        // Segoe MDL2 Assets glyphs
        private const string GlyphMinimize = "\uE921";
        private const string GlyphClose = "\uE8BB";

        private readonly ThemeManager _themeManager;
        private Label _lblTitle;
        private Panel _captionButtonBar;
        private Button _btnMinimize;
        private Button _btnClose;

        public TitleBarControl()
        {
            _themeManager = ThemeManager.Instance;

            Dock = DockStyle.Top;
            Height = DpiAwareService.Scale(AppConfig.TitleBarHeight);
            DoubleBuffered = true;

            BuildLayout();
            ApplyTheme();
        }

        private void BuildLayout()
        {
            // Fixed-width right-side panel, hosting the caption buttons.
            _captionButtonBar = new Panel
            {
                Dock = DockStyle.Right,
                Width = DpiAwareService.Scale(96)
            };

            _btnMinimize = CreateCaptionButton(GlyphMinimize);
            _btnMinimize.Click += (s, e) =>
            {
                if (FindForm() is Form f) f.WindowState = FormWindowState.Minimized;
            };

            _btnClose = CreateCaptionButton(GlyphClose);
            _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            _btnClose.MouseEnter += (s, e) => _btnClose.ForeColor = Color.White;
            _btnClose.MouseLeave += (s, e) => _btnClose.ForeColor = _themeManager.CurrentTheme.TextColor;
            _btnClose.Click += (s, e) => FindForm()?.Close();

            // Dock=Right on each button (add order: Minimize first, Close
            // last → Close ends up outermost, flush against the window's
            // right edge). Dock also stretches each button to the panel's
            // full height automatically — so the hover highlight spans the
            // entire title bar top-to-bottom, matching native Windows
            // caption buttons, instead of a fixed height sitting at a
            // separately-computed vertical offset that could drift out of
            // sync with it.
            _captionButtonBar.Controls.Add(_btnMinimize);
            _captionButtonBar.Controls.Add(_btnClose);

            _lblTitle = new Label
            {
                Text = $"{AppConfig.AppName} — {AppConfig.AppSubtitle}",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(DpiAwareService.Scale(12), 0, 0, 0),
                BackColor = Color.Transparent
            };
            _lblTitle.MouseDown += TitleBar_MouseDown;
            MouseDown += TitleBar_MouseDown;

            // Add order (first → last): Fill title first (innermost), then
            // Right caption-button bar — they never overlap in bounds, so
            // z-order can't hide either one behind the other.
            Controls.Add(_lblTitle);
            Controls.Add(_captionButtonBar);
        }

        private Button CreateCaptionButton(string glyph)
        {
            var btn = new Button
            {
                Text = glyph,
                Dock = DockStyle.Right,
                Width = DpiAwareService.Scale(45),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe MDL2 Assets", 10),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // ─────────────────────────────────────────────────────────────────────
        // WINDOW DRAG (standard borderless-form drag pattern)
        // ─────────────────────────────────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (FindForm() is not Form form) return;

            ReleaseCapture();
            SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        // ─────────────────────────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────────────────────────

        public void ApplyTheme()
        {
            BackColor = _themeManager.CurrentTheme.TitleBarBackground;
            _captionButtonBar.BackColor = _themeManager.CurrentTheme.TitleBarBackground;

            if (_lblTitle != null)
            {
                _lblTitle.BackColor = Color.Transparent;
                // Title text always reads against TitleBarBackground, which
                // is a fixed medium gray in both themes — HeadingColor (pure
                // white/black) keeps solid contrast in either case.
                _lblTitle.ForeColor = _themeManager.CurrentTheme.HeadingColor;
            }

            foreach (var btn in new[] { _btnMinimize, _btnClose })
            {
                if (btn == null) continue;
                btn.ForeColor = _themeManager.CurrentTheme.HeadingColor;
            }

            if (_btnMinimize != null)
                _btnMinimize.FlatAppearance.MouseOverBackColor = GetHoverColor(_themeManager.CurrentTheme.TitleBarBackground);
        }

        /// <summary>
        /// Nudges a color toward white or black (whichever contrasts more)
        /// for a hover state. TitleBarBackground comes from
        /// AppConfig.TitleBarColor and can be set to anything, so this can't
        /// assume it's a dark or light shade the way a fixed literal could.
        /// </summary>
        private static Color GetHoverColor(Color baseColor)
        {
            int brightness = (baseColor.R + baseColor.G + baseColor.B) / 3;
            int delta = brightness < 128 ? 30 : -30;

            return Color.FromArgb(
                Math.Clamp(baseColor.R + delta, 0, 255),
                Math.Clamp(baseColor.G + delta, 0, 255),
                Math.Clamp(baseColor.B + delta, 0, 255));
        }

        // ─────────────────────────────────────────────────────────────────────
        // DPI REFRESH
        // ─────────────────────────────────────────────────────────────────────

        public void RefreshForDpi()
        {
            Height = DpiAwareService.Scale(AppConfig.TitleBarHeight);
            _captionButtonBar.Width = DpiAwareService.Scale(96);

            // Height is handled automatically by Dock=Right; only Width needs
            // updating here.
            foreach (var btn in new[] { _btnMinimize, _btnClose })
                btn.Width = DpiAwareService.Scale(45);
        }
    }
}
