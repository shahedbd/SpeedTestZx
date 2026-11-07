using System.Drawing.Drawing2D;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;
using DpiCommon.Helper;
using FontAwesome.Sharp;

namespace SpeedTestZx.UserControls.Prompts
{
    /// <summary>
    /// Small "PRO" pill on a gated sidebar item, so the upgrade path stays
    /// discoverable. Hidden once the user has access.
    ///
    /// Sits on the nav icon's bottom-right corner, like a notification badge,
    /// rather than after the label: "Connection Monitor" fills the expanded
    /// button, and on the icon the badge also stays visible when the sidebar
    /// is collapsed. It follows the icon when SidebarControl moves/resizes it
    /// (collapse, DPI change).
    ///
    /// Painted rather than a Label on purpose: SidebarControl restyles every
    /// Label inside a nav button on each click, collapse and theme change.
    /// </summary>
    public sealed class ProNavBadge : Control
    {
        private static readonly Font BadgeFont = new("Segoe UI", 5.5f, FontStyle.Bold);

        private readonly ProFeature _feature;

        private ProNavBadge(ProFeature feature)
        {
            _feature = feature;

            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
            Size = new Size(DpiAwareService.Scale(24), DpiAwareService.Scale(11));
            Enabled = false; // never swallow the nav button's click

            EntitlementManager.Instance.EntitlementChanged += OnEntitlementChanged;
        }

        /// <summary>Adds a badge to <paramref name="navButton"/>, pinned to its icon's bottom-right corner.</summary>
        public static void Attach(Button navButton, ProFeature feature)
        {
            var badge = new ProNavBadge(feature);
            var icon = navButton.Controls.OfType<IconPictureBox>().FirstOrDefault();

            void Place()
            {
                if (badge.IsDisposed) return;

                badge.Size = new Size(DpiAwareService.Scale(24), DpiAwareService.Scale(11));
                badge.Location = icon is null
                    ? new Point(DpiAwareService.Scale(24), navButton.Height - badge.Height - DpiAwareService.Scale(4))
                    : new Point(icon.Right - badge.Width / 2, icon.Bottom - badge.Height + DpiAwareService.Scale(4));
            }

            navButton.Controls.Add(badge);
            badge.BringToFront();

            Place();
            if (icon != null)
            {
                icon.LocationChanged += (s, e) => Place();
                icon.SizeChanged += (s, e) => Place();
            }
            navButton.Resize += (s, e) => Place();

            badge.UpdateVisibility();
        }

        private void OnEntitlementChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;

            if (InvokeRequired) BeginInvoke(new Action(UpdateVisibility));
            else UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (IsDisposed || Parent is null) return;

            Visible = !EntitlementManager.Instance.HasAccess(_feature);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int d = rect.Height;

            using var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 90, 180);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
            path.CloseFigure();

            using var brush = new LinearGradientBrush(rect, AppConfig.ProAccentStart, AppConfig.ProAccentEnd,
                LinearGradientMode.Horizontal);
            e.Graphics.FillPath(brush, path);

            TextRenderer.DrawText(e.Graphics, "PRO", BadgeFont, rect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                EntitlementManager.Instance.EntitlementChanged -= OnEntitlementChanged;

            base.Dispose(disposing);
        }
    }
}
