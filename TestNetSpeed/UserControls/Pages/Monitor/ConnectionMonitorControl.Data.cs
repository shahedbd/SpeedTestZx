using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;
using TestNetSpeed.Service.Monitor;
using TestNetSpeed.UserControls.Lib.Charts;

namespace TestNetSpeed.UserControls.Pages.Monitor
{
    /// <summary>
    /// ConnectionMonitorControl — loads the Monitor's files and fills the
    /// page. Reloads when a minute is recorded, an outage starts / ends or a
    /// test is saved. Free sees 24 h of outages; Pro 7 days on the card and
    /// 90 days in the list.
    /// </summary>
    public partial class ConnectionMonitorControl
    {
        private static HeatmapMode _heatMode = HeatmapMode.Speed;

        private int _reloadVersion;

        private sealed record MonitorView(
            List<ProbeMinute> Minutes, List<Outage> Outages24h, List<Outage> OutagesShown, int OlderHidden,
            int CardOutageCount, TimeSpan CardOutageTime, double? Uptime24h, HeatmapGrid? Heat);

        private void HookData()
        {
            _cmbHeatMode.SelectedIndex = (int)_heatMode;
            _cmbHeatMode.SelectedIndexChanged += (s, e) =>
            {
                _heatMode = (HeatmapMode)_cmbHeatMode.SelectedIndex;
                _ = ReloadAsync();
            };

            var monitor = MonitorService.Instance;
            monitor.MinuteRecorded += OnMinute;
            monitor.StatusChanged += OnStatus;
            MonitorStore.OutagesChanged += OnChanged;
            TestHistoryStore.Appended += OnTestSaved;
        }

        private void OnMinute(object? sender, ProbeMinute m) => OnChanged(sender, EventArgs.Empty);
        private void OnTestSaved(object? sender, SpeedTestResult r) => OnChanged(sender, EventArgs.Empty);

        private void OnChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() => _ = ReloadAsync()));
        }

        private void OnStatus(object? sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(RenderStatus));
        }

        private async Task ReloadAsync()
        {
            int version = ++_reloadVersion;
            bool pro = IsPro;
            bool heatAllowed = EntitlementManager.Instance.HasAccess(ProFeature.Heatmap);
            bool historyAllowed = EntitlementManager.Instance.HasAccess(ProFeature.OutageHistory);
            var mode = _heatMode;

            var view = await Task.Run(() => Load(pro, heatAllowed, historyAllowed, mode));
            if (IsDisposed || version != _reloadVersion) return;

            Render(view);
        }

        private static MonitorView Load(bool pro, bool heatAllowed, bool historyAllowed, HeatmapMode mode)
        {
            var now = DateTime.Now;
            var dayAgo = now.AddDays(-1);

            var minutes = MonitorStore.ReadMinutes(dayAgo, now.AddMinutes(1));
            var allOutages = MonitorStore.LoadOutages().OrderByDescending(o => o.Start).ToList();
            var outages24h = allOutages.Where(o => (o.End ?? now) > dayAgo).ToList();

            var shown = historyAllowed ? allOutages : outages24h;
            int hidden = historyAllowed ? 0 : allOutages.Count - outages24h.Count;

            var cardFrom = pro ? now.AddDays(-7) : dayAgo;
            var cardOutages = allOutages.Where(o => (o.End ?? now) > cardFrom).ToList();

            HeatmapGrid? heat = null;
            if (heatAllowed)
            {
                var from = now.AddDays(-MonitorStore.ProRetentionDays);
                heat = mode == HeatmapMode.Ping
                    ? HeatmapData.Ping(MonitorStore.LoadHourly(), from)
                    : HeatmapData.Speed(TestHistoryStore.Load(), from, IspPlanStore.Current);
            }

            return new MonitorView(minutes, outages24h, shown, hidden,
                cardOutages.Count, MonitorStats.OutageTime(cardOutages, cardFrom, now),
                MonitorStats.UptimePct(dayAgo, now, outages24h), heat);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  RENDER
        // ══════════════════════════════════════════════════════════════════════

        private void Render(MonitorView v)
        {
            var palette = ChartPalette.FromTheme();
            var now = DateTime.Now;

            if (v.Uptime24h is double up)
                _cardUptime.SetData($"{up:0.0#}%", $"{v.Minutes.Count / 60.0:0.#} h recorded",
                    up >= 99.5 ? palette.Good : up >= 98 ? palette.Warn : palette.Bad);
            else
                _cardUptime.SetData("—", "Not recorded yet");

            _cardOutages.SetData(v.CardOutageCount.ToString(),
                v.CardOutageCount == 0 ? "No outages" : $"{Format.Duration(v.CardOutageTime)} in total",
                v.CardOutageCount > 0 ? palette.Bad : null);

            var pings = v.Minutes.Where(m => m.MedianMs != null).Select(m => m.MedianMs!.Value).ToList();
            var median = Stats.Median(pings);
            _cardPing.SetData(median is double p ? $"{p:0} ms" : "—",
                pings.Count > 0 ? $"loss {v.Minutes.Average(m => m.LossPct):0.##}% avg" : "Not recorded yet");

            _timeline.Palette = palette;
            _timeline.EmptyText = MonitorService.Instance.IsLogging
                ? "Recording… the first minute appears shortly"
                : "The latency log is off. Turn on \"Monitor latency & outages\" below.";
            _timeline.SetData(v.Minutes, v.Outages24h, now.AddDays(-1), now);

            if (v.Heat != null)
            {
                _heatmap.Palette = palette;
                _heatmap.SetData(v.Heat);
            }
            else if (_heatUpsell != null)
            {
                int tests = TestHistoryStore.Load().Count(r => r.Success && r.TestedAt >= now.AddDays(-90));
                _heatUpsell.Preview = tests > 0 ? $"{tests} tests from the last 90 days are ready to map." : "";
            }

            RenderOutages(v);
            RenderStatus();
        }

        private void RenderOutages(MonitorView v)
        {
            _lvOutages.BeginUpdate();
            _lvOutages.Items.Clear();
            foreach (var o in v.OutagesShown)
            {
                bool sameDay = o.End == null || o.End.Value.Date == o.Start.Date;
                _lvOutages.Items.Add(new ListViewItem(new[]
                {
                    o.Start.ToString("MMM d, HH:mm"),
                    o.End == null ? "ongoing" : o.End.Value.ToString(sameDay ? "HH:mm" : "MMM d HH:mm"),
                    Format.Duration(o.Duration),
                    o.Cause,
                }));
            }
            _lvOutages.EndUpdate();

            _lblOutageTitle.Text = IsPro ? "Outages · 90 days" : "Outages · last 24 h";
            _lblOutageFooter.Text = v.OlderHidden > 0
                ? $"{v.OlderHidden} older outage(s) recorded · the 90-day history is Pro"
                : v.OutagesShown.Count == 0 ? "No outages recorded. Router outages and ISP outages are told apart." : "";
            _lblOutageFooter.ForeColor = v.OlderHidden > 0 ? AppConfig.ProAccentStart : themeManager.CurrentTheme.SecondaryTextColor;
        }

        /// <summary>Next-test card and the live status line — cheap, runs on every status change.</summary>
        private void RenderStatus()
        {
            if (IsDisposed) return;
            var monitor = MonitorService.Instance;

            if (TestScheduler.NextDue is DateTime due)
                _cardNext.SetData(due <= DateTime.Now.AddMinutes(1) ? "Now" : due.ToString("HH:mm"),
                    monitor.LastSkip.Length > 0 ? monitor.LastSkip
                        : $"every {(TestScheduler.IntervalHours == 24 ? "day" : $"{TestScheduler.IntervalHours} h")}");
            else
                _cardNext.SetData("Off", "Automatic tests are off");

            _lblTimelineStatus.Text = monitor.CurrentOutage is { } o
                ? $"● Internet down since {o.Start:HH:mm}"
                : monitor.IsLogging
                    ? (monitor.LastProbeMs is double ms ? $"● Live · {ms:0} ms" : "● Live")
                    : "○ Not recording";
            _lblTimelineStatus.ForeColor = monitor.CurrentOutage != null ? Color.FromArgb(239, 68, 68)
                : monitor.IsLogging ? Color.FromArgb(16, 185, 129) : themeManager.CurrentTheme.SecondaryTextColor;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var monitor = MonitorService.Instance;
                monitor.MinuteRecorded -= OnMinute;
                monitor.StatusChanged -= OnStatus;
                MonitorStore.OutagesChanged -= OnChanged;
                TestHistoryStore.Appended -= OnTestSaved;
            }
            base.Dispose(disposing);
        }
    }
}
