using System.Diagnostics;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using DpiCommon.Helper;
using FontAwesome.Sharp;

namespace TestNetSpeed.Forms
{
    /// <summary>
    /// Reusable About dialog — icon, name, version, description, a feature
    /// list, website/support contact, and developer credit. Everything shown
    /// here comes from AppConfig's "About dialog content" fields — nothing in
    /// this file should need editing when bootstrapping a new app.
    /// </summary>
    public class AboutForm : Form
    {
        private readonly ThemeManager _themeManager;

        // Tracked for ApplyTheme — avoids a control-tree walk.
        private Label _lblAppName;
        private Panel _versionBadge;
        private Label _lblVersion;
        private Panel _featuresCard;
        private Panel _featuresHeader;
        private IconPictureBox _featuresIcon;
        private Panel _contactPanel;
        private IconPictureBox _websiteIcon;
        private IconPictureBox _emailIcon;
        private LinkLabel _linkWebsite;
        private Button _btnCopyEmail;
        private Button _btnClose;
        private readonly List<Panel> _dividers = new();

        public AboutForm()
        {
            _themeManager = ThemeManager.Instance;

            InitializeComponent();
            BuildLayout();
            ApplyTheme();

            _themeManager.ThemeChanged += OnThemeChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        // INIT
        // ─────────────────────────────────────────────────────────────────────

        private void InitializeComponent()
        {
            SuspendLayout();

            ClientSize = DpiAwareService.ScaledSize(440, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = File.Exists(AppConfig.FaviconPath) ? new System.Drawing.Icon(AppConfig.FaviconPath) : null;
            StartPosition = FormStartPosition.CenterParent;
            Text = $"About {AppConfig.AppName}";
            AutoScaleMode = AutoScaleMode.Dpi;

            ResumeLayout(false);
        }

        private void BuildLayout()
        {
            int width = ClientSize.Width;
            int contentWidth = DpiAwareService.Scale(380);
            int contentX = (width - contentWidth) / 2;
            int y = DpiAwareService.Scale(20);

            // ── App icon ─────────────────────────────────────────────────────
            int iconSize = DpiAwareService.Scale(64);
            Controls.Add(new PictureBox
            {
                Image = File.Exists(AppConfig.AppIconPath) ? Image.FromFile(AppConfig.AppIconPath) : null,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(iconSize, iconSize),
                Location = new Point((width - iconSize) / 2, y),
                BackColor = Color.Transparent
            });
            y += iconSize + DpiAwareService.Scale(12);

            // ── App name ─────────────────────────────────────────────────────
            _lblAppName = new Label
            {
                Text = AppConfig.AppName,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            CenterHorizontally(_lblAppName, width, y);
            Controls.Add(_lblAppName);
            y += DpiAwareService.Scale(34);

            // ── Version badge ────────────────────────────────────────────────
            Size badgeSize = DpiAwareService.ScaledSize(90, 22);
            _versionBadge = new Panel { Size = badgeSize, Location = new Point((width - badgeSize.Width) / 2, y) };
            _lblVersion = new Label
            {
                Text = AppConfig.AppVersion,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            _versionBadge.Controls.Add(_lblVersion);
            Controls.Add(_versionBadge);
            y += badgeSize.Height + DpiAwareService.Scale(16);

            // ── Description ──────────────────────────────────────────────────
            var lblDescription = new Label
            {
                Text = AppConfig.AppDescription,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = false,
                TextAlign = ContentAlignment.TopCenter,
                Size = new Size(contentWidth, DpiAwareService.Scale(40)),
                Location = new Point(contentX, y),
                BackColor = Color.Transparent
            };
            Controls.Add(lblDescription);
            y += DpiAwareService.Scale(40);

            y = AddDivider(contentX, contentWidth, y);

            // ── Features card ────────────────────────────────────────────────
            int headerHeight = DpiAwareService.Scale(32);
            int rowHeight = DpiAwareService.Scale(20);
            var features = AppConfig.AboutFeatures ?? new List<string>();
            int cardHeight = headerHeight + DpiAwareService.Scale(8) + features.Count * rowHeight + DpiAwareService.Scale(8);

            _featuresCard = new Panel { Location = new Point(contentX, y), Size = new Size(contentWidth, cardHeight) };

            _featuresHeader = new Panel { Dock = DockStyle.Top, Height = headerHeight };
            _featuresIcon = new IconPictureBox
            {
                IconChar = IconChar.Star,
                IconSize = DpiAwareService.Scale(16),
                Size = DpiAwareService.ScaledSize(16, 16),
                Location = DpiAwareService.ScaledLocation(12, 8),
                BackColor = Color.Transparent
            };
            var lblFeaturesTitle = new Label
            {
                Text = "Key Features",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = DpiAwareService.ScaledLocation(34, 7),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _featuresHeader.Controls.Add(_featuresIcon);
            _featuresHeader.Controls.Add(lblFeaturesTitle);
            _featuresCard.Controls.Add(_featuresHeader);

            int featureY = headerHeight + DpiAwareService.Scale(8);
            foreach (var feature in features)
            {
                _featuresCard.Controls.Add(new Label
                {
                    Text = "•  " + feature,
                    Font = new Font("Segoe UI", 8.5f),
                    AutoSize = false,
                    Size = new Size(contentWidth - DpiAwareService.Scale(20), rowHeight),
                    Location = new Point(DpiAwareService.Scale(14), featureY),
                    BackColor = Color.Transparent
                });
                featureY += rowHeight;
            }

            Controls.Add(_featuresCard);
            y += cardHeight + DpiAwareService.Scale(12);

            y = AddDivider(contentX, contentWidth, y);

            // ── Contact (website + support email) ───────────────────────────
            int contactHeight = DpiAwareService.Scale(48);
            _contactPanel = new Panel { Location = new Point(contentX, y), Size = new Size(contentWidth, contactHeight) };

            // ── Row 1: website — icon + link ──────────────────────────────────
            _websiteIcon = new IconPictureBox
            {
                IconChar = IconChar.Globe,
                IconSize = DpiAwareService.Scale(14),
                Size = DpiAwareService.ScaledSize(16, 16),
                Location = DpiAwareService.ScaledLocation(4, 5),
                BackColor = Color.Transparent
            };
            _linkWebsite = new LinkLabel
            {
                Text = AppConfig.WebsiteUrl,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = DpiAwareService.ScaledLocation(28, 4),
                AutoSize = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _linkWebsite.LinkClicked += (s, e) => OpenUrl(AppConfig.WebsiteUrl);

            // ── Row 2: email — icon + text + copy button ──────────────────────
            _emailIcon = new IconPictureBox
            {
                IconChar = IconChar.Envelope,
                IconSize = DpiAwareService.Scale(14),
                Size = DpiAwareService.ScaledSize(16, 16),
                Location = DpiAwareService.ScaledLocation(4, 25),
                BackColor = Color.Transparent
            };
            var lblEmail = new Label
            {
                Text = AppConfig.SupportEmail,
                Font = new Font("Segoe UI", 8.5f),
                Location = DpiAwareService.ScaledLocation(28, 24),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            // Copy email — icon-only, right after the email text. Measured
            // via TextRenderer rather than lblEmail.Right/.Width — AutoSize
            // labels aren't guaranteed to have their preferred size settled
            // yet at this point (no handle/parent), same reason
            // CenterHorizontally() below uses TextRenderer instead of
            // Label.Width.
            int emailTextWidth = TextRenderer.MeasureText(AppConfig.SupportEmail, lblEmail.Font).Width;
            _btnCopyEmail = new Button
            {
                Text = "📋",
                Font = new Font("Segoe UI Emoji", 8f),
                Size = DpiAwareService.ScaledSize(22, 20),
                Location = new Point(
                    lblEmail.Left + emailTextWidth + DpiAwareService.Scale(6),
                    lblEmail.Top - DpiAwareService.Scale(2)),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            _btnCopyEmail.FlatAppearance.BorderSize = 1;
            new ToolTip().SetToolTip(_btnCopyEmail, "Copy email address");
            _btnCopyEmail.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(AppConfig.SupportEmail);
                    NotificationManager.Instance.ShowSuccess("Email address copied to clipboard.");
                }
                catch (Exception ex)
                {
                    NotificationManager.Instance.ShowError($"Could not copy email.\n{ex.Message}");
                }
            };

            _contactPanel.Controls.Add(_websiteIcon);
            _contactPanel.Controls.Add(_linkWebsite);
            _contactPanel.Controls.Add(_emailIcon);
            _contactPanel.Controls.Add(lblEmail);
            _contactPanel.Controls.Add(_btnCopyEmail);
            Controls.Add(_contactPanel);
            y += contactHeight;

            y = AddDivider(contentX, contentWidth, y);

            // ── Close button ──────────────────────────────────────────────────
            _btnClose = HeaderButtonService.CreateCloseButton((s, e) => Close(), DpiAwareService.Scale(90));
            _btnClose.Location = new Point((width - _btnClose.Width) / 2, y);
            Controls.Add(_btnClose);
            y += _btnClose.Height + DpiAwareService.Scale(16);

            // ── Developer + copyright ────────────────────────────────────────
            var lblDeveloper = new Label
            {
                Text = $"Developed by {AppConfig.DeveloperName}",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            CenterHorizontally(lblDeveloper, width, y);
            Controls.Add(lblDeveloper);
            y += DpiAwareService.Scale(20);

            var lblCopyright = new Label
            {
                Text = $"© {DateTime.Now.Year} {AppConfig.DeveloperName}. All rights reserved.",
                Font = new Font("Segoe UI", 8f),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            CenterHorizontally(lblCopyright, width, y);
            Controls.Add(lblCopyright);
            y += DpiAwareService.Scale(28);

            ClientSize = new Size(width, y);
        }

        private int AddDivider(int x, int w, int y)
        {
            var divider = new Panel { Location = new Point(x, y), Size = new Size(w, DpiAwareService.Scale(1)) };
            _dividers.Add(divider);
            Controls.Add(divider);
            return y + DpiAwareService.Scale(16);
        }

        private static void CenterHorizontally(Label label, int containerWidth, int y)
        {
            int textWidth = TextRenderer.MeasureText(label.Text, label.Font).Width;
            label.Location = new Point((containerWidth - textWidth) / 2, y);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AlertService.ShowError($"Unable to open link: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyTheme()
        {
            var theme = _themeManager.CurrentTheme;

            BackColor = theme.ContentBackground;

            _lblAppName.ForeColor = theme.AccentColor;

            _versionBadge.BackColor = theme.AccentColor;
            _lblVersion.ForeColor = Color.White;

            _featuresCard.BackColor = theme.CardBackground;
            _featuresHeader.BackColor = Color.FromArgb(20, theme.AccentColor);
            _featuresIcon.IconColor = theme.AccentColor;

            _contactPanel.BackColor = Color.Transparent;
            _websiteIcon.IconColor = theme.AccentColor;
            _emailIcon.IconColor = theme.AccentColor;
            _linkWebsite.LinkColor = theme.AccentColor;
            _linkWebsite.ActiveLinkColor = theme.AccentColor;
            _linkWebsite.VisitedLinkColor = theme.AccentColor;

            _btnCopyEmail.BackColor = _themeManager.IsDarkTheme
                ? Color.FromArgb(50, 50, 60)
                : Color.FromArgb(239, 246, 255);
            _btnCopyEmail.ForeColor = theme.TextColor;
            _btnCopyEmail.FlatAppearance.BorderColor = theme.AccentColor;
            _btnCopyEmail.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, theme.AccentColor);

            HeaderButtonService.ApplyBrandStyle(_btnClose);

            foreach (var divider in _dividers)
                divider.BackColor = theme.BorderColor;

            // Text colors — everything else defaults to TextColor; the
            // handful of already-themed labels above are skipped.
            ApplyLabelColors(this, theme);
        }

        private void ApplyLabelColors(Control parent, AppTheme theme)
        {
            foreach (Control control in parent.Controls)
            {
                switch (control)
                {
                    // LinkLabel derives from Label — must come first, it's
                    // already themed (LinkColor etc.) above.
                    case LinkLabel:
                        break;
                    case Label lbl when lbl == _lblAppName || lbl == _lblVersion:
                        break; // already themed above
                    case Label lbl:
                        lbl.ForeColor = theme.TextColor;
                        break;
                }

                if (control.Controls.Count > 0)
                    ApplyLabelColors(control, theme);
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme();
            Invalidate(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _themeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
