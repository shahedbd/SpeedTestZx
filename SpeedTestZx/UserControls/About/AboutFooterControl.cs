using System.Diagnostics;
using DpiCommon.Helper;
using FontAwesome.Sharp;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;

namespace SpeedTestZx.UserControls.About
{
    /// <summary>
    /// About screen footer: divider, website link, support address with a copy
    /// action, the Close button, and the developer/copyright lines. Ported from
    /// CPU ZX Pro's AboutFooterControl (which drops Close; it is kept here so
    /// the dialog still shows everything it did before).
    /// </summary>
    public class AboutFooterControl : Panel
    {
        private readonly ThemeManager _theme;

        private Panel? _divider;
        private IconPictureBox? _globe;
        private IconPictureBox? _mail;
        private LinkLabel? _lnkWebsite;
        private Label? _lblSupport;
        private IconButton? _btnCopyEmail;
        private IconButton? _btnClose;
        private Label? _lblDeveloper;
        private Label? _lblCopyright;
        private readonly ToolTip _tips = new();

        /// <summary>The Close button — the dialog uses it as its CancelButton.</summary>
        public IButtonControl? CloseButton => _btnClose;

        public AboutFooterControl(ThemeManager theme, int width, EventHandler onClose)
        {
            _theme = theme;

            Width = width;
            BackColor = Color.Transparent;
            Margin = new Padding(0, 0, 0, S(12));

            Build(onClose);
        }

        private static int S(int value) => DpiAwareService.Scale(value);

        // ─────────────────────────────────────────────────────────────────────
        // BUILD
        // ─────────────────────────────────────────────────────────────────────

        private void Build(EventHandler onClose)
        {
            var theme = _theme.CurrentTheme;
            int y = 0;

            _divider = new Panel { Size = new Size(Width, S(1)), Location = new Point(0, y) };
            Controls.Add(_divider);
            y += S(14);

            y = AddWebsite(y);
            y = AddSupport(y);

            _btnClose = HeaderButtonService.CreateCloseButton(onClose, S(90));
            _btnClose.Location = new Point((Width - _btnClose.Width) / 2, y + S(4));
            Controls.Add(_btnClose);
            y += _btnClose.Height + S(18);

            _lblDeveloper = CenteredLabel($"Developed by {AppConfig.DeveloperName}",
                new Font("Segoe UI", 8.5f, FontStyle.Bold), theme.TextColor, y);
            Controls.Add(_lblDeveloper);
            y += S(20);

            _lblCopyright = CenteredLabel($"© {DateTime.Now.Year} {AppConfig.DeveloperName}. All rights reserved.",
                new Font("Segoe UI", 8f), theme.SecondaryTextColor, y);
            Controls.Add(_lblCopyright);

            // Breathing room under the last line so it doesn't sit on the edge.
            Height = y + S(18) + S(16);
        }

        /// <summary>Globe + link, centred as one block.</summary>
        private int AddWebsite(int y)
        {
            _globe = RowIcon(IconChar.Globe);

            _lnkWebsite = new LinkLabel
            {
                Text = AppConfig.WebsiteUrl.Replace("https://", "").TrimEnd('/'),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            _lnkWebsite.LinkClicked += (s, e) => OpenUrl(AppConfig.WebsiteUrl);

            int labelWidth = TextRenderer.MeasureText(_lnkWebsite.Text, _lnkWebsite.Font).Width;
            int x = (Width - (_globe.Width + S(4) + labelWidth)) / 2;
            _globe.Location = new Point(x, y);
            _lnkWebsite.Location = new Point(_globe.Right + S(4), y + S(2));

            Controls.Add(_globe);
            Controls.Add(_lnkWebsite);
            return y + S(26);
        }

        /// <summary>Envelope + support address + copy action, centred as one block.</summary>
        private int AddSupport(int y)
        {
            _mail = RowIcon(IconChar.Envelope);

            _lblSupport = new Label
            {
                Text = AppConfig.SupportEmail,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };

            _btnCopyEmail = new IconButton
            {
                IconChar = IconChar.Copy,
                IconSize = S(13),
                Text = string.Empty,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Size = new Size(S(24), S(22)),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _btnCopyEmail.FlatAppearance.BorderSize = 0;
            _btnCopyEmail.Click += (s, e) => CopyEmail();
            _tips.SetToolTip(_btnCopyEmail, "Copy email address");

            // Measured rather than read off the label: AutoSize width isn't
            // settled until the label has a parent.
            int labelWidth = TextRenderer.MeasureText(_lblSupport.Text, _lblSupport.Font).Width;
            int blockWidth = _mail.Width + S(4) + labelWidth + S(2) + _btnCopyEmail.Width;
            int x = (Width - blockWidth) / 2;

            _mail.Location = new Point(x, y);
            _lblSupport.Location = new Point(_mail.Right + S(4), y + S(2));
            _btnCopyEmail.Location = new Point(_lblSupport.Left + labelWidth + S(2), y - S(1));

            Controls.Add(_mail);
            Controls.Add(_lblSupport);
            Controls.Add(_btnCopyEmail);
            return y + S(30);
        }

        private static void CopyEmail()
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

        private static IconPictureBox RowIcon(IconChar icon) => new()
        {
            IconChar = icon,
            // Size before IconSize — IconPictureBox resizes to IconSize.
            Size = new Size(S(18), S(20)),
            IconSize = S(14),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.Transparent,
            Enabled = false
        };

        private Label CenteredLabel(string text, Font font, Color color, int y) => new()
        {
            Text = text,
            Font = font,
            ForeColor = color,
            BackColor = Color.Transparent,
            AutoSize = false,
            UseMnemonic = false,
            Size = new Size(Width, S(18)),
            Location = new Point(0, y),
            TextAlign = ContentAlignment.MiddleCenter
        };

        // ─────────────────────────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────────────────────────

        public void ApplyTheme()
        {
            var theme = _theme.CurrentTheme;

            if (_divider != null) _divider.BackColor = theme.BorderColor;
            if (_globe != null) _globe.IconColor = theme.AccentColor;
            if (_mail != null) _mail.IconColor = theme.SecondaryTextColor;
            if (_lblSupport != null) _lblSupport.ForeColor = theme.SecondaryTextColor;
            if (_lblDeveloper != null) _lblDeveloper.ForeColor = theme.TextColor;
            if (_lblCopyright != null) _lblCopyright.ForeColor = theme.SecondaryTextColor;

            if (_lnkWebsite != null)
            {
                _lnkWebsite.LinkColor = theme.AccentColor;
                _lnkWebsite.ActiveLinkColor = theme.AccentColor;
                _lnkWebsite.VisitedLinkColor = theme.AccentColor;
            }

            if (_btnCopyEmail != null)
            {
                _btnCopyEmail.IconColor = theme.AccentColor;
                _btnCopyEmail.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, theme.AccentColor);
                _btnCopyEmail.FlatAppearance.MouseDownBackColor = Color.FromArgb(60, theme.AccentColor);
            }

            if (_btnClose != null) HeaderButtonService.ApplyBrandStyle(_btnClose);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
