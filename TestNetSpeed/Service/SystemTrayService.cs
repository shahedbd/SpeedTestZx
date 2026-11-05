using TestNetSpeed.Helper;
using FontAwesome.Sharp;

namespace TestNetSpeed.Service
{
    /// <summary>
    /// Wraps a NotifyIcon with a right-click menu (Show App / About / Exit)
    /// and double-click-to-restore. Icon and tooltip come from AppConfig;
    /// whether it's used at all is gated by MainForm via
    /// AppConfig.EnableSystemTray. Fully decoupled from Forms — callers
    /// subscribe to the three events and decide what each one does.
    /// </summary>
    public class SystemTrayService : IDisposable
    {
        public event EventHandler ShowAppRequested;
        public event EventHandler AboutRequested;
        public event EventHandler ExitRequested;

        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;

        public SystemTrayService()
        {
            _contextMenu = new ContextMenuStrip();

            var showItem = new IconMenuItem
            {
                Text = "Show App",
                IconChar = IconChar.WindowRestore,
                IconColor = AppConfig.PrimaryColor,
                IconSize = 16,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            showItem.Click += (s, e) => ShowAppRequested?.Invoke(this, EventArgs.Empty);

            var aboutItem = new IconMenuItem
            {
                Text = "About",
                IconChar = IconChar.InfoCircle,
                IconColor = Color.FromArgb(100, 100, 100),
                IconSize = 16
            };
            aboutItem.Click += (s, e) => AboutRequested?.Invoke(this, EventArgs.Empty);

            var exitItem = new IconMenuItem
            {
                Text = "Exit",
                IconChar = IconChar.PowerOff,
                IconColor = Color.FromArgb(220, 53, 69),
                IconSize = 16
            };
            exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

            _contextMenu.Items.Add(showItem);
            _contextMenu.Items.Add(aboutItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = File.Exists(AppConfig.FaviconPath) ? new System.Drawing.Icon(AppConfig.FaviconPath) : SystemIcons.Application,
                Text = Truncate(AppConfig.AppName, 63), // NotifyIcon.Text hard-caps at 63 chars
                ContextMenuStrip = _contextMenu,
                Visible = false
            };
            _notifyIcon.DoubleClick += (s, e) => ShowAppRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Shows the tray icon (call when the window is hidden to tray).</summary>
        public void Show() => _notifyIcon.Visible = true;

        /// <summary>Hides the tray icon (call once the window is restored).</summary>
        public void Hide() => _notifyIcon.Visible = false;

        public bool IsVisible => _notifyIcon.Visible;

        /// <summary>Tooltip text — the Connection Monitor's status (T58).</summary>
        public void SetTooltip(string text) => _notifyIcon.Text = Truncate(text, 63);

        /// <summary>
        /// Shows a Windows notification balloon from the tray icon. Only
        /// visible if the icon itself is currently shown (call after Show()).
        /// </summary>
        public void ShowBalloonTip(string title, string text, int timeoutMs = 2000,
            ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon.ShowBalloonTip(timeoutMs, title, text, icon);
        }

        private static string Truncate(string text, int maxLength) =>
            text.Length <= maxLength ? text : text[..maxLength];

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        }
    }
}
