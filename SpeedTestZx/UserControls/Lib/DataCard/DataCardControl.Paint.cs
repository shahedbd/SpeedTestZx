// UserControls/Lib/DataCard/DataCardControl.Paint.cs
using System.Drawing.Drawing2D;

namespace SpeedTestZx.UserControls.Lib.DataCard
{
    /// <summary>Rendering for <see cref="DataCardControl"/>.</summary>
    public partial class DataCardControl
    {
        // Fixed point sizes — GDI already renders points at the right pixel
        // size per DPI, so fonts must NOT be multiplied by the DPI scale.
        private static readonly Font ValueFont = new("Segoe UI", 13f, FontStyle.Bold);
        private static readonly Font BadgeFont = new("Segoe UI", 7f, FontStyle.Bold);

        private const TextFormatFlags TextFlags =
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding;

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 4 || Height < 4) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedRect(rect, S(CornerRadius));

            DrawSurface(g, path, rect);
            int textX = DrawIcon(g);
            int right = Width - S(12) - DrawBadge(g);
            DrawText(g, textX, right);
        }

        // ── Surface: fill, accent wash, strip, border ────────────────────────
        private void DrawSurface(Graphics g, GraphicsPath path, Rectangle rect)
        {
            using (var fill = new SolidBrush(Blend(_palette.Surface, _palette.SurfaceHover, _hover)))
                g.FillPath(fill, path);

            var clip = g.Clip;
            g.SetClip(path, CombineMode.Intersect);

            // Soft accent glow fading out from the left edge.
            int washW = Math.Max(1, (int)(Width * 0.6f));
            int washA = (int)(18 + 14 * _hover);
            using (var wash = new LinearGradientBrush(
                new Rectangle(0, 0, washW, Height),
                Color.FromArgb(washA, _accent), Color.FromArgb(0, _accent), 0f))
            {
                g.FillRectangle(wash, 0, 0, washW, Height);
            }

            using (var strip = new SolidBrush(_accent))
                g.FillRectangle(strip, 0, 0, S(4), Height);

            g.Clip = clip;

            var borderColor = Blend(_palette.Border, Color.FromArgb(150, _accent), _hover);
            using var pen = new Pen(borderColor, 1f);
            g.DrawPath(pen, path);
        }

        // ── Icon badge (delegated to the reusable renderer) ──────────────────
        private int DrawIcon(Graphics g)
        {
            int halo = _iconRenderer.ShowHalo ? S(4) : 0;
            int d = Math.Max(S(16), Math.Min(S(IconSize), Height - S(16) - halo * 2));
            var bounds = new Rectangle(S(16) + halo, (Height - d) / 2, d, d);

            _iconRenderer.Draw(g, bounds, DpiScale, _hover);
            return bounds.Right + halo + S(14);
        }

        // ── Badge pill (top-right). Returns the width it occupies. ───────────
        private int DrawBadge(Graphics g)
        {
            if (string.IsNullOrEmpty(_badge)) return 0;

            var color = _badgeColor ?? _accent;
            var textSize = TextRenderer.MeasureText(g, _badge, BadgeFont,
                Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int h = S(18);
            int w = textSize.Width + S(14);
            var pill = new Rectangle(Width - S(10) - w, S(9), w, h);

            using (var pillPath = RoundedRect(pill, h / 2))
            using (var bg = new SolidBrush(Color.FromArgb(40, color)))
                g.FillPath(bg, pillPath);

            TextRenderer.DrawText(g, _badge, BadgeFont, pill, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            return w + S(6);
        }

        // ── Caption / value / sub-text (or loading skeleton) ─────────────────
        private void DrawText(Graphics g, int x, int badgeRight)
        {
            int fullRight = Width - S(12);
            if (fullRight - x < S(20)) return;

            int capH = Font.Height;
            int valH = ValueFont.Height;
            int subH = Font.Height;
            int gap = S(1);
            int top = (Height - (capH + valH + subH + gap * 2)) / 2;

            var capRect = new Rectangle(x, top, Math.Max(0, badgeRight - x), capH);
            var valRect = new Rectangle(x, capRect.Bottom + gap, fullRight - x, valH);
            var subRect = new Rectangle(x, valRect.Bottom + gap, fullRight - x, subH);

            TextRenderer.DrawText(g, _caption, Font, capRect, _palette.SecondaryText, TextFlags);

            if (_isLoading)
            {
                DrawSkeleton(g, valRect, 0.45f, 0.62f);
                DrawSkeleton(g, subRect, 0.65f, 0.55f);
                return;
            }

            TextRenderer.DrawText(g, _value, ValueFont, valRect, _valueColor ?? _accent, TextFlags);
            TextRenderer.DrawText(g, _subText, Font, subRect, _palette.SecondaryText, TextFlags);
        }

        private void DrawSkeleton(Graphics g, Rectangle row, float widthPct, float heightPct)
        {
            int h = Math.Max(S(6), (int)(row.Height * heightPct));
            var bar = new Rectangle(row.X, row.Y + (row.Height - h) / 2,
                Math.Max(S(20), (int)(row.Width * widthPct)), h);

            int alpha = (int)(150 + 90 * (0.5f + 0.5f * MathF.Sin(_pulse)));
            using var path = RoundedRect(bar, h / 2);
            using var brush = new SolidBrush(Color.FromArgb(alpha, _palette.Skeleton));
            g.FillPath(brush, path);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DRAWING UTILITIES
        // ══════════════════════════════════════════════════════════════════════

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Blend(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            if (b.A < 255)
            {
                // Composite a translucent target over 'a' first.
                float ba = b.A / 255f;
                b = Color.FromArgb(
                    (int)(a.R + (b.R - a.R) * ba),
                    (int)(a.G + (b.G - a.G) * ba),
                    (int)(a.B + (b.B - a.B) * ba));
            }
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
}
