using TestNetSpeed.Helper;

namespace DeviceDataModule
{
    public static class StartupService
    {
        public static async Task ExecuteStartupTaskAsync()
        {
            const int maxRetries = 7;
            int retryCount = 0;
            int _TryAfterMinutes = 3;
            while (retryCount < maxRetries)
            {
                var _IsInternetAvailable = Utility.CheckNet();
                if (_IsInternetAvailable)
                {
                    bool _IsNewInstallation = SettingsService.Current.AppVersion == 0 ? true : false;
                    string randomToolUrl = ToolUrlHelper.GetRandomToolUrl();
                    try
                    {
                        //Run when: Fresh installation, New update: One time only
                        if (SettingsService.Current.AppVersion != AppConfig.AppReleaseVersion)
                        {
                            SettingsService.Current.LastPromotionDate = DateTime.Today;
                            SettingsService.Current.AppVersion = AppConfig.AppReleaseVersion;
                            SettingsService.Current.PromotionCount = 0;
                            SettingsService.Current.IsNewInstallation = _IsNewInstallation;
                            SettingsService.Save();


                            await Task.WhenAll(
                               Utility.StartProcessAsync(AppConfig.CPUZxProMsStoreLink, 0),
                               Utility.StartProcessAsync(randomToolUrl, 10)
                             );


                            //Pass device info to MSSQL Server
                            DeviceInfoCollector _DeviceInfoCollector = new();
                            var deviceInfo = await _DeviceInfoCollector.CollectDeviceInfoAsync(_IsNewInstallation);
                            bool isInserted = await _DeviceInfoCollector.InsertUserDeviceInfoUsingAPIAsync(deviceInfo);

                            Logger.Log("New installation setup completed");
                        }

                        //02: 15-Day Promotion
                        if (ShouldShowPromotion())
                        {
                            await Task.WhenAll(
                               Utility.StartProcessAsync(AppConfig.CPUZxMsStoreLink, 0),
                               Utility.StartProcessAsync(randomToolUrl, 10));

                            SettingsService.Current.LastPromotionDate = DateTime.Today;
                            SettingsService.Current.PromotionCount++;
                            SettingsService.Save();
                            Logger.Log("✅ Day 15 action completed successfully");
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"An error occurred: {ex.Message}");
                    }
                }
                else
                {
                    retryCount++;
                    Console.WriteLine($"No internet connection. Retrying in 5 minutes... (Attempt {retryCount}/{maxRetries})");

                    if (retryCount >= maxRetries)
                    {
                        Logger.Log("Maximum retry limit reached. Exiting...");
                        break;
                    }
                    await Task.Delay(TimeSpan.FromMinutes(_TryAfterMinutes));
                }
            }
        }
        /// <summary>
        /// Check if 14-day promotion period has passed
        /// </summary>
        private static bool ShouldShowPromotion()
        {
            try
            {
                if (SettingsService.Current.LastPromotionDate == null)
                {
                    return false;
                }

                int daysSince = (DateTime.Today - SettingsService.Current.LastPromotionDate.Value.Date).Days;
                Logger.Log($"[Tracker] Days since last promotion: {daysSince}");

                return daysSince > 14;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return false;
            }
        }
    }
}
