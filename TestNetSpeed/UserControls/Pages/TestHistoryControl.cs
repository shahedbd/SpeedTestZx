using TestNetSpeed.Helper;
using TestNetSpeed.Model;

namespace TestNetSpeed.UserControls.Pages
{
    /// <summary>
    /// "Test History" nav page — a scrollable log of completed speed tests,
    /// newest first, read from TestHistoryStore. New page (not a WiFi port)
    /// — no equivalent existed before Speed Test's history was just an
    /// in-memory scrollback.
    /// </summary>
    public class TestHistoryControl : DpiAwareUserControl
    {
        private List<SpeedTestResult> _results = new();

        // ── UI refs ───────────────────────────────────────────────────────────
        private Panel _listContainer;
        private Label _lblCount;
        private Label _lblStatus;
        private Panel _emptyState;

        private const int ROW_HEIGHT = 40;
        private const int HEADER_H = 42;
        private const int COL_HEADER_H = 26;

        public TestHistoryControl()
        {
            this.BackColor = themeManager.CurrentTheme.ContentBackground;
            this.Padding = ScaledPadding(12);

            BuildLayout();
            LoadHistory();
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
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // Card
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // Status bar
            this.Controls.Add(outer);

            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, Scale(10)),
                BackColor = themeManager.CurrentTheme.CardBackground,
                BorderStyle = BorderStyle.FixedSingle
            };
            outer.Controls.Add(card, 0, 0);

            _lblStatus = new Label
            {
                Text = "Status: Ready",
                Font = new Font("Segoe UI", 8f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                AutoSize = false,
                Height = Scale(20)
            };
            outer.Controls.Add(_lblStatus, 0, 1);

            BuildHeader(card);
            BuildColumnHeader(card);
            BuildListContainer(card);
            BuildEmptyState(card);
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

            _lblCount = new Label
            {
                Text = "Test History (0)",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.HeadingColor,
                BackColor = Color.Transparent,
                Location = ScaledPoint(12, 11),
                AutoSize = true
            };
            header.Controls.Add(_lblCount);

            var btnRefresh = new Button
            {
                Text = "↺",
                Font = new Font("Segoe UI", 12f),
                Size = ScaledSize(30, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = themeManager.IsDarkTheme
                    ? Color.FromArgb(55, 55, 55)
                    : Color.FromArgb(226, 232, 240),
                ForeColor = themeManager.CurrentTheme.TextColor,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TabStop = false
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => LoadHistory();
            header.Controls.Add(btnRefresh);

            header.SizeChanged += (s, e) =>
            {
                int margin = Scale(8);
                int btnTop = (Scale(HEADER_H) - Scale(26)) / 2;
                btnRefresh.Location = new Point(header.Width - margin - btnRefresh.Width, btnTop);
            };
        }

        // ── Column header ─────────────────────────────────────────────────────

        private void BuildColumnHeader(Panel card)
        {
            var col = new Panel
            {
                Location = new Point(0, Scale(HEADER_H)),
                Size = new Size(card.Width, Scale(COL_HEADER_H)),
                BackColor = themeManager.IsDarkTheme
                    ? Color.FromArgb(38, 38, 38)
                    : Color.FromArgb(226, 232, 240),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(col);

            (string text, int x, int w)[] cols =
            {
                ("Date / Time", Scale(16),  Scale(150)),
                ("Download",    Scale(170), Scale(90)),
                ("Upload",      Scale(262), Scale(90)),
                ("Ping",        Scale(354), Scale(70)),
                ("Jitter",      Scale(426), Scale(70)),
                ("ISP / Server", Scale(498), Scale(200)),
            };

            foreach (var (text, x, w) in cols)
            {
                col.Controls.Add(new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                    BackColor = Color.Transparent,
                    Location = new Point(x, Scale(5)),
                    Size = new Size(w, Scale(18)),
                    TextAlign = ContentAlignment.MiddleLeft
                });
            }
        }

        // ── Scrollable list container ─────────────────────────────────────────

        private void BuildListContainer(Panel card)
        {
            int top = Scale(HEADER_H) + Scale(COL_HEADER_H);

            _listContainer = new Panel
            {
                Location = new Point(0, top),
                Size = new Size(card.Width, Math.Max(card.Height - top, 0)),
                AutoScroll = true,
                BackColor = themeManager.CurrentTheme.CardBackground,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(_listContainer);

            card.SizeChanged += (s, e) =>
            {
                int t = Scale(HEADER_H) + Scale(COL_HEADER_H);
                _listContainer.Location = new Point(0, t);
                _listContainer.Size = new Size(card.Width, Math.Max(card.Height - t, 0));
                if (_emptyState != null)
                {
                    _emptyState.Location = _listContainer.Location;
                    _emptyState.Size = _listContainer.Size;
                }
            };
        }

        // ── Empty state ───────────────────────────────────────────────────────

        private void BuildEmptyState(Panel card)
        {
            _emptyState = new Panel
            {
                Location = _listContainer.Location,
                Size = _listContainer.Size,
                BackColor = themeManager.CurrentTheme.CardBackground,
                Visible = false
            };
            card.Controls.Add(_emptyState);
            _emptyState.BringToFront();

            var lblTitle = new Label
            {
                Text = "No test results yet",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.HeadingColor,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            var lblSub = new Label
            {
                Text = "Run a Speed Test to see your history here.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            _emptyState.Controls.Add(lblTitle);
            _emptyState.Controls.Add(lblSub);

            _emptyState.SizeChanged += (s, e) =>
            {
                if (_emptyState.Width <= 0 || _emptyState.Height <= 0) return;
                int cx = _emptyState.Width / 2;
                int cy = _emptyState.Height / 2;
                lblTitle.Location = new Point(cx - lblTitle.Width / 2, cy - Scale(16));
                lblSub.Location = new Point(cx - lblSub.Width / 2, cy + Scale(10));
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LOAD + RENDER
        // ══════════════════════════════════════════════════════════════════════

        private void LoadHistory()
        {
            _results = TestHistoryStore.Load();
            RenderRows();
            SetStatus($"{_results.Count} result{(_results.Count == 1 ? "" : "s")} loaded.");
        }

        private void RenderRows()
        {
            _listContainer.SuspendLayout();
            _listContainer.Controls.Clear();

            _lblCount.Text = $"Test History ({_results.Count})";
            _emptyState.Visible = _results.Count == 0;

            int y = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                Panel row = BuildRow(_results[i], i, y);
                _listContainer.Controls.Add(row);
                y += row.Height;
            }

            _listContainer.ResumeLayout();
        }

        private Panel BuildRow(SpeedTestResult r, int index, int y)
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
                // Rows are built while the page is still at its pre-dock
                // default size (NavigationManager constructs pages before
                // adding+docking them), so this width is initially wrong.
                // Anchoring left+right keeps it in sync with _listContainer
                // automatically once real layout resolves, instead of
                // staying stuck at the too-narrow construction-time size
                // until the next manual Refresh.
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = rowBg
            };

            (string text, int x, int w, Color color)[] cells =
            {
                (r.TestedAt.ToString("yyyy-MM-dd  HH:mm:ss"), Scale(16),  Scale(150), themeManager.CurrentTheme.TextColor),
                ($"{r.DownloadMbps:F1} Mbps",                 Scale(170), Scale(90),  Color.FromArgb(59, 130, 246)),
                ($"{r.UploadMbps:F1} Mbps",                   Scale(262), Scale(90),  Color.FromArgb(16, 185, 129)),
                ($"{r.PingMs:F0} ms",                         Scale(354), Scale(70),  Color.FromArgb(245, 158, 11)),
                ($"{r.JitterMs:F0} ms",                       Scale(426), Scale(70),  Color.FromArgb(168, 85, 247)),
                (ServerText(r),                               Scale(498), Scale(200), themeManager.CurrentTheme.SecondaryTextColor),
            };

            foreach (var (text, x, w, color) in cells)
            {
                row.Controls.Add(new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = color,
                    BackColor = Color.Transparent,
                    Location = new Point(x, (Scale(ROW_HEIGHT) - Scale(16)) / 2),
                    Size = new Size(w, Scale(16)),
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft
                });
            }

            row.MouseEnter += (s, e) => row.BackColor = themeManager.IsDarkTheme
                ? Color.FromArgb(50, 50, 60)
                : Color.FromArgb(235, 242, 255);
            row.MouseLeave += (s, e) => row.BackColor = rowBg;

            return row;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  STATUS
        // ══════════════════════════════════════════════════════════════════════

        private void SetStatus(string message)
        {
            if (_lblStatus == null) return;

            if (this.InvokeRequired)
                this.Invoke(() => _lblStatus.Text = $"Status: {message}");
            else
                _lblStatus.Text = $"Status: {message}";
        }

        /// <summary>
        /// v2 results: "ISP · Cloudflare SIN". Older results only have the
        /// ping endpoint URL, so show that as before.
        /// </summary>
        private static string ServerText(SpeedTestResult r)
        {
            if (r.Isp.Length == 0 && r.ServerColo.Length == 0)
                return r.ServerUsed;

            var parts = new List<string>();
            if (r.Isp.Length > 0) parts.Add(r.Isp);
            if (r.ServerColo.Length > 0) parts.Add($"Cloudflare {r.ServerColo}");
            return string.Join(" · ", parts);
        }
    }
}
