using System.Diagnostics;
using System.Drawing.Drawing2D;
using DpiCommon.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;

namespace TestNetSpeed.UserControls.Lib
{
    /// <summary>
    /// Live throughput + latency graph for the Speed Test page (T25).
    ///
    /// X axis is time since the test started. Download (blue) and upload
    /// (green) are filled lines on the left Mbps scale; latency probes are
    /// amber dots on a separate ms scale (right), so latency under load is
    /// visible as the dots climbing while a transfer runs. Lost probes are
    /// drawn as small red marks along the top edge.
    ///
    /// Feed it with <see cref="AddThroughput"/> / <see cref="AddLatency"/>
    /// from the page's IProgress handler (UI thread), and <see cref="Reset"/>
    /// at the start of each test. Both axes auto-scale to the data.
    /// </summary>
    public class LiveGraphControl : Control
    {
        private static readonly Color DownColor = Color.FromArgb(59, 130, 246);
        private static readonly Color UpColor = Color.FromArgb(16, 185, 129);
        private static readonly Color LatencyColor = Color.FromArgb(245, 158, 11);
        private static readonly Color LostColor = Color.FromArgb(239, 68, 68);

        private readonly List<(double T, double Mbps)> _down = new();
        private readonly List<(double T, double Mbps)> _up = new();
        private readonly List<(double T, double? Ms)> _latency = new();
        private readonly Stopwatch _clock = new();

        private readonly ThemeManager _theme = ThemeManager.Instance;

        public LiveGraphControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public bool HasData => _down.Count > 0 || _up.Count > 0 || _latency.Count > 0;

        public void Reset()
        {
            _down.Clear();
            _up.Clear();
            _latency.Clear();
            _clock.Restart();
            Invalidate();
        }

        public void AddThroughput(SpeedTestPhase phase, double mbps)
        {
            if (mbps <= 0) return;

            double t = Now();
            if (phase == SpeedTestPhase.Download) _down.Add((t, mbps));
            else if (phase == SpeedTestPhase.Upload) _up.Add((t, mbps));
            else return;

            Invalidate();
        }

        public void AddLatency(double? ms)
        {
            _latency.Add((Now(), ms));
            Invalidate();
        }

        /// <summary>Stops the clock so a finished graph doesn't keep stretching.</summary>
        public void Freeze() => _clock.Stop();

        private double Now()
        {
            if (!_clock.IsRunning && _clock.ElapsedTicks == 0) _clock.Start();
            return _clock.Elapsed.TotalSeconds;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PAINT
        // ══════════════════════════════════════════════════════════════════════

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

            var plot = new Rectangle(s(40), s(8), Width - s(40) - s(40), Height - s(8) - s(22));
            if (plot.Width < s(40) || plot.Height < s(30)) return;

            using var axisFont = new Font("Segoe UI", 7f);
            using var axisBrush = new SolidBrush(axisText);

            if (!HasData)
            {
                using var gridPenEmpty = new Pen(gridColor) { DashStyle = DashStyle.Dot };
                for (int i = 0; i <= 4; i++)
                {
                    float y = plot.Top + plot.Height * i / 4f;
                    g.DrawLine(gridPenEmpty, plot.Left, y, plot.Right, y);
                }

                const string hint = "Live speed and latency appear here during a test";
                var sz = g.MeasureString(hint, axisFont);
                g.DrawString(hint, axisFont, axisBrush,
                    plot.Left + (plot.Width - sz.Width) / 2, plot.Top + (plot.Height - sz.Height) / 2);
                return;
            }

            // ── Scales ───────────────────────────────────────────────────────
            double maxT = Math.Max(1, new[]
            {
                _down.Count > 0 ? _down[^1].T : 0,
                _up.Count > 0 ? _up[^1].T : 0,
                _latency.Count > 0 ? _latency[^1].T : 0,
            }.Max());

            double maxMbps = NiceCeiling(_down.Concat(_up).Select(p => p.Mbps).DefaultIfEmpty(10).Max());
            double maxMs = NiceCeiling(_latency.Where(p => p.Ms != null).Select(p => p.Ms!.Value).DefaultIfEmpty(50).Max());

            float X(double t) => plot.Left + (float)(t / maxT * plot.Width);
            float YMbps(double v) => plot.Bottom - (float)(v / maxMbps * plot.Height);
            float YMs(double v) => plot.Bottom - (float)(Math.Min(v, maxMs) / maxMs * plot.Height);

            // ── Grid + axis labels ───────────────────────────────────────────
            using (var gridPen = new Pen(gridColor) { DashStyle = DashStyle.Dot })
            {
                for (int i = 0; i <= 4; i++)
                {
                    float y = plot.Top + plot.Height * i / 4f;
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);

                    string left = $"{maxMbps * (4 - i) / 4:0.#}";
                    var lsz = g.MeasureString(left, axisFont);
                    g.DrawString(left, axisFont, axisBrush, plot.Left - lsz.Width - s(2), y - lsz.Height / 2);

                    string right = $"{maxMs * (4 - i) / 4:0}";
                    var rsz = g.MeasureString(right, axisFont);
                    g.DrawString(right, axisFont, axisBrush, plot.Right + s(2), y - rsz.Height / 2);
                }
            }

            g.DrawString("Mbps", axisFont, axisBrush, s(2), plot.Bottom + s(6));
            var msSz = g.MeasureString("ms", axisFont);
            g.DrawString("ms", axisFont, axisBrush, Width - msSz.Width - s(2), plot.Bottom + s(6));

            // ── Throughput lines ─────────────────────────────────────────────
            DrawSeries(g, _down, DownColor, X, YMbps, plot);
            DrawSeries(g, _up, UpColor, X, YMbps, plot);

            // ── Latency dots ─────────────────────────────────────────────────
            float dot = Math.Max(3f, s(4));
            using (var latBrush = new SolidBrush(Color.FromArgb(210, LatencyColor)))
            using (var lostPen = new Pen(LostColor, 1.5f))
            {
                foreach (var (t, ms) in _latency)
                {
                    float x = X(t);
                    if (ms is double v)
                        g.FillEllipse(latBrush, x - dot / 2, YMs(v) - dot / 2, dot, dot);
                    else
                        g.DrawLine(lostPen, x, plot.Top, x, plot.Top + s(6));
                }
            }

            DrawLegend(g, plot, axisFont, axisBrush);
        }

        private static void DrawSeries(
            Graphics g, List<(double T, double Mbps)> pts, Color color,
            Func<double, float> x, Func<double, float> y, Rectangle plot)
        {
            if (pts.Count == 0) return;

            // Two streams report interleaved per-stream samples; a 3-point
            // moving average keeps the line readable without hiding trends.
            var smoothed = new List<PointF>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                int from = Math.Max(0, i - 2);
                double avg = 0;
                for (int j = from; j <= i; j++) avg += pts[j].Mbps;
                avg /= i - from + 1;
                smoothed.Add(new PointF(x(pts[i].T), y(avg)));
            }

            if (smoothed.Count == 1)
            {
                using var b = new SolidBrush(color);
                g.FillEllipse(b, smoothed[0].X - 2, smoothed[0].Y - 2, 4, 4);
                return;
            }

            using (var path = new GraphicsPath())
            {
                path.AddLine(smoothed[0].X, plot.Bottom, smoothed[0].X, smoothed[0].Y);
                path.AddLines(smoothed.ToArray());
                path.AddLine(smoothed[^1].X, smoothed[^1].Y, smoothed[^1].X, plot.Bottom);
                path.CloseFigure();

                using var fill = new LinearGradientBrush(plot,
                    Color.FromArgb(90, color), Color.FromArgb(10, color), LinearGradientMode.Vertical);
                g.FillPath(fill, path);
            }

            using var pen = new Pen(color, 2f) { LineJoin = LineJoin.Round };
            g.DrawLines(pen, smoothed.ToArray());
        }

        private void DrawLegend(Graphics g, Rectangle plot, Font font, Brush textBrush)
        {
            int s(int v) => DpiAwareService.Scale(v);

            (string Text, Color Color)[] items =
            [
                ("Download", DownColor),
                ("Upload", UpColor),
                ("Latency", LatencyColor),
            ];

            float x = plot.Left + s(6);
            float y = plot.Bottom + s(6);
            foreach (var (text, color) in items)
            {
                using var b = new SolidBrush(color);
                g.FillEllipse(b, x, y + s(3), s(7), s(7));
                x += s(10);
                g.DrawString(text, font, textBrush, x, y);
                x += g.MeasureString(text, font).Width + s(10);
            }
        }

        /// <summary>Rounds up to 1 / 2 / 5 × 10ⁿ so axis labels stay tidy.</summary>
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
