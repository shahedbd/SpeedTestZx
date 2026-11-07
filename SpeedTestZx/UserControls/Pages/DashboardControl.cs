using FontAwesome.Sharp;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;
using SpeedTestZx.Service;
using SpeedTestZx.UserControls.Lib;
using SpeedTestZx.UserControls.Lib.DataCard;

namespace SpeedTestZx.UserControls.Pages
{
    /// <summary>
    /// "Dashboard" nav page — the landing page (see doc/App_improvement_plan/
    /// Dashboard_plan.md). Answers "how is my connection right now?" and
    /// "how has it been lately?" without running a test:
    ///
    ///   • Card row — last test's download / upload / ping / jitter, plus
    ///     live internet status (T44). DataCard library ported from NetPulseApp.
    ///   • Live traffic — this PC's ↓ / ↑ over the last 60 s from the adapter
    ///     counters, plus a latency probe every 2 s (T45).
    ///   • Last test card — verdicts, bufferbloat, ISP, Run test (T46).
    ///   • Recent tests card — last 10 tests, avg / best / worst (T47).
    ///
    /// With no history yet, the two bottom cards are replaced by the welcome
    /// card. Below 700 px wide the bottom cards stack and the page scrolls
    /// (T48). Live sampling runs only while this page exists and the window
    /// isn't minimised — pages are disposed when you navigate away.
    /// </summary>
    public class DashboardControl : DpiAwareUserControl
    {
        // ── Layout constants (96 DPI) ────────────────────────────────────────
        private const int CardRowHeight = 90;
        private const int TrafficHeight = 220;
        private const int BottomMinHeight = 220;
        private const int Gap = 10;
        private const int TwoColumnMinWidth = 700;
        private const int InnerPad = 12;

        // ── Live sampling ────────────────────────────────────────────────────
        private const int GraphWindowSeconds = 60;
        private const int LatencyIntervalMs = 2000;
        private const int RecentTestCount = 10;

        // Lost probes in a row before the header says "Offline".
        private const int OfflineAfterLost = 3;

        private static readonly Color LiveColor = Color.FromArgb(16, 185, 129);
        private static readonly Color OfflineColor = Color.FromArgb(239, 68, 68);
        private static readonly Color IdleColor = Color.FromArgb(148, 163, 184);

        private readonly NetworkDetailsService _networkService = new();
        private readonly TrafficMonitor _traffic = new();
        private readonly LatencyProbe _probe = new();
        private CancellationTokenSource? _latencyCts;
        private Form? _form;
        private bool _live;
        private bool _inLayout;

        private double _curDown, _curUp;
        private double? _curLatency;
        private int _lostStreak;
        private int _adapterCount = -1;   // -1 = no sample yet

        private SpeedTestResult? _last;
        private bool _hasHistory;

        // Whether the last test has quality scores (v2). A field, not
        // _rowStreaming.Visible: Visible reads false until the page is shown.
        private bool _lastScored;

        // ── Card row ─────────────────────────────────────────────────────────
        private DataCardGrid _statRow = null!;
        private DataCardControl _tileDownload = null!;
        private DataCardControl _tileUpload = null!;
        private DataCardControl _tilePing = null!;
        private DataCardControl _tileInternet = null!;
        private DataCardControl _tileJitter = null!;

        // ── Live traffic ─────────────────────────────────────────────────────
        private Panel _trafficCard = null!;
        private Label _lblTrafficTitle = null!, _lblTrafficStatus = null!, _lblTrafficValues = null!;
        private LiveGraphControl _graph = null!;

        // ── Last test ────────────────────────────────────────────────────────
        private Panel _lastCard = null!;
        private Label _lblLastTitle = null!, _lblLastWhen = null!, _lblLastNumbers = null!;
        private Label _lblLastNote = null!, _lblLastInfo = null!;
        private VerdictRow _rowStreaming = null!, _rowGaming = null!, _rowCalls = null!;
        private Button _btnRunTest = null!;

        // ── Recent tests ─────────────────────────────────────────────────────
        private Panel _recentCard = null!;
        private Label _lblRecentTitle = null!, _lblRecentCount = null!, _lblRecentSummary = null!;
        private RecentTestsChart _recentChart = null!;
        private LinkLabel _lnkHistory = null!;

        // ── First run ────────────────────────────────────────────────────────
        private Panel _welcomeCard = null!;

        public DashboardControl()
        {
            BackColor = themeManager.CurrentTheme.ContentBackground;
            Padding = ScaledPadding(12);
            AutoScroll = true;
            DoubleBuffered = true;

            BuildCardRow();
            BuildTrafficCard();
            BuildLastTestCard();
            BuildRecentCard();
            BuildWelcomeCard();

            LoadHistory();
            DoLayout();

            SizeChanged += (s, e) => DoLayout();
            themeManager.ThemeChanged += OnThemeChanged;
            _traffic.Sample += OnTrafficSample;

            _ = LoadInternetStatusAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD — card row (T44)
        // ══════════════════════════════════════════════════════════════════════

        private void BuildCardRow()
        {
            _statRow = new DataCardGrid { Gap = 12 };

            _tileDownload = AddCard(IconChar.Download, "LAST DOWNLOAD", DataCardAccents.Blue, "No tests yet");
            _tileUpload = AddCard(IconChar.Upload, "LAST UPLOAD", DataCardAccents.Green, "No tests yet");
            _tilePing = AddCard(IconChar.Gauge, "LAST PING", DataCardAccents.Purple, "No tests yet");
            _tileInternet = AddCard(IconChar.Globe, "INTERNET STATUS", DataCardAccents.Sky, "Checking...");
            _tileJitter = AddCard(IconChar.WaveSquare, "JITTER", DataCardAccents.Amber, "No tests yet");

            // Internet status needs a network call — skeleton until it lands.
            _tileInternet.IsLoading = true;

            _statRow.ApplyPalette(CurrentPalette());
            Controls.Add(_statRow);
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

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD — live traffic (T45)
        // ══════════════════════════════════════════════════════════════════════

        private void BuildTrafficCard()
        {
            _trafficCard = MakeCard();

            _lblTrafficTitle = TitleLabel($"Live traffic  ·  last {GraphWindowSeconds} s");
            _lblTrafficStatus = new Label
            {
                Text = "● Starting…",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = IdleColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                UseMnemonic = false,
            };
            _lblTrafficValues = new Label
            {
                Text = "↓ —     ↑ —     Latency —",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.TextColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false,
            };

            _graph = new LiveGraphControl
            {
                WindowSeconds = GraphWindowSeconds,
                MinMbpsScale = 1,
                EmptyHint = "Live traffic appears here in a moment",
                BackColor = themeManager.CurrentTheme.CardBackground,
            };

            _trafficCard.Controls.AddRange(new Control[]
            {
                _lblTrafficTitle, _lblTrafficStatus, _lblTrafficValues, _graph,
            });
            _trafficCard.SizeChanged += (s, e) => LayoutTrafficCard();
            Controls.Add(_trafficCard);
        }

        private void LayoutTrafficCard()
        {
            int w = _trafficCard.ClientSize.Width, h = _trafficCard.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int pad = Scale(InnerPad);
            int rowH = Scale(22);
            int statusW = Scale(160);

            _lblTrafficTitle.SetBounds(pad, pad, Math.Max(0, w - pad * 2 - statusW), rowH);
            _lblTrafficStatus.SetBounds(w - pad - statusW, pad, statusW, rowH);
            _lblTrafficValues.SetBounds(pad, pad + rowH, Math.Max(0, w - pad * 2), rowH);

            int graphY = pad + rowH * 2 + Scale(4);
            _graph.SetBounds(pad, graphY, Math.Max(0, w - pad * 2), Math.Max(0, h - graphY - pad / 2));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD — last test (T46)
        // ══════════════════════════════════════════════════════════════════════

        private void BuildLastTestCard()
        {
            _lastCard = MakeCard();

            _lblLastTitle = TitleLabel("Last test");
            _lblLastWhen = InfoLabel(ContentAlignment.MiddleRight);
            _lblLastNumbers = new Label
            {
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.TextColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false,
            };

            _rowStreaming = new VerdictRow("Streaming");
            _rowGaming = new VerdictRow("Gaming");
            _rowCalls = new VerdictRow("Video calls");

            _lblLastNote = InfoLabel(ContentAlignment.MiddleLeft);
            _lblLastNote.Text = "Run a new test for quality scores.";
            _lblLastInfo = InfoLabel(ContentAlignment.MiddleLeft);

            _btnRunTest = new Button
            {
                Text = "▶  Run speed test",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppConfig.PrimaryColor,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                UseMnemonic = false,
            };
            _btnRunTest.FlatAppearance.BorderSize = 0;
            _btnRunTest.Click += (s, e) => GoTo("Speed Test");

            _lastCard.Controls.AddRange(new Control[]
            {
                _lblLastTitle, _lblLastWhen, _lblLastNumbers,
                _rowStreaming, _rowGaming, _rowCalls,
                _lblLastNote, _lblLastInfo, _btnRunTest,
            });
            _lastCard.SizeChanged += (s, e) => LayoutLastCard();
            Controls.Add(_lastCard);
        }

        private void LayoutLastCard()
        {
            int w = _lastCard.ClientSize.Width, h = _lastCard.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int pad = Scale(InnerPad);
            int inner = Math.Max(0, w - pad * 2);
            int titleH = Scale(24), rowH = Scale(24), infoH = Scale(20);
            int btnH = Scale(34), btnW = Scale(170);

            int y = pad;
            _lblLastTitle.SetBounds(pad, y, inner / 2, titleH);
            _lblLastWhen.SetBounds(pad + inner / 2, y, inner - inner / 2, titleH);
            y += titleH + Scale(2);

            _lblLastNumbers.SetBounds(pad, y, inner, rowH);
            y += rowH + Scale(6);

            bool scored = _lastScored;
            if (scored)
            {
                foreach (var row in new[] { _rowStreaming, _rowGaming, _rowCalls })
                {
                    row.SetBounds(pad, y, inner, rowH);
                    y += rowH;
                }
            }
            else
            {
                _lblLastNote.SetBounds(pad, y, inner, infoH);
                y += infoH;
            }
            y += Scale(4);

            _lblLastInfo.SetBounds(pad, y, inner, infoH);
            y += infoH + Scale(8);

            // Bottom-aligned when there's room, otherwise right after the text.
            int btnY = Math.Max(y, h - pad - btnH);
            _btnRunTest.SetBounds(pad, btnY, btnW, btnH);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD — recent tests (T47)
        // ══════════════════════════════════════════════════════════════════════

        private void BuildRecentCard()
        {
            _recentCard = MakeCard();

            _lblRecentTitle = TitleLabel("Recent tests");
            _lblRecentCount = InfoLabel(ContentAlignment.MiddleRight);
            _recentChart = new RecentTestsChart();
            _recentChart.Click += (s, e) => GoTo("Test History");
            _lblRecentSummary = InfoLabel(ContentAlignment.MiddleLeft);

            var accent = themeManager.CurrentTheme.AccentColor;
            _lnkHistory = new LinkLabel
            {
                Text = "View history →",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                LinkColor = accent,
                ActiveLinkColor = accent,
                VisitedLinkColor = accent,
                LinkBehavior = LinkBehavior.HoverUnderline,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                UseMnemonic = false,
            };
            _lnkHistory.LinkClicked += (s, e) => GoTo("Test History");

            _recentCard.Controls.AddRange(new Control[]
            {
                _lblRecentTitle, _lblRecentCount, _recentChart, _lblRecentSummary, _lnkHistory,
            });
            _recentCard.SizeChanged += (s, e) => LayoutRecentCard();
            Controls.Add(_recentCard);
        }

        private void LayoutRecentCard()
        {
            int w = _recentCard.ClientSize.Width, h = _recentCard.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int pad = Scale(InnerPad);
            int inner = Math.Max(0, w - pad * 2);
            int titleH = Scale(24), infoH = Scale(20);

            _lblRecentTitle.SetBounds(pad, pad, inner / 2, titleH);
            _lblRecentCount.SetBounds(pad + inner / 2, pad, inner - inner / 2, titleH);

            int linkY = h - pad - infoH;
            int summaryY = linkY - infoH;
            int chartY = pad + titleH + Scale(4);

            _recentChart.SetBounds(pad, chartY, inner, Math.Max(0, summaryY - chartY - Scale(4)));
            _lblRecentSummary.SetBounds(pad, summaryY, inner, infoH);
            _lnkHistory.SetBounds(pad, linkY, inner, infoH);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD — welcome (first run only, T48)
        // ══════════════════════════════════════════════════════════════════════

        private void BuildWelcomeCard()
        {
            _welcomeCard = MakeCard();

            var lblTitle = new Label
            {
                Text = $"Welcome to {AppConfig.AppName}",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.HeadingColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                UseMnemonic = false,
            };

            var lblDesc = new Label
            {
                Text = "Run a speed test to check your download, upload, and ping —\nevery result is saved to Test History automatically.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                AutoSize = true,
            };

            var btnStart = new Button
            {
                Text = "▶  Start Speed Test",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Size = ScaledSize(180, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppConfig.PrimaryColor,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                UseMnemonic = false,
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += (s, e) => GoTo("Speed Test");

            _welcomeCard.Controls.AddRange(new Control[] { lblTitle, lblDesc, btnStart });

            void LayoutWelcome()
            {
                int w = _welcomeCard.ClientSize.Width, h = _welcomeCard.ClientSize.Height;
                if (w <= 0 || h <= 0) return;

                int cx = w / 2;
                int total = lblTitle.Height + Scale(12) + lblDesc.Height + Scale(20) + btnStart.Height;
                int y = Math.Max(Scale(12), (h - total) / 2);

                lblTitle.Location = new Point(cx - lblTitle.Width / 2, y);
                y += lblTitle.Height + Scale(12);
                lblDesc.Location = new Point(cx - lblDesc.Width / 2, y);
                y += lblDesc.Height + Scale(20);
                btnStart.Location = new Point(cx - btnStart.Width / 2, y);
            }

            _welcomeCard.SizeChanged += (s, e) => LayoutWelcome();
            Controls.Add(_welcomeCard);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PAGE LAYOUT (T48) — side by side ≥ 700 px, stacked below
        // ══════════════════════════════════════════════════════════════════════

        private void DoLayout()
        {
            if (_inLayout) return;
            _inLayout = true;
            try
            {
                var pad = Padding;
                int w = ClientSize.Width - pad.Horizontal;
                int h = ClientSize.Height - pad.Vertical;
                if (w <= 0 || h <= 0) return;

                int gap = Scale(Gap);
                int x = pad.Left + AutoScrollPosition.X;
                int y = pad.Top + AutoScrollPosition.Y;
                int top = y;

                _statRow.SetBounds(x, y, w, Scale(CardRowHeight));
                y += Scale(CardRowHeight) + gap;

                _trafficCard.SetBounds(x, y, w, Scale(TrafficHeight));
                y += Scale(TrafficHeight) + gap;

                int remaining = h - (y - top);
                int minBottom = Scale(BottomMinHeight);

                _welcomeCard.Visible = !_hasHistory;
                _lastCard.Visible = _hasHistory;
                _recentCard.Visible = _hasHistory;

                int contentBottom;
                if (!_hasHistory)
                {
                    int ch = Math.Max(minBottom, remaining);
                    _welcomeCard.SetBounds(x, y, w, ch);
                    contentBottom = y + ch;
                }
                else if (w >= Scale(TwoColumnMinWidth))
                {
                    int ch = Math.Max(minBottom, remaining);
                    int colW = (w - gap) / 2;
                    _lastCard.SetBounds(x, y, colW, ch);
                    _recentCard.SetBounds(x + colW + gap, y, w - colW - gap, ch);
                    contentBottom = y + ch;
                }
                else
                {
                    _lastCard.SetBounds(x, y, w, minBottom);
                    y += minBottom + gap;
                    _recentCard.SetBounds(x, y, w, minBottom);
                    contentBottom = y + minBottom;
                }

                // Scroll only when the content is taller than the page.
                int needed = contentBottom - top + pad.Vertical;
                var minSize = needed > ClientSize.Height ? new Size(0, needed) : Size.Empty;
                if (AutoScrollMinSize != minSize) AutoScrollMinSize = minSize;
            }
            finally
            {
                _inLayout = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LOAD — history (sync) and internet status (async)
        // ══════════════════════════════════════════════════════════════════════

        private void LoadHistory()
        {
            var history = TestHistoryStore.Load();   // newest first
            _last = history.FirstOrDefault(r => r.Success);
            _hasHistory = _last != null;

            if (_last == null) return;
            var r = _last;

            _tileDownload.SetData($"{r.DownloadMbps:F1} Mbps", "Download speed");
            _tileUpload.SetData($"{r.UploadMbps:F1} Mbps", "Upload speed");
            _tilePing.SetData($"{r.PingMs:F0} ms", "Round trip");

            var (jitterNote, jitterLevel) = QualityScorer.JitterNote(r.JitterMs);
            _tileJitter.SetData($"{r.JitterMs:F0} ms", jitterNote, QualityScorer.LevelColor(jitterLevel));

            FillLastTestCard(r);

            var recent = HistoryStats.Recent(history, RecentTestCount);
            _recentChart.SetTests(recent);
            _lblRecentCount.Text = recent.Count == 1 ? "1 test" : $"last {recent.Count}";

            var sum = HistoryStats.Summarise(recent);
            _lblRecentSummary.Text = sum == null ? "" :
                $"Avg ↓ {sum.AvgDownMbps:0.#}  ·  Best {sum.BestDownMbps:0.#}  ·  Worst {sum.WorstDownMbps:0.#} Mbps" +
                $"     Avg ↑ {sum.AvgUpMbps:0.#} Mbps";
        }

        private void FillLastTestCard(SpeedTestResult r)
        {
            _lblLastWhen.Text = $"{r.TestedAt:HH:mm}  ·  {r.TestedAt:dd MMM yyyy}";
            _lblLastNumbers.Text =
                $"↓ {r.DownloadMbps:F1} Mbps     ↑ {r.UploadMbps:F1} Mbps     Ping {r.PingMs:F0} ms";

            bool scored = _lastScored = r.SchemaVersion >= 2;
            foreach (var row in new[] { _rowStreaming, _rowGaming, _rowCalls })
                row.Visible = scored;
            _lblLastNote.Visible = !scored;

            var info = new List<string>();

            if (scored)
            {
                var q = QualityScorer.Score(r);
                _rowStreaming.Show(q.Streaming);
                _rowGaming.Show(q.Gaming);
                _rowCalls.Show(q.Calls);

                if (r.BufferbloatMs is not null)
                {
                    info.Add($"Bufferbloat {q.BufferbloatGrade}");

                    // Grade pill on the Ping card, like NetPulseApp's latency card.
                    _tilePing.Badge = $"Grade {q.BufferbloatGrade}";
                    _tilePing.BadgeColor = QualityScorer.GradeColor(q.BufferbloatGrade);
                }
            }

            if (r.PctOfPlan is double pct) info.Add($"{pct:0}% of plan");   // T33
            if (r.Isp.Length > 0) info.Add(r.Isp);
            if (r.ServerColo.Length > 0) info.Add($"Server {r.ServerColo}");
            _lblLastInfo.Text = string.Join("  ·  ", info);

            LayoutLastCard();
        }

        private async Task LoadInternetStatusAsync()
        {
            try
            {
                // Task.Run: creating the HTTP client and starting TLS took ~45 ms
                // on the UI thread at launch. The await still resumes here.
                string pubIp = await Task.Run(() => _networkService.GetPublicIpAsync());
                if (IsDisposed) return;

                bool online = !string.IsNullOrEmpty(pubIp) && pubIp != "Unavailable";
                // ISP from the last test when known — more useful at a
                // glance than the raw IP, which Network Info already shows.
                string isp = _last?.Isp ?? "";
                _tileInternet.SetData(
                    online ? "Connected" : "Offline",
                    !online ? "No internet"
                        : isp.Length > 0 ? isp
                        : $"IP: {pubIp}",
                    online ? DataCardAccents.Green : DataCardAccents.Red);
            }
            catch
            {
                if (!IsDisposed)
                    _tileInternet.SetData("Unknown", "Could not check");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LIVE — start / stop / pause on minimise
        // ══════════════════════════════════════════════════════════════════════

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            HookForm();
            UpdateLiveState();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            HookForm();
            UpdateLiveState();
        }

        private void HookForm()
        {
            if (_form != null) return;
            _form = FindForm();
            if (_form == null) return;

            _form.Resize += OnFormStateChanged;
            _form.VisibleChanged += OnFormStateChanged;
        }

        private void OnFormStateChanged(object? sender, EventArgs e) => UpdateLiveState();

        /// <summary>Runs live sampling only while the page and window can be seen.</summary>
        private void UpdateLiveState()
        {
            if (IsDisposed) return;

            bool shouldRun = IsHandleCreated && Visible
                && _form != null && _form.Visible
                && _form.WindowState != FormWindowState.Minimized;

            if (shouldRun) StartLive();
            else StopLive();
        }

        private void StartLive()
        {
            if (_live) return;
            _live = true;

            // Fresh graph on every (re)start — a minimised gap would otherwise
            // draw as a straight line between the old and new readings.
            _graph.Reset();
            _lostStreak = 0;
            _curLatency = null;

            _traffic.Start();

            _latencyCts = new CancellationTokenSource();
            _ = LatencyLoopAsync(_latencyCts.Token);

            UpdateTrafficHeader();
        }

        private void StopLive()
        {
            if (!_live) return;
            _live = false;

            _traffic.Stop();
            _latencyCts?.Cancel();
            _latencyCts?.Dispose();
            _latencyCts = null;

            UpdateTrafficHeader();
        }

        /// <summary>One probe at a time, every LatencyIntervalMs, until cancelled.</summary>
        private async Task LatencyLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    long started = Environment.TickCount64;

                    double? ms = await _probe.ProbeAsync();
                    if (ct.IsCancellationRequested || IsDisposed) return;

                    OnLatencySample(ms);

                    long elapsed = Environment.TickCount64 - started;
                    await Task.Delay((int)Math.Max(0, LatencyIntervalMs - elapsed), ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal stop.
            }
        }

        private void OnTrafficSample(object? sender, TrafficSample s)
        {
            if (IsDisposed) return;

            _curDown = s.DownMbps;
            _curUp = s.UpMbps;
            _adapterCount = s.AdapterCount;

            _graph.AddTraffic(s.DownMbps, s.UpMbps);
            UpdateTrafficHeader();
        }

        private void OnLatencySample(double? ms)
        {
            _curLatency = ms;
            _lostStreak = ms == null ? _lostStreak + 1 : 0;

            _graph.AddLatency(ms);
            UpdateTrafficHeader();
        }

        private void UpdateTrafficHeader()
        {
            if (IsDisposed) return;

            string latency = _curLatency is double ms ? $"{ms:0} ms" : "—";
            _lblTrafficValues.Text = _adapterCount < 0
                ? "↓ —     ↑ —     Latency —"
                : $"↓ {Rate(_curDown)}     ↑ {Rate(_curUp)}     Latency {latency}";

            (string text, Color color) = !_live ? ("● Paused", IdleColor)
                : _adapterCount == 0 ? ("● No active adapter", IdleColor)
                : _lostStreak >= OfflineAfterLost ? ("● Offline", OfflineColor)
                : _adapterCount < 0 ? ("● Starting…", IdleColor)
                : ("● Live", LiveColor);

            _lblTrafficStatus.Text = text;
            _lblTrafficStatus.ForeColor = color;
        }

        /// <summary>Kbps below 1 Mbps, Mbps above.</summary>
        private static string Rate(double mbps) =>
            mbps < 1 ? $"{mbps * 1000:0} Kbps" : $"{mbps:0.0} Mbps";

        // ══════════════════════════════════════════════════════════════════════
        //  PIECES
        // ══════════════════════════════════════════════════════════════════════

        private Panel MakeCard() => new()
        {
            BackColor = themeManager.CurrentTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
        };

        private Label TitleLabel(string text) => new()
        {
            Text = text,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = themeManager.CurrentTheme.HeadingColor,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            UseMnemonic = false,
        };

        private Label InfoLabel(ContentAlignment align) => new()
        {
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
            BackColor = Color.Transparent,
            TextAlign = align,
            AutoEllipsis = true,
            UseMnemonic = false,
        };

        /// <summary>
        /// Navigating disposes this page, so do it after the current click
        /// handler has returned.
        /// </summary>
        private void GoTo(string label)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() => NavigationManager.Instance.NavigateTo(label)));
        }

        private void OnThemeChanged(object? sender, ThemeChangedEventArgs e) =>
            _statRow.ApplyPalette(CurrentPalette());

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopLive();
                _traffic.Sample -= OnTrafficSample;
                _traffic.Dispose();
                themeManager.ThemeChanged -= OnThemeChanged;

                if (_form != null)
                {
                    _form.Resize -= OnFormStateChanged;
                    _form.VisibleChanged -= OnFormStateChanged;
                    _form = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
