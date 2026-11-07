using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeviceDataModule;
using Microsoft.Win32;
using SpeedTestZx.Helper;
using SpeedTestZx.Model;

namespace SpeedTestZx.Service.Store
{
    /// <summary>
    /// Last Store-verified entitlement, on disk, for offline starts. A
    /// convenience cache only — the Store is the source of truth, and this can
    /// never grant Pro beyond verifiedAt + <see cref="GracePeriod"/> (a lifetime
    /// license has no expiry; the window only bounds how long a refund/revoke
    /// goes unseen).
    ///
    /// HMAC-checked with a per-machine, per-user key so a hand-edited file is
    /// rejected (reads as "no cache" → Free). Not a DRM scheme; it stops casual
    /// edits. Its own file, not the settings file, so Settings never touches it.
    /// </summary>
    public static class EntitlementCache
    {
        /// <summary>How long a verified Pro license is honored with no Store contact.</summary>
        public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(14);

        private static readonly string CachePath = Path.Combine(
            ApplicationDataManager.Instance.ApplicationDataDirectory, "entitlement.json");

        private static readonly object _lock = new();

        /// <summary>On-disk shape. Mac covers every other field.</summary>
        public sealed class Entry
        {
            public bool HasProLicense { get; set; }
            public DateTimeOffset VerifiedAtUtc { get; set; }
            public string Mac { get; set; } = string.Empty;
        }

        /// <summary>Writes a Store-verified entitlement. Unverified results are never cached.</summary>
        public static void Save(StoreEntitlement entitlement, DateTimeOffset nowUtc)
        {
            if (!entitlement.IsVerified) return;

            var entry = new Entry
            {
                HasProLicense = entitlement.HasProLicense,
                VerifiedAtUtc = nowUtc
            };
            entry.Mac = ComputeMac(entry);

            try
            {
                lock (_lock)
                    File.WriteAllText(CachePath, JsonSerializer.Serialize(entry));
            }
            catch (Exception ex)
            {
                Logger.Log($"Entitlement cache write failed: {ex.Message}");
            }
        }

        /// <summary>The cached entry, or null when missing, unreadable or tampered with.</summary>
        public static Entry? Load()
        {
            try
            {
                string json;
                lock (_lock)
                {
                    if (!File.Exists(CachePath)) return null;
                    json = File.ReadAllText(CachePath);
                }

                var entry = JsonSerializer.Deserialize<Entry>(json);
                if (entry is null) return null;

                byte[] expected = Encoding.ASCII.GetBytes(ComputeMac(entry));
                byte[] actual = Encoding.ASCII.GetBytes(entry.Mac ?? string.Empty);
                return CryptographicOperations.FixedTimeEquals(expected, actual) ? entry : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// True when a cached Pro license may still be honored at
        /// <paramref name="nowUtc"/>. Pure, so the grace boundary is testable
        /// with a fake clock.
        /// </summary>
        public static bool IsFresh(Entry? entry, DateTimeOffset nowUtc)
        {
            if (entry is null || !entry.HasProLicense) return false;

            // Clock moved back past the verification time — don't trust it.
            if (nowUtc < entry.VerifiedAtUtc) return false;

            return nowUtc < entry.VerifiedAtUtc + GracePeriod;
        }

        // ─────────────────────────────────────────────────────────────────────
        // INTEGRITY
        // ─────────────────────────────────────────────────────────────────────

        private static string ComputeMac(Entry entry)
        {
            string payload = string.Join("|",
                entry.HasProLicense ? "1" : "0",
                entry.VerifiedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture));

            using var hmac = new HMACSHA256(Key);
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        private static byte[]? _key;

        /// <summary>Per-install key: this machine's GUID plus the Windows user.</summary>
        private static byte[] Key => _key ??= SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{AppConfig.AppDataFolderName}|{ReadMachineGuid()}|{Environment.UserName}"));

        private static string ReadMachineGuid()
        {
            try
            {
                // 64-bit view on purpose: this is a win-x86 build, and the
                // WOW6432Node copy of the key has no MachineGuid.
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return key?.GetValue("MachineGuid") as string ?? Environment.MachineName;
            }
            catch
            {
                return Environment.MachineName;
            }
        }
    }
}
