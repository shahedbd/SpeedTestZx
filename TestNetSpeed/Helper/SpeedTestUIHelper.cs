using System.Drawing.Drawing2D;
using DpiCommon.Helper;
using SpeedTestZx.Model;

namespace SpeedTestZx.Helper
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
            double gaugePeak,
            bool isDarkTheme)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Inset scales with the gauge's own size (not a flat pixel
            // constant) so the tick/label ring reserved outside the arc
            // stays proportionate at both the small end (the panel's
            // minimum size floor) and the large end, instead of the old
            // fixed 10px inset — which, combined with the label ring sitting
            // 14px further out, actually placed labels a few pixels *outside*
            // the panel's own bounds and clipped them.
            int size = Math.Min(bounds.Width, bounds.Height);
            int inset = Math.Max(34, (int)(size * 0.15));

            var rect = new Rectangle(
                bounds.X + inset, bounds.Y + inset,
                size - inset * 2, size - inset * 2);

            int r = rect.Width / 2;
            float cx = rect.X + r;
            float cy = rect.Y + r;

            // Theme-aware — previously a single hardcoded dark-navy track
            // color regardless of theme. Fine on dark backgrounds, but
            // never actually designed for light mode.
            Color trackColor = isDarkTheme
                ? Color.FromArgb(42, 42, 58)
                : Color.FromArgb(222, 225, 232);
            Color tickColor = isDarkTheme
                ? Color.FromArgb(66, 66, 88)
                : Color.FromArgb(196, 200, 210);
            Color labelColor = isDarkTheme
                ? Color.FromArgb(150, 150, 172)
                : Color.FromArgb(112, 116, 130);

            // ── Tick marks — minor every 50 Mbps, major at each labeled
            // value. Sits in the ring between the arc and the numbers so
            // the dial reads as an instrument face rather than a bare
            // stroke with a few floating digits around it.
            for (double v = 0; v <= MaxMbps; v += 50)
            {
                bool major = GaugeMarkers.Any(m => m.Value == v);
                DrawTick(g, cx, cy, r, v,
                    major ? tickColor : Color.FromArgb(tickColor.A * 2 / 3, tickColor),
                    major);
            }

            // ── Track arc ────────────────────────────────────────────────────
            using (var trackPen = new Pen(trackColor, 14f)
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

                float x1 = cx + (r - 10) * (float)Math.Cos(rad);
                float y1 = cy + (r - 10) * (float)Math.Sin(rad);
                float x2 = cx + (r + 2) * (float)Math.Cos(rad);
                float y2 = cy + (r + 2) * (float)Math.Sin(rad);

                using var peakPen = new Pen(Color.FromArgb(255, 200, 0), 3f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(peakPen, x1, y1, x2, y2);
            }

            // ── Speed labels ─────────────────────────────────────────────────
            using var lblFont = new Font("Segoe UI", 7f, FontStyle.Bold);
            using var lblBrush = new SolidBrush(labelColor);

            foreach (var (label, value) in GaugeMarkers)
            {
                double frac = value / MaxMbps;
                float angle = StartAngle + (float)(frac * SweepTotal);
                double rad = angle * Math.PI / 180.0;
                float lr = r + 24;
                float lx = cx + lr * (float)Math.Cos(rad);
                float ly = cy + lr * (float)Math.Sin(rad);

                var sz = g.MeasureString(label, lblFont);
                g.DrawString(label, lblFont, lblBrush,
                    lx - sz.Width / 2, ly - sz.Height / 2);
            }

            PaintNeedle(g, rect, gaugeValue, trackColor);
        }

        // ── Tick mark at one gauge value — drawn just outside the arc's
        // own outer edge (arc stroke is 14px wide, so its outer edge sits
        // at r+7) so it never gets painted over by the arc itself.
        private static void DrawTick(
            Graphics g, float cx, float cy, int r, double value, Color color, bool major)
        {
            double frac = value / MaxMbps;
            float angle = StartAngle + (float)(frac * SweepTotal);
            double rad = angle * Math.PI / 180.0;

            float innerR = r + 9;
            float outerR = r + (major ? 18 : 13);

            float x1 = cx + innerR * (float)Math.Cos(rad);
            float y1 = cy + innerR * (float)Math.Sin(rad);
            float x2 = cx + outerR * (float)Math.Cos(rad);
            float y2 = cy + outerR * (float)Math.Sin(rad);

            using var pen = new Pen(color, major ? 2f : 1f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, x1, y1, x2, y2);
        }

        // ── Animated needle at the current value ─────────────────────────────
        public static void PaintNeedle(
            Graphics g,
            Rectangle rect,
            double gaugeValue,
            Color hubRingColor)
        {
            double clamped = Math.Clamp(gaugeValue, 0, MaxMbps);
            if (clamped < 0.5) return;

            float angle = StartAngle + (float)(clamped / MaxMbps * SweepTotal);
            double rad = angle * Math.PI / 180.0;
            int r = rect.Width / 2;
            float cx = rect.X + r;
            float cy = rect.Y + r;

            // Needle from 55% radius to 92% radius
            float x1 = cx + (r * 0.55f) * (float)Math.Cos(rad);
            float y1 = cy + (r * 0.55f) * (float)Math.Sin(rad);
            float x2 = cx + (r * 0.92f) * (float)Math.Cos(rad);
            float y2 = cy + (r * 0.92f) * (float)Math.Sin(rad);

            Color needleColor = GetGaugeColor(clamped / MaxMbps);
            using var pen = new Pen(needleColor, 3f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, x1, y1, x2, y2);

            // Hub ring in the dial's own track color — reads as the needle
            // being mounted into the dial rather than a dot floating on
            // top of it.
            using var ringPen = new Pen(hubRingColor, 2f);
            g.DrawEllipse(ringPen, cx - 6, cy - 6, 12, 12);

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
        public static (Label ValueLabel, Panel Box) MakeStatBox(
            string title, string initialValue, Color accentColor, bool isDarkTheme)
        {
            int s(int v) => DpiAwareService.Scale(v);

            var box = new Panel
            {
                Margin = new Padding(s(4)),
                // Previously a single hardcoded dark-navy fill regardless of
                // theme — a dark box on an otherwise light-themed card.
                BackColor = isDarkTheme
                    ? Color.FromArgb(28, 28, 38)
                    : Color.FromArgb(243, 244, 248)
            };

            // Colored top strip ties each card to the same metric colors
            // used on the Dashboard tiles and the Test History table, so a
            // user reads "blue = download" consistently across the app.
            var strip = new Panel
            {
                Dock = DockStyle.Top,
                Height = s(3),
                BackColor = accentColor
            };

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(s(4), s(3), s(4), s(4))
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 7.5f),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = s(16),
                BackColor = Color.Transparent,
                ForeColor = isDarkTheme
                    ? Color.FromArgb(130, 130, 150)
                    : Color.FromArgb(110, 114, 128),
                TextAlign = ContentAlignment.BottomCenter
            };

            var lblValue = new Label
            {
                Text = initialValue,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ForeColor = accentColor,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Add Fill first, then Top — so Top docks above Fill.
            content.Controls.Add(lblValue);
            content.Controls.Add(lblTitle);

            box.Controls.Add(content);
            box.Controls.Add(strip);

            return (lblValue, box);
        }

        // ── Big label text ───────────────────────────────────────────────────
        public static string FormatGaugeValue(double value) =>
            value < 0.5 ? "—"
            : value >= 100 ? $"{value:F0}"
                           : $"{value:F1}";
    }
}
