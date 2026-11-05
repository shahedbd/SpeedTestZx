// UserControls/Layout/TitleBarControl.Theme.cs
using TestNetSpeed.Helper;
using DpiCommon.Helper;

namespace TestNetSpeed.UserControls.Layout
{
    /// <summary>
    /// TitleBarControl — gradient colours and DPI refresh.
    /// </summary>
    public partial class TitleBarControl
    {
        /// <summary>
        /// Re-applies the caption colours. Both themes derive the bar from the
        /// app icon's base colour (AppConfig.PrimaryColor): dark mode blends it
        /// toward black for a deep gradient, light mode toward white for a pale
        /// one. So the bar follows the brand colour automatically, with no
        /// separate title-bar colour to maintain.
        ///
        /// The ramp runs left-to-right from the washed-out end into the deep
        /// brand colour on the right, so the caption buttons sit on the
        /// strongest part of the bar.
        ///
        /// Text contrast is measured against the gradient's LIGHTEST stop —
        /// the left one here, in both themes — so the worst case for
        /// readability is what decides the caption colour.
        /// </summary>
        public void ApplyTheme()
        {
            bool isDark = _themeManager.IsDarkTheme;

            if (isDark)
            {
                _gradientStart = Blend(AppConfig.PrimaryColor, Color.Black, 0.75);
                _gradientEnd = Blend(AppConfig.PrimaryColor, Color.Black, 0.45);
            }
            else
            {
                _gradientStart = Blend(AppConfig.PrimaryColor, Color.White, 0.82);
                _gradientEnd = Blend(AppConfig.PrimaryColor, Color.White, 0.55);
            }

            // Flat fallback matching the left stop; the visible bar is painted
            // in OnPaint. Repaint because the stops just changed.
            BackColor = _gradientStart;
            Invalidate();

            // The caption bar must NOT take a solid colour — it would sit as an
            // opaque block over the gradient it is meant to sit on.
            if (_captionButtonBar != null)
                _captionButtonBar.BackColor = Color.Transparent;

            Color contrastBase = isDark ? _gradientEnd : _gradientStart;
            _captionForeColor = GetContrastColor(contrastBase);

            if (_lblTitle != null)
            {
                _lblTitle.BackColor = Color.Transparent;
                _lblTitle.ForeColor = _captionForeColor;
            }

            foreach (var btn in new[] { _btnMinimize, _btnClose })
            {
                if (btn == null) continue;

                btn.BackColor = Color.Transparent;
                btn.ForeColor = _captionForeColor;
            }

            // Close keeps its own red hover (set in BuildLayout); only the
            // minimize hover has to track the bar colour.
            if (_btnMinimize != null)
                _btnMinimize.FlatAppearance.MouseOverBackColor = GetHoverColor(contrastBase);
        }

        /// <summary>Linear blend of <paramref name="from"/> toward <paramref name="to"/> by <paramref name="amount"/> (0..1).</summary>
        private static Color Blend(Color from, Color to, double amount)
        {
            return Color.FromArgb(
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }

        /// <summary>White on dark/saturated backgrounds, near-black on light ones.</summary>
        private static Color GetContrastColor(Color backColor)
        {
            // Perceived luminance (Rec. 601) — catches saturated colours like
            // the blue and red brand shades that a plain average would rate
            // mid-range and then pick the wrong text colour for.
            double luminance = (backColor.R * 0.299 + backColor.G * 0.587 + backColor.B * 0.114) / 255.0;
            return luminance < 0.6 ? Color.White : Color.FromArgb(24, 24, 24);
        }

        /// <summary>
        /// Nudges a colour toward white or black (whichever contrasts more) for
        /// a hover state. The bar derives from AppConfig.PrimaryColor, which
        /// can be set to anything, so this cannot assume a light or dark
        /// shade the way a fixed literal could.
        /// </summary>
        private static Color GetHoverColor(Color baseColor)
        {
            int brightness = (baseColor.R + baseColor.G + baseColor.B) / 3;
            int delta = brightness < 128 ? 30 : -30;

            return Color.FromArgb(
                Math.Clamp(baseColor.R + delta, 0, 255),
                Math.Clamp(baseColor.G + delta, 0, 255),
                Math.Clamp(baseColor.B + delta, 0, 255));
        }

        // ─────────────────────────────────────────────────────────────────────
        // DPI REFRESH
        // ─────────────────────────────────────────────────────────────────────

        public void RefreshForDpi()
        {
            Height = DpiAwareService.Scale(AppConfig.TitleBarHeight);
            _captionButtonBar.Width = DpiAwareService.Scale(96);
            _iconPanel.Width = DpiAwareService.Scale(40);

            // Height is handled automatically by Dock=Right; only Width needs
            // updating here.
            foreach (var btn in new[] { _btnMinimize, _btnClose })
                btn.Width = DpiAwareService.Scale(45);

            // Icon is absolutely positioned inside its panel, so it has to be
            // re-centred for the new size.
            LayoutAppIcon();

            // The gradient spans ClientRectangle, which just changed.
            Invalidate();
        }
    }
}
