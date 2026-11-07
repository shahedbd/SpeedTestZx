using SpeedTestZx.Managers;
using DpiCommon.Helper;
using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace SpeedTestZx.UserControls.Lib
{
    /// <summary>
    /// Modern iOS/Android-style on/off switch — a themed, custom-drawn
    /// replacement for CheckBox on settings rows. Drop-in-familiar API
    /// (Checked / CheckedChanged) so it slots into any boolean setting.
    /// </summary>
    public class ToggleSwitchControl : Control
    {
        private bool _checked;
        public event EventHandler CheckedChanged;

        // Built in code only (Settings sections), never
        // designer-placed, so neither belongs in a .Designer.cs (WFO1000).
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Track color when on. Defaults to the current theme's accent color.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color OnColor { get; set; } = Color.Empty;

        public ToggleSwitchControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            // DPI-scaled like every other Settings control — a hardcoded
            // 42x22 renders at half the intended relative size on a 200%
            // display, where the rows around it scale but this didn't.
            Size = new Size(DpiAwareService.Scale(42), DpiAwareService.Scale(22));
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Space or Keys.Enter)
                Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var trackRect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color trackColor = _checked
                ? (OnColor != Color.Empty ? OnColor : ThemeManager.Instance.CurrentTheme.AccentColor)
                : ThemeManager.Instance.CurrentTheme.BorderColor;

            using (var path = RoundedRect(trackRect, Height / 2))
            using (var brush = new SolidBrush(trackColor))
                g.FillPath(brush, path);

            // Insets derived from the (already scaled) Height so the thumb
            // keeps its proportions at any DPI instead of hugging the track
            // edge as the control grows.
            int inset = Math.Max(2, DpiAwareService.Scale(2));
            int thumbDiameter = Height - inset * 2;
            int thumbTravel = Math.Max(inset, DpiAwareService.Scale(3));
            int thumbX = _checked ? Width - thumbDiameter - thumbTravel : thumbTravel;
            var thumbRect = new Rectangle(thumbX, inset, thumbDiameter, thumbDiameter);
            using (var thumbBrush = new SolidBrush(Color.White))
                g.FillEllipse(thumbBrush, thumbRect);

            if (Focused)
            {
                using var focusPen = new Pen(Color.FromArgb(140, trackColor), 1.5f) { DashStyle = DashStyle.Dot };
                g.DrawPath(focusPen, RoundedRect(Rectangle.Inflate(trackRect, -1, -1), Height / 2));
            }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
