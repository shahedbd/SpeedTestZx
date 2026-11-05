namespace TestNetSpeed.Model;

/// <summary>
/// The user's Microsoft Store entitlement. The default instance is the
/// fail-closed state: nothing verified, nothing granted.
/// </summary>
public sealed class StoreEntitlement
{
    /// <summary>
    /// True when the one-time (lifetime) Pro add-on license is active.
    /// A Durable add-on never expires; it only goes inactive on refund/revoke.
    /// </summary>
    public bool HasProLicense { get; init; }

    /// <summary>
    /// True only when the Store actually answered. False means the check
    /// could not be completed (offline, unpackaged run, Store error), which
    /// is not the same as "not purchased" — the cached entitlement may then
    /// apply within its grace window (see EntitlementCache).
    /// </summary>
    public bool IsVerified { get; init; }
}
