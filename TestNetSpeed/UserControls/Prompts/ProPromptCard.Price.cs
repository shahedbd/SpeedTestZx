using System.Drawing.Drawing2D;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;

namespace TestNetSpeed.UserControls.Prompts
{
    /// <summary>
    /// ProPromptCard — live Store price, CTA busy state and the rounded card
    /// chrome with its gradient top bar.
    /// </summary>
    internal partial class ProPromptCard
    {
        private const string BuyLabel = "  Get Pro - Lifetime";

        // ══════════════════════════════════════════════════════════════════
        //  STORE BADGE
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Microsoft Store badge, loaded once and shared — a PictureBox does not
        /// dispose its Image, so loading per card would leak a handle every time
        /// the prompt is rebuilt.
        /// </summary>
        private static Image? _storeBadge;
        private static bool _storeBadgeLoaded;

        private static Image? StoreBadge
        {
            get
            {
                if (_storeBadgeLoaded) return _storeBadge;
                _storeBadgeLoaded = true;

                try
                {
                    if (File.Exists(AppConfig.MicrosoftStoreAppIconPath))
                        _storeBadge = Image.FromFile(AppConfig.MicrosoftStoreAppIconPath);
                }
                catch
                {
                    // Decorative — a missing or bad asset must not break the prompt.
                }

                return _storeBadge;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PRICE
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Price row: large amount + small "one-time".</summary>
        private Control BuildPriceBlock()
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, S(10))
            };

            _lblPrice = new Label
            {
                Text = ExtractAmount(AppConfig.LifetimeProPrice),
                Font = new Font("Segoe UI", 22f, FontStyle.Bold),
                ForeColor = _theme.CurrentTheme.HeadingColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0)
            };
            row.Controls.Add(_lblPrice);

            row.Controls.Add(new Label
            {
                Text = "one-time",
                Font = new Font("Segoe UI", 10f),
                ForeColor = _theme.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(S(4), S(16), 0, 0)
            });

            return row;
        }

        /// <summary>
        /// Replaces the fallback with the Store's localized price. Never blocks
        /// the card; on any failure the fallback stays.
        /// </summary>
        private async Task LoadPriceAsync()
        {
            try
            {
                string price = await EntitlementManager.Instance.GetFormattedPriceAsync();

                if (!IsDisposed && !_lblPrice.IsDisposed && !string.IsNullOrWhiteSpace(price))
                    _lblPrice.Text = ExtractAmount(price);
            }
            catch
            {
                // Leave the AppConfig fallback in place.
            }
        }

        /// <summary>
        /// Strips any "/period" suffix so only the amount renders beside the
        /// "one-time" label, whatever form the fallback text takes.
        /// </summary>
        private static string ExtractAmount(string price)
        {
            if (string.IsNullOrWhiteSpace(price)) return string.Empty;

            int slash = price.IndexOf('/');
            return (slash >= 0 ? price[..slash] : price).Trim();
        }

        // ══════════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════════

        public bool IsBusy { get; private set; }

        /// <summary>Disables the CTA while the Store dialog is open, so a double click can't start two purchases.</summary>
        public void SetBusy(bool busy)
        {
            IsBusy = busy;
            if (_btnBuy == null || _btnBuy.IsDisposed) return;

            _btnBuy.Enabled = !busy;
            _btnBuy.Text = busy ? "  Opening Microsoft Store..." : BuyLabel;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PAINT — rounded card with a gradient top bar
        // ══════════════════════════════════════════════════════════════════

        private void Card_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = GetRoundedRectangle(ClientRectangle, S(16)))
            {
                using var brush = new SolidBrush(BackColor);
                e.Graphics.FillPath(brush, path);

                using var borderPen = new Pen(_theme.CurrentTheme.BorderColor, 1f);
                e.Graphics.DrawPath(borderPen, path);
            }

            using var topBrush = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, Width), S(4)), AccentStart, AccentEnd, LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(topBrush, S(16), 0, Width - S(32), S(4));
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter - 1, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter - 1, bounds.Bottom - diameter - 1, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter - 1, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}
