using System.Drawing.Drawing2D;
using DpiCommon.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;

namespace TestNetSpeed.UserControls.Lib
{
    /// <summary>
    /// Small owner-drawn bar chart for the Dashboard's Recent tests card
    /// (T47): one ↓ / ↑ bar pair per test, oldest on the left, on a shared
    /// Mbps scale. Clicking anywhere raises the normal Click event (the
    /// Dashboard opens Test History). Fewer than two tests shows a hint
    /// instead, since one bar isn't a trend.
    /// </summary>
    public class RecentTestsChart : Control
    {
        private static readonly Color DownColor = Color.FromArgb(59, 130, 246);
        private static readonly Color UpColor = Color.FromArgb(16, 185, 129);

        private readonly ThemeManager _theme = ThemeManager.Instance;
        private List<SpeedTestResult> _tests = new();

        public RecentTestsChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        /// <summary>Tests to draw, oldest first (see HistoryStats.Recent).</summary>
        public void SetTests(IEnumerable<SpeedTestResult> oldestFirst)
        {
            _tests = oldestFirst.ToList();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int s(int v) => DpiAwareService.Scale(v);
            bool dark = _theme.IsDarkTheme;
            Color gridColor = dark ? Color.FromArgb(48, 48, 60) : Color.FromArgb(226, 229, 236);
            Color axisText = dark ? Color.FromArgb(140, 140, 160) : Color.FromArgb(110, 114, 128);

            using var axisFont = new Font("Segoe UI", 7f);
            using var axisBrush = new SolidBrush(axisText);

            var plot = new Rectangle(s(34), s(6), Width - s(34) - s(6), Height - s(6) - s(18));
            if (plot.Width < s(40) || plot.Height < s(30)) return;

            if (_tests.Count < 2)
            {
                const string hint = "Run more tests to see a trend";
                var hs = g.MeasureString(hint, axisFont);
                g.DrawString(hint, axisFont, axisBrush,
                    (Width - hs.Width) / 2, (Height - hs.Height) / 2);
                return;
            }

            double max = NiceCeiling(_tests.Max(t => Math.Max(t.DownloadMbps, t.UploadMbps)));

            // ── Grid + scale ────────────────────────────────────────────────
            using (var gridPen = new Pen(gridColor) { DashStyle = DashStyle.Dot })
            {
                for (int i = 0; i <= 2; i++)
                {
                    float y = plot.Top + plot.Height * i / 2f;
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);

                    string label = $"{max * (2 - i) / 2:0.#}";
                    var ls = g.MeasureString(label, axisFont);
                    g.DrawString(label, axisFont, axisBrush, plot.Left - ls.Width - s(2), y - ls.Height / 2);
                }
            }

            // ── Bars ────────────────────────────────────────────────────────
            float slot = plot.Width / (float)_tests.Count;
            float barW = Math.Max(2f, Math.Min(s(14), slot * 0.32f));
            float pairGap = Math.Max(1f, barW * 0.2f);

            using var downBrush = new SolidBrush(DownColor);
            using var upBrush = new SolidBrush(UpColor);

            // Show a date under every bar pair only when there's room.
            bool labelEvery = slot >= g.MeasureString("00 Mmm", axisFont).Width + s(4);

            for (int i = 0; i < _tests.Count; i++)
            {
                var t = _tests[i];
                float cx = plot.Left + slot * i + slot / 2;

                DrawBar(g, downBrush, cx - pairGap / 2 - barW, barW, t.DownloadMbps, max, plot);
                DrawBar(g, upBrush, cx + pairGap / 2, barW, t.UploadMbps, max, plot);

                bool edge = i == 0 || i == _tests.Count - 1;
                if (labelEvery || edge)
                {
                    string date = t.TestedAt.ToString("dd MMM");
                    var ds = g.MeasureString(date, axisFont);
                    float x = Math.Clamp(cx - ds.Width / 2, plot.Left, plot.Right - ds.Width);
                    g.DrawString(date, axisFont, axisBrush, x, plot.Bottom + s(2));
                }
            }
        }

        private static void DrawBar(Graphics g, Brush brush, float x, float w, double mbps, double max, Rectangle plot)
        {
            float h = (float)(Math.Max(0, mbps) / max * plot.Height);
            if (h < 1) h = 1;   // a zero result still shows as a sliver
            g.FillRectangle(brush, x, plot.Bottom - h, w, h);
        }

        /// <summary>Rounds up to 1 / 2 / 5 × 10ⁿ so the scale stays tidy.</summary>
        private static double NiceCeiling(double v)
        {
            if (v <= 0) return 1;

            double exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
            double f = v / exp;
            double nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
            return nice * exp;
        }
    }
}
