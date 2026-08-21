using TestNetSpeed.Helper;
using TestNetSpeed.Model;
using TestNetSpeed.Service;

namespace TestNetSpeed.UserControls.Pages
{
    /// <summary>
    /// "Speed Test" nav page — animated gauge + Ping/Download/Upload test
    /// against Cloudflare's speed endpoints, with a running history log.
    /// Ported from WiFiPasswordViewer as-is — this control never had any
    /// WiFi-specific coupling.
    /// </summary>
    public class SpeedTestControl : DpiAwareUserControl
    {
        private readonly SpeedTestService _service = new();

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
        private Button _btnStart, _btnCancel;
        private RichTextBox _historyBox;

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

            // ── Two-column body: gauge (left) / history (right) ────────────────
            var columns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            columns.Controls.Add(BuildGaugePanel(), 0, 0);
            columns.Controls.Add(BuildHistoryPanel(), 1, 0);
            card.Controls.Add(columns);
        }

        // ── Gauge panel ───────────────────────────────────────────────────────

        private Panel BuildGaugePanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, Scale(8), 0)
            };

            _gaugePanel = new Panel { BackColor = Color.Transparent };
            _gaugePanel.Paint += (s, e) =>
                SpeedTestUIHelper.PaintGauge(
                    e.Graphics,
                    new Rectangle(0, 0, _gaugePanel.Width, _gaugePanel.Height),
                    _gaugeValue, _gaugePeak);

            _lblBigValue = new Label
            {
                Text = "—",
                Font = new Font("Segoe UI", 32f, FontStyle.Bold),
                AutoSize = false,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White
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

            (_lblDownVal, var downBox) = SpeedTestUIHelper.MakeStatBox("↓ Download", "—");
            (_lblUpVal, var upBox) = SpeedTestUIHelper.MakeStatBox("↑ Upload", "—");
            (_lblPingVal, var pingBox) = SpeedTestUIHelper.MakeStatBox("Ping", "—");
            (_lblJitterVal, var jitterBox) = SpeedTestUIHelper.MakeStatBox("Jitter", "—");
            _statRow.Controls.AddRange(new Control[] { downBox, upBox, pingBox, jitterBox });

            _btnStart = CreateButton("▶  Start Test", Color.FromArgb(59, 130, 246));
            _btnStart.Click += async (s, e) => await StartTestAsync();

            _btnCancel = CreateButton("■  Cancel", Color.FromArgb(239, 68, 68));
            _btnCancel.Enabled = false;
            _btnCancel.Click += (s, e) => _cts?.Cancel();

            panel.Controls.AddRange(new Control[]
            {
                _gaugePanel, _lblBigValue, lblUnit, _lblPhase,
                _progressTrack, _statRow, _btnStart, _btnCancel
            });

            panel.SizeChanged += (s, e) => LayoutGaugePanel(panel, lblUnit);
            panel.HandleCreated += (s, e) => LayoutGaugePanel(panel, lblUnit);

            return panel;
        }

        // ── Gauge layout (absolute positioning) ─────────────────────────────

        private void LayoutGaugePanel(Panel panel, Label lblUnit)
        {
            int w = panel.ClientSize.Width;
            int h = panel.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int gaugeSize = Math.Min(w - Scale(32), (int)(h * 0.40));
            gaugeSize = Math.Max(gaugeSize, Scale(140));
            int gaugeX = (w - gaugeSize) / 2;
            int gaugeY = Scale(8);

            _gaugePanel.SetBounds(gaugeX, gaugeY, gaugeSize, gaugeSize);

            int y = gaugeY + gaugeSize - Scale(18);

            _lblBigValue.SetBounds(0, y, w, Scale(48));
            y += Scale(46);

            lblUnit.SetBounds(0, y, w, Scale(20));
            y += Scale(22);

            _lblPhase.SetBounds(Scale(8), y, w - Scale(16), Scale(20));
            y += Scale(26);

            _progressTrack.SetBounds(Scale(8), y, w - Scale(16), Scale(6));
            y += Scale(14);

            int statH = Scale(52);
            _statRow.SetBounds(0, y, w, statH);
            int boxW = w / 4;
            for (int i = 0; i < _statRow.Controls.Count; i++)
                _statRow.Controls[i].SetBounds(i * boxW, 0, boxW, statH);
            y += statH + Scale(12);

            int btnH = Scale(34);
            int btnW = (w - Scale(20)) / 2;
            _btnStart.SetBounds(Scale(4), y, btnW, btnH);
            _btnCancel.SetBounds(Scale(12) + btnW, y, btnW, btnH);
        }

        // ── History panel ────────────────────────────────────────────────────

        private Panel BuildHistoryPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = Scale(28),
                BackColor = Color.Transparent
            };

            var lblHistory = new Label
            {
                Text = "Test History",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = ScaledPoint(0, 6)
            };

            var btnClear = new Button
            {
                Text = "Clear",
                Font = new Font("Segoe UI", 8f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                AutoSize = true,
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) => _historyBox.Clear();

            header.Controls.AddRange(new Control[] { lblHistory, btnClear });
            header.SizeChanged += (s, e) =>
                btnClear.Location = new Point(header.Width - btnClear.Width - Scale(4), Scale(4));

            _historyBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 8.5f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BackColor = Color.FromArgb(14, 14, 20),
                ForeColor = Color.FromArgb(180, 190, 200)
            };

            panel.Controls.Add(_historyBox);
            panel.Controls.Add(header);

            return panel;
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

            _lastResult = await _service.RunFullTestAsync(progress, _cts.Token);

            if (_lastResult.Success)
            {
                AppendHistory(_lastResult);
                TestHistoryStore.Append(_lastResult);
            }

            SetRunningState(false);

            if (!_lastResult.Success && !string.IsNullOrEmpty(_lastResult.ErrorMessage))
                SetStatus(_lastResult.ErrorMessage);
        }

        private void OnProgress(SpeedTestProgress p)
        {
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
                    if (_lastResult != null)
                    {
                        _lblDownVal.Text = $"{_lastResult.DownloadMbps:F1}";
                        _lblUpVal.Text = $"{_lastResult.UploadMbps:F1}";
                        _lblPingVal.Text = $"{_lastResult.PingMs:F0}";
                        _lblJitterVal.Text = $"{_lastResult.JitterMs:F0}";
                    }
                    AnimateTo(0);
                    _gaugePeak = 0;
                    break;

                case SpeedTestPhase.Failed:
                    _lblPhase.ForeColor = Color.FromArgb(239, 68, 68);
                    break;
            }

            SetStatus(p.StatusText);
        }

        private void AppendHistory(SpeedTestResult r)
        {
            foreach (var (text, color) in SpeedTestUIHelper.BuildHistoryEntry(r))
            {
                _historyBox.SelectionStart = _historyBox.TextLength;
                _historyBox.SelectionLength = 0;
                _historyBox.SelectionColor = color;
                _historyBox.AppendText(text);
            }
            _historyBox.ScrollToCaret();
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
