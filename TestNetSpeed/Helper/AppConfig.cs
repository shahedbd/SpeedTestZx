// Helper/AppConfig.cs
using TestNetSpeed.Forms;
using TestNetSpeed.UserControls.Pages;
using FontAwesome.Sharp;

namespace TestNetSpeed.Helper
{
    /// <summary>
    /// Single source of truth for everything that changes when bootstrapping a
    /// new app from this template: identity, layout, theme colors, and the
    /// sidebar's nav items. No other file should need structural changes.
    /// </summary>
    public static class AppConfig
    {
        // ── App identity ─────────────────────────────────────────────────────
        public static string AppName = "Test Net Speed";
        public static string AppSubtitle = "Test your internet speed, anytime";
        // Placeholder — this rebuild replaces an existing Store listing, so
        // set this to match the actual next Store version before release,
        // not 1.0.0.0.
        public static string AppVersion = "Version 2.0.0.0";

        public static string AppIconPath => Path.Combine(Application.StartupPath, "Resources", "180x180.png");
        public static string FaviconPath => Path.Combine(Application.StartupPath, "Resources", "favicon.ico");
        public static string MsStoreIconPath => Path.Combine(Application.StartupPath, "Resources", "ms_store_48x48.ico");


        //Device Data Constants ─────────────────────────────────────────────────────
        // Same placeholder caveat as AppVersion above — set to the real next
        // Store release version (int form) before publishing.
        public static int AppReleaseVersion = 2000;
        public static string AppDataFolderName = "TestNetSpeed";
        public static string NetSpeedPlusMsStoreLink = "ms-windows-store://pdp/?productid=9P00PF8JTJ1L";
        public static string CPUZxMsStoreLink = "ms-windows-store://pdp/?productid=9P5G6W4FPNS2";
        public static string CPUZxProMsStoreLink = "ms-windows-store://pdp/?productid=9N5RXJCZB734";

        //API Constants
        public static string apiSecretKey = "c96524b3-dad4-4146-aa4a-7e6b99b92d8b";
        public static string apiUrlLocal = "http://localhost:85/api/deviceinstallationinfoapi/add-new";
        public static string apiUrlProd = "https://storeapi.zerobytebd.com/api/deviceinstallationinfoapi/add-new";
        public static string CountryNameAPIServiceURL = "https://ipapi.co/country_name/";



        // ── System tray ──────────────────────────────────────────────────────
        // When true, closing the window hides it to the tray instead of
        // exiting (restore via the tray icon's "Show App" / double-click, or
        // exit via its "Exit" item). When false, the close button just
        // closes the app normally and no tray icon is ever shown.
        public static bool EnableSystemTray = true;

        // ── About dialog content ────────────────────────────────────────────
        // Everything Forms/AboutForm.cs shows — edit these, not AboutForm.cs.
        public static string AppDescription = "A fast, reliable internet speed test with detailed connection diagnostics.";
        public static string DeveloperName = "Zero Byte Software Solutions";
        public static string WebsiteUrl = "https://zerobytebd.com";
        public static string SupportEmail = "shahedbddev@gmail.com";

        // Reflects only what's actually implemented so far — extend this as
        // each tab in the dev plan lands (Network Info, Network Tools, Test
        // History, Export, Dashboard), same as the WiFi build's About list
        // grew incrementally rather than describing unbuilt features.
        public static List<string> AboutFeatures = new()
        {
            "At-a-glance dashboard — last result and connection status",
            "Real-time internet speed test — download, upload, ping, jitter",
            "Test history — every result saved locally, browsable anytime",
            "Detailed network adapter & connection info",
            "Built-in network tools — Ping, DNS Lookup, IP Lookup, Traceroute",
            "System tray support with notifications",
            "Dark / Light theme, DPI-aware from 100% to 200%",
            "Collapsible sidebar navigation",
        };

        // ── Layout dimensions (base values at 96 DPI) ───────────────────────────
        public static int TitleBarHeight = 35;
        public static int HeaderHeight = 60;
        public static int SidebarWidth = 200;  //250
        public static int SidebarCollapsedWidth = 60;

        // ── Theme & colors ───────────────────────────────────────────────────
        // PLACEHOLDER — Resources/180x180.png and favicon.ico are still
        // WiFiPasswordViewer's lock icon, carried over from the copy this
        // project started from. This blue is a reasonable stand-in, not
        // sampled from a real Test Net Speed icon. Once real app art
        // exists, replace Resources/180x180.png + favicon.ico and re-sample
        // this color from it (see WiFiPasswordViewer's AppConfig.cs history
        // for the sampling approach) — TitleBarColor and
        // HeaderButtonService's brand colors both derive from this value,
        // so fixing it here is enough to re-brand the whole app.
        //public static Color PrimaryColor = Color.FromArgb(0, 95, 210); // #005FD2 — placeholder
        public static Color PrimaryColor = Color.FromArgb(49, 46, 196); // #312EC4



        public static Color BackgroundColor = Color.FromArgb(18, 18, 18);
        public static Color SurfaceColor = Color.FromArgb(35, 35, 35);
        public static Color TextPrimaryColor = Color.FromArgb(230, 230, 230);
        public static Color TextSecondaryColor = Color.FromArgb(180, 180, 180);

        // Title bar's own "chrome" color — fixed across both dark and light
        // theme (like PrimaryColor) so the title bar always reads as its own
        // distinct strip rather than blending into the header/content below it.
        // Same value as PrimaryColor (the app logo's core color).
        public static Color TitleBarColor = PrimaryColor;

        // ── Navigation items ─────────────────────────────────────────────────
        // Add, remove, or rename nav items here without touching any control
        // or manager code — NavigationManager renders and wires these up.
        //
        // Built out incrementally per doc/Internet Speed Checker Plus -
        // Development Plan.md's build order — Export is still to come,
        // added here once implemented, same as WiFiPasswordViewer's nav
        // list grew one tab at a time rather than pre-declaring stubs for
        // unbuilt pages.
        //
        // Dashboard is listed first so NavigationManager's startup
        // auto-click (first non-modal item) lands there — it's the app's
        // landing page.
        public static List<NavItem> NavItems = new()
        {
            new NavItem { Label = "Dashboard", Icon = IconChar.ChartLine, IconColor = Color.FromArgb(14, 165, 233), ControlType = typeof(DashboardControl) },
            new NavItem { Label = "Settings", Icon = IconChar.Gear, IconColor = Color.FromArgb(0, 120, 212), ControlType = typeof(SettingsPageControl) },
            new NavItem { Label = "Speed Test", Icon = IconChar.GaugeHigh, IconColor = Color.FromArgb(59, 130, 246), ControlType = typeof(SpeedTestControl) },
            new NavItem { Label = "Test History", Icon = IconChar.ClockRotateLeft, IconColor = Color.FromArgb(16, 185, 129), ControlType = typeof(TestHistoryControl) },
            new NavItem { Label = "Network Info", Icon = IconChar.NetworkWired, IconColor = Color.FromArgb(14, 165, 233), ControlType = typeof(NetworkInfoControl) },
            new NavItem { Label = "Network Tools", Icon = IconChar.Screwdriver, IconColor = Color.FromArgb(168, 85, 247), ControlType = typeof(NetworkToolsControl) },

            // ── App ── kept last so they sit at the bottom of the sidebar
            // (same order as Net Speed Meter Plus: Our Apps, then About).
            new NavItem { Label = "Our Apps", Icon = IconChar.Store, IconColor = Color.FromArgb(99, 102, 241), ControlType = typeof(OurAppsControl) },
            // ShowModal instead of ControlType — About is a dialog, not a
            // content page, so there's nothing to dock into the content panel.
            new NavItem
            {
                Label = "About",
                Icon = IconChar.CircleInfo,
                IconColor = Color.FromArgb(148, 163, 184),
                ShowModal = () =>
                {
                    using var about = new AboutForm();
                    about.ShowDialog();
                }
            },
        };

        // ── Other apps by the same developer ────────────────────────────────
        // Shown on the "Our Apps" tab. Sorted paid-first, then by lifetime
        // Acquisitions descending within each group — most credible apps
        // lead the list, paid ones get first billing. Names, descriptions,
        // and store links sourced from doc/MS App Product Identity.md;
        // Acquisitions and IsPaid from the developer's tracking sheet.
        // Excludes this app's own listing (Internet Speed Checker Plus).
        // Descriptions are short one-liners inferred from each app's
        // name/category, not pulled from their actual Store listings —
        // worth a pass to tighten these up against the real listing copy
        // later. WiFi Password Viewer and Invoice Gen aren't on the
        // tracking sheet — Acquisitions is 0 for both, so they sort last
        // and skip the install-count badge rather than showing "0".
        public static List<OurApp> OurApps = new()
        {
            // ── Paid ──
            new("Net Speed Meter Plus", "Real-time internet speed monitor with history", IconChar.GaugeHigh, Color.FromArgb(16, 185, 129), "ms-windows-store://pdp/?productid=9P00PF8JTJ1L", 27_298, true),
            new("CPU ZX Pro", "Detailed CPU, GPU, and system specs at a glance", IconChar.Microchip, Color.FromArgb(245, 158, 11), "ms-windows-store://pdp/?productid=9N5RXJCZB734", 434, true),
            // ── Free ──
            new("Net Speed Meter", "Simple real-time bandwidth monitor", IconChar.GaugeHigh, Color.FromArgb(34, 197, 94), "ms-windows-store://pdp/?productid=9NMBX01PXZ4L", 1_020_306, false),
            new("CPU ZX", "Quick CPU and hardware identification", IconChar.Microchip, Color.FromArgb(245, 158, 11), "ms-windows-store://pdp/?productid=9P5G6W4FPNS2", 118_452, false),
            new("Net Monitor", "Monitor network activity and connections", IconChar.NetworkWired, Color.FromArgb(14, 165, 233), "ms-windows-store://pdp/?productid=9NXDTMLPSNDJ", 57_945, false),
            new("Resource Monitors", "Live CPU, memory, and disk monitoring", IconChar.GaugeHigh, Color.FromArgb(59, 130, 246), "ms-windows-store://pdp/?productid=9N98PRCFC5G6", 16_879, false),
            new("Image Resizer App", "Batch resize and compress images", IconChar.Image, Color.FromArgb(245, 158, 11), "ms-windows-store://pdp/?productid=9NK3ZJ1GXC09", 10_305, false),
            new("PC Health Optimizer", "Clean junk files and speed up your PC", IconChar.HeartPulse, Color.FromArgb(244, 63, 94), "ms-windows-store://pdp/?productid=9NS6VX9KQ7VB", 7_531, false),
            new("DU Net Meter", "Track daily and monthly data usage", IconChar.ChartLine, Color.FromArgb(16, 185, 129), "ms-windows-store://pdp/?productid=9NSJ7K77H0DG", 7_025, false),
            new("Anything Search", "Find any file on your PC instantly", IconChar.MagnifyingGlass, Color.FromArgb(99, 102, 241), "ms-windows-store://pdp/?productid=9P4NR3J63FVP", 5_292, false),
            new("YT Downloader Z", "Download YouTube videos and audio", IconChar.Download, Color.FromArgb(239, 68, 68), "ms-windows-store://pdp/?productid=9N9L7T4G8XGN", 2_782, false),
            new("IP Address App", "Check your local and public IP address instantly", IconChar.Globe, Color.FromArgb(6, 182, 212), "ms-windows-store://pdp/?productid=9N988P6T8DKF", 2_081, false),
            new("Screen Capture Plus", "Capture and annotate screenshots in seconds", IconChar.Camera, Color.FromArgb(14, 165, 233), "ms-windows-store://pdp/?productid=9N6LF7LXX915", 642, false),
            new("Video Noise Reducer", "Cleans up grainy, noisy video footage", IconChar.Film, Color.FromArgb(168, 85, 247), "ms-windows-store://pdp/?productid=9PBW159C660S", 245, false),
            new("Clipboard History Mini", "Keeps a running history of everything you copy", IconChar.Clipboard, Color.FromArgb(59, 130, 246), "ms-windows-store://pdp/?productid=9NSS9STD30LK", 181, false),
            new("WiFi Password Viewer", "View and manage saved WiFi passwords", IconChar.Wifi, Color.FromArgb(168, 85, 247), "ms-windows-store://pdp/?productid=9NJBJ525FNTD", 0, false),
            new("Invoice Gen", "Create and manage invoices for your business", IconChar.FileInvoiceDollar, Color.FromArgb(16, 185, 129), "ms-windows-store://pdp/?productid=9NZVS7X3WV30", 0, false),
        };
    }

    /// <summary>A single sidebar navigation entry.</summary>
    public class NavItem
    {
        public string Label { get; set; }
        public IconChar Icon { get; set; }

        /// <summary>Icon's own brand color — shown at all times, active or
        /// not, so the sidebar reads as colorful rather than monochrome.</summary>
        public Color IconColor { get; set; }

        /// <summary>Content page to load into the content panel. Mutually
        /// exclusive with ShowModal — set exactly one of the two.</summary>
        public Type ControlType { get; set; }

        /// <summary>Opens a modal dialog instead of loading a page (e.g. an
        /// About box). When set, ControlType is ignored and the sidebar's
        /// active-page highlight doesn't change.</summary>
        public Action ShowModal { get; set; }
    }

    /// <summary>One entry on the "Our Apps" tab — another Store app by the
    /// same developer. Acquisitions is lifetime install count (0 = unknown,
    /// not lack of installs) — drives both the sort order and whether the
    /// install-count badge shows at all. IsPaid sorts ahead of everything
    /// free.</summary>
    public record OurApp(string Name, string Description, IconChar Icon, Color IconColor, string StoreLink, int Acquisitions, bool IsPaid);
}
