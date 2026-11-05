namespace TestNetSpeed.Model;

/// <summary>
/// Everything the one-time Pro purchase unlocks (doc/App_improvement_plan/
/// Pro_plan.md, "Free vs Pro"). Call sites gate on the feature, not the tier,
/// so a feature can later move tier by editing
/// EntitlementManager.HasAccess(ProFeature) alone.
/// </summary>
public enum ProFeature
{
    // ── ISP Report Card ─────────────────────────────────────────────────────

    /// <summary>30 / 90-day and custom report ranges (free: last 7 days).</summary>
    ReportLongRange,

    /// <summary>PDF complaint report (T34).</summary>
    ReportPdf,

    /// <summary>"$ of undelivered speed" cost view (T52).</summary>
    ReportCost,

    /// <summary>By-hour-of-day chart and the worst-hours breakdown.</summary>
    ReportWorstHours,

    // ── Connection Monitor ──────────────────────────────────────────────────

    /// <summary>Automatic tests every 1, 3 or 6 h (free: once a day).</summary>
    FrequentTests,

    /// <summary>Latency and loss log beyond the last 24 h (T54).</summary>
    LatencyLog,

    /// <summary>Outage history beyond the last 24 h (T35).</summary>
    OutageHistory,

    /// <summary>Weekday × hour heatmap (T55).</summary>
    Heatmap,

    /// <summary>Slow, high-latency and back-online alerts (free: internet down only).</summary>
    SmartAlerts,
}
