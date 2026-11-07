using DpiCommon.Helper;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;

namespace TestNetSpeed.UserControls.About
{
    /// <summary>
    /// About screen header: app icon, name, version and edition badges, the
    /// subtitle and description. Ported from CPU ZX Pro's AboutHeaderControl;
    /// the edition badge is passed in so the Free and Pro screens share it.
    /// </summary>
    public class AboutHeaderControl : Panel
    {
        private readonly ThemeManager _theme;

        private PictureBox? _pbIcon;
        private Label? _lblAppName;
        private Label? _lblTagline;
        private Label? _lblDescription;
        private Panel? _versionBadge;
        private Label? _lblVersion;

        /// <param name="editionLabel">FREE or PRO.</param>
        public AboutHeaderControl(ThemeManager theme, int width, string editionLabel, Color editionColor)
        {
            _theme = theme;

            Width = width;
            BackColor = Color.Transparent;
            Margin = new Padding(0, 0, 0, S(6));

            Build(editionLabel, editionColor);
        }

        private static int S(int value) => DpiAwareService.Scale(value);

        // ─────────────────────────────────────────────────────────────────────
        // BUILD
        // ─────────────────────────────────────────────────────────────────────

        private void Build(string editionLabel, Color editionColor)
        {
            var theme = _theme.CurrentTheme;
            int y = S(4);

            int iconSize = S(64);
            _pbIcon = new PictureBox
            {
                Image = File.Exists(AppConfig.AppIconPath) ? Image.FromFile(AppConfig.AppIconPath) : null,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(iconSize, iconSize),
                Location = new Point((Width - iconSize) / 2, y),
                BackColor = Color.Transparent
            };
            Controls.Add(_pbIcon);
            y += iconSize + S(6);

            _lblAppName = CenteredLabel(AppConfig.AppName,
                new Font("Segoe UI", 20f, FontStyle.Bold), theme.HeadingColor, y);
            Controls.Add(_lblAppName);
            y += _lblAppName.Height + S(8);

            // ── Version + edition badges, side by side ────────────────────────
            _versionBadge = BuildBadge(AppConfig.AppVersion.Replace("Version ", "v"),
                theme.SecondaryTextColor, out _lblVersion);
            var editionBadge = BuildBadge(editionLabel, editionColor, out _);

            int gap = S(8);
            int startX = (Width - (_versionBadge.Width + gap + editionBadge.Width)) / 2;
            _versionBadge.Location = new Point(startX, y);
            editionBadge.Location = new Point(startX + _versionBadge.Width + gap, y);

            Controls.Add(_versionBadge);
            Controls.Add(editionBadge);
            y += editionBadge.Height + S(14);

            _lblTagline = CenteredLabel(AppConfig.AppSubtitle,
                new Font("Segoe UI", 10f, FontStyle.Bold), theme.TextColor, y);
            Controls.Add(_lblTagline);
            y += _lblTagline.Height + S(2);

            _lblDescription = CenteredLabel(AppConfig.AppDescription,
                new Font("Segoe UI", 8.5f), theme.SecondaryTextColor, y);
            Controls.Add(_lblDescription);
            y += _lblDescription.Height + S(10);

            Height = y;
        }

        /// <summary>Pill badge — tinted background, coloured bold text.</summary>
        private static Panel BuildBadge(string text, Color accent, out Label label)
        {
            var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            int width = TextRenderer.MeasureText(text, font).Width + S(20);

            var badge = new Panel
            {
                Size = new Size(width, S(22)),
                BackColor = Color.FromArgb(38, accent)
            };

            label = new Label
            {
                Text = text,
                Font = font,
                ForeColor = accent,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };
            badge.Controls.Add(label);

            return badge;
        }

        /// <summary>
        /// Full-width centred label, sized to the text it actually renders
        /// (wrapping to the header width) — a fixed height clipped descenders
        /// on the large app-name font and cut long descriptions.
        /// </summary>
        private Label CenteredLabel(string text, Font font, Color color, int y)
        {
            var measured = TextRenderer.MeasureText(text, font,
                new Size(Width, int.MaxValue), TextFormatFlags.WordBreak);

            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(Width, measured.Height + S(6)),
                Location = new Point(0, y),
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────────────────────────

        public void ApplyTheme()
        {
            var theme = _theme.CurrentTheme;

            if (_lblAppName != null) _lblAppName.ForeColor = theme.HeadingColor;
            if (_lblTagline != null) _lblTagline.ForeColor = theme.TextColor;
            if (_lblDescription != null) _lblDescription.ForeColor = theme.SecondaryTextColor;
            if (_lblVersion != null) _lblVersion.ForeColor = theme.SecondaryTextColor;
            if (_versionBadge != null) _versionBadge.BackColor = Color.FromArgb(38, theme.SecondaryTextColor);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _pbIcon?.Image?.Dispose();
            base.Dispose(disposing);
        }
    }
}
