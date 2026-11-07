using SpeedTestZx.Helper;
using SpeedTestZx.Model;

namespace SpeedTestZx.Service
{
    /// <summary>
    /// Runs speed tests outside any page (T06), so the Connection Monitor can
    /// test while the Speed Test page is closed. One test at a time, app-wide:
    /// a manual test and a scheduled one never overlap.
    ///
    /// Every successful result is stamped (adapter type, % of plan,
    /// scheduled flag) and saved to history here — pages no longer save.
    /// </summary>
    public sealed class SpeedTestRunner
    {
        private static readonly Lazy<SpeedTestRunner> _instance = new(() => new SpeedTestRunner());
        public static SpeedTestRunner Instance => _instance.Value;

        private readonly SpeedTestService _service = new();
        private int _running;

        private SpeedTestRunner() { }

        public bool IsRunning => Volatile.Read(ref _running) == 1;

        /// <summary>True while the running test is a manual one (scheduled tests yield to it).</summary>
        public bool IsManualRunning { get; private set; }

        /// <summary>Raised after every finished test, saved or not. May fire off the UI thread.</summary>
        public event EventHandler<SpeedTestResult>? Completed;

        /// <summary>
        /// Runs one test. Returns null, without testing, when another test
        /// is already running.
        /// </summary>
        public async Task<SpeedTestResult?> RunAsync(
            IProgress<SpeedTestProgress>? progress, bool scheduled, CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
                return null;

            IsManualRunning = !scheduled;
            try
            {
                var route = Task.Run(() => NetworkRoute.Current());
                var result = await _service.RunFullTestAsync(progress, ct, light: scheduled);

                if (result.Success)
                {
                    result.AdapterType = (await route).AdapterType;
                    result.Scheduled = scheduled;
                    IspPlanStore.Grade(result);

                    // The history file now grows past 200 tests — save off the UI thread.
                    await Task.Run(() => TestHistoryStore.Append(result));
                }

                Completed?.Invoke(this, result);
                return result;
            }
            finally
            {
                IsManualRunning = false;
                Volatile.Write(ref _running, 0);
            }
        }
    }
}
