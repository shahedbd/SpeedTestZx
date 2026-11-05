using Windows.Services.Store;
using TestNetSpeed.Helper;
using TestNetSpeed.Model;
using TestNetSpeed.Service.Store;

namespace TestNetSpeed.Managers
{
    /// <summary>
    /// App-wide singleton that owns the current Microsoft Store entitlement and
    /// answers every tier/feature access check. Ported from WiFiPasswordViewer.
    ///
    /// Single source of truth for gating: nothing else in the app branches on
    /// the Store, the cache or a flag — call HasAccess(ProFeature) instead.
    ///
    /// Tier resolution, first match wins:
    ///   1. AppConfig.TestAccess override (testing only).
    ///   2. A Store-verified entitlement from this session — live "not active" wins.
    ///   3. The on-disk cache, only while fresh (EntitlementCache.IsFresh).
    ///   4. Free (fail closed).
    /// </summary>
    public sealed class EntitlementManager
    {
        private static readonly Lazy<EntitlementManager> _instance = new(() => new EntitlementManager());
        public static EntitlementManager Instance => _instance.Value;

        /// <summary>Wall clock, replaceable so grace-period logic can be tested.</summary>
        internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

        private MicrosoftStoreService? _storeService;
        private bool _serviceHasWindow;
        private EntitlementCache.Entry? _cached;
        private bool _cacheLoaded;

        private readonly TaskCompletionSource _firstCheck =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// The most recent Store answer. Defaults to unverified / no access
        /// until RefreshAsync() completes.
        /// </summary>
        public StoreEntitlement Current { get; private set; } = new();

        /// <summary>When the Store was last asked (successfully or not).</summary>
        public DateTimeOffset? LastCheckedUtc { get; private set; }

        /// <summary>When the Store last gave a verified answer (from the cache), for the About card.</summary>
        public DateTimeOffset? LastVerifiedUtc => Cached?.VerifiedAtUtc;

        /// <summary>
        /// Completes when the first RefreshAsync of this session has finished,
        /// whatever its result. StartupService waits on it (with its own cap)
        /// so a Pro user never sees a startup promotion.
        /// </summary>
        public Task FirstCheck => _firstCheck.Task;

        /// <summary>Raised after a Store answer or a tier change. May fire off the UI thread.</summary>
        public event EventHandler? EntitlementChanged;

        private EntitlementManager()
        {
        }

        // ─────────────────────────────────────────────────────────────────────
        // TIER
        // ─────────────────────────────────────────────────────────────────────

        public UserTier CurrentTier
        {
            get
            {
                if (TryGetTestOverride(out var forced))
                    return forced;

                if (Current.IsVerified)
                    return Current.HasProLicense ? UserTier.Pro : UserTier.Free;

                return EntitlementCache.IsFresh(Cached, Clock()) ? UserTier.Pro : UserTier.Free;
            }
        }

        /// <summary>True when the current tier meets or exceeds the required tier.</summary>
        public bool HasAccess(UserTier requiredTier) => CurrentTier >= requiredTier;

        /// <summary>Every ProFeature requires Pro today — change the mapping here only.</summary>
        public bool HasAccess(ProFeature feature) => HasAccess(UserTier.Pro);

        /// <summary>Shorthand for HasAccess(UserTier.Pro).</summary>
        public bool IsPro => HasAccess(UserTier.Pro);

        /// <summary>
        /// Re-announces the tier after AppConfig.TestAccess was changed at
        /// runtime (the debug-only row in Settings → Behavior).
        /// </summary>
        public void NotifyTestAccessChanged() => EntitlementChanged?.Invoke(this, EventArgs.Empty);

        private EntitlementCache.Entry? Cached
        {
            get
            {
                if (!_cacheLoaded)
                {
                    _cached = EntitlementCache.Load();
                    _cacheLoaded = true;
                }
                return _cached;
            }
        }

        /// <summary>
        /// Test-only override from <see cref="AppConfig.TestAccess"/>. It sits
        /// ahead of every real check so <see cref="TestAccessMode.Gated"/> keeps
        /// the Free UI reachable on a machine that owns a Pro license.
        /// </summary>
        private static bool TryGetTestOverride(out UserTier tier)
        {
            switch (AppConfig.TestAccess)
            {
                case TestAccessMode.FullAccess:
                    tier = UserTier.Pro;
                    return true;
                case TestAccessMode.Gated:
                    tier = UserTier.Free;
                    return true;
                default:
                    tier = UserTier.Free;
                    return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // STORE
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Call once a window handle exists (MainForm load). Safe to call more
        /// than once; upgrades a window-less service to one that can show the
        /// Store purchase dialog.
        /// </summary>
        public void Initialize(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero || _serviceHasWindow)
                return;

            _storeService = new MicrosoftStoreService(windowHandle);
            _serviceHasWindow = true;

#if !DEBUG
            if (AppConfig.TestAccess != TestAccessMode.Off)
                Logger.Log($"WARNING: AppConfig.TestAccess is {AppConfig.TestAccess} in a release build.");

            if (!AppConfig.HasRealProStoreId)
                Logger.Log($"WARNING: AppConfig.LifetimeProStoreId is still a placeholder ('{AppConfig.LifetimeProStoreId}') — Pro cannot be bought.");
#endif
        }

        /// <summary>
        /// Re-checks the Store license. A verified answer replaces Current and
        /// the cache; an unverified one (offline, timeout, error) leaves the
        /// cache to decide. Never throws.
        /// </summary>
        /// <param name="timeout">Optional cap so a caller never hangs on the Store.</param>
        public async Task RefreshAsync(TimeSpan? timeout = null)
        {
            _storeService ??= new MicrosoftStoreService(IntPtr.Zero);
            var before = CurrentTier;

            StoreEntitlement result;
            try
            {
                var check = _storeService.GetEntitlementAsync();

                if (timeout is { } limit && await Task.WhenAny(check, Task.Delay(limit)) != check)
                    result = new StoreEntitlement();
                else
                    result = await check;
            }
            catch (Exception ex)
            {
                Logger.Log($"Entitlement refresh failed: {ex.Message}");
                result = new StoreEntitlement();
            }

            var now = Clock();
            LastCheckedUtc = now;
            Current = result;

            if (result.IsVerified)
            {
                EntitlementCache.Save(result, now);
                _cached = EntitlementCache.Load();
                _cacheLoaded = true;
            }

            _firstCheck.TrySetResult();

            if (before != CurrentTier || result.IsVerified)
                EntitlementChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Refreshes only when the last check is older than <paramref name="maxAge"/>.</summary>
        public Task RefreshIfStaleAsync(TimeSpan maxAge) =>
            LastCheckedUtc is { } last && Clock() - last < maxAge
                ? Task.CompletedTask
                : RefreshAsync();

        /// <summary>
        /// Starts the one-time Pro purchase flow and refreshes the entitlement on
        /// success. Null when the Store cannot show UI (no window yet).
        /// </summary>
        public async Task<StorePurchaseStatus?> PurchaseProAsync()
        {
            if (_storeService == null || !_serviceHasWindow)
                return null;

            var status = await _storeService.PurchaseProAsync();

            if (status is StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased)
                await RefreshAsync();

            return status;
        }

        /// <summary>
        /// The Store's localized price (e.g. "$9.99"), falling back to
        /// AppConfig.LifetimeProPrice when the Store has nothing.
        /// </summary>
        public async Task<string> GetFormattedPriceAsync()
        {
            if (_storeService == null)
                return AppConfig.LifetimeProPrice;

            try
            {
                var product = await _storeService.GetProProductAsync();
                var price = product?.Price?.FormattedPrice;

                return string.IsNullOrWhiteSpace(price) ? AppConfig.LifetimeProPrice : price;
            }
            catch
            {
                return AppConfig.LifetimeProPrice;
            }
        }
    }
}
