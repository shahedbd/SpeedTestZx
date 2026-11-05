// UserControls/Lib/DataCard/DataCardPalette.cs
namespace TestNetSpeed.UserControls.Lib.DataCard
{
    /// <summary>
    /// Surface colors for a <see cref="DataCardControl"/>. Kept independent of
    /// any app-specific theme manager so the card can be dropped into other
    /// projects — the host maps its own theme onto this record.
    /// </summary>
    public sealed record DataCardPalette(
        Color Surface,
        Color SurfaceHover,
        Color Border,
        Color Text,
        Color SecondaryText,
        Color Skeleton)
    {
        public static DataCardPalette Dark { get; } = new(
            Surface: Color.FromArgb(37, 37, 38),
            SurfaceHover: Color.FromArgb(44, 44, 46),
            Border: Color.FromArgb(55, 55, 58),
            Text: Color.FromArgb(240, 240, 240),
            SecondaryText: Color.FromArgb(160, 160, 165),
            Skeleton: Color.FromArgb(58, 58, 62));

        public static DataCardPalette Light { get; } = new(
            Surface: Color.White,
            SurfaceHover: Color.FromArgb(248, 250, 252),
            Border: Color.FromArgb(222, 226, 232),
            Text: Color.FromArgb(33, 33, 33),
            SecondaryText: Color.FromArgb(100, 106, 115),
            Skeleton: Color.FromArgb(229, 231, 235));

        /// <summary>Builds a palette from a host theme's basic colors.</summary>
        public static DataCardPalette From(
            Color surface, Color border, Color text, Color secondaryText, bool isDark)
        {
            return new DataCardPalette(
                Surface: surface,
                SurfaceHover: Shift(surface, isDark ? 8 : -5),
                Border: border,
                Text: text,
                SecondaryText: secondaryText,
                Skeleton: Shift(surface, isDark ? 22 : -18));
        }

        private static Color Shift(Color c, int delta) => Color.FromArgb(
            c.A,
            Math.Clamp(c.R + delta, 0, 255),
            Math.Clamp(c.G + delta, 0, 255),
            Math.Clamp(c.B + delta, 0, 255));
    }

    /// <summary>Ready-made accent colors (Tailwind 500 tones).</summary>
    public static class DataCardAccents
    {
        public static readonly Color Blue = Color.FromArgb(59, 130, 246);
        public static readonly Color Green = Color.FromArgb(16, 185, 129);
        public static readonly Color Sky = Color.FromArgb(14, 165, 233);
        public static readonly Color Amber = Color.FromArgb(245, 158, 11);
        public static readonly Color Red = Color.FromArgb(239, 68, 68);
        public static readonly Color Purple = Color.FromArgb(139, 92, 246);
        public static readonly Color Pink = Color.FromArgb(236, 72, 153);
        public static readonly Color Slate = Color.FromArgb(100, 116, 139);
    }
}
