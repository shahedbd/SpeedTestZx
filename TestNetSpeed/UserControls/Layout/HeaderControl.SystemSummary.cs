using FontAwesome.Sharp;
using TestNetSpeed.Service;

namespace TestNetSpeed.UserControls.Layout
{
    /// <summary>
    /// HeaderControl — loads the system summary (PC name, processor, RAM, OS
    /// and uptime) from <see cref="SystemInfoService"/> off the UI thread and
    /// pushes it into the strip built in HeaderControl.Summary.cs.
    /// </summary>
    public partial class HeaderControl
    {
        /// <summary>Kicks off the summary load; fire-and-forget from the host.</summary>
        public void LoadSystemSummary() => _ = LoadSystemSummaryAsync();

        private async Task LoadSystemSummaryAsync()
        {
            try
            {
                // WMI takes seconds — keep it off the UI thread. The await
                // resumes on the UI thread (called from MainForm), so the strip
                // is never touched from a worker thread before its handle exists.
                var items = await Task.Run(BuildSummaryItems);
                SetSummary(items);
            }
            catch
            {
                SetSummaryText("System information unavailable");
            }
        }

        private static SummaryItem[] BuildSummaryItems()
        {
            var service = new SystemInfoService();
            var os = service.GetOperatingSystemInfo();
            string cpu = service.GetProcessorName();
            string ram = (service.GetTotalPhysicalMemory() / (1024d * 1024 * 1024)).ToString("F1");

            var items = new List<SummaryItem>
            {
                new(IconChar.Desktop, os?.CSName ?? "Unknown PC", Color.FromArgb(20, 184, 166)),
                new(IconChar.Microchip, TruncateCpuName(cpu), Color.FromArgb(249, 115, 22), cpu),
                new(IconChar.Memory, $"{ram} GB RAM", Color.FromArgb(59, 130, 246))
            };

            if (!string.IsNullOrWhiteSpace(os?.Caption))
                items.Add(new(IconChar.Globe, os.Caption.Replace("Microsoft ", "").Trim(), Color.FromArgb(14, 165, 233)));

            if (os?.LastBootUpTime is DateTime boot)
                items.Add(new(IconChar.Clock, FormatUptime(DateTime.Now - boot), Color.FromArgb(245, 158, 11)));

            return items.ToArray();
        }

        /// <summary>"Up 3d 4h" / "Up 5h 12m" — compact so it fits at the end of the strip.</summary>
        private static string FormatUptime(TimeSpan up)
        {
            if (up < TimeSpan.Zero) up = TimeSpan.Zero;

            return up.TotalDays >= 1
                ? $"Up {(int)up.TotalDays}d {up.Hours}h"
                : $"Up {up.Hours}h {up.Minutes}m";
        }

        /// <summary>
        /// Caps the processor name so it can't push the later segments off the
        /// end of the bar — full names like "AMD Ryzen 7 5700G with Radeon
        /// Graphics" otherwise eat the space the others need. The tooltip still
        /// carries the full name.
        /// </summary>
        private static string TruncateCpuName(string name, int maxLength = 26)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length <= maxLength)
                return name;

            int cut = name.LastIndexOf(' ', maxLength - 1);
            string trimmed = (cut > 10 ? name[..cut] : name[..maxLength]).TrimEnd();
            return trimmed + "…";
        }
    }
}
