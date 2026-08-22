using System.Diagnostics;
using System.Drawing.Drawing2D;
using DpiCommon.Helper;
using FontAwesome.Sharp;
using TestNetSpeed.Helper;

namespace TestNetSpeed.UserControls.Pages
{
    /// <summary>
    /// "Our Apps" nav page — cross-promotes the developer's other Store
    /// apps (AppConfig.OurApps). New page (not a WiFi port); no equivalent
    /// visible gallery existed there — WiFiPasswordViewer's only cross-promo
    /// mechanism is StartupService's silent background Store-page opens on
    /// install/every-15-days, which is unrelated and untouched by this.
    /// </summary>
    public class OurAppsControl : DpiAwareUserControl
    {
        private Panel _listContainer;

        private const int ROW_HEIGHT = 80;
        private const int HEADER_H = 50;

        public OurAppsControl()
        {
            this.BackColor = themeManager.CurrentTheme.ContentBackground;
            this.Padding = ScaledPadding(12);

            BuildLayout();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD LAYOUT
        // ══════════════════════════════════════════════════════════════════════

        private void BuildLayout()
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = themeManager.CurrentTheme.CardBackground,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(card);

            BuildHeader(card);
            BuildListContainer(card);
            RenderRows();
        }

        // ── Header ────────────────────────────────────────────────────────────

        private void BuildHeader(Panel card)
        {
            var header = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(card.Width, Scale(HEADER_H)),
                BackColor = themeManager.IsDarkTheme
                    ? Color.FromArgb(40, 40, 40)
                    : Color.FromArgb(241, 245, 249),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(header);

            header.Controls.Add(new Label
            {
                Text = "Our Apps",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.HeadingColor,
                BackColor = Color.Transparent,
                Location = ScaledPoint(14, 7),
                AutoSize = true
            });

            header.Controls.Add(new Label
            {
                Text = $"More tools from {AppConfig.DeveloperName}",
                Font = new Font("Segoe UI", 8f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                Location = ScaledPoint(14, 28),
                AutoSize = true
            });
        }

        // ── Scrollable list container ─────────────────────────────────────────

        private void BuildListContainer(Panel card)
        {
            _listContainer = new Panel
            {
                Location = new Point(0, Scale(HEADER_H)),
                Size = new Size(card.Width, Math.Max(card.Height - Scale(HEADER_H), 0)),
                AutoScroll = true,
                BackColor = themeManager.CurrentTheme.CardBackground,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(_listContainer);

            card.SizeChanged += (s, e) =>
            {
                int t = Scale(HEADER_H);
                _listContainer.Location = new Point(0, t);
                _listContainer.Size = new Size(card.Width, Math.Max(card.Height - t, 0));
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ROWS
        // ══════════════════════════════════════════════════════════════════════

        private void RenderRows()
        {
            // TestHistoryControl's row loop wraps its Controls.Add calls in
            // Suspend/ResumeLayout — this one didn't, and children added
            // deep in a 17-iteration loop without it could pick up anchor
            // margins mid-layout-thrash instead of against settled bounds.
            _listContainer.SuspendLayout();

            var apps = AppConfig.OurApps;
            int y = 0;
            for (int i = 0; i < apps.Count; i++)
            {
                var row = BuildRow(apps[i], i, y);
                _listContainer.Controls.Add(row);
                y += row.Height;
            }

            _listContainer.ResumeLayout();
        }

        private Panel BuildRow(OurApp app, int index, int y)
        {
            bool isEven = index % 2 == 0;
            Color rowBg = isEven
                ? themeManager.CurrentTheme.CardBackground
                : (themeManager.IsDarkTheme
                    ? Color.FromArgb(38, 38, 38)
                    : Color.FromArgb(248, 250, 252));

            var row = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(_listContainer.Width - Scale(2), Scale(ROW_HEIGHT)),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = rowBg
            };

            // ── Icon "logo" — a colored circle with the app's category
            // icon. No real per-app logo art is available locally, so this
            // stands in for one, matching the colorful-icon language this
            // app already uses (nav sidebar, StatTileControl).
            int circleSize = Scale(44);
            var iconCircle = new Panel
            {
                Location = new Point(Scale(14), (Scale(ROW_HEIGHT) - circleSize) / 2),
                Size = new Size(circleSize, circleSize),
                BackColor = app.IconColor
            };
            MakeCircle(iconCircle);
            iconCircle.Controls.Add(new IconPictureBox
            {
                IconChar = app.Icon,
                IconColor = Color.White,
                IconSize = (int)(circleSize * 0.5),
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Enabled = false
            });
            row.Controls.Add(iconCircle);

            // "Get from Microsoft Store" + icon needs real width — sized
            // for that text at this font, not the old bare "Get" label.
            int btnW = Scale(190);
            int textX = Scale(14) + circleSize + Scale(12);
            int textW = Math.Max(row.Width - textX - btnW - Scale(24), Scale(60));

            var lblName = new Label
            {
                Text = app.Name,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.TextColor,
                BackColor = Color.Transparent,
                Location = new Point(textX, Scale(16)),
                Size = new Size(textW, Scale(18)),
                // Row panels stretch to their real width once docked (via
                // their own Anchor below in RenderRows/BuildRow), but that
                // only resizes the row itself — children need their own
                // Anchor to follow, otherwise they stay stuck at the tiny
                // width the row had before this page was docked into its
                // real container, clipping name/description to a few chars.
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };
            row.Controls.Add(lblName);

            var lblDesc = new Label
            {
                Text = app.Description,
                Font = new Font("Segoe UI", 8f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                Location = new Point(textX, Scale(36)),
                Size = new Size(textW, Scale(32)),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };
            row.Controls.Add(lblDesc);

            // Install-count badge — social proof, using the real
            // Acquisitions numbers behind the sort order. Omitted (not
            // "0 installs") when there's no data; the button's own position
            // stays fixed either way so rows line up consistently.
            if (app.Acquisitions > 0)
            {
                row.Controls.Add(new Label
                {
                    Text = FormatInstallCount(app.Acquisitions),
                    Font = new Font("Segoe UI", 7.5f),
                    ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                    BackColor = Color.Transparent,
                    Location = new Point(row.Width - btnW - Scale(14), Scale(16)),
                    Size = new Size(btnW, Scale(14)),
                    TextAlign = ContentAlignment.MiddleRight,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                });
            }

            var btnGet = new Button
            {
                Text = "Get from Microsoft Store",
                Image = GetStoreIcon(),
                ImageAlign = ContentAlignment.MiddleCenter,
                TextAlign = ContentAlignment.MiddleCenter,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Size = new Size(btnW, Scale(30)),
                Location = new Point(row.Width - btnW - Scale(14), Scale(34)),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppConfig.PrimaryColor,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            btnGet.FlatAppearance.BorderSize = 0;
            btnGet.Click += (s, e) => OpenStoreLink(app.StoreLink);
            row.Controls.Add(btnGet);

            row.MouseEnter += (s, e) => row.BackColor = themeManager.IsDarkTheme
                ? Color.FromArgb(50, 50, 60)
                : Color.FromArgb(235, 242, 255);
            row.MouseLeave += (s, e) => row.BackColor = rowBg;

            return row;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════════════════════════════

        // Loaded once and reused across all 17 button instances rather than
        // re-reading the .ico file per row.
        private static Image _storeIcon;

        private static Image GetStoreIcon()
        {
            if (_storeIcon != null) return _storeIcon;

            try
            {
                if (File.Exists(AppConfig.MsStoreIconPath))
                {
                    using var icon = new System.Drawing.Icon(AppConfig.MsStoreIconPath);
                    _storeIcon = DpiAwareService.ScaleIcon(icon, 16);
                }
            }
            catch
            {
                // Missing/corrupt icon file — button still works, just
                // without the image.
            }

            return _storeIcon;
        }

        private static string FormatInstallCount(int n) => n switch
        {
            >= 1_000_000 => $"{n / 1_000_000}M+ installs",
            >= 1_000 => $"{n / 1_000}K+ installs",
            _ => $"{n} installs"
        };

        private static void MakeCircle(Panel p)
        {
            var path = new GraphicsPath();
            path.AddEllipse(0, 0, p.Width, p.Height);
            p.Region = new Region(path);
        }

        // Store protocol links (ms-windows-store://) resolve through the
        // shell, same as AboutForm's website/email links — no UseShellExecute
        // means Windows can't hand the URI to the Store app's protocol
        // handler.
        private static void OpenStoreLink(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                // Store app unavailable or the link failed to resolve —
                // non-fatal, nothing else on this page depends on it.
            }
        }
    }
}
