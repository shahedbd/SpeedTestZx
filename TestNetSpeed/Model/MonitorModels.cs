using System.Text.Json.Serialization;

namespace TestNetSpeed.Model;

/// <summary>
/// One minute of the latency and loss log (T54), one JSON line in
/// Monitor/probes/yyyy-MM-dd.jsonl. Short property names keep a day's file
/// near 150 KB. Internet = 1.1.1.1, Gw = the default gateway (router).
/// </summary>
public sealed class ProbeMinute
{
    /// <summary>Start of the minute, local time.</summary>
    [JsonPropertyName("t")] public DateTime Time { get; set; }

    /// <summary>Median internet RTT in ms; null when every probe was lost.</summary>
    [JsonPropertyName("ms")] public double? MedianMs { get; set; }

    [JsonPropertyName("max")] public double? MaxMs { get; set; }

    /// <summary>Internet probe loss, 0–100.</summary>
    [JsonPropertyName("loss")] public double LossPct { get; set; }

    [JsonPropertyName("gw")] public double? GatewayMs { get; set; }

    /// <summary>Router probe loss, 0–100; null when no gateway was found.</summary>
    [JsonPropertyName("gwLoss")] public double? GatewayLossPct { get; set; }

    /// <summary>Probes sent this minute (normally 6).</summary>
    [JsonPropertyName("n")] public int Count { get; set; }
}

/// <summary>An internet outage (T35). End is null while it is still going on.</summary>
public sealed class Outage
{
    public const string CauseRouter = "Router / Wi-Fi";
    public const string CauseIsp = "ISP / internet";

    public DateTime Start { get; set; }
    public DateTime? End { get; set; }

    /// <summary><see cref="CauseRouter"/> or <see cref="CauseIsp"/>.</summary>
    public string Cause { get; set; } = CauseIsp;

    [JsonIgnore]
    public TimeSpan Duration => (End ?? DateTime.Now) - Start;
}

/// <summary>
/// Hourly roll-up of the probe log (Monitor/hourly.json) so the 90-day
/// heatmap opens without parsing 90 daily files.
/// </summary>
public sealed class HourStat
{
    /// <summary>Start of the hour, local time.</summary>
    public DateTime Hour { get; set; }

    /// <summary>Median of the per-minute medians.</summary>
    public double? PingMs { get; set; }

    public double LossPct { get; set; }

    /// <summary>Minutes recorded in this hour.</summary>
    public int Minutes { get; set; }
}

/// <summary>The user's internet plan (T33), stored only on this PC.</summary>
public sealed class IspPlan
{
    public string Isp { get; set; } = string.Empty;
    public double DownMbps { get; set; }
    public double UpMbps { get; set; }
    public double MonthlyPrice { get; set; }
    public string Currency { get; set; } = "$";

    /// <summary>Optional, for the PDF cover only.</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Optional, for the PDF cover only.</summary>
    public string Address { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsSet => DownMbps > 0;
}
