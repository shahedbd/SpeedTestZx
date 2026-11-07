using System.Text.Json;
using System.Text.Json.Serialization;


namespace DeviceDataModule
{
    public class SettingsService
    {
        private static readonly string SettingsFileName = "appsettings.json";
        public static readonly string SettingsFilePath;
        private static AppSettingsVm _currentSettings;
        private static readonly object _lock = new object();

        static SettingsService()
        {
            //string appDataFolder = GetApplicationDataDir();
            string appDataDir = ApplicationDataManager.Instance.ApplicationDataDirectory;
            SettingsFilePath = Path.Combine(appDataDir, SettingsFileName);
        }
        public static string GetSettingsFilePath()
        {
            return SettingsFilePath;
        }

        /// <summary>
        /// Gets the current settings instance (singleton pattern)
        /// </summary>
        public static AppSettingsVm Current
        {
            get
            {
                if (_currentSettings == null)
                {
                    lock (_lock)
                    {
                        if (_currentSettings == null)
                        {
                            _currentSettings = Load();
                        }
                    }
                }
                return _currentSettings;
            }
        }

        /// <summary>
        /// Loads settings from JSON file
        /// </summary>
        public static AppSettingsVm Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() }
                    };

                    return JsonSerializer.Deserialize<AppSettingsVm>(json, options) ?? new AppSettingsVm();
                }
            }
            catch (Exception ex)
            {
                // Log error (implement your logging mechanism)
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            // Return default settings if file doesn't exist or error occurs
            return new AppSettingsVm();
        }

        /// <summary>
        /// Saves current settings to JSON file
        /// </summary>
        public static void Save()
        {
            Save(_currentSettings ?? new AppSettingsVm());
        }

        /// <summary>
        /// Saves provided settings to JSON file
        /// </summary>
        public static void Save(AppSettingsVm settings)
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() }
                    };

                    string json = JsonSerializer.Serialize(settings, options);
                    File.WriteAllText(SettingsFilePath, json);
                    _currentSettings = settings;
                }
                catch (Exception ex)
                {
                    // Log error
                    System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Reloads settings from file
        /// </summary>
        public static void Reload()
        {
            lock (_lock)
            {
                _currentSettings = Load();
            }
        }

        /// <summary>
        /// Resets settings to default values
        /// </summary>
        public static void ResetToDefaults()
        {
            lock (_lock)
            {
                _currentSettings = new AppSettingsVm();
                Save();
            }
        }

        /// <summary>
        /// Creates a backup of current settings
        /// </summary>
        public static bool CreateBackup(string backupFileName = null)
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return false;

                string backupName = backupFileName ?? $"appsettings_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string appDataDir = ApplicationDataManager.Instance.ApplicationDataDirectory;
                string backupPath = Path.Combine(appDataDir, backupName);

                File.Copy(SettingsFilePath, backupPath, true);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating backup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores settings from a backup file
        /// </summary>
        public static bool RestoreFromBackup(string backupFilePath)
        {
            try
            {
                if (!File.Exists(backupFilePath))
                    return false;

                File.Copy(backupFilePath, SettingsFilePath, true);
                Reload();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring backup: {ex.Message}");
                return false;
            }
        }       
    }
}
