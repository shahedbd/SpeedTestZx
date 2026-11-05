using DeviceDataModule;
using System.Text.Json;
using TestNetSpeed.Model;

namespace TestNetSpeed.Helper
{
    /// <summary>
    /// Local JSON log of completed speed tests. Same JSON-in-AppData shape
    /// as WiFiPasswordViewer's WifiProfileDataCache, but not a TTL cache —
    /// this is an append-only history, not something that expires. Newest
    /// entry first. Every result is kept (Pro_plan.md decision 5, T49): the
    /// old 200-test cap was only 25 days at the 3 h Pro schedule, too short
    /// for 30 / 90-day reports. Compact JSON keeps ~3,000 tests near 2 MB.
    ///
    /// History must never be lost to a crash, so:
    ///   • Saves go to a .tmp file first and are swapped in with
    ///     File.Replace (atomic on NTFS), so the real file is never
    ///     half-written. The previous version is kept as the backup file.
    ///   • A file that can't be read is renamed aside (".corrupt-…") and
    ///     never overwritten, and the backup is loaded instead. Before this,
    ///     an unreadable file loaded as empty and the next test overwrote
    ///     the whole history with a single entry.
    ///   • One lock around every read and write, since saves can come from
    ///     the UI and (later) from scheduled tests.
    /// </summary>
    public static class TestHistoryStore
    {
        private static readonly string appDataDir = ApplicationDataManager.Instance.ApplicationDataDirectory;

        private static readonly string HistoryFile = Path.Combine(appDataDir, "TestHistory.json");
        private static readonly string BackupFile = Path.Combine(appDataDir, "TestHistory.bak.json");
        private static readonly string TempFile = HistoryFile + ".tmp";

        private static readonly object Gate = new();

        // Set when an unreadable main file couldn't be moved aside. The next
        // save must then NOT rotate it into the backup slot, or it would
        // overwrite the good backup with the broken file.
        private static bool _mainIsCorrupt;

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        /// <summary>
        /// Raised after a result is saved — scheduled tests save from the
        /// Monitor while a page may be open. May fire off the UI thread.
        /// </summary>
        public static event EventHandler<SpeedTestResult>? Appended;

        // ─────────────────────────────────────────────────────────────────────
        // LOAD  — newest first
        // ─────────────────────────────────────────────────────────────────────

        public static List<SpeedTestResult> Load()
        {
            lock (Gate)
                return LoadUnlocked();
        }

        private static List<SpeedTestResult> LoadUnlocked()
        {
            if (File.Exists(HistoryFile))
            {
                if (TryRead(HistoryFile, out var history))
                    return history;

                // Unreadable (half-written by a crash under the old save
                // code, or damaged on disk). Keep it for recovery rather
                // than letting the next save overwrite it.
                _mainIsCorrupt = !SetAsideCorrupt();
            }

            // No usable main file: fall back to the previous version, if any.
            // (A missing main file with a backup present means a save was
            // interrupted mid-swap.)
            if (File.Exists(BackupFile) && TryRead(BackupFile, out var backup))
            {
                Logger.Log($"Test history restored from backup ({backup.Count} tests).");
                return backup;
            }

            return new List<SpeedTestResult>();
        }

        /// <summary>True when the file parsed; false (and an empty list) when it didn't.</summary>
        private static bool TryRead(string path, out List<SpeedTestResult> history)
        {
            try
            {
                string json = File.ReadAllText(path);
                history = JsonSerializer.Deserialize<List<SpeedTestResult>>(json, Options)
                          ?? new List<SpeedTestResult>();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to read test history '{Path.GetFileName(path)}': {ex.Message}");
                history = new List<SpeedTestResult>();
                return false;
            }
        }

        /// <summary>Renames the unreadable main file aside. False when that failed.</summary>
        private static bool SetAsideCorrupt()
        {
            string target = Path.Combine(appDataDir,
                $"TestHistory.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            try
            {
                File.Move(HistoryFile, target);
                Logger.Log($"Unreadable test history moved to '{Path.GetFileName(target)}'.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Couldn't set aside unreadable test history: {ex.Message}");
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // APPEND  — call after a successful completed test
        // ─────────────────────────────────────────────────────────────────────

        public static void Append(SpeedTestResult result)
        {
            bool saved = false;
            lock (Gate)
            {
                try
                {
                    var history = LoadUnlocked();
                    history.Insert(0, result);
                    SaveUnlocked(history);
                    saved = true;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to save test history: {ex.Message}");
                }
            }

            if (saved)
                Appended?.Invoke(null, result);
        }

        /// <summary>
        /// Writes the whole list to a temp file, then swaps it in. The old
        /// file becomes the backup, so there's always one complete copy.
        /// </summary>
        private static void SaveUnlocked(List<SpeedTestResult> history)
        {
            string json = JsonSerializer.Serialize(history, Options);

            // Flush to disk before the swap, so a power cut right after can't
            // leave an empty file in place.
            using (var fs = new FileStream(TempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs))
            {
                writer.Write(json);
                writer.Flush();
                fs.Flush(flushToDisk: true);
            }

            if (!File.Exists(HistoryFile))
            {
                File.Move(TempFile, HistoryFile);
                return;
            }

            // A broken main file is replaced without becoming the backup.
            string? backup = _mainIsCorrupt ? null : BackupFile;

            try
            {
                File.Replace(TempFile, HistoryFile, backup, ignoreMetadataErrors: true);
                _mainIsCorrupt = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                // File.Replace can fail on some file systems or when another
                // process briefly holds the file. Fall back to copy + move:
                // not atomic, but the backup is written first.
                Logger.Log($"Atomic history save failed, using fallback: {ex.Message}");
                if (backup != null)
                    File.Copy(HistoryFile, backup, overwrite: true);
                File.Move(TempFile, HistoryFile, overwrite: true);
                _mainIsCorrupt = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // CLEAR
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Deletes the history and its backup (otherwise Load would restore
        /// the backup). Set-aside corrupt files are left alone.
        /// </summary>
        public static void Clear()
        {
            lock (Gate)
            {
                foreach (var path in new[] { HistoryFile, BackupFile, TempFile })
                {
                    try
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Failed to clear '{Path.GetFileName(path)}': {ex.Message}");
                    }
                }
            }
        }
    }
}
