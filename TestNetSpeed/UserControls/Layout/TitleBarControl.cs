// UserControls/Layout/TitleBarControl.cs
using System.Runtime.InteropServices;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using DpiCommon.Helper;

namespace SpeedTestZx.UserControls.Layout
{
    /// <summary>
    /// Part 01 — custom-drawn title bar. Shows AppConfig.AppName + AppSubtitle
    /// on the left and Minimize/Close buttons on the right. No maximize button —
    /// fixed-layout template design.
    /// </summary>
    public partial class TitleBarControl : UserControl
    {
        // Segoe MDL2 Assets glyphs
        private const string GlyphMinimize = "\uE921";
        private const string GlyphClose = "\uE8BB";

        /// <summary>
        /// The bar is a horizontal gradient derived from the app icon's base
        /// colour (AppConfig.PrimaryColor), so re-branding the app only needs
        /// that one colour changed. ApplyTheme sets the stops per theme.
        ///
        /// Every child is BackColor.Transparent so the gradient shows through:
        /// WinForms renders a transparent child by invoking the PARENT's
        /// OnPaint, not merely copying its BackColor, which is what makes that
        /// work. BackColor stays a flat fallback for the unpainted slivers
        /// during a resize.
        /// </summary>
        private Color _gradientStart = AppConfig.PrimaryColor;
        private Color _gradientEnd = AppConfig.PrimaryColor;

        /// <summary>Caption text/glyph colour, measured against the gradient — see ApplyTheme.</summary>
        private Color _captionForeColor = Color.White;

        private readonly ThemeManager _themeManager;
        private Panel _iconPanel;
        private PictureBox _pbAppIcon;
        private Icon _appIcon;
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // The brush spans ClientRectangle, so the gradient must be redrawn
            // whenever that changes.
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // A minimized window lays its children out at zero width, and a
            // restore from the taskbar paints once before the real size comes
            // back. LinearGradientBrush throws on an empty rectangle, so skip
            // that frame — the resize that follows invalidates and repaints.
            if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0)
            {
                base.OnPaint(e);
                return;
            }

            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                ClientRectangle, _gradientStart, _gradientEnd,
                System.Drawing.Drawing2D.LinearGradientMode.Horizontal);

            e.Graphics.FillRectangle(brush, ClientRectangle);

            base.OnPaint(e);
        }

        private void BuildLayout()
        {
            // ── Left: app icon ────────────────────────────────────────────────
            _pbAppIcon = new PictureBox
            {
                Image = LoadAppIcon(),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            _iconPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = DpiAwareService.Scale(40),
                BackColor = Color.Transparent
            };
            _iconPanel.Controls.Add(_pbAppIcon);

            // The icon is part of the caption, so dragging it must move the
            // window like the rest of the bar does.
            _iconPanel.MouseDown += TitleBar_MouseDown;
            _pbAppIcon.MouseDown += TitleBar_MouseDown;

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
            _btnClose.MouseLeave += (s, e) => _btnClose.ForeColor = _captionForeColor;
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
            Controls.Add(_iconPanel);

            LayoutAppIcon();
        }

        /// <summary>Centres the icon inside its left panel at the current DPI.</summary>
        private void LayoutAppIcon()
        {
            if (_pbAppIcon == null || _iconPanel == null) return;

            int size = DpiAwareService.Scale(22);

            _pbAppIcon.Size = new Size(size, size);
            _pbAppIcon.Location = new Point(
                (_iconPanel.Width - size) / 2,
                (Height - size) / 2);
        }

        private Image LoadAppIcon()
        {
            try
            {
                if (!File.Exists(AppConfig.FaviconPath)) return null;

                // Kept in a field so Dispose can release it: Icon.ToBitmap()
                // returns a NEW bitmap, and the Icon itself stays a live GDI
                // handle that the PictureBox never owns.
                _appIcon = new Icon(AppConfig.FaviconPath);
                return _appIcon.ToBitmap();
            }
            catch
            {
                // Decorative only — a missing or unreadable icon must never
                // stop the window opening.
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pbAppIcon?.Image?.Dispose();
                _appIcon?.Dispose();
            }

            base.Dispose(disposing);
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

    }
}
