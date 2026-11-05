using System.Text.Json;
using DeviceDataModule;
using TestNetSpeed.Model;

namespace TestNetSpeed.Helper
{
    /// <summary>
    /// The user's internet plan (T33) in IspPlan.json — its own file, so
    /// resetting Settings never wipes it. Read on every test to stamp
    /// % of plan, so it's cached in memory.
    /// </summary>
    public static class IspPlanStore
    {
        private static readonly string PlanFile = Path.Combine(
            ApplicationDataManager.Instance.ApplicationDataDirectory, "IspPlan.json");

        private static readonly object Gate = new();
        private static IspPlan? _cached;

        /// <summary>Raised after the plan is saved.</summary>
        public static event EventHandler? Changed;

        public static IspPlan Current
        {
            get
            {
                lock (Gate)
                    return _cached ??= Read();
            }
        }

        public static void Save(IspPlan plan)
        {
            lock (Gate)
            {
                try
                {
                    File.WriteAllText(PlanFile, JsonSerializer.Serialize(plan));
                    _cached = plan;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to save ISP plan: {ex.Message}");
                    throw;
                }
            }
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Stamps % of plan on a fresh result (no-op when no plan is set).</summary>
        public static void Grade(SpeedTestResult result)
        {
            var plan = Current;
            if (!plan.IsSet) return;

            result.PctOfPlan = Math.Round(result.DownloadMbps / plan.DownMbps * 100, 1);
            if (plan.UpMbps > 0)
                result.UpPctOfPlan = Math.Round(result.UploadMbps / plan.UpMbps * 100, 1);
        }

        private static IspPlan Read()
        {
            try
            {
                if (File.Exists(PlanFile))
                    return JsonSerializer.Deserialize<IspPlan>(File.ReadAllText(PlanFile)) ?? new IspPlan();
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to read ISP plan: {ex.Message}");
            }
            return new IspPlan();
        }
    }
}
