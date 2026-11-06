using TestNetSpeed.Model;
using TestNetSpeed.UserControls.Lib.Charts;

namespace TestNetSpeed.Helper.Report
{
    /// <summary>
    /// The PDF complaint report (T34, Pro), in the order Pro_plan.md lists:
    /// cover, summary, charts, outage table, test table, method note. Only
    /// data inside the chosen range goes in. Charts are drawn by the same
    /// GDI+ code as on screen, rendered at 2× and embedded.
    /// </summary>
    public static class PdfReportBuilder
    {
        private const int ChartWidth = 1800, ChartHeight = 620;
        private const float ChartDpi = 192;

        public static void Write(ReportData d, string path)
        {
            using var pdf = new PdfLayout($"{AppConfig.AppName} — ISP Report Card");

            Cover(pdf, d);
            Summary(pdf, d);
            Charts(pdf, d);
            Outages(pdf, d);
            Tests(pdf, d);
            Method(pdf, d);

            pdf.AddFooters($"{AppConfig.AppName} · ISP Report Card · generated {DateTime.Now:yyyy-MM-dd HH:mm}");
            pdf.Save(path);
        }

        private static void Cover(PdfLayout pdf, ReportData d)
        {
            var p = d.Plan;
            pdf.Text("Internet Service Report", pdf.Title, after: 2);
            pdf.Text($"{d.From:MMMM d, yyyy} – {d.To.AddMinutes(-1):MMMM d, yyyy}", pdf.Body, PdfLayout.Muted, 12);

            var rows = new List<(string, string)>
            {
                ("Internet provider", p.Isp.Length > 0 ? p.Isp : "—"),
                ("Plan", p.IsSet ? $"{p.DownMbps:0.#} Mbps down / {p.UpMbps:0.#} Mbps up" : "Not set"),
            };
            if (p.MonthlyPrice > 0) rows.Add(("Monthly price", Format.Money(p.Currency, p.MonthlyPrice)));
            if (p.CustomerName.Length > 0) rows.Add(("Customer", p.CustomerName));
            if (p.Address.Length > 0) rows.Add(("Address", p.Address));
            rows.Add(("Tests included", d.WiredOnly ? $"{d.Tests.Count} (wired connection only)" : $"{d.Tests.Count}"));
            pdf.KeyValues(rows);
        }

        private static void Summary(PdfLayout pdf, ReportData d)
        {
            pdf.SectionHeading("Summary");

            var rows = new List<(string, string)>();
            if (d.HasPlan)
            {
                rows.Add(("Median download, % of plan", Format.Pct(d.MedianPct)));
                rows.Add(("Worst 10% of tests, download", Format.Pct(d.P10Pct)));
                rows.Add(("Median upload, % of plan", Format.Pct(d.MedianUpPct)));
                rows.Add(("Worst 10% of tests, upload", Format.Pct(d.P10UpPct)));
                rows.Add(("Tests below 50% of plan", Format.Pct(d.Below50)));
                rows.Add(("Tests below 80% of plan", Format.Pct(d.Below80)));
            }
            rows.Add(("Median download", Format.Mbps(d.MedianDown)));
            rows.Add(("Median upload", Format.Mbps(d.MedianUp)));
            rows.Add(("Number of tests", d.Tests.Count.ToString()));
            if (d.WorstHours is { } wh) rows.Add(("Slowest time of day", Format.HourRange(wh.Start, wh.End)));
            rows.Add(("Uptime (recorded time)", d.UptimePct is double up ? $"{up:0.0#}%" : "Not recorded"));
            rows.Add(("Total outage time", $"{Format.Duration(d.OutageTime)} in {d.Outages.Count} outage(s)"));
            if (d.UndeliveredCost is double cost)
                rows.Add(("Estimated undelivered speed, per month", Format.Money(d.Plan.Currency, cost)));
            pdf.KeyValues(rows);
        }

        private static void Charts(PdfLayout pdf, ReportData d)
        {
            double chartHeight = pdf.Width * ChartHeight / ChartWidth;

            pdf.SectionHeading(d.HasPlan ? "Download as % of plan, daily median" : "Download speed, daily median (Mbps)", chartHeight);
            using (var trend = new PlanTrendChart { Palette = ChartPalette.Print })
            {
                trend.SetData(d.Daily, d.From, d.To.AddMinutes(-1), d.HasPlan);
                using var bmp = trend.ToBitmap(ChartWidth, ChartHeight, ChartDpi);
                pdf.Image(bmp);
            }

            pdf.SectionHeading(d.HasPlan ? "By hour of day, median % of plan" : "By hour of day, median Mbps", chartHeight);
            using (var hours = new HourBarChart { Palette = ChartPalette.Print })
            {
                hours.SetData(d.ByHour, d.HasPlan, d.WorstHours);
                using var bmp = hours.ToBitmap(ChartWidth, ChartHeight, ChartDpi);
                pdf.Image(bmp);
            }
        }

        private static void Outages(PdfLayout pdf, ReportData d)
        {
            pdf.SectionHeading("Outages");
            if (d.Outages.Count == 0)
            {
                pdf.Text(d.UptimePct == null
                    ? "The connection monitor was not running in this period, so outages were not recorded."
                    : "No outages were recorded in this period.", pdf.Body, PdfLayout.Muted);
                return;
            }

            pdf.Table(
                new[] { "Start", "End", "Duration", "Cause" },
                new[] { 0.28, 0.28, 0.16, 0.28 },
                d.Outages.Select(o => new[]
                {
                    o.Start.ToString("yyyy-MM-dd HH:mm"),
                    o.End?.ToString("yyyy-MM-dd HH:mm") ?? "ongoing",
                    Format.Duration(o.Duration),
                    o.Cause,
                }));
        }

        private static void Tests(PdfLayout pdf, ReportData d)
        {
            pdf.SectionHeading("All tests");
            pdf.Table(
                new[] { "Time", "Download", "Upload", "% of plan", "Ping", "Server", "Adapter" },
                new[] { 0.22, 0.13, 0.13, 0.11, 0.09, 0.20, 0.12 },
                d.Tests.Select(r => new[]
                {
                    r.TestedAt.ToString("yyyy-MM-dd HH:mm") + (r.Scheduled ? " (auto)" : ""),
                    $"{r.DownloadMbps:0.#} Mbps",
                    $"{r.UploadMbps:0.#} Mbps",
                    Format.Pct(ReportStats.PctOfPlan(r, d.Plan)),
                    $"{r.PingMs:0} ms",
                    r.ServerColo.Length > 0 ? $"Cloudflare {r.ServerColo}" : "Cloudflare",
                    r.AdapterType.Length > 0 ? r.AdapterType : "—",
                }));
        }

        private static void Method(PdfLayout pdf, ReportData d)
        {
            pdf.SectionHeading("How these numbers were measured");
            pdf.Text(
                "Speed tests use Cloudflare's speed test servers (speed.cloudflare.com). Download and upload are " +
                "measured over several parallel connections for a fixed time window, and the result is bytes " +
                "transferred divided by time, leaving out the first part of the window while the connection ramps " +
                "up. Automatic tests (marked \"auto\") use the same method with a shorter window.\n" +
                "Summary figures use the median, not the average, so one unusual test cannot swing the result. " +
                "The 10th percentile shows the worst 10% of tests.\n" +
                "Outages are found by a probe to 1.1.1.1 every 10 seconds: three lost probes in a row start an " +
                "outage and two answers end it. The router is probed at the same time; if it answered while the " +
                "internet did not, the cause is recorded as \"ISP / internet\".\n" +
                (d.WiredOnly
                    ? "Only tests made over a wired (Ethernet) connection are included, so Wi-Fi conditions do not affect the result."
                    : "Wired (Ethernet) tests are preferred; Wi-Fi tests can be slower for reasons unrelated to the provider."),
                pdf.Body);
        }
    }
}
