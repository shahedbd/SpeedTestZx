using Windows.ApplicationModel;
using Windows.Services.Store;
using WinRT.Interop;
using TestNetSpeed.Helper;
using TestNetSpeed.Model;

namespace TestNetSpeed.Service.Store
{
    /// <summary>
    /// Thin StoreContext wrapper: entitlement, product, price, purchase.
    /// Ported from WiFiPasswordViewer.
    ///
    /// Every failure is fail-closed: an entitlement the Store could not confirm
    /// comes back with <see cref="StoreEntitlement.IsVerified"/> false and no
    /// Pro license. Only EntitlementManager talks to this class.
    /// </summary>
    public sealed class MicrosoftStoreService
    {
        private readonly StoreContext _storeContext;
        private StoreProduct? _proProduct;

        /// <param name="windowHandle">
        /// Owner window for the Store's purchase dialog. IntPtr.Zero is allowed
        /// for a license-only check that never shows Store UI.
        /// </param>
        public MicrosoftStoreService(IntPtr windowHandle)
        {
            _storeContext = StoreContext.GetDefault();

            // Required for desktop apps when Store APIs display UI.
            if (windowHandle != IntPtr.Zero)
                InitializeWithWindow.Initialize(_storeContext, windowHandle);
        }

        /// <summary>
        /// False for an unpackaged run (F5 from Visual Studio): with no package
        /// identity the Store cannot say anything about this user. Use
        /// AppConfig.TestAccess to exercise both tiers there.
        /// </summary>
        public static bool HasPackageIdentity
        {
            get
            {
                try { return Package.Current?.Id != null; }
                catch { return false; }
            }
        }

        /// <summary>
        /// Scans the app's add-on licenses for the lifetime Pro add-on.
        /// Never throws; an unconfirmed check returns an unverified entitlement.
        /// </summary>
        public async Task<StoreEntitlement> GetEntitlementAsync()
        {
            if (!HasPackageIdentity)
                return new StoreEntitlement();

            try
            {
                StoreAppLicense? appLicense = await _storeContext.GetAppLicenseAsync();
                if (appLicense is null)
                    return new StoreEntitlement();

                bool hasPro = false;

                foreach (var item in appLicense.AddOnLicenses)
                {
                    StoreLicense license = item.Value;

                    // SkuStoreId is "<StoreId>/<SkuId>", so match the prefix.
                    if (!license.SkuStoreId.StartsWith(
                            AppConfig.LifetimeProStoreId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    hasPro = license.IsActive;
                    break;
                }

                return new StoreEntitlement
                {
                    HasProLicense = hasPro,
                    IsVerified = true
                };
            }
            catch (Exception ex)
            {
                // Do not grant paid features when Store verification cannot be completed.
                Logger.Log($"Microsoft Store entitlement error: {ex.Message}");
                return new StoreEntitlement();
            }
        }

        /// <summary>Loads the lifetime Pro product — a one-time "Durable" add-on.</summary>
        public async Task<StoreProduct?> GetProProductAsync()
        {
            if (_proProduct != null)
                return _proProduct;

            if (!HasPackageIdentity)
                return null;

            try
            {
                var result = await _storeContext.GetAssociatedStoreProductsAsync(new[] { "Durable" });

                if (result.ExtendedError != null)
                {
                    Logger.Log($"Store product error: {result.ExtendedError.Message}");
                    return null;
                }

                foreach (var item in result.Products)
                {
                    if (item.Value.StoreId.Equals(
                            AppConfig.LifetimeProStoreId, StringComparison.OrdinalIgnoreCase))
                    {
                        _proProduct = item.Value;
                        return item.Value;
                    }
                }

                Logger.Log("Lifetime Pro product was not found in the Store.");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Get Pro product error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Opens the Store purchase dialog. Must run on the UI thread.
        /// A missing product maps to ServerError so the caller shows the
        /// Store-unavailable message rather than treating it as a cancel.
        /// </summary>
        public async Task<StorePurchaseStatus> PurchaseProAsync()
        {
            try
            {
                var entitlement = await GetEntitlementAsync();
                if (entitlement.HasProLicense)
                    return StorePurchaseStatus.AlreadyPurchased;

                var product = await GetProProductAsync();
                if (product == null)
                    return StorePurchaseStatus.ServerError;

                StorePurchaseResult result = await product.RequestPurchaseAsync();

                if (result.ExtendedError != null)
                    Logger.Log($"Purchase error: {result.ExtendedError.Message}");

                return result.Status;
            }
            catch (Exception ex)
            {
                Logger.Log($"Pro purchase error: {ex.Message}");
                return StorePurchaseStatus.ServerError;
            }
        }
    }
}
