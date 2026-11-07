using System.Drawing.Drawing2D;
using DpiCommon.Helper;
using SpeedTestZx.Helper;
using FontAwesome.Sharp;

namespace SpeedTestZx.UserControls.Lib
{
    /// <summary>
    /// The "Go Pro" call to action, in one place.
    ///
    /// A gradient pill with a gold crown, deliberately unlike every other button
    /// in the app: the neutral action buttons are chrome, this one is the offer.
    ///
    /// Ported from DuplicateFinderZx (itself from NetSpeedMeterPlus / CPU ZX Pro)
    /// so the offer looks the same across the products. The ramp is
    /// AppConfig.ProAccentStart → ProAccentEnd, the same as the upgrade card's
    /// top bar and the sidebar PRO badge, so button and prompt read as one flow.
    /// </summary>
    public static class PremiumButton
    {
        /// <summary>Shared CTA wording — change it here and every surface follows.</summary>
        public const string Label = "Go Pro";

        public static Color GradientStart => AppConfig.ProAccentStart;
        public static Color GradientEnd => AppConfig.ProAccentEnd;
        public static readonly Color CrownGold = Color.FromArgb(251, 191, 36);

        /// <summary>
        /// Builds the CTA at the given size. Hover is wired here, so callers only
        /// ever have to call <see cref="Apply"/> again after a resize.
        /// </summary>
        public static IconButton Create(Size size, int iconSize, float fontSize = 9f, string? text = null)
        {
            var btn = new IconButton
            {
                Text = "  " + (text ?? Label),
                IconChar = IconChar.Crown,
                IconColor = CrownGold,
                IconSize = iconSize,
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                ForeColor = Color.White,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                TextAlign = ContentAlignment.MiddleCenter,
                ImageAlign = ContentAlignment.MiddleCenter,
                FlatStyle = FlatStyle.Flat,
                Size = size,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => Apply(btn, hovered: true);
            btn.MouseLeave += (s, e) => Apply(btn, hovered: false);

            Apply(btn);

            return btn;
        }

        /// <summary>
        /// Applies (or re-applies) the gradient pill look. Call after every size
        /// change — the gradient is a bitmap sized to the button, so it has to be
        /// regenerated rather than just recoloured.
        /// </summary>
        public static void Apply(Button btn, bool hovered = false)
        {
            if (btn == null || btn.IsDisposed || btn.Width <= 0 || btn.Height <= 0) return;

            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.UseVisualStyleBackColor = false;
            btn.BackColor = GradientStart;   // shows through only if the image fails

            if (btn is IconButton iconBtn)
                iconBtn.IconColor = CrownGold;

            // Lift the whole ramp on hover instead of flashing a flat colour —
            // the FlatAppearance colours sit under the background image and would
            // never be seen anyway.
            Color start = hovered ? Lighten(GradientStart, 0.18f) : GradientStart;
            Color end = hovered ? Lighten(GradientEnd, 0.18f) : GradientEnd;

            ApplyGradient(btn, start, end);
            ApplyPillRegion(btn);
        }

        /// <summary>Rounds the button into a pill.</summary>
        private static void ApplyPillRegion(Button btn, int baseRadius = 8)
        {
            int radius = Math.Min(DpiAwareService.Scale(baseRadius), btn.Height / 2);
            int diameter = radius * 2;
            var bounds = new Rectangle(0, 0, btn.Width, btn.Height);

            using var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter - 1, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter - 1, bounds.Bottom - diameter - 1, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter - 1, diameter, diameter, 90, 90);
            path.CloseFigure();

            btn.Region?.Dispose();
            btn.Region = new Region(path);
        }

        /// <summary>
        /// Paints the button with a gradient sized to it, disposing the previous
        /// one — WinForms never disposes BackgroundImage, so a resize or hover
        /// sweep would otherwise leak a bitmap per tick.
        /// </summary>
        private static void ApplyGradient(Button btn, Color start, Color end)
        {
            var previous = btn.BackgroundImage;

            var bitmap = new Bitmap(Math.Max(1, btn.Width), Math.Max(1, btn.Height));
            using (var g = Graphics.FromImage(bitmap))
            using (var brush = new LinearGradientBrush(
                new Rectangle(Point.Empty, bitmap.Size), start, end, LinearGradientMode.Horizontal))
            {
                g.FillRectangle(brush, 0, 0, bitmap.Width, bitmap.Height);
            }

            btn.BackgroundImage = bitmap;
            btn.BackgroundImageLayout = ImageLayout.Stretch;
            previous?.Dispose();
        }

        /// <summary>Nudges a colour toward white — used for hover states.</summary>
        private static Color Lighten(Color color, float amount) => Color.FromArgb(
            color.A,
            (int)Math.Clamp(color.R + (255 - color.R) * amount, 0, 255),
            (int)Math.Clamp(color.G + (255 - color.G) * amount, 0, 255),
            (int)Math.Clamp(color.B + (255 - color.B) * amount, 0, 255));
    }
}
