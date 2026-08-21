using DeviceDataModule;
using System.Diagnostics;
using System.Text.Json;
using TestNetSpeed.Model;

namespace TestNetSpeed.Helper
{
    /// <summary>
    /// Local JSON log of completed speed tests. Same JSON-in-AppData shape
    /// as WiFiPasswordViewer's WifiProfileDataCache, but not a TTL cache —
    /// this is an append-only history, not something that expires. Newest
    /// entry first; capped at MaxEntries so the file doesn't grow
    /// unbounded (no pagination in the Test History table, so 200 is
    /// plenty without needing one).
    /// </summary>
    public static class TestHistoryStore
    {
        private static readonly string appDataDir = ApplicationDataManager.Instance.ApplicationDataDirectory;

        private static readonly string HistoryFile =
            Path.Combine(appDataDir, "TestHistory.json");

        private const int MaxEntries = 200;

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        // ─────────────────────────────────────────────────────────────────────
        // LOAD  — newest first
        // ─────────────────────────────────────────────────────────────────────

        public static List<SpeedTestResult> Load()
        {
            try
            {
                if (!File.Exists(HistoryFile))
                    return new List<SpeedTestResult>();

                string json = File.ReadAllText(HistoryFile);
                return JsonSerializer.Deserialize<List<SpeedTestResult>>(json, Options)
                    ?? new List<SpeedTestResult>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load test history: {ex.Message}");
                return new List<SpeedTestResult>();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // APPEND  — call after a successful completed test
        // ─────────────────────────────────────────────────────────────────────

        public static void Append(SpeedTestResult result)
        {
            try
            {
                var history = Load();
                history.Insert(0, result);

                if (history.Count > MaxEntries)
                    history.RemoveRange(MaxEntries, history.Count - MaxEntries);

                string json = JsonSerializer.Serialize(history, Options);
                File.WriteAllText(HistoryFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save test history: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // CLEAR
        // ─────────────────────────────────────────────────────────────────────

        public static void Clear()
        {
            try
            {
                if (File.Exists(HistoryFile))
                    File.Delete(HistoryFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clear test history: {ex.Message}");
            }
        }
    }
}
