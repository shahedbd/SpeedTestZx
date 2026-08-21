using TestNetSpeed.Service;

namespace TestNetSpeed.UserControls.Wifi
{
    /// <summary>
    /// "Network Details" nav page — a dedicated, full-width home for
    /// <see cref="WifiNetworkDetailsControl"/> (the same panel embedded in
    /// the Dashboard's split view, promoted here to its own tab).
    /// </summary>
    public class NetworkDetailsControl : DpiAwareUserControl
    {
        private readonly NetworkDetailsService _networkService = new();

        private WifiNetworkDetailsControl _details;
        private Label _lblStatus;

        public NetworkDetailsControl()
        {
            this.BackColor = themeManager.CurrentTheme.ContentBackground;
            this.Padding = ScaledPadding(12);

            BuildLayout();
        }

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
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // Body
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // Status bar

            this.Controls.Add(outer);

            _details = new WifiNetworkDetailsControl(_networkService)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, Scale(10))
            };
            _details.StatusChanged += SetStatus;
            outer.Controls.Add(_details, 0, 0);

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
        }

        private void SetStatus(string message)
        {
            if (_lblStatus == null) return;

            if (this.InvokeRequired)
                this.Invoke(() => _lblStatus.Text = $"Status: {message}");
            else
                _lblStatus.Text = $"Status: {message}";
        }
    }
}
