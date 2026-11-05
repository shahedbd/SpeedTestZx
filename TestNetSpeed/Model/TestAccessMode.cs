namespace TestNetSpeed.Model;

/// <summary>
/// Test-only tier override for <see cref="Helper.AppConfig.TestAccess"/>.
/// Consumed in exactly one place — EntitlementManager.TryGetTestOverride.
///
/// MUST be <see cref="Off"/> in every shipped build; <see cref="FullAccess"/>
/// gives every Pro feature away.
/// </summary>
public enum TestAccessMode
{
    /// <summary>No override; the real Store entitlement decides. Ship this.</summary>
    Off,

    /// <summary>Every Pro feature unlocked, exactly as a Pro owner sees it.</summary>
    FullAccess,

    /// <summary>
    /// Free baseline only. Ignores the Store license and its cache, so the
    /// preview and upgrade prompts stay reachable on a machine that already
    /// owns Pro — no refund, no reinstall.
    /// </summary>
    Gated
}
