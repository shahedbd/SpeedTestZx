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

        // ── Connection Monitor (Pro_plan.md Phase 2) ──────────────────────
        // Everything starts off: nothing runs in the background until the
        // user turns it on from the Connection Monitor page.

        /// <summary>Latency / loss log and outage detection (probe every 10 s).</summary>
        public bool MonitorEnabled { get; set; } = false;

        public bool ScheduledTestsEnabled { get; set; } = false;

        /// <summary>Pro: 1, 3 or 6 (default 3, decision 3). Free always tests daily.</summary>
        public int TestIntervalHours { get; set; } = 3;

        public bool TestOnMetered { get; set; } = false;
        public bool TestOnBatterySaver { get; set; } = false;
        public DateTime? LastScheduledTestAt { get; set; } = null;

        /// <summary>The "Start with Windows?" prompt was shown once (decision 4).</summary>
        public bool StartWithWindowsOffered { get; set; } = false;

        // ── Alerts (T56) ───────────────────────────────────────────────────
        public bool AlertDown { get; set; } = true;
        public bool AlertBackOnline { get; set; } = true;
        public bool AlertSlow { get; set; } = true;
        public int SlowPctOfPlan { get; set; } = 50;
        public int SlowHours { get; set; } = 3;
        public bool AlertHighLatency { get; set; } = false;
        public int HighLatencyMs { get; set; } = 150;
        public bool QuietHoursEnabled { get; set; } = false;
        public int QuietStartHour { get; set; } = 23;
        public int QuietEndHour { get; set; } = 7;
    }
}