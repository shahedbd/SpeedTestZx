using System.ComponentModel;
using System.Drawing.Drawing2D;
using TestNetSpeed.Managers;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.Pages.Settings
{
    /// <summary>
    /// A clickable mini-window preview for one app theme: sidebar + content
    /// strip in that theme's real colors, a radio dot and a caption below.
    /// Ported from PC Health Optimizer, Dark/Light only (ThemeManager is a
    /// bool here — no "Use Windows" mode).
    /// </summary>
    internal sealed class ThemePreviewCard : Control
    {
        public bool IsDark { get; }
        private readonly string _caption;
        private bool _selected;
        private bool _hover;

        private static int S(int value) => SettingsStyle.Scale(value);

        public ThemePreviewCard(bool isDark, string caption)
        {
            IsDark = isDark;
            _caption = caption;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            Size = new Size(S(136), S(110));
            Margin = new Padding(0, 0, S(12), 0);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.RadioButton;
            AccessibleName = caption;
            BackColor = ThemeManager.Instance.CurrentTheme.CardBackground;
        }

        // Built in code only, never designer-placed (WFO1000).
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => _selected;
            set { _selected = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Space or Keys.Enter) OnClick(EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var page = ThemeManager.Instance.CurrentTheme;

            var preview = new Rectangle(1, 1, Width - 3, S(70));
            using (var path = CardStyle.RoundedRect(preview, S(6)))
            {
                g.SetClip(path);
                PaintMiniWindow(g, preview, ThemeManager.Instance.GetTheme(IsDark), IsDark);
                g.ResetClip();

                Color border = _selected ? page.AccentColor : _hover ? page.SecondaryTextColor : page.BorderColor;
                using var pen = new Pen(border, _selected ? S(2) : 1);
                g.DrawPath(pen, path);
            }

            // Radio dot + caption.
            int dot = S(14);
            var dotRect = new Rectangle(S(2), preview.Bottom + S(12), dot, dot);
            using (var ring = new Pen(_selected ? page.AccentColor : page.SecondaryTextColor, S(2) / 1.5f))
                g.DrawEllipse(ring, dotRect);
            if (_selected)
            {
                using var fill = new SolidBrush(page.AccentColor);
                g.FillEllipse(fill, Rectangle.Inflate(dotRect, -S(4), -S(4)));
            }

            TextRenderer.DrawText(g, _caption, Font, new Point(dotRect.Right + S(6), dotRect.Y - S(2)), page.TextColor);

            if (Focused)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(0, 0, Width - 1, Height - 1));
        }

        /// <summary>Sidebar strip, a heading bar and two card blocks — enough to read as "the app".</summary>
        private static void PaintMiniWindow(Graphics g, Rectangle r, AppTheme t, bool isDark)
        {
            using var content = new SolidBrush(t.ContentBackground);
            using var sidebar = new SolidBrush(t.SidebarBackground);
            // Light cards are white on white — borrow the border tone so they read.
            using var card = new SolidBrush(isDark ? t.CardBackground : t.BorderColor);
            using var text = new SolidBrush(t.SecondaryTextColor);
            using var accent = new SolidBrush(t.AccentColor);

            g.FillRectangle(content, r);
            var side = new Rectangle(r.X, r.Y, r.Width / 4, r.Height);
            g.FillRectangle(sidebar, side);
            g.FillRectangle(accent, side.X + S(4), side.Y + S(8), side.Width - S(8), S(4));

            int x = side.Right + S(8), w = r.Right - x - S(8);
            g.FillRectangle(text, x, r.Y + S(8), w / 2, S(4));
            g.FillRectangle(card, x, r.Y + S(20), w, S(18));
            g.FillRectangle(card, x, r.Y + S(44), w, S(18));
        }
    }
}
