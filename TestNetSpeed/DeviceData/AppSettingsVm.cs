namespace DeviceDataModule
{
    public class AppSettingsVm
    {
        
        public int AppVersion { get; set; } = 0;
        public int LaunchCount { get; set; } = 0;
        public string LastAppVersion { get; set; } = string.Empty;
        public DateTime? LastUpdateDate { get; set; } = null;
        public DateTime? LastPromotionDate { get; set; } = null;
        public int PromotionCount { get; set; }
        public bool IsNewInstallation { get; set; }

        // ── User preferences ─────────────────────────────────────────────
        public bool IsDarkMode { get; set; } = true;
        public bool IsSidebarCollapsed { get; set; } = false;

        // Default true — matches the app's existing unconditional
        // minimize-to-tray behavior (AppConfig.EnableSystemTray), so
        // existing installs see no behavior change until they opt out.
        public bool MinimizeToTrayOnClose { get; set; } = true;
    }
}