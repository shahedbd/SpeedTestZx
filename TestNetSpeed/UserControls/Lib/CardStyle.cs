using System.Drawing.Drawing2D;

namespace TestNetSpeed.UserControls.Lib
{
    /// <summary>
    /// The rounded-rectangle card surface (ported from PC Health Optimizer's
    /// Lib/CardStyle). Colors are read from the theme at paint time; only the
    /// geometry is here.
    /// </summary>
    internal static class CardStyle
    {
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
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

        /// <summary>
        /// Paints <paramref name="surface"/> as a themed card. Wire straight to
        /// the panel's Paint event; the panel keeps BackColor for the 1px frame
        /// the rounded corners leave uncovered.
        /// </summary>
        public static void PaintCard(Control surface, PaintEventArgs e, int radius)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var theme = Managers.ThemeManager.Instance.CurrentTheme;

            using var path = RoundedRect(
                new Rectangle(1, 1, surface.Width - 2, surface.Height - 2), radius);
            using var bg = new SolidBrush(theme.CardBackground);
            using var pen = new Pen(theme.BorderColor, 1f);

            e.Graphics.FillPath(bg, path);
            e.Graphics.DrawPath(pen, path);
        }
    }
}
