using System.Drawing.Drawing2D;

namespace SpeedTestZx.UserControls.Lib.Charts
{
    /// <summary>
    /// "% of plan over time" (T51): one point per day — the daily median
    /// download as % of plan — joined by a line, with a dashed 100% line.
    /// Without a plan it plots median Mbps and skips the 100% line.
    /// Points are coloured green / amber / red by % of plan.
    /// </summary>
    public sealed class PlanTrendChart : ChartControl
    {
        private List<(DateTime Day, double? Value)> _points = new();
        private DateTime _from, _to;
        private bool _isPct;

        public void SetData(List<(DateTime Day, double? Value)> points, DateTime from, DateTime to, bool isPct)
        {
            _points = points;
            _from = from.Date;
            _to = to.Date;
            _isPct = isPct;
            Invalidate();
        }

        protected override void Draw(Graphics g, Rectangle bounds)
        {
            var values = _points.Where(p => p.Value != null).ToList();
            if (values.Count == 0) { DrawEmpty(g, bounds); return; }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var font = AxisFont();

            int left = Px(g, 40), right = Px(g, 10), top = Px(g, 10), bottom = Px(g, 22), lh = Px(g, 16);
            var plot = new Rectangle(bounds.X + left, bounds.Y + top,
                Math.Max(1, bounds.Width - left - right), Math.Max(1, bounds.Height - top - bottom));

            double max = Math.Max(values.Max(p => p.Value!.Value), _isPct ? 110 : 1);
            max = NiceCeiling(max);

            // Grid + Y labels (0, ¼, ½, ¾, max).
            using (var grid = new Pen(Palette.Grid))
            {
                for (int i = 0; i <= 4; i++)
                {
                    double v = max * i / 4;
                    int y = YFor(v, max, plot);
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    string label = _isPct ? $"{v:0}%" : $"{v:0}";
                    DrawText(g, label, font, new Rectangle(bounds.X, y - lh / 2, left - 4, lh),
                        StringAlignment.Far);
                }
            }

            if (_isPct)
            {
                using var dash = new Pen(Palette.Good, 1.5f) { DashStyle = DashStyle.Dash };
                int y100 = YFor(100, max, plot);
                g.DrawLine(dash, plot.Left, y100, plot.Right, y100);
            }

            // X labels: first, middle and last day.
            double days = Math.Max(1, (_to - _from).TotalDays);
            // First label left-aligned, last right-aligned, so neither is clipped at the edge.
            int labelW = Px(g, 60);
            var labels = new[] { (_from, StringAlignment.Near), (_from.AddDays(Math.Round(days / 2)), StringAlignment.Center), (_to, StringAlignment.Far) };
            foreach (var (day, align) in labels)
            {
                int x = XFor(day, days, plot);
                int labelX = align switch
                {
                    StringAlignment.Near => x - Px(g, 4),
                    StringAlignment.Far => x - labelW + Px(g, 4),
                    _ => x - labelW / 2,
                };
                DrawText(g, day.ToString("MMM d"), font, new Rectangle(labelX, plot.Bottom + 4, labelW, lh), align, StringAlignment.Near);
            }

            var pts = values.Select(p => new PointF(XFor(p.Day, days, plot), YFor(p.Value!.Value, max, plot))).ToArray();
            if (pts.Length > 1)
            {
                using var line = new Pen(Palette.Line, 2f);
                g.DrawLines(line, pts);
            }

            for (int i = 0; i < pts.Length; i++)
            {
                double v = values[i].Value!.Value;
                using var dot = new SolidBrush(_isPct ? Palette.ForPct(v) : Palette.Line);
                g.FillEllipse(dot, pts[i].X - 3.5f, pts[i].Y - 3.5f, 7, 7);
            }
        }

        private int XFor(DateTime day, double days, Rectangle plot) =>
            plot.Left + (int)Math.Round((day.Date - _from).TotalDays / days * plot.Width);

        private static int YFor(double value, double max, Rectangle plot) =>
            plot.Bottom - (int)Math.Round(Math.Clamp(value / max, 0, 1) * plot.Height);

        internal static double NiceCeiling(double value)
        {
            if (value <= 0) return 1;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
            foreach (var step in new[] { 1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10 })
                if (step * magnitude >= value) return step * magnitude;
            return 10 * magnitude;
        }
    }
}
