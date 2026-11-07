using SpeedTestZx.Helper;
using SpeedTestZx.Service.Store;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace SpeedTestZx.Service.Monitor
{
    /// <summary>
    /// "Start with Windows" through the MSIX StartupTask declared in
    /// WinAppPackaging/Package.appxmanifest (T58). Never switched on silently
    /// (decision 4): the manifest ships it disabled, and it's only enabled
    /// when the user says yes. Unpackaged (F5) runs have no StartupTask, so
    /// every call is a safe no-op there.
    /// </summary>
    public static class WindowsStartup
    {
        /// <summary>Must match the TaskId in Package.appxmanifest.</summary>
        private const string TaskId = "SpeedTestAppStartupTaskChecker";

        public static bool IsSupported => MicrosoftStoreService.HasPackageIdentity;

        public static async Task<bool> IsEnabledAsync()
        {
            var task = await GetTaskAsync();
            return task?.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }

        /// <summary>
        /// Enables the task. Windows may refuse when the user turned it off in
        /// Task Manager (DisabledByUser) — the result says whether it's on now.
        /// </summary>
        public static async Task<bool> EnableAsync()
        {
            var task = await GetTaskAsync();
            if (task == null) return false;

            try
            {
                var state = await task.RequestEnableAsync();
                return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup task enable failed: {ex.Message}");
                return false;
            }
        }

        public static async Task DisableAsync()
        {
            var task = await GetTaskAsync();
            try { task?.Disable(); }
            catch (Exception ex) { Logger.Log($"Startup task disable failed: {ex.Message}"); }
        }

        // The first WinRT call of the process costs ~65 ms, so Program reads
        // this on a worker at launch and MainForm gets the cached answer.
        private static readonly Lazy<bool> _startupActivation = new(ReadStartupActivation);

        /// <summary>True when Windows started the app at sign-in — MainForm then starts in the tray.</summary>
        public static bool IsStartupActivation() => _startupActivation.Value;

        private static bool ReadStartupActivation()
        {
            if (!IsSupported) return false;
            try
            {
                return AppInstance.GetActivatedEventArgs()?.Kind == ActivationKind.StartupTask;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<StartupTask?> GetTaskAsync()
        {
            if (!IsSupported) return null;
            try
            {
                return await StartupTask.GetAsync(TaskId);
            }
            catch (Exception ex)
            {
                Logger.Log($"Startup task lookup failed: {ex.Message}");
                return null;
            }
        }
    }
}
