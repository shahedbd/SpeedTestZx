using SpeedTestZx.Helper;

namespace SpeedTestZx.UserControls.Lib.Charts
{
    /// <summary>
    /// "By hour of day" (T51): 24 bars, each the median % of plan (or Mbps
    /// without a plan) of the tests run in that hour. Hours with no tests
    /// show a thin empty stub; the worst 3-hour window is shaded.
    /// </summary>
    public sealed class HourBarChart : ChartControl
    {
        private double?[] _values = new double?[24];
        private bool _isPct;
        private (int Start, int End)? _worst;

        public void SetData(double?[] values, bool isPct, (int Start, int End)? worst)
        {
            _values = values;
            _isPct = isPct;
            _worst = worst;
            Invalidate();
        }

        protected override void Draw(Graphics g, Rectangle bounds)
        {
            if (_values.All(v => v == null)) { DrawEmpty(g, bounds); return; }

            using var font = AxisFont();
            int left = Px(g, 36), right = Px(g, 6), top = Px(g, 8), bottom = Px(g, 20), lh = Px(g, 16);
            var plot = new Rectangle(bounds.X + left, bounds.Y + top,
                Math.Max(1, bounds.Width - left - right), Math.Max(1, bounds.Height - top - bottom));

            double max = PlanTrendChart.NiceCeiling(Math.Max(_values.Max(v => v ?? 0), _isPct ? 100 : 1));
            float slot = plot.Width / 24f;

            if (_worst is { } w)
            {
                using var shade = new SolidBrush(Color.FromArgb(40, Palette.Bad));
                for (int h = w.Start; h < w.End; h++)
                    g.FillRectangle(shade, plot.Left + (h % 24) * slot, plot.Top, slot, plot.Height);
            }

            using (var grid = new Pen(Palette.Grid))
            {
                for (int i = 0; i <= 2; i++)
                {
                    double v = max * i / 2;
                    int y = plot.Bottom - (int)(v / max * plot.Height);
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    DrawText(g, _isPct ? $"{v:0}%" : $"{v:0}", font,
                        new Rectangle(bounds.X, y - lh / 2, left - 4, lh),
                        StringAlignment.Far);
                }
            }

            for (int h = 0; h < 24; h++)
            {
                float x = plot.Left + h * slot + slot * 0.15f;
                float bw = slot * 0.7f;

                if (_values[h] is double v)
                {
                    float bh = (float)(Math.Clamp(v / max, 0, 1) * plot.Height);
                    using var brush = new SolidBrush(_isPct ? Palette.ForPct(v) : Palette.Line);
                    g.FillRectangle(brush, x, plot.Bottom - bh, bw, Math.Max(1, bh));
                }
                else
                {
                    using var empty = new SolidBrush(Palette.Empty);
                    g.FillRectangle(empty, x, plot.Bottom - 2, bw, 2);
                }

                if (h % 3 == 0)
                    DrawText(g, Format.Hour(h), font,
                        new Rectangle((int)(plot.Left + h * slot) - Px(g, 8), plot.Bottom + 3, (int)slot * 3, lh),
                        StringAlignment.Near, StringAlignment.Near);
            }
        }
    }
}
