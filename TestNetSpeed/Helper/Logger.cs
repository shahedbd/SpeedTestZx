namespace SpeedTestZx.Helper
{
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(DeviceDataModule.ApplicationDataManager.Instance.LogsDirectory, "app_log.txt");

        public static void Log(Exception ex)
        {
            try
            {
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";
                File.AppendAllText(LogPath, entry);
            }
            catch { }
        }

        public static void Log(string message)
        {
            try
            {
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, entry);
            }
            catch { }
        }
    }
}
