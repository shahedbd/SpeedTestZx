using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;
using SpeedTestZx.Service;

namespace SpeedTestZx.UserControls.Pages
{
    /// <summary>
    /// "Speed Test" nav page — a single centered gauge-first layout: animated
    /// speedometer, Download/Upload/Ping/Jitter stat cards, Start/Cancel, and
    /// a link to the dedicated Test History tab. Originally ported from
    /// WiFiPasswordViewer with a split gauge/history-log layout; the inline
    /// history log was dropped once Test History became its own tab (step 3)
    /// — showing the same data twice added nothing, and the freed width lets
    /// the gauge be the page's actual focal point.
    /// </summary>
    public class SpeedTestControl : DpiAwareUserControl
    {

        // ── State ─────────────────────────────────────────────────────────────
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private SpeedTestResult _lastResult;

        // ── Gauge animation ───────────────────────────────────────────────────
        private double _gaugeValue;
        private double _gaugeTarget;
        private double _gaugePeak;
        private System.Windows.Forms.Timer _animTimer;
        private System.Windows.Forms.Timer _liveTimer;

        // ── UI refs ───────────────────────────────────────────────────────────
        private Label _lblStatus;
        private Panel _gaugePanel;
        private Label _lblBigValue;
        private Label _lblPhase;
        private Panel _progressTrack;
        private Panel _progressFill;
        private Panel _statRow;
        private Label _lblDownVal, _lblUpVal, _lblPingVal, _lblJitterVal;
        private Button _btnStart, _btnCancel, _btnViewHistory;

        // Right-hand column: live graph, latency, quality score, share card.
        private SpeedTestInsightsPanel _insights;

        public SpeedTestControl()
        {
            this.BackColor = themeManager.CurrentTheme.ContentBackground;
            this.Padding = ScaledPadding(12);

            BuildLayout();
            SetupAnimTimer();
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
                BorderStyle = BorderStyle.FixedSingle,
                Padding = ScaledPadding(10)
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

            card.Controls.Add(BuildTestPanel());
        }

        // ── Test panel — gauge, stats, controls, all centered as one block ──────

        private Panel BuildTestPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            _gaugePanel = new Panel { BackColor = Color.Transparent };
            _gaugePanel.Paint += (s, e) =>
                SpeedTestUIHelper.PaintGauge(
                    e.Graphics,
                    new Rectangle(0, 0, _gaugePanel.Width, _gaugePanel.Height),
                    _gaugeValue, _gaugePeak, themeManager.IsDarkTheme);

            _lblBigValue = new Label
            {
                Text = "—",
                Font = new Font("Segoe UI", 40f, FontStyle.Bold),
                AutoSize = false,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                // Was hardcoded white — invisible on a light-theme card.
                ForeColor = themeManager.CurrentTheme.HeadingColor
            };

            var lblUnit = new Label
            {
                Text = "Mbps",
                Font = new Font("Segoe UI", 10f),
                AutoSize = false,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor
            };

            _lblPhase = new Label
            {
                Text = "Press Start to begin",
                Font = new Font("Segoe UI", 9f),
                AutoSize = false,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor
            };

            _progressTrack = new Panel
            {
                Height = Scale(6),
                BackColor = themeManager.IsDarkTheme
                    ? Color.FromArgb(35, 35, 48)
                    : Color.FromArgb(220, 222, 228)
            };
            _progressFill = new Panel
            {
                Height = Scale(6),
                Width = 0,
                Location = Point.Empty,
                BackColor = Color.FromArgb(59, 130, 246)
            };
            _progressTrack.Controls.Add(_progressFill);

            _statRow = new Panel { BackColor = Color.Transparent };

            bool isDark = themeManager.IsDarkTheme;
            (_lblDownVal, var downBox) = SpeedTestUIHelper.MakeStatBox("↓ Download", "—", Color.FromArgb(59, 130, 246), isDark);
            (_lblUpVal, var upBox) = SpeedTestUIHelper.MakeStatBox("↑ Upload", "—", Color.FromArgb(16, 185, 129), isDark);
            (_lblPingVal, var pingBox) = SpeedTestUIHelper.MakeStatBox("Ping", "—", Color.FromArgb(245, 158, 11), isDark);
            (_lblJitterVal, var jitterBox) = SpeedTestUIHelper.MakeStatBox("Jitter", "—", Color.FromArgb(168, 85, 247), isDark);
            _statRow.Controls.AddRange(new Control[] { downBox, upBox, pingBox, jitterBox });

            _btnStart = CreateButton("▶  Start Test", Color.FromArgb(59, 130, 246));
            _btnStart.Click += async (s, e) => await StartTestAsync();

            _btnCancel = CreateButton("■  Cancel", Color.FromArgb(239, 68, 68));
            _btnCancel.Enabled = false;
            _btnCancel.Click += (s, e) => _cts?.Cancel();

            // Only entry point back to Test History now that this page no
            // longer shows an inline log of its own.
            _btnViewHistory = new Button
            {
                Text = "View Full History →",
                Font = new Font("Segoe UI", 8.5f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                AutoSize = true,
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                TabStop = false
            };
            _btnViewHistory.FlatAppearance.BorderSize = 0;
            _btnViewHistory.Click += (s, e) => NavigationManager.Instance.NavigateTo("Test History");

            _insights = new SpeedTestInsightsPanel();

            panel.Controls.AddRange(new Control[]
            {
                _gaugePanel, _lblBigValue, lblUnit, _lblPhase,
                _progressTrack, _statRow, _btnStart, _btnCancel, _btnViewHistory,
                _insights
            });

            panel.SizeChanged += (s, e) => LayoutTestPanel(panel, lblUnit);
            panel.HandleCreated += (s, e) => LayoutTestPanel(panel, lblUnit);

            return panel;
        }

        // ── Layout (absolute positioning, centered within a capped width) ───

        private void LayoutTestPanel(Panel panel, Label lblUnit)
        {
            int w = panel.ClientSize.Width;
            int h = panel.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            _btnViewHistory.Location = new Point(w - _btnViewHistory.Width - Scale(4), Scale(4));

            // Two columns: gauge block on the left, insights on the right.
            // Below ~700px there isn't room for both, so the insights hide
            // and the gauge block centres on its own as before.
            bool twoColumns = w >= Scale(700);
            int leftW = twoColumns ? Math.Clamp((int)(w * 0.45), Scale(300), Scale(480)) : w;

            // Capped so the block stays a well-proportioned centerpiece
            // instead of stretching edge-to-edge on a wide window.
            int contentW = Math.Min(leftW - Scale(24), Scale(480));
            int contentX = (leftW - contentW) / 2;

            _insights.Visible = twoColumns;
            if (twoColumns)
            {
                int insightsX = leftW + Scale(12);
                int insightsY = _btnViewHistory.Bottom + Scale(4);
                _insights.SetBounds(insightsX, insightsY,
                    w - insightsX - Scale(8), h - insightsY - Scale(4));
            }

            int gaugeSize = Math.Min(contentW - Scale(40), (int)(h * 0.44));
            gaugeSize = Math.Max(gaugeSize, Scale(160));
            int gaugeX = contentX + (contentW - gaugeSize) / 2;
            int gaugeY = Scale(28);

            _gaugePanel.SetBounds(gaugeX, gaugeY, gaugeSize, gaugeSize);

            int y = gaugeY + gaugeSize - Scale(20);

            // Box height must clear the full glyph height of a 40pt bold
            // font (ascender+descender), not just its point size — a box
            // sized too close to the font size clips the top and bottom of
            // the digits equally under MiddleCenter alignment. 56 (a 1.4x
            // ratio) clipped; the rest of this layout uses a ~1.5-2x ratio
            // for its other labels, so match that here too.
            _lblBigValue.SetBounds(contentX, y, contentW, Scale(66));
            y += Scale(64);

            lblUnit.SetBounds(contentX, y, contentW, Scale(20));
            y += Scale(24);

            _lblPhase.SetBounds(contentX + Scale(8), y, contentW - Scale(16), Scale(20));
            y += Scale(28);

            _progressTrack.SetBounds(contentX + Scale(8), y, contentW - Scale(16), Scale(6));
            y += Scale(18);

            int statH = Scale(58);
            _statRow.SetBounds(contentX, y, contentW, statH);
            int boxW = contentW / 4;
            for (int i = 0; i < _statRow.Controls.Count; i++)
                _statRow.Controls[i].SetBounds(i * boxW, 0, boxW, statH);
            y += statH + Scale(16);

            int btnH = Scale(36);
            int btnGap = Scale(12);
            int btnW = (contentW - btnGap) / 2;
            _btnStart.SetBounds(contentX, y, btnW, btnH);
            _btnCancel.SetBounds(contentX + btnW + btnGap, y, btnW, btnH);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ANIMATION
        // ══════════════════════════════════════════════════════════════════════

        private void SetupAnimTimer()
        {
            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += (s, e) =>
            {
                if (Math.Abs(_gaugeValue - _gaugeTarget) < 0.3)
                {
                    _gaugeValue = _gaugeTarget;
                    _animTimer.Stop();
                }
                else
                {
                    double delta = _gaugeTarget - _gaugeValue;
                    _gaugeValue += delta * 0.15;
                }

                _gaugePanel.Invalidate();
                _lblBigValue.Text = SpeedTestUIHelper.FormatGaugeValue(_gaugeValue);
            };

            _liveTimer = new System.Windows.Forms.Timer { Interval = 120 };
            _liveTimer.Tick += (s, e) =>
            {
                if (!_isRunning || _gaugeTarget <= 0) return;

                double jitter = (Random.Shared.NextDouble() - 0.5) * 4.0;
                double display = Math.Clamp(_gaugeTarget + jitter, 0, SpeedTestUIHelper.MaxMbps);

                _gaugeValue += (display - _gaugeValue) * 0.25;
                _gaugePanel.Invalidate();
                _lblBigValue.Text = SpeedTestUIHelper.FormatGaugeValue(_gaugeValue);
            };
        }

        private void AnimateTo(double targetMbps)
        {
            _gaugeTarget = targetMbps;
            _gaugePeak = Math.Max(_gaugePeak, targetMbps);
            _animTimer?.Start();
            if (_isRunning) _liveTimer?.Start();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TEST RUNNER
        // ══════════════════════════════════════════════════════════════════════

        private async Task StartTestAsync()
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            SetRunningState(true);
            ResetDisplay();

            var progress = new Progress<SpeedTestProgress>(OnProgress);

            // SpeedTestRunner (T06) stamps % of plan and saves to history.
            var result = await SpeedTestRunner.Instance.RunAsync(progress, scheduled: false, _cts.Token);
            if (result == null)
            {
                SetRunningState(false);
                SetStatus("A scheduled test is running. Try again in a moment.");
                return;
            }
            _lastResult = result;

            if (_lastResult.Success)
            {
                // Set here rather than in OnProgress's Complete case:
                // Progress<T> posts asynchronously, so the Complete report
                // can arrive before _lastResult has been assigned.
                _lblDownVal.Text = $"{_lastResult.DownloadMbps:F1}";
                _lblUpVal.Text = $"{_lastResult.UploadMbps:F1}";
                _lblPingVal.Text = $"{_lastResult.PingMs:F0}";
                _lblJitterVal.Text = $"{_lastResult.JitterMs:F0}";
            }

            _insights.ShowResult(_lastResult);

            SetRunningState(false);

            if (!_lastResult.Success && !string.IsNullOrEmpty(_lastResult.ErrorMessage))
                SetStatus(_lastResult.ErrorMessage);
        }

        private void OnProgress(SpeedTestProgress p)
        {
            if (IsDisposed) return;

            _insights.OnProgress(p);

            // Latency probe samples only feed the live graph — they carry
            // no status text, percent or Mbps for the gauge.
            if (p.IsLatencySample) return;

            _lblPhase.Text = p.StatusText;

            int trackW = _progressTrack.Width;
            if (trackW > 0)
                _progressFill.Width = (int)(trackW * p.Percent / 100.0);

            _progressFill.BackColor = SpeedTestUIHelper.GetProgressColor(p.Phase);

            if (p.ValueMbps > 0)
                AnimateTo(p.ValueMbps);

            switch (p.Phase)
            {
                case SpeedTestPhase.Download when p.Percent == 100:
                    _lblDownVal.Text = $"{p.ValueMbps:F1}";
                    break;

                case SpeedTestPhase.Upload when p.Percent == 100:
                    _lblUpVal.Text = $"{p.ValueMbps:F1}";
                    break;

                case SpeedTestPhase.Complete:
                    // Final stat values are set in StartTestAsync once the
                    // result is in hand — _lastResult here may still be the
                    // previous test's.
                    AnimateTo(0);
                    _gaugePeak = 0;
                    break;

                case SpeedTestPhase.Failed:
                    _lblPhase.ForeColor = Color.FromArgb(239, 68, 68);
                    break;
            }

            SetStatus(p.StatusText);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  STATE + DISPLAY
        // ══════════════════════════════════════════════════════════════════════

        private void SetRunningState(bool running)
        {
            if (this.InvokeRequired) { this.Invoke(() => SetRunningState(running)); return; }

            _isRunning = running;
            _btnStart.Enabled = !running;
            _btnCancel.Enabled = running;

            if (!running)
            {
                _liveTimer?.Stop();
                Task.Delay(1500).ContinueWith(_ =>
                {
                    if (!IsDisposed)
                        BeginInvoke(() => AnimateTo(0));
                });
            }
        }

        private void ResetDisplay()
        {
            _gaugeValue = 0;
            _gaugeTarget = 0;
            _gaugePeak = 0;
            _gaugePanel.Invalidate();

            _lblBigValue.Text = "—";
            _lblPhase.Text = "Initialising...";
            _lblPhase.ForeColor = themeManager.CurrentTheme.SecondaryTextColor;
            _lblDownVal.Text = "—";
            _lblUpVal.Text = "—";
            _lblPingVal.Text = "—";
            _lblJitterVal.Text = "—";
            _progressFill.Width = 0;

            _insights.Reset();
        }

        private void SetStatus(string msg)
        {
            if (_lblStatus == null) return;

            if (this.InvokeRequired)
                this.Invoke(() => _lblStatus.Text = $"Status: {msg}");
            else
                _lblStatus.Text = $"Status: {msg}";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════════════════════════════

        private Button CreateButton(string text, Color backColor)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts?.Cancel();
                _animTimer?.Stop();
                _animTimer?.Dispose();
                _liveTimer?.Stop();
                _liveTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
