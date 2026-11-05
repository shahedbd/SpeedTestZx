namespace TestNetSpeed.Model;

/// <summary>
/// The two product tiers. Ordered so a higher tier includes everything a
/// lower tier can access (Pro ⊇ Free) — gating checks compare with &gt;=.
///
///   Free — every page; Pro pages show a preview from the user's own data.
///   Pro  — one-time (lifetime) Pro add-on, verified via the Microsoft Store.
/// </summary>
public enum UserTier
{
    Free = 0,
    Pro = 1
}
