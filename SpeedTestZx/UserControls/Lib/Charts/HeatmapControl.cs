using SpeedTestZx.Helper;

namespace SpeedTestZx.UserControls.Lib.Charts
{
    /// <summary>
    /// Weekday × hour heatmap (T55). Colour scale:
    ///   • speed, % of plan — red ≤ 50%, amber 75%, green ≥ 100%;
    ///   • speed, Mbps (no plan) — relative to the fastest cell;
    ///   • ping — green ≤ 30 ms to red ≥ 150 ms.
    /// Cells with too few samples (2 tests; 1 hour for ping) are drawn empty.
    /// Hovering a cell shows its value in a tooltip.
    /// </summary>
    public sealed class HeatmapControl : ChartControl
    {
        private static readonly string[] Days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        private HeatmapGrid? _grid;
        private double _maxMbps = 1;
        private Rectangle _plot;
        private readonly ToolTip _tip = new() { InitialDelay = 200, ReshowDelay = 100 };
        private (int D, int H)? _hover;

        public void SetData(HeatmapGrid grid)
        {
            _grid = grid;
            _maxMbps = 1;
            foreach (var v in grid.Values) if (v is double x && x > _maxMbps) _maxMbps = x;
            Invalidate();
        }

        protected override void Draw(Graphics g, Rectangle bounds)
        {
            if (_grid == null || _grid.FilledCells == 0) { DrawEmpty(g, bounds); return; }

            using var font = AxisFont();
            int left = Px(g, 36), top = Px(g, 4), bottom = Px(g, 18), lh = Px(g, 16);
            _plot = new Rectangle(bounds.X + left, bounds.Y + top,
                Math.Max(1, bounds.Width - left - Px(g, 6)), Math.Max(1, bounds.Height - top - bottom));

            float cw = _plot.Width / 24f, ch = _plot.Height / 7f;
            float gap = Math.Max(1, Px(g, 2));

            for (int d = 0; d < 7; d++)
            {
                DrawText(g, Days[d], font, new Rectangle(bounds.X, (int)(_plot.Top + d * ch), left - Px(g, 4), (int)ch),
                    StringAlignment.Far);

                for (int h = 0; h < 24; h++)
                {
                    var color = _grid.Counts[d, h] >= _grid.MinSamples && _grid.Values[d, h] is double v
                        ? ColorFor(v)
                        : Palette.Empty;
                    using var brush = new SolidBrush(color);
                    g.FillRectangle(brush, _plot.Left + h * cw, _plot.Top + d * ch, cw - gap, ch - gap);
                }
            }

            for (int h = 0; h < 24; h += 3)
                DrawText(g, Format.Hour(h), font,
                    new Rectangle((int)(_plot.Left + h * cw), _plot.Bottom + 2, (int)(cw * 3), lh),
                    StringAlignment.Near, StringAlignment.Near);
        }

        private Color ColorFor(double v)
        {
            if (_grid!.Mode == HeatmapMode.Ping)
                return Ramp(1 - Math.Clamp((v - 30) / 120, 0, 1));
            if (_grid.IsPct)
                return Ramp(Math.Clamp((v - 50) / 50, 0, 1));
            return Ramp(Math.Clamp(v / _maxMbps, 0, 1));
        }

        /// <summary>0 = bad (red) → 0.5 amber → 1 good (green).</summary>
        private Color Ramp(double t) => t < 0.5
            ? Blend(Palette.Bad, Palette.Warn, t * 2)
            : Blend(Palette.Warn, Palette.Good, (t - 0.5) * 2);

        private static Color Blend(Color a, Color b, double t) => Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_grid == null || !_plot.Contains(e.Location)) { HideTip(); return; }

            int h = Math.Clamp((int)((e.X - _plot.Left) / (_plot.Width / 24f)), 0, 23);
            int d = Math.Clamp((int)((e.Y - _plot.Top) / (_plot.Height / 7f)), 0, 6);
            if (_hover == (d, h)) return;
            _hover = (d, h);

            int n = _grid.Counts[d, h];
            string value = n < _grid.MinSamples || _grid.Values[d, h] is not double v
                ? "Not enough data"
                : _grid.Mode == HeatmapMode.Ping ? $"{v:0} ms median ping"
                : _grid.IsPct ? $"{v:0}% of plan (median)" : $"{v:0.#} Mbps (median)";
            _tip.SetToolTip(this, $"{Days[d]} {Format.Hour(h)}: {value} · {n} sample(s)");
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            HideTip();
        }

        private void HideTip()
        {
            _hover = null;
            _tip.SetToolTip(this, null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tip.Dispose();
            base.Dispose(disposing);
        }
    }
}
