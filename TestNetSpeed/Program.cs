using TestNetSpeed.Forms;
using TestNetSpeed.Helper;
using DpiCommon.Helper;

namespace TestNetSpeed
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (Common.PriorProcess() != null)
            {
                MessageBox.Show("Another instance of the app is already running.", AppConfig.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Initialize DPI awareness FIRST
            DpiAwareService.Initialize();

            // Enable visual styles for modern appearance
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Set high DPI support for modern displays
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            Application.Run(new MainForm());
        }
    }
}
