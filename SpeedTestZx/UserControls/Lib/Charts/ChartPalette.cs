using SpeedTestZx.Managers;

namespace SpeedTestZx.UserControls.Lib.Charts
{
    /// <summary>
    /// Colours the Report Card / Monitor charts draw with. The same chart code
    /// renders on screen (theme colours) and into the PDF (<see cref="Print"/>,
    /// always light), so charts take a palette instead of reading the theme.
    /// </summary>
    public sealed record ChartPalette(
        Color Background, Color Grid, Color Text, Color Line, Color Good, Color Warn, Color Bad, Color Empty)
    {
        public static ChartPalette FromTheme()
        {
            var t = ThemeManager.Instance.CurrentTheme;
            return new ChartPalette(t.CardBackground, t.GraphGridColor, t.SecondaryTextColor,
                Color.FromArgb(59, 130, 246), Color.FromArgb(16, 185, 129),
                Color.FromArgb(245, 158, 11), Color.FromArgb(239, 68, 68),
                ThemeManager.Instance.IsDarkTheme ? Color.FromArgb(48, 48, 52) : Color.FromArgb(232, 234, 238));
        }

        /// <summary>White background, for the PDF.</summary>
        public static readonly ChartPalette Print = new(Color.White, Color.FromArgb(220, 224, 230),
            Color.FromArgb(90, 96, 110), Color.FromArgb(37, 99, 235), Color.FromArgb(22, 163, 74),
            Color.FromArgb(217, 119, 6), Color.FromArgb(220, 38, 38), Color.FromArgb(236, 238, 242));

        /// <summary>Green ≥ 80% of plan, amber ≥ 50%, red below.</summary>
        public Color ForPct(double pct) => pct >= 80 ? Good : pct >= 50 ? Warn : Bad;
    }

    /// <summary>Base for the static-render charts: double-buffered, repaints on resize.</summary>
    public abstract class ChartControl : Control
    {
        protected ChartControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public ChartPalette Palette { get; set; } = ChartPalette.FromTheme();

        /// <summary>Shown centred when there is nothing to draw.</summary>
        public string EmptyText { get; set; } = "Not enough data yet";

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Palette.Background);
            Draw(e.Graphics, ClientRectangle);
        }

        protected abstract void Draw(Graphics g, Rectangle bounds);

        /// <summary>
        /// Renders the chart to a bitmap (PDF export). A higher
        /// <paramref name="dpi"/> scales text and margins with it, so a
        /// 2× bitmap looks like the on-screen chart, only sharper.
        /// </summary>
        public Bitmap ToBitmap(int width, int height, float dpi = 96)
        {
            var bmp = new Bitmap(width, height);
            bmp.SetResolution(dpi, dpi);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Palette.Background);
            Draw(g, new Rectangle(0, 0, width, height));
            return bmp;
        }

        protected void DrawEmpty(Graphics g, Rectangle bounds)
        {
            using var font = new Font("Segoe UI", 9f);
            DrawText(g, EmptyText, font, bounds, StringAlignment.Center, StringAlignment.Center);
        }

        /// <summary>
        /// GDI+ text (not TextRenderer), so it follows the Graphics' DPI and
        /// renders the same on screen and into a high-DPI bitmap.
        /// </summary>
        protected void DrawText(Graphics g, string text, Font font, Rectangle rect,
            StringAlignment horizontal, StringAlignment vertical = StringAlignment.Center)
        {
            using var brush = new SolidBrush(Palette.Text);
            using var format = new StringFormat
            {
                Alignment = horizontal,
                LineAlignment = vertical,
                Trimming = StringTrimming.EllipsisCharacter,
            };
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.DrawString(text, font, brush, rect, format);
        }

        protected static Font AxisFont() => new("Segoe UI", 7.5f);

        /// <summary>Scales a 96-DPI pixel value to the surface being drawn on.</summary>
        protected static int Px(Graphics g, int value) => (int)Math.Round(value * g.DpiX / 96f);
    }
}
