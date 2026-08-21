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
        public static string AppVersion = "Version 1.0.0.0";

        public static string AppIconPath => Path.Combine(Application.StartupPath, "Resources", "180x180.png");
        public static string FaviconPath => Path.Combine(Application.StartupPath, "Resources", "favicon.ico");


        //Device Data Constants ─────────────────────────────────────────────────────
        // Same placeholder caveat as AppVersion above — set to the real next
        // Store release version (int form) before publishing.
        public static int AppReleaseVersion = 1000;
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
            "Real-time internet speed test — download, upload, ping, jitter",
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
        public static Color PrimaryColor = Color.FromArgb(0, 95, 210); // #005FD2 — placeholder

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
        // Development Plan.md's build order — Dashboard, Test History,
        // Network Info, Network Tools, and Export get added here as each is
        // implemented, same as WiFiPasswordViewer's nav list grew one tab
        // at a time rather than pre-declaring stubs for unbuilt pages.
        public static List<NavItem> NavItems = new()
        {
            new NavItem { Label = "Speed Test", Icon = IconChar.GaugeHigh, IconColor = Color.FromArgb(59, 130, 246), ControlType = typeof(SpeedTestControl) },
            // ShowModal instead of ControlType — About is a dialog, not a
            // content page, so there's nothing to dock into the content panel.
            new NavItem { Label = "About",     Icon = IconChar.InfoCircle, IconColor = Color.FromArgb(148, 163, 184), ShowModal = () => new AboutForm().ShowDialog() },
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
}
