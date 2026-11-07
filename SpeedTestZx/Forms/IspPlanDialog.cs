using System.Globalization;
using DpiCommon.Helper;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;
using SpeedTestZx.UserControls.Lib;

namespace SpeedTestZx.Forms
{
    /// <summary>
    /// Plan entry for the ISP Report Card (T33): ISP, download / upload Mbps,
    /// monthly price and currency, plus an optional name and address for the
    /// PDF cover. The ISP is pre-filled from the last test. Stored only on
    /// this PC (IspPlanStore).
    /// </summary>
    public sealed class IspPlanDialog : Form
    {
        private readonly TextBox _isp, _down, _up, _price, _currency, _name, _address;
        private readonly Label _error;

        public IspPlanDialog(string detectedIsp)
        {
            var theme = ThemeManager.Instance.CurrentTheme;
            var plan = IspPlanStore.Current;

            Text = "Your internet plan";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = theme.CardBackground;
            ForeColor = theme.TextColor;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.None;   // sizes are scaled by hand (S)
            Icon = File.Exists(AppConfig.FaviconPath) ? new Icon(AppConfig.FaviconPath) : null;

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(S(16)),
                AutoSize = true,
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var intro = Note("Grades every test against what you pay for. Stored only on this PC.");
            grid.Controls.Add(intro);
            grid.SetColumnSpan(intro, 2);

            _isp = Field(grid, "Internet provider", plan.Isp.Length > 0 ? plan.Isp : detectedIsp);
            _down = Field(grid, "Download (Mbps)", plan.DownMbps > 0 ? plan.DownMbps.ToString(CultureInfo.CurrentCulture) : "");
            _up = Field(grid, "Upload (Mbps)", plan.UpMbps > 0 ? plan.UpMbps.ToString(CultureInfo.CurrentCulture) : "");
            _price = Field(grid, "Monthly price", plan.MonthlyPrice > 0 ? plan.MonthlyPrice.ToString("0.##", CultureInfo.CurrentCulture) : "");
            _currency = Field(grid, "Currency symbol", plan.Currency);
            _name = Field(grid, "Your name (PDF, optional)", plan.CustomerName);
            _address = Field(grid, "Address (PDF, optional)", plan.Address);

            _error = Note("");
            _error.ForeColor = theme.ErrorColor;
            grid.Controls.Add(_error);
            grid.SetColumnSpan(_error, 2);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                AutoSize = true,
                Padding = new Padding(S(12), S(4), S(12), S(12)),
            };
            var save = MakeButton("Save", primary: true);
            var cancel = MakeButton("Cancel", primary: false);
            save.Click += (s, e) => TrySave();
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.AddRange(new Control[] { save, cancel });

            Controls.Add(grid);
            Controls.Add(buttons);
            AcceptButton = save;
            CancelButton = cancel;

            ClientSize = new Size(S(460), grid.PreferredSize.Height + buttons.PreferredSize.Height);
        }

        private static int S(int value) => DpiAwareService.Scale(value);

        private TextBox Field(TableLayoutPanel grid, string label, string value)
        {
            grid.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, S(6), S(12), S(6)),
            });

            var box = new TextBox { Text = value, Dock = DockStyle.Fill, Margin = new Padding(0, S(4), 0, S(4)) };
            PageCards.ThemeInput(box);
            grid.Controls.Add(box);
            return box;
        }

        private static Label Note(string text) => new()
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(DpiAwareService.Scale(420), 0),
            ForeColor = ThemeManager.Instance.CurrentTheme.SecondaryTextColor,
            Margin = new Padding(0, 0, 0, DpiAwareService.Scale(8)),
        };

        private static Button MakeButton(string text, bool primary)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(DpiAwareService.Scale(90), DpiAwareService.Scale(32)),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(DpiAwareService.Scale(6), 0, 0, 0),
                Cursor = Cursors.Hand,
            };
            if (primary)
            {
                b.BackColor = AppConfig.PrimaryColor;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
            }
            else
            {
                SettingsStyle.ThemeGhostButton(b);
            }
            return b;
        }

        private void TrySave()
        {
            if (!TryNumber(_down.Text, out double down) || down <= 0)
            {
                _error.Text = "Enter your plan's download speed in Mbps, e.g. 100.";
                _down.Focus();
                return;
            }

            TryNumber(_up.Text, out double up);
            TryNumber(_price.Text, out double price);

            var plan = new IspPlan
            {
                Isp = _isp.Text.Trim(),
                DownMbps = down,
                UpMbps = Math.Max(0, up),
                MonthlyPrice = Math.Max(0, price),
                Currency = _currency.Text.Trim(),
                CustomerName = _name.Text.Trim(),
                Address = _address.Text.Trim(),
            };

            try
            {
                IspPlanStore.Save(plan);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch
            {
                _error.Text = "Couldn't save the plan. Please try again.";
            }
        }

        private static bool TryNumber(string text, out double value) =>
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
