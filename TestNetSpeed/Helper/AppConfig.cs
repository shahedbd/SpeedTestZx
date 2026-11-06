// Helper/AppConfig.cs
using TestNetSpeed.Forms;
using TestNetSpeed.Model;
using TestNetSpeed.UserControls.Pages;
using TestNetSpeed.UserControls.Pages.Monitor;
using TestNetSpeed.UserControls.Pages.Report;
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
        // ── TEST-ONLY ACCESS OVERRIDE ────────────────────────────────────────
        // The single switch for exercising both sides of the paywall before a
        // release (same as WiFiPasswordViewer). It overrides the real tier
        // outright — the Store license and its offline cache are ignored — so
        // Gated really does gate even on a machine that already owns Pro.
        // Unpackaged F5 runs have no package identity, so the Store always
        // reports Free there; use FullAccess to test Pro. Debug builds can also
        // flip it at runtime from Settings → Behavior → "Pro access (debug)".
        //
        // MUST be Off in every shipped build; FullAccess gives Pro away. A
        // release build logs a warning at startup when it is not Off.
        public static TestAccessMode TestAccess = TestAccessMode.FullAccess;

        // ── App identity ─────────────────────────────────────────────────────
        public static string AppName = "Speed Test ZX";
        public static string AppSubtitle = "Test your internet speed, anytime";
        public static string AppVersion = "Version 2.0.1.0";
        public static int AppReleaseVersion = 2010;
        public static string AppDataFolderName = "TestNetSpeed";

        public static string AppIconPath => Path.Combine(Application.StartupPath, "Resources", "180x180.png");
        public static string FaviconPath => Path.Combine(Application.StartupPath, "Resources", "favicon.ico");
        public static string MsStoreIconPath => Path.Combine(Application.StartupPath, "Resources", "ms_store_48x48.ico");


        //Device Data Constants ─────────────────────────────────────────────────────
        // Same placeholder caveat as AppVersion above — set to the real next
        // Store release version (int form) before publishing.
        public static string NetSpeedPlusMsStoreLink = "ms-windows-store://pdp/?productid=9P00PF8JTJ1L";
        public static string CPUZxMsStoreLink = "ms-windows-store://pdp/?productid=9P5G6W4FPNS2";
        public static string CPUZxProMsStoreLink = "ms-windows-store://pdp/?productid=9N5RXJCZB734";

        // ── Go Pro (one-time Store add-on, Pro_plan.md decision 1) ───────────
        // Durable add-on — never expires, no renewal. TODO: replace the
        // placeholder with the add-on's Store ID (9N…) from Partner Center
        // before release. While it is a placeholder the Store finds no
        // product: purchase reports an error and the prompt shows the
        // fallback price below. A release build logs a warning.
        public const string LifetimeProStoreId = "I_WILL_ADD_LIVE_ID_SOON";

        // Fallback text only — the live, localized price comes from the Store.
        public const string LifetimeProPrice = "$9.99";

        /// <summary>True once LifetimeProStoreId looks like a real Store ID (12 chars, starts with 9).</summary>
        public static bool HasRealProStoreId =>
            LifetimeProStoreId.Length == 12 && LifetimeProStoreId[0] == '9'
            && LifetimeProStoreId.All(char.IsLetterOrDigit);

        // Microsoft Store badge on the upgrade prompt's trust line.
        public static string MicrosoftStoreAppIconPath => MsStoreIconPath;

        // Go Pro pill, upgrade-card gradient and the PRO badge.
        public static Color ProAccentStart = Color.FromArgb(234, 88, 12);
        public static Color ProAccentEnd = Color.FromArgb(219, 39, 119);

        // What the upgrade prompt sells, in order (six fit without "+N more").
        public static (IconChar Icon, string Text, Color Accent)[] ProBenefits =
        [
            (IconChar.FilePdf, "ISP Report Card: 30 / 90-day ranges and a PDF for your ISP", Color.FromArgb(239, 68, 68)),
            (IconChar.DollarSign, "Cost view: what undelivered speed costs you each month", Color.FromArgb(34, 197, 94)),
            (IconChar.Clock, "Automatic tests every 1, 3 or 6 hours", Color.FromArgb(59, 130, 246)),
            (IconChar.WaveSquare, "90 days of latency, loss and outage history", Color.FromArgb(14, 165, 233)),
            (IconChar.TableCells, "Weekday × hour heatmap of when your internet is bad", Color.FromArgb(245, 158, 11)),
            (IconChar.Bell, "Smart alerts: slow speed, high latency, back online", Color.FromArgb(168, 85, 247)),
        ];

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
            "ISP Report Card — % of your plan on every test, PDF report (Pro)",
            "Connection Monitor — scheduled tests, outage log, heatmap, alerts",
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
        public static Color PrimaryColor = Color.FromArgb(194, 65, 12);      // #C2410C
        public static Color BackgroundColor = Color.FromArgb(28, 25, 23);    // #1C1917
        public static Color SurfaceColor = Color.FromArgb(35, 31, 28);       // #231F1C
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
            new NavItem { Label = "Speed Test", Icon = IconChar.GaugeHigh, IconColor = Color.FromArgb(59, 130, 246), ControlType = typeof(SpeedTestControl) },
            new NavItem { Label = "Test History", Icon = IconChar.ClockRotateLeft, IconColor = Color.FromArgb(16, 185, 129), ControlType = typeof(TestHistoryControl) },
            new NavItem { Label = "Network Info", Icon = IconChar.NetworkWired, IconColor = Color.FromArgb(14, 165, 233), ControlType = typeof(NetworkInfoControl) },
            new NavItem { Label = "Network Tools", Icon = IconChar.Screwdriver, IconColor = Color.FromArgb(168, 85, 247), ControlType = typeof(NetworkToolsControl) },

            // ── Pro ── pages open for everyone; Pro-only parts show an
            // inline upsell. ProBadge draws the "PRO" pill until unlocked.
            new NavItem { Label = "ISP Report Card", Icon = IconChar.FileContract, IconColor = Color.FromArgb(234, 88, 12), ControlType = typeof(IspReportCardControl), ProBadge = ProFeature.ReportLongRange },
            new NavItem { Label = "Connection Monitor", Icon = IconChar.HeartPulse, IconColor = Color.FromArgb(239, 68, 68), ControlType = typeof(ConnectionMonitorControl), ProBadge = ProFeature.Heatmap },

            // ── App ── kept last so they sit at the bottom of the sidebar.
            // Settings leads this group, as in NetPulseApp (Settings just
            // above About); Our Apps sits between them, as in Net Speed
            // Meter Plus.
            new NavItem { Label = "Settings", Icon = IconChar.Gear, IconColor = Color.FromArgb(0, 120, 212), ControlType = typeof(SettingsPageControl) },
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

        /// <summary>When set, the nav item shows a "PRO" pill until the user
        /// has this feature. The page itself still opens.</summary>
        public ProFeature? ProBadge { get; set; }
    }
}
