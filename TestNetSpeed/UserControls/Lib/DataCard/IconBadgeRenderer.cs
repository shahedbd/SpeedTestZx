// UserControls/Lib/DataCard/IconBadgeRenderer.cs
using FontAwesome.Sharp;
using System.Drawing.Drawing2D;

namespace TestNetSpeed.UserControls.Lib.DataCard
{
    /// <summary>Visual style of an <see cref="IconBadgeRenderer"/> badge.</summary>
    public enum IconBadgeStyle
    {
        /// <summary>Glossy gradient circle, white glyph.</summary>
        Circle,
        /// <summary>Glossy gradient rounded square, white glyph.</summary>
        Squircle,
        /// <summary>Tinted accent background, accent-colored glyph.</summary>
        Soft,
        /// <summary>Accent ring, accent-colored glyph.</summary>
        Outline
    }

    /// <summary>
    /// Draws a FontAwesome icon inside a decorated badge (shadow, halo,
    /// gradient, gloss, glyph shadow). Owns its glyph bitmap cache, so any
    /// owner-drawn control can reuse it — see <see cref="IconBadge"/> and
    /// <see cref="DataCardControl"/>.
    /// </summary>
    public sealed class IconBadgeRenderer : IDisposable
    {
        private IconChar _icon = IconChar.None;
        private Color _accent = DataCardAccents.Blue;

        private Bitmap? _glyph;
        private Bitmap? _glyphShadow;
        private int _cachedSize;
        private Color _cachedColor;

        public IconChar Icon
        {
            get => _icon;
            set { if (_icon != value) { _icon = value; ResetCache(); } }
        }

        public Color Accent
        {
            get => _accent;
            set { if (_accent != value) { _accent = value; ResetCache(); } }
        }

        public IconBadgeStyle Style { get; set; } = IconBadgeStyle.Circle;

        /// <summary>Glyph size relative to the badge (0.3 – 0.8).</summary>
        public float GlyphScale { get; set; } = 0.48f;

        /// <summary>Draw the outer halo ring (needs ~4px of room around bounds).</summary>
        public bool ShowHalo { get; set; } = true;

        /// <summary>
        /// Draws the badge in <paramref name="bounds"/>. <paramref name="hover"/>
        /// (0 → 1) brightens the halo and grows the badge slightly.
        /// </summary>
        public void Draw(Graphics g, Rectangle bounds, float dpiScale, float hover = 0f)
        {
            int S(float v) => (int)Math.Round(v * dpiScale);

            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = Rectangle.Inflate(bounds, S(1.5f * hover), S(1.5f * hover));
            bool filled = Style is IconBadgeStyle.Circle or IconBadgeStyle.Squircle;

            if (ShowHalo)
            {
                var halo = Rectangle.Inflate(r, S(4), S(4));
                using var haloPath = Shape(halo);
                using var haloBrush = new SolidBrush(Color.FromArgb((int)(28 + 32 * hover), _accent));
                g.FillPath(haloBrush, haloPath);
            }

            using var path = Shape(r);

            if (filled) DrawFilled(g, path, r, S);
            else DrawTinted(g, path, S);

            DrawGlyph(g, r, filled, S);

            g.SmoothingMode = oldSmoothing;
        }

        // ── Filled styles: shadow, gradient, gloss, rim ─────────────────────
        private void DrawFilled(Graphics g, GraphicsPath path, Rectangle r, Func<float, int> S)
        {
            // Soft drop shadow: a few stacked, offset translucent layers.
            for (int i = 3; i >= 1; i--)
            {
                var sr = Rectangle.Inflate(r, S(i * 0.5f), S(i * 0.5f));
                sr.Offset(0, S(2));
                using var sp = Shape(sr);
                using var sb = new SolidBrush(Color.FromArgb(22, Darken(_accent, 0.6f)));
                g.FillPath(sb, sp);
            }

            using (var grad = new LinearGradientBrush(r, Lighten(_accent, 0.25f), Darken(_accent, 0.32f), 55f))
                g.FillPath(grad, path);

            // Gloss: white fading out over the top half.
            var gloss = new Rectangle(r.X, r.Y, r.Width, Math.Max(1, r.Height / 2 + 1));
            var clip = g.Clip;
            g.SetClip(path, CombineMode.Intersect);
            using (var glossBrush = new LinearGradientBrush(gloss,
                Color.FromArgb(70, Color.White), Color.FromArgb(0, Color.White), 90f))
            {
                g.FillRectangle(glossBrush, gloss);
            }
            g.Clip = clip;

            using var rim = new Pen(Color.FromArgb(60, Color.White), Math.Max(1f, S(1)));
            g.DrawPath(rim, path);
        }

        // ── Soft / Outline styles ────────────────────────────────────────────
        private void DrawTinted(Graphics g, GraphicsPath path, Func<float, int> S)
        {
            if (Style == IconBadgeStyle.Soft)
            {
                using var fill = new SolidBrush(Color.FromArgb(45, _accent));
                g.FillPath(fill, path);
            }

            int ringAlpha = Style == IconBadgeStyle.Outline ? 230 : 90;
            using var ring = new Pen(Color.FromArgb(ringAlpha, _accent),
                Style == IconBadgeStyle.Outline ? Math.Max(1.5f, S(2)) : Math.Max(1f, S(1)));
            g.DrawPath(ring, path);
        }

        // ── Glyph with a subtle drop shadow ──────────────────────────────────
        private void DrawGlyph(Graphics g, Rectangle r, bool filled, Func<float, int> S)
        {
            int size = Math.Max(8, (int)(Math.Min(r.Width, r.Height) * Math.Clamp(GlyphScale, 0.3f, 0.8f)));
            var color = filled ? Color.White : Lighten(_accent, 0.1f);
            EnsureGlyph(size, color);
            if (_glyph == null) return;

            int x = r.X + (r.Width - _glyph.Width) / 2;
            int y = r.Y + (r.Height - _glyph.Height) / 2;

            if (filled && _glyphShadow != null)
                g.DrawImage(_glyphShadow, x, y + S(1), _glyphShadow.Width, _glyphShadow.Height);

            g.DrawImage(_glyph, x, y, _glyph.Width, _glyph.Height);
        }

        private void EnsureGlyph(int size, Color color)
        {
            if (_glyph != null && _cachedSize == size && _cachedColor == color) return;

            ResetCache();
            try
            {
                _glyph = RenderGlyph(_icon, color, size);
                _glyphShadow = RenderGlyph(_icon, Color.FromArgb(70, Darken(_accent, 0.7f)), size);
                _cachedSize = size;
                _cachedColor = color;
            }
            catch
            {
                ResetCache();   // unknown glyph — badge still renders
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UTILITIES
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// FontAwesome.Sharp shrinks the glyph to fit its measured text box,
        /// so it comes out ~77% of the requested size (NetPulseApp's own
        /// renderer fills the full em). Oversizing the bitmap by this factor
        /// keeps the glyph at the size <see cref="GlyphScale"/> intends.
        /// </summary>
        internal const float FontAwesomeSharpFill = 1.3f;

        private static Bitmap RenderGlyph(IconChar icon, Color color, int size) =>
            icon.ToBitmap(IconFont.Auto, (int)Math.Round(size * FontAwesomeSharpFill), color);

        private GraphicsPath Shape(Rectangle r)
        {
            var path = new GraphicsPath();
            if (Style == IconBadgeStyle.Squircle)
            {
                int d = Math.Max(2, (int)(Math.Min(r.Width, r.Height) * 0.56f));
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
            }
            else
            {
                path.AddEllipse(r);
            }
            return path;
        }

        internal static Color Lighten(Color c, float t) => Mix(c, Color.White, t);
        internal static Color Darken(Color c, float t) => Mix(c, Color.Black, t);

        private static Color Mix(Color a, Color b, float t) => Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));

        private void ResetCache()
        {
            _glyph?.Dispose();
            _glyphShadow?.Dispose();
            _glyph = null;
            _glyphShadow = null;
            _cachedSize = 0;
        }

        public void Dispose() => ResetCache();
    }
}
