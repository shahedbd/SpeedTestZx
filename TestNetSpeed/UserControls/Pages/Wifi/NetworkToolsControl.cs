using TestNetSpeed.Model;
using TestNetSpeed.Service;

namespace TestNetSpeed.UserControls.Wifi
{
    /// <summary>
    /// "Network Tools" nav page — Ping, DNS Lookup, IP Lookup and Traceroute,
    /// each run against <see cref="NetworkToolsService"/> and streamed into a
    /// terminal-style output box.
    /// </summary>
    public class NetworkToolsControl : DpiAwareUserControl
    {
        private readonly NetworkToolsService _service = new();

        private static readonly string[] ToolNames =
            { "Ping", "DNS Lookup", "IP Lookup", "Traceroute" };

        // ── State ─────────────────────────────────────────────────────────────
        private CancellationTokenSource _cts;
        private bool _isRunning;

        // ── UI refs ───────────────────────────────────────────────────────────
        private Panel _card;
        private TabControl _tabs;
        private RichTextBox _outputBox;
        private Label _lblStatus;
        private Button _btnRun;
        private Button _btnCancel;
        private Button _btnClear;
        private Button _btnCopy;

        // ── Per-tab inputs ────────────────────────────────────────────────────
        private TextBox _pingHost;
        private NumericUpDown _pingCount;
        private TextBox _dnsHost;
        private TextBox _ipInput;
        private TextBox _traceHost;

        public NetworkToolsControl()
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

            _card = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, Scale(10)),
                BackColor = themeManager.CurrentTheme.CardBackground,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = ScaledPadding(10)
            };
            outer.Controls.Add(_card, 0, 0);

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

            // ── Tabs (tool selector) ────────────────────────────────────────
            _tabs = new TabControl
            {
                Dock = DockStyle.Top,
                Height = Scale(110),
                Font = new Font("Segoe UI", 9f)
            };
            _tabs.TabPages.Add(BuildPingTab());
            _tabs.TabPages.Add(BuildDnsTab());
            _tabs.TabPages.Add(BuildIpTab());
            _tabs.TabPages.Add(BuildTraceTab());

            // ── Action bar ───────────────────────────────────────────────────
            var actionBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = Scale(46),
                BackColor = Color.Transparent
            };

            _btnRun = CreateButton("▶  Run", Color.FromArgb(59, 130, 246), true);
            _btnCancel = CreateButton("■  Cancel", Color.FromArgb(239, 68, 68), true);
            _btnClear = CreateButton("✕  Clear", Color.Transparent, false);
            _btnCopy = CreateButton("⧉  Copy", Color.Transparent, false);
            _btnCancel.Enabled = false;

            _btnRun.Click += async (s, e) => await RunToolAsync();
            _btnCancel.Click += (s, e) => _cts?.Cancel();
            _btnClear.Click += (s, e) => ClearOutput();
            _btnCopy.Click += (s, e) => CopyOutput();

            actionBar.Controls.AddRange(new Control[] { _btnRun, _btnCancel, _btnClear, _btnCopy });
            actionBar.SizeChanged += (s, e) =>
            {
                int gap = Scale(6);
                int btnH = Scale(30);
                int btnY = (actionBar.Height - btnH) / 2;
                int x = Scale(2);

                foreach (var btn in new[] { _btnRun, _btnCancel, _btnClear, _btnCopy })
                {
                    btn.Location = new Point(x, btnY);
                    btn.Height = btnH;
                    x += btn.Width + gap;
                }
            };

            // ── Output section ───────────────────────────────────────────────
            var outputHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = Scale(26),
                BackColor = Color.Transparent
            };
            outputHeader.Controls.Add(new Label
            {
                Text = "Output",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = ScaledPoint(2, 6)
            });

            var divider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = themeManager.CurrentTheme.BorderColor
            };

            _outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BackColor = Color.FromArgb(14, 14, 20),
                ForeColor = Color.FromArgb(200, 210, 220),
                Padding = ScaledPadding(6)
            };

            // Dock order: Fill first, then Top items — last-added Top ends up topmost.
            _card.Controls.Add(_outputBox);
            _card.Controls.Add(outputHeader);
            _card.Controls.Add(divider);
            _card.Controls.Add(actionBar);
            _card.Controls.Add(_tabs);
        }

        // ── Tab builders ──────────────────────────────────────────────────────

        private TabPage BuildPingTab()
        {
            _pingHost = CreateTextBox("e.g. google.com  or  8.8.8.8");
            _pingHost.KeyDown += OnEnterKey;

            _pingCount = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 20,
                Value = 4,
                Width = Scale(64),
                Font = new Font("Segoe UI", 9f)
            };

            var layout = CreateTabLayout(2);
            AddInputRow(layout, 0, "Host:", _pingHost);
            AddInputRow(layout, 1, "Count:", _pingCount);

            return WrapTab("Ping", layout);
        }

        private TabPage BuildDnsTab()
        {
            _dnsHost = CreateTextBox("e.g. google.com");
            _dnsHost.KeyDown += OnEnterKey;

            var layout = CreateTabLayout(1);
            AddInputRow(layout, 0, "Domain:", _dnsHost);

            return WrapTab("DNS Lookup", layout);
        }

        private TabPage BuildIpTab()
        {
            _ipInput = CreateTextBox("e.g. 192.168.1.1  or  google.com");
            _ipInput.KeyDown += OnEnterKey;

            var layout = CreateTabLayout(1);
            AddInputRow(layout, 0, "IP / Domain:", _ipInput);

            return WrapTab("IP Lookup", layout);
        }

        private TabPage BuildTraceTab()
        {
            _traceHost = CreateTextBox("e.g. google.com  or  8.8.8.8");
            _traceHost.KeyDown += OnEnterKey;

            var warn = new Label
            {
                Text = "⚠  May take up to 60 seconds.",
                Font = new Font("Segoe UI", 8f),
                AutoSize = true,
                ForeColor = Color.FromArgb(245, 158, 11),
                BackColor = Color.Transparent
            };

            var layout = CreateTabLayout(2);
            AddInputRow(layout, 0, "Host:", _traceHost);
            layout.Controls.Add(warn, 1, 1);

            return WrapTab("Traceroute", layout);
        }

        private TableLayoutPanel CreateTabLayout(int rows)
        {
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = rows,
                Padding = ScaledPadding(4, 6, 4, 0)
            };

            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Scale(90)));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            for (int i = 0; i < rows; i++)
                tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(32)));

            return tlp;
        }

        private void AddInputRow(TableLayoutPanel tlp, int row, string labelText, Control input)
        {
            var lbl = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 9f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, Scale(3), Scale(4), Scale(3));

            tlp.Controls.Add(lbl, 0, row);
            tlp.Controls.Add(input, 1, row);
        }

        private TabPage WrapTab(string title, Control content)
        {
            var tab = new TabPage
            {
                Text = title,
                BackColor = themeManager.CurrentTheme.CardBackground,
                Padding = ScaledPadding(4)
            };
            content.Dock = DockStyle.Fill;
            tab.Controls.Add(content);
            return tab;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  RUN
        // ══════════════════════════════════════════════════════════════════════

        private async Task RunToolAsync()
        {
            if (_isRunning) return;

            int toolIndex = _tabs.SelectedIndex;
            string toolName = toolIndex >= 0 && toolIndex < ToolNames.Length
                ? ToolNames[toolIndex] : string.Empty;

            string input = GetCurrentInput().Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                AppendLine("⚠  Please enter a host or IP address.", Color.FromArgb(245, 158, 11));
                return;
            }

            _cts = new CancellationTokenSource();
            SetRunningState(true);
            ClearOutput();

            AppendLine($"── {toolName}  ·  {input} ──────────────────────", Color.FromArgb(59, 130, 246));
            AppendLine(string.Empty);

            var progress = new Progress<string>(line =>
            {
                if (IsHandleCreated)
                    BeginInvoke(() => AppendLine(line));
            });

            try
            {
                NetworkToolResult result = toolIndex switch
                {
                    0 => await _service.PingAsync(input, (int)_pingCount.Value, progress, _cts.Token),
                    1 => await _service.DnsLookupAsync(input, _cts.Token),
                    2 => await _service.IpLookupAsync(input, _cts.Token),
                    3 => await _service.TraceRouteAsync(input, progress, _cts.Token),
                    _ => new NetworkToolResult { Success = false, ErrorMessage = "Unknown tool selected." }
                };

                if (toolIndex is 1 or 2)
                {
                    if (result.Success)
                        AppendLine(result.Output ?? string.Empty);
                    else
                        AppendLine($"✖  {result.ErrorMessage}", Color.FromArgb(239, 68, 68));
                }
                else if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
                {
                    AppendLine($"\n✖  {result.ErrorMessage}", Color.FromArgb(239, 68, 68));
                }

                SetStatus(result.Success
                    ? $"{toolName} completed successfully."
                    : $"{toolName} failed.");
            }
            catch (OperationCanceledException)
            {
                AppendLine("\n[Cancelled by user]", Color.FromArgb(245, 158, 11));
                SetStatus("Cancelled.");
            }
            catch (Exception ex)
            {
                AppendLine($"\n✖  Unexpected error: {ex.Message}", Color.FromArgb(239, 68, 68));
                SetStatus("Error.");
            }
            finally
            {
                SetRunningState(false);
            }
        }

        private string GetCurrentInput() => _tabs.SelectedIndex switch
        {
            0 => _pingHost.Text,
            1 => _dnsHost.Text,
            2 => _ipInput.Text,
            3 => _traceHost.Text,
            _ => string.Empty
        };

        private void OnEnterKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || _isRunning) return;
            e.SuppressKeyPress = true;
            _ = RunToolAsync();
        }

        private void SetRunningState(bool running)
        {
            if (this.InvokeRequired) { this.Invoke(() => SetRunningState(running)); return; }

            _isRunning = running;
            _btnRun.Enabled = !running;
            _btnCancel.Enabled = running;
            _tabs.Enabled = !running;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  OUTPUT
        // ══════════════════════════════════════════════════════════════════════

        private void AppendLine(string text, Color? color = null)
        {
            if (_outputBox.InvokeRequired)
            {
                _outputBox.Invoke(() => AppendLine(text, color));
                return;
            }

            _outputBox.SuspendLayout();
            _outputBox.SelectionStart = _outputBox.TextLength;
            _outputBox.SelectionLength = 0;
            _outputBox.SelectionColor = color ?? _outputBox.ForeColor;
            _outputBox.AppendText(text + "\n");
            _outputBox.ScrollToCaret();
            _outputBox.ResumeLayout();
        }

        private void ClearOutput()
        {
            if (_outputBox.InvokeRequired)
                _outputBox.Invoke(ClearOutput);
            else
                _outputBox.Clear();
        }

        private void CopyOutput()
        {
            if (string.IsNullOrWhiteSpace(_outputBox.Text)) return;
            try
            {
                Clipboard.SetText(_outputBox.Text);
                SetStatus("Output copied to clipboard.");
            }
            catch { /* clipboard busy */ }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════════════════════════════

        private Button CreateButton(string text, Color backColor, bool solid)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Padding = new Padding(10, 0, 10, 0),
                BackColor = solid ? backColor : (themeManager.IsDarkTheme
                    ? Color.FromArgb(45, 45, 45) : Color.FromArgb(240, 241, 243)),
                ForeColor = solid ? Color.White : themeManager.CurrentTheme.TextColor
            };
            btn.FlatAppearance.BorderSize = solid ? 0 : 1;
            btn.FlatAppearance.BorderColor = themeManager.CurrentTheme.BorderColor;
            return btn;
        }

        private TextBox CreateTextBox(string placeholder) => new()
        {
            Font = new Font("Segoe UI", 9f),
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = placeholder,
            BackColor = themeManager.IsDarkTheme ? Color.FromArgb(55, 55, 55) : Color.White,
            ForeColor = themeManager.CurrentTheme.TextColor
        };

        private void SetStatus(string msg)
        {
            if (_lblStatus == null) return;

            if (this.InvokeRequired)
                this.Invoke(() => _lblStatus.Text = $"Status: {msg}");
            else
                _lblStatus.Text = $"Status: {msg}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _cts?.Cancel();
            base.Dispose(disposing);
        }
    }
}
