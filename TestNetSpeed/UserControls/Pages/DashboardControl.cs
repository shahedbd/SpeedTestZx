using TestNetSpeed.Managers;
using TestNetSpeed.Model;
using TestNetSpeed.Service;
using FontAwesome.Sharp;
using TestNetSpeed.Helper;
using TestNetSpeed.UserControls.Lib;
using TestNetSpeed.UserControls.Lib.DataCard;

namespace TestNetSpeed.UserControls.Pages
{
    /// <summary>
    /// "Dashboard" nav page — the landing page. A data-card row (DataCard library ported from NetPulseApp; originally mirroring
    /// WifiHeaderControl's layout pattern from WiFiPasswordViewer) fed from
    /// the most recent Test History entry, plus a welcome card that jumps
    /// straight to Speed Test. New page, not a WiFi port — this app's
    /// Dashboard didn't exist until Test History existed to feed it.
    ///
    /// The ISP name comes from the last test (SpeedTestService looks it up
    /// from Cloudflare during every run) and shows on the Internet Status
    /// tile; the welcome card adds the last test's Quality Score.
    /// </summary>
    public class DashboardControl : DpiAwareUserControl
    {
        private readonly NetworkDetailsService _networkService = new();

        private DataCardGrid _statRow = null!;
        private DataCardControl _tileDownload = null!;
        private DataCardControl _tileUpload = null!;
        private DataCardControl _tilePing = null!;
        private DataCardControl _tileInternet = null!;
        private DataCardControl _tileLastTested = null!;
        private Label _lblLastQuality;

        private const int TILE_HEIGHT = 90;

        public DashboardControl()
        {
            this.BackColor = themeManager.CurrentTheme.ContentBackground;
            this.Padding = ScaledPadding(12);

            BuildLayout();
            themeManager.ThemeChanged += OnThemeChanged;
            _ = LoadAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD LAYOUT
        // ══════════════════════════════════════════════════════════════════════

        private void BuildLayout()
        {
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(TILE_HEIGHT)));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            this.Controls.Add(outer);

            outer.Controls.Add(BuildTileRow(), 0, 0);
            outer.Controls.Add(BuildWelcomeCard(), 0, 1);
        }

        // ── Stat card row ─────────────────────────────────────────────────────
        // Same DataCard pattern as NetPulseApp's dashboard: owner-drawn cards
        // with an accent strip, gradient icon badge and hover lift, laid out
        // equal-width by DataCardGrid.

        private DataCardGrid BuildTileRow()
        {
            _statRow = new DataCardGrid
            {
                Dock = DockStyle.Fill,
                Gap = 12,
                Margin = new Padding(0, 0, 0, Scale(10))
            };

            _tileDownload = AddCard(IconChar.Download, "LAST DOWNLOAD", DataCardAccents.Blue, "No tests yet");
            _tileUpload = AddCard(IconChar.Upload, "LAST UPLOAD", DataCardAccents.Green, "No tests yet");
            _tilePing = AddCard(IconChar.Gauge, "LAST PING", DataCardAccents.Purple, "No tests yet");
            _tileInternet = AddCard(IconChar.Globe, "INTERNET STATUS", DataCardAccents.Sky, "Checking...");
            _tileLastTested = AddCard(IconChar.Clock, "LAST TESTED", DataCardAccents.Amber, "No tests yet");

            // Internet status needs a network call — skeleton until it lands.
            _tileInternet.IsLoading = true;

            _statRow.ApplyPalette(CurrentPalette());
            return _statRow;
        }

        private DataCardControl AddCard(IconChar icon, string caption, Color accent, string subText)
        {
            var card = _statRow.AddCard(new DataCardControl(icon, caption, accent));
            card.Value = "—";
            card.SubText = subText;
            return card;
        }

        /// <summary>Data cards are theme-agnostic — map this app's theme onto their palette.</summary>
        private DataCardPalette CurrentPalette()
        {
            var t = themeManager.CurrentTheme;
            return DataCardPalette.From(
                t.CardBackground, t.BorderColor, t.TextColor,
                t.SecondaryTextColor, themeManager.IsDarkTheme);
        }

        private void OnThemeChanged(object? sender, ThemeChangedEventArgs e) =>
            _statRow.ApplyPalette(CurrentPalette());

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                themeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        // ── Welcome card ──────────────────────────────────────────────────────

        private Panel BuildWelcomeCard()
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = themeManager.CurrentTheme.CardBackground,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblTitle = new Label
            {
                Text = $"Welcome to {AppConfig.AppName}",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.HeadingColor,
                BackColor = Color.Transparent,
                AutoSize = true
            };

            var lblDesc = new Label
            {
                Text = "Run a speed test to check your download, upload, and ping —\nevery result is saved to Test History automatically.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                AutoSize = true
            };

            // Last test's Quality Score — empty until a v2 test exists.
            _lblLastQuality = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.TextColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                UseMnemonic = false
            };

            var btnStart = new Button
            {
                Text = "▶  Start Speed Test",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Size = ScaledSize(180, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppConfig.PrimaryColor,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += (s, e) => NavigationManager.Instance.NavigateTo("Speed Test");

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblDesc);
            card.Controls.Add(_lblLastQuality);
            card.Controls.Add(btnStart);

            void LayoutCard()
            {
                if (card.Width <= 0 || card.Height <= 0) return;

                int cx = card.Width / 2;
                int y = card.Height / 2 - Scale(60);

                lblTitle.Location = new Point(cx - lblTitle.Width / 2, y);
                y += lblTitle.Height + Scale(12);

                lblDesc.Location = new Point(cx - lblDesc.Width / 2, y);
                y += lblDesc.Height + Scale(14);

                if (_lblLastQuality.Text.Length > 0)
                {
                    _lblLastQuality.Location = new Point(cx - _lblLastQuality.Width / 2, y);
                    y += _lblLastQuality.Height + Scale(14);
                }
                else
                {
                    y += Scale(6);
                }

                btnStart.Location = new Point(cx - btnStart.Width / 2, y);
            }

            card.SizeChanged += (s, e) => LayoutCard();
            _lblLastQuality.TextChanged += (s, e) => LayoutCard();

            return card;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LOAD
        // ══════════════════════════════════════════════════════════════════════

        private async Task LoadAsync()
        {
            // Most recent test — TestHistoryStore already returns newest first.
            var last = TestHistoryStore.Load().FirstOrDefault();

            if (last != null)
            {
                _tileDownload.SetData($"{last.DownloadMbps:F1} Mbps", "Download speed");
                _tileUpload.SetData($"{last.UploadMbps:F1} Mbps", "Upload speed");
                _tilePing.SetData($"{last.PingMs:F0} ms", $"Jitter {last.JitterMs:F0} ms");
                _tileLastTested.SetData(last.TestedAt.ToString("HH:mm"), last.TestedAt.ToString("dd MMM yyyy"));

                if (last.SchemaVersion >= 2)
                {
                    var q = QualityScorer.Score(last);
                    _lblLastQuality.Text =
                        $"Last test:  Streaming {q.Streaming.Level}  ·  Gaming {q.Gaming.Level}  ·  Video calls {q.Calls.Level}";

                    // Bufferbloat grade as a corner pill, like NetPulseApp's latency card.
                    if (last.BufferbloatMs is not null)
                    {
                        _tilePing.Badge = $"Grade {q.BufferbloatGrade}";
                        _tilePing.BadgeColor = QualityScorer.GradeColor(q.BufferbloatGrade);
                    }
                }
            }

            try
            {
                string pubIp = await _networkService.GetPublicIpAsync();
                bool online = !string.IsNullOrEmpty(pubIp) && pubIp != "Unavailable";
                string status = online ? "Connected" : "Offline";
                // ISP from the last test when known — more useful at a
                // glance than the raw IP, which Network Info already shows.
                string isp = last?.Isp ?? "";
                _tileInternet.SetData(status,
                    !online ? "No internet"
                    : isp.Length > 0 ? isp
                    : $"IP: {pubIp}",
                    online ? DataCardAccents.Green : DataCardAccents.Red);
            }
            catch
            {
                _tileInternet.SetData("Unknown", "Could not check");
            }
        }
    }
}
