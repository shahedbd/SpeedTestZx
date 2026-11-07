using SpeedTestZx.Forms;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;
using SpeedTestZx.UserControls.Lib.Charts;
using SpeedTestZx.UserControls.Prompts;

namespace SpeedTestZx.UserControls.Pages.Report
{
    /// <summary>
    /// IspReportCardControl — range selection, loading the report and
    /// filling the cards. Free: last 7 days; the other ranges open the Pro
    /// prompt. Reloads when a test is saved or the plan changes.
    /// </summary>
    public partial class IspReportCardControl
    {
        /// <summary>Kept across page rebuilds (theme change, Pro unlock).</summary>
        private static class LastState
        {
            public static int Range;
            public static bool WiredOnly;
            public static DateTime CustomFrom = DateTime.Today.AddDays(-30);
            public static DateTime CustomTo = DateTime.Today;
        }

        private static readonly int[] RangeDays = { 7, 30, 90 };
        private const int CustomIndex = 3;

        private ReportData? _data;
        private bool _loadingRange;
        private int _reloadVersion;

        private static bool HasLongRange => EntitlementManager.Instance.HasAccess(ProFeature.ReportLongRange);

        private void HookData()
        {
            _loadingRange = true;
            _cmbRange.SelectedIndex = HasLongRange ? LastState.Range : 0;
            _dtFrom.Value = LastState.CustomFrom;
            _dtTo.Value = LastState.CustomTo;
            _chkWired.Checked = LastState.WiredOnly;
            _loadingRange = false;
            ShowCustomPickers();

            _cmbRange.SelectedIndexChanged += (s, e) => OnRangeChanged();
            _dtFrom.ValueChanged += (s, e) => { LastState.CustomFrom = _dtFrom.Value.Date; _ = ReloadAsync(); };
            _dtTo.ValueChanged += (s, e) => { LastState.CustomTo = _dtTo.Value.Date; _ = ReloadAsync(); };
            _chkWired.CheckedChanged += (s, e) => { LastState.WiredOnly = _chkWired.Checked; _ = ReloadAsync(); };

            TestHistoryStore.Appended += OnTestSaved;
            IspPlanStore.Changed += OnDataChanged;
            MonitorStore.OutagesChanged += OnDataChanged;

            ApplyProGates();
        }

        private void OnRangeChanged()
        {
            if (_loadingRange) return;

            if (_cmbRange.SelectedIndex > 0 && !HasLongRange)
            {
                _loadingRange = true;
                _cmbRange.SelectedIndex = 0;
                _loadingRange = false;
                PromptManager.Current?.Show(ProFeature.ReportLongRange, () => { });
                return;
            }

            LastState.Range = _cmbRange.SelectedIndex;
            ShowCustomPickers();
            _ = ReloadAsync();
        }

        private void ShowCustomPickers()
        {
            bool custom = _cmbRange.SelectedIndex == CustomIndex;
            _showCustom = custom;
            _dtFrom.Visible = _dtTo.Visible = custom;
            LayoutTrendCard();
        }

        private (DateTime From, DateTime To) CurrentRange()
        {
            if (_cmbRange.SelectedIndex == CustomIndex)
            {
                var from = LastState.CustomFrom.Date;
                var to = LastState.CustomTo.Date.AddDays(1);
                return from < to ? (from, to) : (to.AddDays(-1), from.AddDays(1));
            }

            int days = RangeDays[Math.Clamp(_cmbRange.SelectedIndex, 0, RangeDays.Length - 1)];
            return (DateTime.Today.AddDays(-(days - 1)), DateTime.Now.AddMinutes(1));
        }

        private void OnTestSaved(object? sender, SpeedTestResult result) => OnDataChanged(sender, EventArgs.Empty);

        private void OnDataChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() => _ = ReloadAsync()));
        }

        private async Task ReloadAsync()
        {
            int version = ++_reloadVersion;
            var (from, to) = CurrentRange();
            bool wiredOnly = _chkWired.Checked;
            var plan = IspPlanStore.Current;

            var data = await Task.Run(() => ReportStats.Build(TestHistoryStore.Load(), from, to, wiredOnly, plan));
            if (IsDisposed || version != _reloadVersion) return;

            _data = data;
            Render(data);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  RENDER
        // ══════════════════════════════════════════════════════════════════════

        private void Render(ReportData d)
        {
            var plan = d.Plan;
            var palette = ChartPalette.FromTheme();

            _lblPlan.Text = plan.IsSet
                ? $"ISP: {(plan.Isp.Length > 0 ? plan.Isp : "—")}     Plan: {plan.DownMbps:0.#} / {plan.UpMbps:0.#} Mbps" +
                  (plan.MonthlyPrice > 0 ? $"     {Format.Money(plan.Currency, plan.MonthlyPrice)}/mo" : "")
                : "Set your internet plan to grade every test as a % of what you pay for.";
            _btnEditPlan.Text = plan.IsSet ? "Edit plan" : "Set plan";

            _lblTrendTitle.Text = plan.IsSet ? "% of plan over time (daily median)" : "Download over time (daily median, Mbps)";

            // % of plan
            if (plan.IsSet && d.MedianPct is double pct)
                _cardPct.SetData(Format.Pct(pct), $"median ↓ · worst 10%: {Format.Pct(d.P10Pct)}", palette.ForPct(pct));
            else
                _cardPct.SetData(d.MedianDown is double down ? Format.Mbps(down) : "—",
                    plan.IsSet ? "No tests in this range" : "median ↓ · set your plan to grade");

            // Below 50%
            if (plan.IsSet && d.Tests.Count > 0)
                _cardBelow.SetData(Format.Pct(d.Below50), $"of {d.Tests.Count} tests · {Format.Pct(d.Below80)} below 80%",
                    d.Below50 >= 20 ? palette.Bad : null);
            else
                _cardBelow.SetData("—", plan.IsSet ? "No tests in this range" : "Set your plan");

            // Worst hours (Pro)
            if (!EntitlementManager.Instance.HasAccess(ProFeature.ReportWorstHours))
                _cardWorst.SetData("Pro", d.WorstHours != null ? "Found · unlock to see" : "Unlock to see");
            else if (d.WorstHours is { } wh)
                _cardWorst.SetData(Format.HourRange(wh.Start, wh.End), "lowest median of the day");
            else
                _cardWorst.SetData("—", "Needs 3+ tests in a 3-hour window");

            // Uptime
            if (d.UptimePct is double up)
                _cardUptime.SetData($"{up:0.0#}%", $"{d.Outages.Count} outage(s) · {Format.Duration(d.OutageTime)}",
                    up >= 99.5 ? palette.Good : up >= 98 ? palette.Warn : palette.Bad);
            else
                _cardUptime.SetData("—", "Turn on the Connection Monitor");

            _showWired = d.MixedAdapters || _chkWired.Checked;
            _chkWired.Visible = _showWired;
            LayoutTrendCard();

            _trend.Palette = palette;
            _trend.SetData(d.Daily, d.From, d.To.AddMinutes(-1), plan.IsSet);
            _hours.Palette = palette;
            _hours.SetData(d.ByHour, plan.IsSet, d.WorstHours);

            RenderReportCard(d);
            UpdateUpsellPreviews(d);
        }

        private void RenderReportCard(ReportData d)
        {
            if (d.UndeliveredCost is double cost && d.MedianDown is double down)
            {
                var p = d.Plan;
                _lblCost.Text = $"You pay {Format.Money(p.Currency, p.MonthlyPrice)} a month for {p.DownMbps:0.#} Mbps and got a " +
                                $"median of {down:0.#} Mbps: about {Format.Money(p.Currency, cost)} of undelivered speed a month (estimate).";
            }
            else
            {
                _lblCost.Text = d.Plan.IsSet && d.Plan.MonthlyPrice <= 0
                    ? "Add your monthly price to the plan to see the cost view."
                    : d.Plan.IsSet ? "No tests in this range yet." : "Set your plan to see the cost view.";
            }

            _lblReportNote.Text = $"{d.Tests.Count} tests · {d.Outages.Count} outages · " +
                                  $"{d.From:MMM d} – {d.To.AddMinutes(-1):MMM d}. The PDF includes charts, " +
                                  "the outage table, every test and a method note.";
            LayoutReportCard();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PRO GATES
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Swaps the Pro-only parts for upsell cards when the user doesn't have Pro.</summary>
        private void ApplyProGates()
        {
            if (!EntitlementManager.Instance.HasAccess(ProFeature.ReportWorstHours))
            {
                _hours.Visible = false;
                _hourUpsell = new ProUpsellCard(ProFeature.ReportWorstHours);
                _hourCard.Controls.Add(_hourUpsell);
            }

            if (!EntitlementManager.Instance.HasAccess(ProFeature.ReportPdf))
            {
                _lblCost.Visible = _lblReportNote.Visible = _btnExport.Visible = false;
                _reportUpsell = new ProUpsellCard(ProFeature.ReportPdf);
                _reportCard.Controls.Add(_reportUpsell);
            }

            LayoutHourCard();
            LayoutReportCard();
        }

        /// <summary>"From your data" lines on the upsell cards.</summary>
        private void UpdateUpsellPreviews(ReportData d)
        {
            if (_hourUpsell != null)
            {
                int hours = d.ByHour.Count(v => v != null);
                _hourUpsell.Preview = hours > 0 ? $"You have tests from {hours} different hours of the day ready to compare." : "";
            }

            if (_reportUpsell != null)
                _reportUpsell.Preview = d.Tests.Count > 0
                    ? $"{d.Tests.Count} tests and {d.Outages.Count} outages are ready for your ISP report."
                    : "";
        }

        private void EditPlan()
        {
            string lastIsp = TestHistoryStore.Load().FirstOrDefault(r => r.Success && r.Isp.Length > 0)?.Isp ?? "";
            using var dialog = new IspPlanDialog(lastIsp);
            dialog.ShowDialog(FindForm());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TestHistoryStore.Appended -= OnTestSaved;
                IspPlanStore.Changed -= OnDataChanged;
                MonitorStore.OutagesChanged -= OnDataChanged;
            }
            base.Dispose(disposing);
        }
    }
}
