using System.Drawing.Drawing2D;
using DpiCommon.Helper;
using FontAwesome.Sharp;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.Prompts
{
    /// <summary>
    /// The inline Pro upsell (T50) that stands in for a Pro-only part of a
    /// page — never a blank lock screen. It says what you get, shows a
    /// preview built from the user's own data ("3 outages in the last
    /// 90 days are ready to view"), and has an "Unlock Pro" button that
    /// opens the purchase prompt for that feature.
    ///
    /// Pages swap it out on their own when the tier changes: MainForm rebuilds
    /// the active page on EntitlementChanged.
    /// </summary>
    public sealed class ProUpsellCard : Panel
    {
        private readonly ProFeature _feature;
        private readonly IconPictureBox _icon;
        private readonly Label _lblTitle, _lblDescription, _lblPreview;
        private readonly IconButton _btnUnlock;

        public ProUpsellCard(ProFeature feature, string preview = "")
        {
            _feature = feature;
            var theme = ThemeManager.Instance.CurrentTheme;

            BackColor = theme.CardBackground;
            DoubleBuffered = true;

            _icon = new IconPictureBox
            {
                IconChar = IconChar.Crown,
                IconColor = PremiumButton.CrownGold,
                IconSize = S(22),
                Size = new Size(S(28), S(28)),
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Enabled = false,
            };
            _lblTitle = MakeLabel(ProPromptContent.FeatureName(feature) + " · Pro", 10.5f, FontStyle.Bold, theme.HeadingColor);
            _lblDescription = MakeLabel(ProPromptContent.FeatureDescription(feature), 9f, FontStyle.Regular, theme.SecondaryTextColor);
            _lblPreview = MakeLabel(preview, 9f, FontStyle.Bold, theme.TextColor);

            _btnUnlock = PremiumButton.Create(new Size(S(150), S(34)), S(15), 9.5f, "Unlock Pro");
            _btnUnlock.Click += (s, e) => PromptManager.Current?.Show(_feature, () => { });

            Controls.AddRange(new Control[] { _icon, _lblTitle, _lblDescription, _lblPreview, _btnUnlock });

            SizeChanged += (s, e) => DoLayout();
            Paint += OnPaintCard;
        }

        /// <summary>The "from your data" line. Empty hides it.</summary>
        public string Preview
        {
            get => _lblPreview.Text;
            set { _lblPreview.Text = value ?? ""; DoLayout(); }
        }

        private static int S(int value) => DpiAwareService.Scale(value);

        private static Label MakeLabel(string text, float size, FontStyle style, Color color) => new()
        {
            Text = text,
            Font = new Font("Segoe UI", size, style),
            ForeColor = color,
            BackColor = Color.Transparent,
            AutoEllipsis = true,
            UseMnemonic = false,
        };

        /// <summary>Centred stack: icon + title, description, preview, button.</summary>
        private void DoLayout()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int pad = S(14);
            int inner = Math.Max(0, w - pad * 2);
            int lineH = S(20);
            bool hasPreview = _lblPreview.Text.Length > 0;

            int descH = MeasureHeight(_lblDescription, inner);
            int previewH = hasPreview ? MeasureHeight(_lblPreview, inner) : 0;
            int blockH = S(28) + S(6) + descH + (hasPreview ? S(6) + previewH : 0) + S(12) + _btnUnlock.Height;
            int y = Math.Max(pad, (h - blockH) / 2);

            int titleW = Math.Min(inner - S(34), TextRenderer.MeasureText(_lblTitle.Text, _lblTitle.Font).Width + S(4));
            int titleX = (w - (S(34) + titleW)) / 2;
            _icon.Location = new Point(titleX, y);
            _lblTitle.SetBounds(titleX + S(34), y + S(4), titleW, lineH);
            y += S(28) + S(6);

            Center(_lblDescription, y, inner, descH);
            y += descH;

            _lblPreview.Visible = hasPreview;
            if (hasPreview)
            {
                y += S(6);
                Center(_lblPreview, y, inner, previewH);
                y += previewH;
            }

            y += S(12);
            _btnUnlock.Location = new Point((w - _btnUnlock.Width) / 2, Math.Min(y, h - _btnUnlock.Height - S(6)));
        }

        private void Center(Label label, int y, int width, int height)
        {
            label.TextAlign = ContentAlignment.TopCenter;
            label.SetBounds((ClientSize.Width - width) / 2, y, width, height);
        }

        /// <summary>Wrapped height, measured a little narrower than the label so the last line is never cut.</summary>
        private static int MeasureHeight(Label label, int width) =>
            TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(1, width - DpiAwareService.Scale(12)), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + DpiAwareService.Scale(4);

        /// <summary>Thin border plus the Pro gradient bar along the top.</summary>
        private void OnPaintCard(object? sender, PaintEventArgs e)
        {
            var theme = ThemeManager.Instance.CurrentTheme;
            using (var pen = new Pen(theme.BorderColor))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            var bar = new Rectangle(0, 0, Math.Max(1, Width), S(3));
            using var brush = new LinearGradientBrush(bar, AppConfig.ProAccentStart, AppConfig.ProAccentEnd,
                LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, bar);
        }
    }
}
