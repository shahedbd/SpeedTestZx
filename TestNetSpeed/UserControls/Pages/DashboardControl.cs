using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;
using TestNetSpeed.Service;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.Pages
{
    /// <summary>
    /// "Dashboard" nav page — the landing page. A stat-tile row (mirroring
    /// WifiHeaderControl's layout pattern from WiFiPasswordViewer) fed from
    /// the most recent Test History entry, plus a welcome card that jumps
    /// straight to Speed Test. New page, not a WiFi port — this app's
    /// Dashboard didn't exist until Test History existed to feed it.
    ///
    /// Deliberately omits an "ISP name" tile — that needs IP-geolocation
    /// lookup logic ported from the old NetSpeedMeterPlus app
    /// (test_code/NetSpeedMeterPlus/Data/Model/IPGeolocationViewModel.cs),
    /// which the dev plan flagged as an open decision, not yet resolved.
    /// </summary>
    public class DashboardControl : DpiAwareUserControl
    {
        private readonly NetworkDetailsService _networkService = new();

        private StatTileControl _tileDownload;
        private StatTileControl _tileUpload;
        private StatTileControl _tilePing;
        private StatTileControl _tileInternet;
        private StatTileControl _tileLastTested;

        private const int TILE_HEIGHT = 90;

        public DashboardControl()
        {
            this.BackColor = themeManager.CurrentTheme.ContentBackground;
            this.Padding = ScaledPadding(12);

            BuildLayout();
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

        // ── Stat tile row ─────────────────────────────────────────────────────

        private TableLayoutPanel BuildTileRow()
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, Scale(10))
            };

            for (int i = 0; i < 5; i++)
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _tileDownload = CreateTile("⬇", Color.FromArgb(37, 99, 235), "Last Download", "—", "Mbps", Color.FromArgb(59, 130, 246));
            _tileUpload = CreateTile("⬆", Color.FromArgb(5, 150, 105), "Last Upload", "—", "Mbps", Color.FromArgb(16, 185, 129));
            _tilePing = CreateTile("⚡", Color.FromArgb(126, 34, 206), "Last Ping", "—", "ms", Color.FromArgb(168, 85, 247));
            _tileInternet = CreateTile("🌐", Color.FromArgb(2, 132, 199), "Internet Status", "Checking...", "Please wait", Color.FromArgb(14, 165, 233));
            _tileLastTested = CreateTile("🕐", Color.FromArgb(180, 83, 9), "Last Tested", "—", "No tests yet", Color.FromArgb(245, 158, 11));

            AddTileToTable(table, _tileDownload, 0, leftMargin: 0);
            AddTileToTable(table, _tileUpload, 1);
            AddTileToTable(table, _tilePing, 2);
            AddTileToTable(table, _tileInternet, 3);
            AddTileToTable(table, _tileLastTested, 4, rightMargin: 0);

            return table;
        }

        private StatTileControl CreateTile(
            string icon, Color iconBg, string caption, string value, string sub, Color accent)
        {
            return new StatTileControl(new StatTileControl.TileConfig(
                Icon: icon,
                IconBackground: iconBg,
                Caption: caption,
                Value: value,
                SubText: sub,
                AccentColor: accent
            ));
        }

        private void AddTileToTable(
            TableLayoutPanel table, StatTileControl tile, int col,
            int leftMargin = 4, int rightMargin = 4)
        {
            tile.Dock = DockStyle.Fill;
            tile.Margin = new Padding(leftMargin, 0, rightMargin, 0);
            table.Controls.Add(tile, col, 0);
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
            card.Controls.Add(btnStart);

            card.SizeChanged += (s, e) =>
            {
                if (card.Width <= 0 || card.Height <= 0) return;

                int cx = card.Width / 2;
                int y = card.Height / 2 - Scale(60);

                lblTitle.Location = new Point(cx - lblTitle.Width / 2, y);
                y += lblTitle.Height + Scale(12);

                lblDesc.Location = new Point(cx - lblDesc.Width / 2, y);
                y += lblDesc.Height + Scale(20);

                btnStart.Location = new Point(cx - btnStart.Width / 2, y);
            };

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
                _tileDownload.UpdateValue($"{last.DownloadMbps:F1}");
                _tileUpload.UpdateValue($"{last.UploadMbps:F1}");
                _tilePing.UpdateValue($"{last.PingMs:F0}");
                _tilePing.UpdateSubText($"ms  ·  Jitter {last.JitterMs:F0}ms");
                _tileLastTested.UpdateValue(last.TestedAt.ToString("HH:mm:ss"));
                _tileLastTested.UpdateSubText(last.TestedAt.ToString("dd MMM yyyy"));
            }

            try
            {
                string pubIp = await _networkService.GetPublicIpAsync();
                bool online = !string.IsNullOrEmpty(pubIp) && pubIp != "Unavailable";
                _tileInternet.UpdateValue(online ? "Connected" : "Offline");
                _tileInternet.UpdateSubText(online ? $"IP: {pubIp}" : "No internet");
            }
            catch
            {
                _tileInternet.UpdateValue("Unknown");
                _tileInternet.UpdateSubText("Could not check");
            }
        }
    }
}
