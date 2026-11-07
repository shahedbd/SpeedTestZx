using SpeedTestZx.Forms;
using SpeedTestZx.Helper;
using DpiCommon.Helper;
using DeviceDataModule;
using FontAwesome.Sharp;
using SpeedTestZx.Service.Monitor;

namespace SpeedTestZx
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // One instance per exe path. A named mutex answers in ~1 ms; listing
            // processes and reading their MainModule took tens of ms.
            using var instance = new Mutex(true, SingleInstanceName(), out bool firstInstance);
            if (!firstInstance)
            {
                MessageBox.Show("Another instance of the app is already running.", AppConfig.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Read settings and history, load the icon font and ask Windows how
            // the app was started, on worker threads while the UI thread sets
            // up DPI and builds the window.
            // Both stores lock and the font loads once, so the UI thread's own
            // first use waits for, or reuses, this work.
            _ = Task.Run(WarmUpData);
            _ = Task.Run(WarmUpIcons);
            _ = Task.Run(WindowsStartup.IsStartupActivation);   // first WinRT call, cached

            // Initialize DPI awareness FIRST
            DpiAwareService.Initialize();

            // Enable visual styles for modern appearance
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Set high DPI support for modern displays
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            Application.Run(new MainForm());
        }

        private static string SingleInstanceName() =>
            @"Local\SpeedTestZx-" + (Environment.ProcessPath ?? AppConfig.AppName)
                .ToLowerInvariant().Replace('\\', '_').Replace(':', '_');

        private static void WarmUpData()
        {
            try
            {
                _ = SettingsService.Current;
                _ = TestHistoryStore.Load();   // first-use JSON cost + file into cache
            }
            catch { /* the UI thread loads again and handles errors */ }
        }

        private static void WarmUpIcons()
        {
            try
            {
                using var _ = IconChar.Gear.ToBitmap(IconFont.Auto, 16, Color.Black);
            }
            catch { /* only a warm-up */ }
        }
    }
}
