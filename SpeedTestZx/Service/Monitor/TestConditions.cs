using System.Net.NetworkInformation;
using DeviceDataModule;
using SpeedTestZx.Helper;
using Windows.Networking.Connectivity;
using Windows.System.Power;

namespace SpeedTestZx.Service.Monitor
{
    /// <summary>
    /// "Is now a good time for a scheduled test?" (Pro_plan.md "Data safety").
    /// Returns the reason to skip, or null to go ahead:
    ///   • metered connection (unless allowed in Settings),
    ///   • battery saver on (unless allowed),
    ///   • a manual test running, or the line already busy with a download.
    /// </summary>
    public static class TestConditions
    {
        /// <summary>Above this combined ↓+↑ rate the line counts as busy.</summary>
        private const double BusyBytesPerSecond = 1_000_000;

        public static async Task<string?> SkipReasonAsync()
        {
            var settings = SettingsService.Current;

            if (SpeedTestRunner.Instance.IsRunning)
                return "another test was running";

            if (!settings.TestOnMetered && IsMetered())
                return "metered connection";

            if (!settings.TestOnBatterySaver && IsBatterySaverOn())
                return "battery saver is on";

            if (await IsLineBusyAsync())
                return "the connection was busy";

            return null;
        }

        public static bool IsMetered()
        {
            try
            {
                var cost = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost();
                return cost != null &&
                       (cost.NetworkCostType is NetworkCostType.Fixed or NetworkCostType.Variable
                        || cost.Roaming || cost.OverDataLimit);
            }
            catch (Exception ex)
            {
                Logger.Log($"Metered check failed: {ex.Message}");
                return false;
            }
        }

        public static bool IsBatterySaverOn()
        {
            try
            {
                return PowerManager.EnergySaverStatus == EnergySaverStatus.On;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Samples the adapter byte counters 2 s apart — the Dashboard traffic monitor's source.</summary>
        private static async Task<bool> IsLineBusyAsync()
        {
            try
            {
                long before = TotalBytes();
                await Task.Delay(2000);
                long after = TotalBytes();
                return (after - before) / 2.0 > BusyBytesPerSecond;
            }
            catch
            {
                return false;
            }
        }

        private static long TotalBytes() => NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Sum(n =>
            {
                var s = n.GetIPStatistics();
                return s.BytesReceived + s.BytesSent;
            });
    }
}
