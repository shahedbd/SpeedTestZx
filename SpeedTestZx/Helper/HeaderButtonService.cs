// Helper/HeaderButtonService.cs
using DpiCommon.Helper;
using FontAwesome.Sharp;

namespace SpeedTestZx.Helper
{
    /// <summary>
    /// Factory service for building consistently styled header action buttons.
    /// All sizing, spacing, hover states, and icon alignment are centralised here.
    /// </summary>
    public static class HeaderButtonService
    {
        // ── Brand palette ─────────────────────────────────────────────────────
        // Derived from AppConfig.PrimaryColor (the app logo's core color) so
        // hover/pressed states stay in sync automatically if that ever changes.
        public static readonly Color BrandColor = AppConfig.PrimaryColor;
        public static readonly Color BrandColorHover = ControlPaint.Dark(AppConfig.PrimaryColor, 0.1f);
        public static readonly Color BrandColorDark = ControlPaint.Dark(AppConfig.PrimaryColor, 0.25f);

        // ── Shared button metrics ─────────────────────────────────────────────
        public static int ButtonHeight => DpiAwareService.Scale(34);
        public static int IconSize => DpiAwareService.Scale(14);
        public static int IconOnlyWidth => DpiAwareService.Scale(40);

        // ── Button gap between each button ────────────────────────────────────
        public static int ButtonGap => DpiAwareService.Scale(6);

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC FACTORY METHODS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Labeled icon button — icon left, text right. Used for Settings,
        /// Small UI, Refresh.
        /// </summary>
        public static IconButton CreateLabeledButton(
            string label,
            IconChar icon,
            EventHandler onClick,
            int widthOverride = 0)
        {
            int w = widthOverride > 0
                ? widthOverride
                : MeasureButtonWidth(label);

            var btn = BuildBase(icon, w);
            btn.Text = label;
            btn.TextImageRelation = TextImageRelation.ImageBeforeText;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.ImageAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(
                DpiAwareService.Scale(8), 0,
                DpiAwareService.Scale(8), 0);

            btn.Click += onClick;
            return btn;
        }

        /// <summary>
        /// Icon-only square button. Used for the theme toggle.
        /// Pass a tooltip text so screen readers and hovering users understand it.
        /// </summary>
        public static IconButton CreateIconOnlyButton(
            IconChar icon,
            string tooltipText,
            EventHandler onClick,
            ToolTip? toolTip = null)
        {
            var btn = BuildBase(icon, IconOnlyWidth);
            btn.Text = string.Empty;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.ImageAlign = ContentAlignment.MiddleCenter;

            if (toolTip != null && !string.IsNullOrEmpty(tooltipText))
                toolTip.SetToolTip(btn, tooltipText);

            btn.Click += onClick;
            return btn;
        }

        /// <summary>
        /// Standard branded Close button for dialog forms.
        /// Matches the app's brand palette with icon + label.
        /// </summary>
        /// <param name="onClick">Click handler to attach.</param>
        /// <param name="widthOverride">Optional fixed width. Defaults to auto-measured.</param>
        public static IconButton CreateCloseButton(
            EventHandler onClick,
            int widthOverride = 0)
        {
            int w = widthOverride > 0
                ? widthOverride
                : MeasureButtonWidth("  Close");

            var btn = BuildBase(IconChar.Times, w);
            btn.Text = "  Close";
            btn.TextImageRelation = TextImageRelation.ImageBeforeText;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.ImageAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(
                DpiAwareService.Scale(6), 0,
                DpiAwareService.Scale(6), 0);

            btn.Click += onClick;
            return btn;
        }

        // ─────────────────────────────────────────────────────────────────────
        // LAYOUT HELPER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates the total pixel width consumed by a button group,
        /// including gaps between them. Use for right-edge offset math.
        /// </summary>
        public static int GroupWidth(IEnumerable<Button> buttons, int rightMargin = 0)
        {
            int total = rightMargin;
            foreach (var btn in buttons)
                total += btn.Width + ButtonGap;
            return total;
        }

        /// <summary>
        /// Positions a list of buttons right-to-left inside a parent panel,
        /// vertically centred. Call after all buttons are sized.
        /// </summary>
        public static void PositionButtonsRightAligned(
            int parentWidth,
            int parentHeight,
            int rightMargin,
            params Button[] buttons)
        {
            int x = parentWidth - rightMargin;
            int centerY = (parentHeight - ButtonHeight) / 2;

            foreach (var btn in buttons)
            {
                x -= btn.Width;
                btn.Location = new Point(x, centerY);
                x -= ButtonGap;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // THEME REFRESH
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Re-applies brand colours and hover states to any header button.
        /// Safe to call on every theme toggle.
        /// </summary>
        public static void ApplyBrandStyle(Button btn)
        {
            if (btn == null) return;
            btn.BackColor = BrandColor;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = BrandColorHover;
            btn.FlatAppearance.MouseDownBackColor = BrandColorDark;
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private static IconButton BuildBase(IconChar icon, int width)
        {
            var btn = new IconButton
            {
                IconChar = icon,
                IconColor = Color.White,
                IconSize = IconSize,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = BrandColor,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(width, ButtonHeight),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = BrandColorHover;
            btn.FlatAppearance.MouseDownBackColor = BrandColorDark;
            return btn;
        }

        /// <summary>
        /// Estimates a comfortable button width from the label string length.
        /// Avoids magic numbers scattered across builders.
        /// </summary>
        public static int MeasureButtonWidth(string label)
        {
            // icon (14) + left pad (8) + ~7px per char + right pad (8)
            int estimated = DpiAwareService.Scale(14 + 8 + label.Length * 7 + 8);
            int minimum = DpiAwareService.Scale(88);
            return Math.Max(estimated, minimum);
        }
    }
}
