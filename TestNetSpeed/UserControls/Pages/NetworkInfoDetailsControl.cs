using TestNetSpeed.Model;
using TestNetSpeed.Service;

namespace TestNetSpeed.UserControls.Pages
{
    /// <summary>
    /// Renders the current connection's adapter/IP/gateway/DNS/etc. as a
    /// scrollable list of copyable rows. Ported from WiFiPasswordViewer's
    /// WifiNetworkDetailsControl as-is (renamed only) — every field here
    /// was already generic network info, nothing WiFi-specific.
    /// </summary>
    public class NetworkInfoDetailsControl : DpiAwareUserControl
    {
        // ── Service ───────────────────────────────────────────────────────────
        private readonly NetworkDetailsService _networkService;

        // ── State ─────────────────────────────────────────────────────────────
        private NetworkDetails? _details;
        private bool _isLoading = false;

        // ── UI value label refs (for live patch without full rebuild) ──────────
        private readonly Dictionary<string, Label> _valueLabels = new();

        // ── UI refs ───────────────────────────────────────────────────────────
        private Panel _headerPanel;
        private Panel _bodyPanel;
        private Button _btnRefresh;
        private Button _btnCopyAll;

        // ── Row definitions ───────────────────────────────────────────────────
        private static readonly (string Icon, Color IconColor, string Label)[] RowDefs =
        [
            ("🔌", Color.FromArgb(59,  130, 246), "Adapter Name"),
            ("🔗", Color.FromArgb(20,  184, 166), "Connection Type"),
            ("📍", Color.FromArgb(16,  185, 129), "Local IP Address"),
            ("🌐", Color.FromArgb(14,  165, 233), "Public IP Address"),
            ("🔲", Color.FromArgb(168, 85,  247), "Subnet Mask"),
            ("→",  Color.FromArgb(245, 158, 11),  "Default Gateway"),
            ("☰",  Color.FromArgb(239, 68,  68),  "DNS Servers"),
            ("📟", Color.FromArgb(107, 114, 128), "MAC Address"),
            ("⚡", Color.FromArgb(234, 179, 8),   "Link Speed"),
            ("🌐", Color.FromArgb(99,  102, 241), "IPv6 Address"),
        ];

        // ── Public event ──────────────────────────────────────────────────────
        public event Action<string>? StatusChanged;

        public NetworkInfoDetailsControl(NetworkDetailsService networkService)
        {
            _networkService = networkService;

            this.BackColor = themeManager.CurrentTheme.CardBackground;
            this.BorderStyle = BorderStyle.FixedSingle;

            // Build skeleton UI immediately — no data yet
            BuildSkeleton();

            // Load data once the control is actually shown and sized
            this.HandleCreated += (_, _) => _ = LoadAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD SKELETON  (called once in constructor)
        // ══════════════════════════════════════════════════════════════════════

        private void BuildSkeleton()
        {
            this.Controls.Clear();
            _valueLabels.Clear();

            // ── Outer TableLayoutPanel: header row + body ─────────────────────
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
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, Scale(44)));  // header
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));         // body
            this.Controls.Add(outer);

            // ── Header ────────────────────────────────────────────────────────
            _headerPanel = BuildHeader();
            outer.Controls.Add(_headerPanel, 0, 0);

            // ── Body (scrollable rows) ────────────────────────────────────────
            _bodyPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            outer.Controls.Add(_bodyPanel, 0, 1);

            // Render rows with placeholder values
            RenderRows();
        }

        // ── Header ────────────────────────────────────────────────────────────
        private Panel BuildHeader()
        {
            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = themeManager.IsDarkTheme
                    ? Color.FromArgb(40, 40, 40)
                    : Color.FromArgb(241, 245, 249)
            };

            // ── Title — smaller font so it doesn't crowd the buttons ─────────────
            header.Controls.Add(new Label
            {
                Text = "Network Details  (Current Connection)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.HeadingColor,
                BackColor = Color.Transparent,
                Location = ScaledPoint(12, 13),
                AutoSize = true
            });

            // ── Refresh button — icon only, fixed size ────────────────────────────
            _btnRefresh = new Button
            {
                Text = "↺",
                Font = new Font("Segoe UI", 11f),
                Size = ScaledSize(32, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = themeManager.IsDarkTheme
                    ? Color.FromArgb(55, 55, 55)
                    : Color.FromArgb(226, 232, 240),
                ForeColor = themeManager.CurrentTheme.TextColor,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TabStop = false
            };
            _btnRefresh.FlatAppearance.BorderSize = 0;
            _btnRefresh.Click += async (s, e) =>
            {
                _btnRefresh.Enabled = false;
                _btnRefresh.Text = "…";
                await LoadAsync();
                _btnRefresh.Enabled = true;
                _btnRefresh.Text = "↺";
            };

            // ── Copy All button — fixed size ──────────────────────────────────────
            _btnCopyAll = new Button
            {
                Text = "⊕ Copy All",
                Font = new Font("Segoe UI", 8f),
                Size = ScaledSize(76, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TabStop = false
            };
            _btnCopyAll.FlatAppearance.BorderSize = 0;
            _btnCopyAll.Click += BtnCopyAll_Click;

            // ── Position buttons using SizeChanged — guaranteed correct width ─────
            // Total button block width: Refresh(32) + gap(4) + CopyAll(76) + margin(8)
            header.SizeChanged += (_, _) =>
            {
                int margin = Scale(8);
                int gap = Scale(4);
                int btnTop = (Scale(44) - Scale(28)) / 2;   // vertically centred

                _btnCopyAll.Location = new Point(
                    header.Width - margin - _btnCopyAll.Width,
                    btnTop);

                _btnRefresh.Location = new Point(
                    _btnCopyAll.Left - gap - _btnRefresh.Width,
                    btnTop);
            };

            header.Controls.Add(_btnRefresh);
            header.Controls.Add(_btnCopyAll);

            return header;
        }
        private Button MakeHeaderButton(
            string text, Color backColor, bool useAccent = true)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 8f),
                Size = ScaledSize(80, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = useAccent
                    ? Color.White
                    : themeManager.CurrentTheme.TextColor,
                Cursor = Cursors.Hand,
                Margin = new Padding(Scale(4), 0, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // ── Rows ──────────────────────────────────────────────────────────────

        private void RenderRows()
        {
            _bodyPanel.Controls.Clear();
            _valueLabels.Clear();

            int y = 0;
            foreach (var (icon, iconColor, label) in RowDefs)
            {
                string value = GetCurrentValue(label);
                var row = BuildRow(icon, iconColor, label, value, y);
                _bodyPanel.Controls.Add(row);
                y += row.Height;
            }
        }

        private string GetCurrentValue(string label) => label switch
        {
            "Adapter Name" => _details?.AdapterName ?? "Loading...",
            "Connection Type" => _details?.ConnectionType ?? "Loading...",
            "Local IP Address" => _details?.LocalIpAddress ?? "Loading...",
            "Public IP Address" => _details?.PublicIpAddress ?? "Fetching...",
            "Subnet Mask" => _details?.SubnetMask ?? "Loading...",
            "Default Gateway" => _details?.Gateway ?? "Loading...",
            "DNS Servers" => _details?.DnsServers ?? "Loading...",
            "MAC Address" => _details?.MacAddress ?? "Loading...",
            "Link Speed" => _details?.LinkSpeed ?? "Loading...",
            "IPv6 Address" => _details?.IPv6Address ?? "Loading...",
            _ => "—"
        };

        private Panel BuildRow(
            string icon, Color iconColor,
            string label, string value, int y)
        {
            int rowH = Scale(38);
            bool isLoading = value is "Loading..." or "Fetching...";

            var row = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(_bodyPanel.Width > 0
                    ? _bodyPanel.Width : 400, rowH),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Separator line at bottom
            row.Controls.Add(new Panel
            {
                Location = new Point(Scale(12), rowH - 1),
                Size = new Size(Math.Max(row.Width - Scale(24), 10), 1),
                BackColor = themeManager.IsDarkTheme
                    ? Color.FromArgb(50, 50, 50)
                    : Color.FromArgb(226, 232, 240),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });

            // ── Coloured icon circle ──────────────────────────────────────────
            int circleSize = Scale(22);
            var iconCircle = new Panel
            {
                Location = new Point(Scale(10),
                    (rowH - circleSize) / 2),
                Size = new Size(circleSize, circleSize),
                BackColor = Color.FromArgb(30,
                    iconColor.R, iconColor.G, iconColor.B)
            };
            MakeCircle(iconCircle);

            iconCircle.Controls.Add(new Label
            {
                Text = icon,
                Font = new Font("Segoe UI Emoji", 9f),
                ForeColor = iconColor,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            row.Controls.Add(iconCircle);

            // ── Key label ─────────────────────────────────────────────────────
            row.Controls.Add(new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                Location = new Point(Scale(38), (rowH - Scale(16)) / 2),
                Size = new Size(Scale(135), Scale(16)),
                TextAlign = ContentAlignment.MiddleLeft
            });

            // ── Value label — click to copy ───────────────────────────────────
            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = isLoading
                    ? themeManager.CurrentTheme.SecondaryTextColor
                    : iconColor,
                BackColor = Color.Transparent,
                Location = new Point(Scale(178), (rowH - Scale(16)) / 2),
                Size = new Size(
                    Math.Max(row.Width - Scale(190), 10), Scale(16)),
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
                             | AnchorStyles.Right
            };

            lblValue.Click += (s, e) =>
            {
                string v = lblValue.Text;
                if (!string.IsNullOrEmpty(v) &&
                    v is not ("N/A" or "Loading..." or "Fetching..."))
                {
                    Clipboard.SetText(v);
                    StatusChanged?.Invoke($"'{label}' copied: {v}");
                }
            };
            lblValue.MouseEnter += (s, e) =>
                lblValue.ForeColor = Color.FromArgb(59, 130, 246);
            lblValue.MouseLeave += (s, e) =>
                lblValue.ForeColor = isLoading
                    ? themeManager.CurrentTheme.SecondaryTextColor
                    : iconColor;

            row.Controls.Add(lblValue);

            // Register for live patching
            _valueLabels[label] = lblValue;

            return row;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LOAD  — two-phase: local instant, public IP async
        // ══════════════════════════════════════════════════════════════════════

        public async Task LoadAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            StatusChanged?.Invoke("Loading network details...");

            try
            {
                // Phase 1 — local data (instant, no I/O)
                _details = await _networkService.GetLocalDetailsAsync();
                InvokeIfRequired(RenderRows);
                StatusChanged?.Invoke("Local details loaded.");

                // Phase 2 — public IP (HTTP, 1-3s, non-blocking)
                string pubIp = await _networkService.GetPublicIpAsync();
                if (_details != null)
                    _details.PublicIpAddress = pubIp;

                InvokeIfRequired(() =>
                    PatchValue("Public IP Address", pubIp));

                StatusChanged?.Invoke("Network details refreshed.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NetworkInfoDetailsControl] {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LIVE PATCH
        // ══════════════════════════════════════════════════════════════════════

        private void PatchValue(string label, string newValue)
        {
            if (!_valueLabels.TryGetValue(label, out var lbl)) return;

            lbl.Text = newValue;

            // Restore accent color now that it's real data
            var def = RowDefs.FirstOrDefault(r => r.Label == label);
            lbl.ForeColor = def != default
                ? def.IconColor
                : themeManager.CurrentTheme.TextColor;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  COPY ALL
        // ══════════════════════════════════════════════════════════════════════

        private void BtnCopyAll_Click(object sender, EventArgs e)
        {
            if (_details == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Adapter Name:     {_details.AdapterName}");
            sb.AppendLine($"Connection Type:  {_details.ConnectionType}");
            sb.AppendLine($"Local IP:         {_details.LocalIpAddress}");
            sb.AppendLine($"Public IP:        {_details.PublicIpAddress}");
            sb.AppendLine($"Subnet Mask:      {_details.SubnetMask}");
            sb.AppendLine($"Default Gateway:  {_details.Gateway}");
            sb.AppendLine($"DNS Servers:      {_details.DnsServers}");
            sb.AppendLine($"MAC Address:      {_details.MacAddress}");
            sb.AppendLine($"Link Speed:       {_details.LinkSpeed}");
            sb.AppendLine($"IPv6 Address:     {_details.IPv6Address}");

            Clipboard.SetText(sb.ToString());
            StatusChanged?.Invoke("All network details copied to clipboard.");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════════════════════════════

        private void InvokeIfRequired(Action action)
        {
            if (this.InvokeRequired)
                this.Invoke(action);
            else
                action();
        }

        private static void MakeCircle(Panel p)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, p.Width, p.Height);
            p.Region = new Region(path);
        }
    }
}
