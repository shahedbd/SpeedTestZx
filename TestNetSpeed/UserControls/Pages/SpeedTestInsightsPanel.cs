using System.Drawing.Imaging;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.Pages
{
    /// <summary>
    /// Right-hand column of the Speed Test page — everything that turns the
    /// raw numbers into answers:
    ///
    ///   • Live graph of speed and latency during the test (T25)
    ///   • Latency at 3 stages, packet loss, bufferbloat grade (T11–T14)
    ///   • Quality Score verdicts + streaming estimate (T15, T29)
    ///   • ISP and test server (T05)
    ///   • Copy / save the shareable result card (T16)
    ///
    /// The page forwards every progress report to <see cref="OnProgress"/>
    /// and the finished result to <see cref="ShowResult"/>.
    /// </summary>
    public class SpeedTestInsightsPanel : DpiAwareUserControl
    {
        private static readonly Color DimAccent = Color.FromArgb(148, 163, 184);

        private SpeedTestResult? _result;
        private QualityReport? _report;

        private Label _lblGraphTitle, _lblLatencyTitle, _lblQualityTitle;
        private LiveGraphControl _graph;

        private Panel _latencyRow;
        private Label _lblIdle, _lblLoadDown, _lblLoadUp, _lblLoss, _lblBloat;

        private VerdictRow _rowStreaming, _rowGaming, _rowCalls;
        private Label _lblStreamingEstimate;
        private Label _lblIsp;

        private Button _btnCopy, _btnSave;

        public SpeedTestInsightsPanel()
        {
            BackColor = Color.Transparent;
            DoubleBuffered = true;

            Build();
            SizeChanged += (s, e) => DoLayout();
            ShowEmpty();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD
        // ══════════════════════════════════════════════════════════════════════

        private void Build()
        {
            _lblGraphTitle = SectionTitle("Live speed & latency");
            _graph = new LiveGraphControl { BackColor = themeManager.CurrentTheme.CardBackground };

            _lblLatencyTitle = SectionTitle("Latency & stability");
            _latencyRow = new Panel { BackColor = Color.Transparent };

            bool dark = themeManager.IsDarkTheme;
            var amber = Color.FromArgb(245, 158, 11);
            (_lblIdle, var idleBox) = SpeedTestUIHelper.MakeStatBox("Idle ms", "—", amber, dark);
            (_lblLoadDown, var downBox) = SpeedTestUIHelper.MakeStatBox("↓ Loaded ms", "—", Color.FromArgb(59, 130, 246), dark);
            (_lblLoadUp, var upBox) = SpeedTestUIHelper.MakeStatBox("↑ Loaded ms", "—", Color.FromArgb(16, 185, 129), dark);
            (_lblLoss, var lossBox) = SpeedTestUIHelper.MakeStatBox("Packet loss", "—", Color.FromArgb(239, 68, 68), dark);
            (_lblBloat, var bloatBox) = SpeedTestUIHelper.MakeStatBox("Bufferbloat", "—", DimAccent, dark);
            _latencyRow.Controls.AddRange(new Control[] { idleBox, downBox, upBox, lossBox, bloatBox });

            _lblQualityTitle = SectionTitle("Connection quality");
            _rowStreaming = new VerdictRow(this, "Streaming");
            _rowGaming = new VerdictRow(this, "Gaming");
            _rowCalls = new VerdictRow(this, "Video calls");

            _lblStreamingEstimate = InfoLabel();
            _lblIsp = InfoLabel();

            _btnCopy = ActionButton("⧉  Copy result card");
            _btnCopy.Click += (s, e) => CopyCard();
            _btnSave = ActionButton("⤓  Save as PNG");
            _btnSave.Click += (s, e) => SaveCard();

            Controls.AddRange(new Control[]
            {
                _lblGraphTitle, _graph,
                _lblLatencyTitle, _latencyRow,
                _lblQualityTitle, _rowStreaming, _rowGaming, _rowCalls,
                _lblStreamingEstimate, _lblIsp,
                _btnCopy, _btnSave,
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LAYOUT — graph takes whatever height is left after the fixed rows
        // ══════════════════════════════════════════════════════════════════════

        private void DoLayout()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int titleH = Scale(22);
            int statH = Scale(54);
            int verdictH = Scale(26);
            int infoH = Scale(20);
            int btnH = Scale(32);
            int gap = Scale(8);

            int fixedH = titleH + gap                     // graph title
                       + titleH + statH + gap             // latency
                       + titleH + verdictH * 3 + gap      // quality
                       + infoH * 2 + gap                  // streaming + ISP
                       + btnH;

            int graphH = Math.Max(Scale(90), h - fixedH - gap);

            int y = 0;
            _lblGraphTitle.SetBounds(0, y, w, titleH); y += titleH;
            _graph.SetBounds(0, y, w, graphH); y += graphH + gap;

            _lblLatencyTitle.SetBounds(0, y, w, titleH); y += titleH;
            _latencyRow.SetBounds(0, y, w, statH);
            int boxW = w / _latencyRow.Controls.Count;
            for (int i = 0; i < _latencyRow.Controls.Count; i++)
                _latencyRow.Controls[i].SetBounds(i * boxW, 0, boxW - Scale(4), statH);
            y += statH + gap;

            _lblQualityTitle.SetBounds(0, y, w, titleH); y += titleH;
            foreach (var row in new[] { _rowStreaming, _rowGaming, _rowCalls })
            {
                row.SetBounds(0, y, w, verdictH);
                y += verdictH;
            }
            y += gap;

            _lblStreamingEstimate.SetBounds(0, y, w, infoH); y += infoH;
            _lblIsp.SetBounds(0, y, w, infoH); y += infoH + gap;

            int bw = (w - gap) / 2;
            _btnCopy.SetBounds(0, y, bw, btnH);
            _btnSave.SetBounds(bw + gap, y, w - bw - gap, btnH);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FEED
        // ══════════════════════════════════════════════════════════════════════

        public void Reset()
        {
            _result = null;
            _report = null;
            _graph.Reset();
            ShowEmpty();
            _lblStreamingEstimate.Text = "Testing…";
            _lblIsp.Text = "";
        }

        public void OnProgress(SpeedTestProgress p)
        {
            if (p.IsLatencySample)
            {
                _graph.AddLatency(p.LatencyMs);
                return;
            }

            if (p.Phase is SpeedTestPhase.Download or SpeedTestPhase.Upload && p.Percent < 100)
                _graph.AddThroughput(p.Phase, p.ValueMbps);
        }

        public void ShowResult(SpeedTestResult r)
        {
            _graph.Freeze();

            if (!r.Success)
            {
                ShowEmpty();
                _lblStreamingEstimate.Text = "Run a test to see what your connection is good for.";
                return;
            }

            _result = r;
            _report = QualityScorer.Score(r);

            _lblIdle.Text = Ms(r.IdleLatencyMs);
            _lblLoadDown.Text = Ms(r.LoadedLatencyDownMs);
            _lblLoadUp.Text = Ms(r.LoadedLatencyUpMs);
            _lblLoss.Text = r.PacketLossPct is double loss
                ? $"{loss:0.#}%{(r.LatencyMethod == "TCP" ? "*" : "")}"
                : "—";

            _lblBloat.Text = _report.BufferbloatGrade;
            _lblBloat.ForeColor = QualityScorer.GradeColor(_report.BufferbloatGrade);

            _rowStreaming.Show(_report.Streaming);
            _rowGaming.Show(_report.Gaming);
            _rowCalls.Show(_report.Calls);

            _lblStreamingEstimate.Text =
                $"Streaming estimate: {_report.StreamingEstimate.Resolution}  ·  Buffering risk: {_report.StreamingEstimate.BufferingRisk}" +
                (r.LatencyMethod == "TCP" ? "   (* ICMP blocked — loss is approximate)" : "");

            _lblIsp.Text = BuildIspText(r);

            _btnCopy.Enabled = true;
            _btnSave.Enabled = true;
        }

        private void ShowEmpty()
        {
            foreach (var lbl in new[] { _lblIdle, _lblLoadDown, _lblLoadUp, _lblLoss, _lblBloat })
                lbl.Text = "—";
            _lblBloat.ForeColor = DimAccent;

            _rowStreaming.Clear();
            _rowGaming.Clear();
            _rowCalls.Clear();

            _lblStreamingEstimate.Text = "Run a test to see what your connection is good for.";
            _lblIsp.Text = "";

            _btnCopy.Enabled = false;
            _btnSave.Enabled = false;
        }

        private static string Ms(double? v) => v is double ms ? $"{ms:0}" : "—";

        private static string BuildIspText(SpeedTestResult r)
        {
            var parts = new List<string>();
            if (r.Isp.Length > 0) parts.Add($"ISP: {r.Isp}");

            string where = string.Join(", ", new[] { r.ClientCity, r.ClientCountry }.Where(s => s.Length > 0));
            if (where.Length > 0) parts.Add(where);

            if (r.ServerColo.Length > 0) parts.Add($"Server: Cloudflare {r.ServerColo}");

            return parts.Count > 0 ? string.Join("  ·  ", parts) : "ISP: unavailable";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SHARE CARD (T16)
        // ══════════════════════════════════════════════════════════════════════

        private void CopyCard()
        {
            if (_result == null || _report == null) return;

            try
            {
                // Clipboard.SetImage copies with flush, so the bitmap can be
                // disposed straight after.
                using var bmp = ResultCardRenderer.Render(_result, _report);
                Clipboard.SetImage(bmp);
                NotificationManager.Instance.ShowSuccess("Result card copied — paste it anywhere.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Copy result card failed: {ex.Message}");
                NotificationManager.Instance.ShowError("Couldn't copy the result card.");
            }
        }

        private void SaveCard()
        {
            if (_result == null || _report == null) return;

            using var dlg = new SaveFileDialog
            {
                Title = "Save result card",
                Filter = "PNG image (*.png)|*.png",
                FileName = $"{AppConfig.AppName.Replace(" ", "")}_{_result.TestedAt:yyyyMMdd_HHmm}.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                AddExtension = true,
                DefaultExt = "png",
            };

            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            try
            {
                using var bmp = ResultCardRenderer.Render(_result, _report);
                bmp.Save(dlg.FileName, ImageFormat.Png);
                NotificationManager.Instance.ShowSuccess("Result card saved.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Save result card failed: {ex.Message}");
                NotificationManager.Instance.ShowError("Couldn't save the result card.");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PIECES
        // ══════════════════════════════════════════════════════════════════════

        private Label SectionTitle(string text) => new()
        {
            Text = text,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = themeManager.CurrentTheme.HeadingColor,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
        };

        private Label InfoLabel() => new()
        {
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            AutoEllipsis = true,
            UseMnemonic = false,
        };

        private Button ActionButton(string text)
        {
            var accent = themeManager.CurrentTheme.AccentColor;
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                ForeColor = accent,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false,
                UseMnemonic = false,
                UseVisualStyleBackColor = false,
            };
            btn.FlatAppearance.BorderColor = accent;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, accent);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, accent);
            return btn;
        }

        /// <summary>"Streaming   GREAT   Plenty of headroom" — name, level, reason.</summary>
        private sealed class VerdictRow : Panel
        {
            private readonly Label _name, _level, _reason;
            private readonly SpeedTestInsightsPanel _owner;

            public VerdictRow(SpeedTestInsightsPanel owner, string useCase)
            {
                _owner = owner;
                BackColor = Color.Transparent;

                var theme = owner.themeManager.CurrentTheme;
                _name = new Label
                {
                    Text = useCase,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = theme.TextColor,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleLeft,
                };
                _level = new Label
                {
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleLeft,
                };
                _reason = new Label
                {
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = theme.SecondaryTextColor,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    UseMnemonic = false,
                };

                Controls.AddRange(new Control[] { _name, _level, _reason });
                SizeChanged += (s, e) => LayoutRow();
            }

            public void Show(QualityVerdict v)
            {
                _level.Text = v.Level.ToString().ToUpperInvariant();
                _level.ForeColor = QualityScorer.LevelColor(v.Level);
                _reason.Text = v.Reason;
            }

            public void Clear()
            {
                _level.Text = "—";
                _level.ForeColor = _owner.themeManager.CurrentTheme.SecondaryTextColor;
                _reason.Text = "";
            }

            private void LayoutRow()
            {
                int nameW = DpiCommon.Helper.DpiAwareService.Scale(96);
                int levelW = DpiCommon.Helper.DpiAwareService.Scale(64);
                _name.SetBounds(0, 0, nameW, Height);
                _level.SetBounds(nameW, 0, levelW, Height);
                _reason.SetBounds(nameW + levelW, 0, Math.Max(0, Width - nameW - levelW), Height);
            }
        }
    }
}
