using System.Drawing.Drawing2D;
using DpiCommon.Helper;
using TestNetSpeed.Model;

namespace TestNetSpeed.Helper
{
    /// <summary>
    /// All drawing, layout, and stat-box factory logic for the Speed Test
    /// page. NetworkToolsControl-style pages call methods here — GDI+ stays
    /// out of the page class itself.
    /// </summary>
    public static class SpeedTestUIHelper
    {
        // ── Gauge constants ──────────────────────────────────────────────────
        public const float StartAngle = 150f;
        public const float SweepTotal = 240f;
        public const double MaxMbps = 1000.0;

        // ── Speed labels drawn around the arc ────────────────────────────────
        private static readonly (string Label, double Value)[] GaugeMarkers =
        [
            ("0",   0),
            ("100", 100),
            ("250", 250),
            ("500", 500),
            ("750", 750),
            ("1G",  1000)
        ];

        // ══════════════════════════════════════════════════════════════════════
        //  GAUGE PAINTER
        // ══════════════════════════════════════════════════════════════════════
        public static void PaintGauge(
            Graphics g,
            Rectangle bounds,
            double gaugeValue,
            double gaugePeak)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(
                bounds.X + 10, bounds.Y + 10,
                bounds.Width - 20,
                bounds.Height - 20);

            // ── Track arc ────────────────────────────────────────────────────
            using (var trackPen = new Pen(Color.FromArgb(38, 38, 52), 14f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(trackPen, rect, StartAngle, SweepTotal);
            }

            // ── Value arc ────────────────────────────────────────────────────
            double clamped = Math.Clamp(gaugeValue, 0, MaxMbps);
            float sweep = (float)(clamped / MaxMbps * SweepTotal);

            if (sweep > 0.5f)
            {
                Color arcColor = GetGaugeColor(clamped / MaxMbps);
                using var valuePen = new Pen(arcColor, 14f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(valuePen, rect, StartAngle, sweep);
            }

            // ── Peak tick ────────────────────────────────────────────────────
            if (gaugePeak > 0)
            {
                double peakFrac = Math.Clamp(gaugePeak / MaxMbps, 0, 1);
                float peakAngle = StartAngle + (float)(peakFrac * SweepTotal);
                double rad = peakAngle * Math.PI / 180.0;
                int r = rect.Width / 2;
                float cx = rect.X + r;
                float cy = rect.Y + r;

                float x1 = cx + (r - 10) * (float)Math.Cos(rad);
                float y1 = cy + (r - 10) * (float)Math.Sin(rad);
                float x2 = cx + (r + 2) * (float)Math.Cos(rad);
                float y2 = cy + (r + 2) * (float)Math.Sin(rad);

                using var peakPen = new Pen(Color.FromArgb(255, 200, 0), 3f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(peakPen, x1, y1, x2, y2);
            }

            // ── Speed labels ─────────────────────────────────────────────────
            using var lblFont = new Font("Segoe UI", 6.5f);
            using var lblBrush = new SolidBrush(Color.FromArgb(100, 100, 120));

            foreach (var (label, value) in GaugeMarkers)
            {
                double frac = value / MaxMbps;
                float angle = StartAngle + (float)(frac * SweepTotal);
                double rad = angle * Math.PI / 180.0;
                int r = rect.Width / 2 + 14;
                float lx = rect.X + rect.Width / 2f + r * (float)Math.Cos(rad);
                float ly = rect.Y + rect.Height / 2f + r * (float)Math.Sin(rad);

                var sz = g.MeasureString(label, lblFont);
                g.DrawString(label, lblFont, lblBrush,
                    lx - sz.Width / 2, ly - sz.Height / 2);
            }

            PaintNeedle(g, rect, gaugeValue);
        }

        // ── Animated needle at the current value ─────────────────────────────
        public static void PaintNeedle(
            Graphics g,
            Rectangle rect,
            double gaugeValue)
        {
            double clamped = Math.Clamp(gaugeValue, 0, MaxMbps);
            if (clamped < 0.5) return;

            float angle = StartAngle + (float)(clamped / MaxMbps * SweepTotal);
            double rad = angle * Math.PI / 180.0;
            int r = rect.Width / 2;
            float cx = rect.X + r;
            float cy = rect.Y + r;

            // Needle from 60% radius to 95% radius
            float x1 = cx + (r * 0.60f) * (float)Math.Cos(rad);
            float y1 = cy + (r * 0.60f) * (float)Math.Sin(rad);
            float x2 = cx + (r * 0.95f) * (float)Math.Cos(rad);
            float y2 = cy + (r * 0.95f) * (float)Math.Sin(rad);

            Color needleColor = GetGaugeColor(clamped / MaxMbps);
            using var pen = new Pen(needleColor, 2.5f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, x1, y1, x2, y2);

            using var dotBrush = new SolidBrush(needleColor);
            g.FillEllipse(dotBrush, cx - 4, cy - 4, 8, 8);
        }

        // ── Gauge colour ─────────────────────────────────────────────────────
        public static Color GetGaugeColor(double fraction) => fraction switch
        {
            < 0.15 => Color.FromArgb(59, 130, 246),
            < 0.35 => Color.FromArgb(14, 165, 233),
            < 0.60 => Color.FromArgb(16, 185, 129),
            < 0.80 => Color.FromArgb(245, 158, 11),
            _ => Color.FromArgb(239, 68, 68),
        };

        // ── Progress-bar colour ──────────────────────────────────────────────
        public static Color GetProgressColor(SpeedTestPhase phase) => phase switch
        {
            SpeedTestPhase.Ping => Color.FromArgb(168, 85, 247),
            SpeedTestPhase.Download => Color.FromArgb(59, 130, 246),
            SpeedTestPhase.Upload => Color.FromArgb(16, 185, 129),
            SpeedTestPhase.Complete => Color.FromArgb(16, 185, 129),
            SpeedTestPhase.Failed => Color.FromArgb(239, 68, 68),
            _ => Color.FromArgb(80, 80, 100),
        };

        // ══════════════════════════════════════════════════════════════════════
        //  STAT BOX FACTORY — returns (valueLabel, containerPanel)
        // ══════════════════════════════════════════════════════════════════════
        public static (Label ValueLabel, Panel Box) MakeStatBox(string title, string initialValue)
        {
            int s(int v) => DpiAwareService.Scale(v);

            var box = new Panel
            {
                Margin = new Padding(s(3)),
                BackColor = Color.FromArgb(28, 28, 38),
                Padding = new Padding(s(4))
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 7.5f),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = s(16),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(130, 130, 150),
                TextAlign = ContentAlignment.BottomCenter
            };

            var lblValue = new Label
            {
                Text = initialValue,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Add Fill first, then Top — so Top docks above Fill.
            box.Controls.Add(lblValue);
            box.Controls.Add(lblTitle);

            return (lblValue, box);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HISTORY ENTRY BUILDER — (text, color) segments for a RichTextBox
        // ══════════════════════════════════════════════════════════════════════
        public static IEnumerable<(string Text, Color Color)> BuildHistoryEntry(SpeedTestResult r)
        {
            yield return ($"\n── {r.TestedAt:HH:mm:ss} ────────────────\n",
                Color.FromArgb(59, 130, 246));
            yield return ("  ↓ Download : ", Color.FromArgb(140, 140, 160));
            yield return ($"{r.DownloadMbps:F2} Mbps\n", Color.FromArgb(16, 185, 129));
            yield return ("  ↑ Upload   : ", Color.FromArgb(140, 140, 160));
            yield return ($"{r.UploadMbps:F2} Mbps\n", Color.FromArgb(14, 165, 233));
            yield return ("  Ping       : ", Color.FromArgb(140, 140, 160));
            yield return ($"{r.PingMs:F0} ms\n", Color.FromArgb(245, 158, 11));
            yield return ("  Jitter     : ", Color.FromArgb(140, 140, 160));
            yield return ($"{r.JitterMs:F0} ms\n", Color.FromArgb(168, 85, 247));
        }

        // ── Big label text ───────────────────────────────────────────────────
        public static string FormatGaugeValue(double value) =>
            value < 0.5 ? "—"
            : value >= 100 ? $"{value:F0}"
                           : $"{value:F1}";
    }
}
