using System.Drawing.Drawing2D;
using SpeedTestZx.Model;

namespace SpeedTestZx.UserControls.Lib.Charts
{
    /// <summary>
    /// "Latency &amp; loss, last 24 h" (T57): per-minute median ping as a line,
    /// minutes with loss as red ticks along the bottom, outages as shaded
    /// red bands, and time with no data (PC off, app closed) as grey
    /// "not recorded" bands — gaps are never drawn as a flat line.
    /// </summary>
    public sealed class LatencyTimelineChart : ChartControl
    {
        private static readonly TimeSpan GapAfter = TimeSpan.FromMinutes(3);

        private List<ProbeMinute> _minutes = new();
        private List<Outage> _outages = new();
        private DateTime _from, _to;

        public void SetData(List<ProbeMinute> minutes, List<Outage> outages, DateTime from, DateTime to)
        {
            _minutes = minutes;
            _outages = outages;
            _from = from;
            _to = to;
            Invalidate();
        }

        protected override void Draw(Graphics g, Rectangle bounds)
        {
            if (_minutes.Count == 0) { DrawEmpty(g, bounds); return; }

            using var font = AxisFont();
            int left = Px(g, 40), right = Px(g, 8), top = Px(g, 8), bottom = Px(g, 20), lh = Px(g, 16);
            var plot = new Rectangle(bounds.X + left, bounds.Y + top,
                Math.Max(1, bounds.Width - left - right), Math.Max(1, bounds.Height - top - bottom));

            var pings = _minutes.Where(m => m.MedianMs != null).Select(m => m.MedianMs!.Value).OrderBy(v => v).ToList();
            double p95 = pings.Count > 0 ? pings[(int)((pings.Count - 1) * 0.95)] : 50;
            double max = PlanTrendChart.NiceCeiling(Math.Max(20, p95 * 1.2));

            DrawBands(g, plot);

            using (var grid = new Pen(Palette.Grid))
            {
                for (int i = 0; i <= 2; i++)
                {
                    double v = max * i / 2;
                    int y = plot.Bottom - (int)(v / max * plot.Height);
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    DrawText(g, $"{v:0} ms", font, new Rectangle(bounds.X, y - lh / 2, left - 4, lh), StringAlignment.Far);
                }
            }

            for (int i = 0; i <= 4; i++)
            {
                var t = _from + (_to - _from) * i / 4;
                int x = X(t, plot), w = Px(g, 48);
                // Ends aligned inward so the first / last label is never clipped.
                var (labelX, align) = i switch
                {
                    0 => (x - Px(g, 4), StringAlignment.Near),
                    4 => (x - w + Px(g, 4), StringAlignment.Far),
                    _ => (x - w / 2, StringAlignment.Center),
                };
                DrawText(g, t.ToString("HH:mm"), font, new Rectangle(labelX, plot.Bottom + 2, w, lh), align, StringAlignment.Near);
            }

            DrawLine(g, plot, max);
            DrawLossTicks(g, plot);
        }

        /// <summary>Grey for not-recorded gaps, red for outages.</summary>
        private void DrawBands(Graphics g, Rectangle plot)
        {
            using var gap = new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(60, Palette.Text), Color.Transparent);
            var prev = _from;
            foreach (var m in _minutes.Append(new ProbeMinute { Time = _to }))
            {
                if (m.Time - prev > GapAfter)
                    g.FillRectangle(gap, Rect(prev.AddMinutes(1), m.Time, plot));
                prev = m.Time;
            }

            using var outage = new SolidBrush(Color.FromArgb(60, Palette.Bad));
            foreach (var o in _outages)
                g.FillRectangle(outage, Rect(o.Start, o.End ?? DateTime.Now, plot));
        }

        private void DrawLine(Graphics g, Rectangle plot, double max)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Palette.Line, Math.Max(1, Px(g, 1)) * 1.5f);

            var run = new List<PointF>();
            DateTime? prev = null;
            foreach (var m in _minutes)
            {
                bool broken = m.MedianMs == null || (prev is DateTime p && m.Time - p > GapAfter);
                if (broken) Flush(g, pen, run);
                if (m.MedianMs is double ms)
                    run.Add(new PointF(X(m.Time, plot), plot.Bottom - (float)(Math.Min(ms, max) / max * plot.Height)));
                prev = m.Time;
            }
            Flush(g, pen, run);
            g.SmoothingMode = SmoothingMode.Default;
        }

        private static void Flush(Graphics g, Pen pen, List<PointF> run)
        {
            if (run.Count > 1) g.DrawLines(pen, run.ToArray());
            else if (run.Count == 1) g.DrawEllipse(pen, run[0].X - 1, run[0].Y - 1, 2, 2);
            run.Clear();
        }

        private void DrawLossTicks(Graphics g, Rectangle plot)
        {
            using var pen = new Pen(Palette.Bad, Math.Max(1, Px(g, 2)));
            int tick = Px(g, 6);
            foreach (var m in _minutes.Where(m => m.LossPct > 0))
            {
                int x = X(m.Time, plot);
                int h = Math.Max(2, (int)(tick * Math.Min(1, m.LossPct / 50)));
                g.DrawLine(pen, x, plot.Bottom, x, plot.Bottom - h);
            }
        }

        private int X(DateTime t, Rectangle plot)
        {
            double span = Math.Max(1, (_to - _from).TotalSeconds);
            return plot.Left + (int)Math.Round(Math.Clamp((t - _from).TotalSeconds / span, 0, 1) * plot.Width);
        }

        private RectangleF Rect(DateTime start, DateTime end, Rectangle plot)
        {
            int x1 = X(start, plot), x2 = X(end, plot);
            return new RectangleF(x1, plot.Top, Math.Max(1, x2 - x1), plot.Height);
        }
    }
}
