using FontAwesome.Sharp;

namespace TestNetSpeed.Helper
{
    /// <summary>
    /// The portfolio the Our Apps tab lists — the one place to edit when an app
    /// ships, is renamed, or changes price. Ported from NetSpeedMeterPlus.
    ///
    /// Names and product ids mirror Doc/Zero_byte_MS_all_apps.csv — all 21
    /// apps, so the full portfolio is shown. Order is computed, not curated:
    /// paid apps first, then free apps by install count, so a refreshed count
    /// re-ranks the list on its own. Acquisitions below 1,000 hide the count.
    /// </summary>
    public static class OurAppsCatalog
    {
        /// <summary>Free apps at or above this many installs are shown as Popular.</summary>
        public const int PopularThreshold = 10_000;

        public static IReadOnlyList<OurApp> All { get; } = Apps()
            .OrderByDescending(a => a.IsPaid)
            .ThenByDescending(a => a.Acquisitions)
            .ThenBy(a => a.Name)
            .ToList();

        public static IEnumerable<OurApp> Featured => All.Where(a => a.IsPaid);
        public static IEnumerable<OurApp> Popular => All.Where(a => !a.IsPaid && a.IsPopular);
        public static IEnumerable<OurApp> More => All.Where(a => !a.IsPaid && !a.IsPopular);

        /// <summary>"Zero Byte Software Solutions" — AppConfig.DeveloperName without any trailing web address.</summary>
        public static string DeveloperShortName => AppConfig.DeveloperName.Split(',')[0].Trim();

        private static List<OurApp> Apps() => new()
        {
            // ── Paid ──
            new("Net Speed Meter Plus",        "Live upload and download speed on your taskbar, with usage history",  "Network",      IconChar.GaugeHigh,         Color.FromArgb(16, 185, 129), "9P00PF8JTJ1L",    27_298, true),
            new("CPU ZX Pro",                  "In-depth CPU, GPU, memory and system details, plus live monitoring",  "System",       IconChar.Microchip,         Color.FromArgb(245, 158, 11), "9N5RXJCZB734",       434, true),

            // ── Free ──
            new("Net Speed Meter",             "Lightweight real-time internet speed monitor",                        "Network",      IconChar.GaugeHigh,         Color.FromArgb(34, 197, 94),  "9NMBX01PXZ4L", 1_020_306, false),
            new("CPU ZX",                      "Identify your CPU, motherboard and memory in seconds",                "System",       IconChar.Microchip,         Color.FromArgb(245, 158, 11), "9P5G6W4FPNS2",   118_452, false),
            new("Net Monitor",                 "See which apps use your network and how much",                        "Network",      IconChar.NetworkWired,      Color.FromArgb(14, 165, 233), "9NXDTMLPSNDJ",    57_945, false),
            new("Resource Monitors",           "Live CPU, memory and disk usage at a glance",                         "System",       IconChar.Gauge,             Color.FromArgb(59, 130, 246), "9N98PRCFC5G6",    16_879, false),
            new("Image Resizer App",           "Resize and compress photos in bulk",                                  "Media",        IconChar.Images,            Color.FromArgb(245, 158, 11), "9NK3ZJ1GXC09",    10_305, false),
            new("PC Health Optimizer",         "Clear junk files and keep your PC running fast",                      "System",       IconChar.HeartPulse,        Color.FromArgb(244, 63, 94),  "9NS6VX9KQ7VB",    15_531, false),
            new("Speed Meter Plus",            "Track daily and monthly data usage against your plan",                "Network",      IconChar.ChartLine,         Color.FromArgb(16, 185, 129), "9NSJ7K77H0DG",     8_025, false),
            new("Anything Search",             "Find any file on your PC the moment you type",                        "Productivity", IconChar.MagnifyingGlass,   Color.FromArgb(99, 102, 241), "9P4NR3J63FVP",    11_025, false),
            new("YT Downloader Z",             "Save videos and audio for offline viewing",                           "Media",        IconChar.Download,          Color.FromArgb(239, 68, 68),  "9N9L7T4G8XGN",     2_782, false),
            new("Video Noise Reducer",         "Clean up grainy, noisy video footage",                                "Media",        IconChar.Film,              Color.FromArgb(168, 85, 247), "9PBW159C660S",       245, false),
            new("Clipboard History Mini",      "Keep a searchable history of everything you copy",                    "Productivity", IconChar.Clipboard,         Color.FromArgb(59, 130, 246), "9NSS9STD30LK",       181, false),
            new("Screen Capture Plus",         "Capture, annotate and share screenshots",                             "Productivity", IconChar.Camera,            Color.FromArgb(99, 102, 241), "9N6LF7LXX915",     1_000, false),
            new("Battery ZX",                  "Battery health, charge cycles and smart alerts",                      "System",       IconChar.BatteryFull,       Color.FromArgb(34, 197, 94),  "9N4DQ561H3M2",       100, false),
            new("IP Address App",              "Your public and local IP details in one place",                       "Network",      IconChar.Globe,             Color.FromArgb(14, 165, 233), "9N988P6T8DKF",     2_500, false),
            new("Internet Speed Checker Plus", "Test download, upload and ping on demand",                            "Network",      IconChar.GaugeHigh,         Color.FromArgb(6, 182, 212),  "9N6FRDSDXM8C",    16_079, false),
            new("Markdown Viewer MV",          "Open and preview Markdown files cleanly",                             "Productivity", IconChar.FileLines,         Color.FromArgb(148, 163, 184), "9MW5RXWWQ4N0",      200, false),
            new("WiFi Password Viewer",        "Recover and manage saved Wi-Fi passwords",                            "Network",      IconChar.Wifi,              Color.FromArgb(168, 85, 247), "9NJBJ525FNTD",     2_000, false),
            new("Invoice Gen",                 "Create professional invoices in minutes",                             "Business",     IconChar.FileInvoiceDollar, Color.FromArgb(16, 185, 129), "9NZVS7X3WV30",       100, false),
            new("PDF HQ",                      "View, merge and organise PDF documents",                              "Productivity", IconChar.FilePdf,           Color.FromArgb(239, 68, 68),  "9ND01BDP8HDT",        50, false),
        };
    }

    /// <summary>
    /// One cross-promoted Store app. Both links derive from the product id, so
    /// there is a single value per app to keep in step with the Store.
    /// </summary>
    public record OurApp(
        string Name,
        string Description,
        string Category,
        IconChar Icon,
        Color Accent,
        string ProductId,
        int Acquisitions,
        bool IsPaid)
    {
        /// <summary>Opens the Store app directly (needs ShellExecute).</summary>
        public string StoreLink => $"ms-windows-store://pdp/?productid={ProductId}";

        /// <summary>Browser fallback for PCs without the Store app.</summary>
        public string WebLink => $"https://apps.microsoft.com/detail/{ProductId}";

        public bool IsPopular => Acquisitions >= OurAppsCatalog.PopularThreshold;

        /// <summary>
        /// Rounded-down Store-style count ("1M+", "118K+", "7.5K+"). Empty
        /// below 1,000 — a small number undersells an app more than no number.
        /// </summary>
        public string InstallsText => Acquisitions switch
        {
            >= 1_000_000 => $"{Acquisitions / 1_000_000}M+ installs",
            >= 10_000 => $"{Acquisitions / 1_000}K+ installs",
            >= 1_000 => $"{Acquisitions / 100 / 10.0:0.#}K+ installs",
            _ => string.Empty,
        };
    }
}
