using System.Text.Json.Serialization;

namespace SpeedTestZx.Model;

public class SpeedTestResult
{
    /// <summary>
    /// History format version. Old files have no such field, so they load
    /// as 1; SpeedTestService stamps new results with CurrentSchemaVersion.
    /// 2 adds latency under load, packet loss, stability and ISP / server
    /// location — all nullable or empty, so v1 rows still display.
    /// 3 adds % of plan, adapter type and scheduled (Pro_plan.md T33 / T53).
    /// </summary>
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; set; } = 1;

    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public double PingMs { get; set; }
    public double JitterMs { get; set; }
    public string ServerUsed { get; set; } = string.Empty;
    public DateTime TestedAt { get; set; } = DateTime.Now;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    // ── v2: latency at 3 stages (T11 / T14) ─────────────────────────────
    // All three come from the same LatencyProbe (ICMP, or TCP connect as a
    // fallback), so they compare like with like. PingMs above is the
    // HTTP-based ping and is kept as-is for continuity with old history.

    /// <summary>Median probe latency with the line idle.</summary>
    public double? IdleLatencyMs { get; set; }

    /// <summary>Median probe latency while the download is running.</summary>
    public double? LoadedLatencyDownMs { get; set; }

    /// <summary>Median probe latency while the upload is running.</summary>
    public double? LoadedLatencyUpMs { get; set; }

    // ── v2: packet loss (T12) ────────────────────────────────────────────

    /// <summary>Share of idle probes that got no reply, 0–100.</summary>
    public double? PacketLossPct { get; set; }

    /// <summary>"ICMP", or "TCP" when ICMP was blocked (loss is then approximate).</summary>
    public string LatencyMethod { get; set; } = string.Empty;

    // ── v2: throughput stability (T29) ───────────────────────────────────

    /// <summary>
    /// How steady the download rate was, 0–100 (100 = flat line). Drives
    /// the streaming estimate and buffering risk.
    /// </summary>
    public double? DownloadStabilityPct { get; set; }

    // ── v2: ISP and test location (T05) ──────────────────────────────────

    public string Isp { get; set; } = string.Empty;
    public string ClientCity { get; set; } = string.Empty;
    public string ClientCountry { get; set; } = string.Empty;

    /// <summary>Cloudflare data centre that served the test (IATA code, e.g. "SIN").</summary>
    public string ServerColo { get; set; } = string.Empty;

    // ── v3: ISP plan grading (T33) ───────────────────────────────────────

    /// <summary>Download ÷ the plan's download, in percent, when a plan was set at test time.</summary>
    public double? PctOfPlan { get; set; }

    /// <summary>Upload ÷ the plan's upload, in percent — graded separately.</summary>
    public double? UpPctOfPlan { get; set; }

    /// <summary>"Ethernet", "Wi-Fi", or empty when unknown (old rows). Wi-Fi slowness isn't the ISP's fault.</summary>
    public string AdapterType { get; set; } = string.Empty;

    // ── v3: Connection Monitor (T53) ─────────────────────────────────────

    /// <summary>True for an automatic (light) test run by the Monitor.</summary>
    public bool Scheduled { get; set; }

    // ── Derived ──────────────────────────────────────────────────────────

    [JsonIgnore]
    public bool IsWired => AdapterType == "Ethernet";

    /// <summary>Worst of the two loaded latencies, or null when neither was measured.</summary>
    [JsonIgnore]
    public double? LoadedLatencyMs =>
        (LoadedLatencyDownMs, LoadedLatencyUpMs) switch
        {
            (null, null) => null,
            (double d, null) => d,
            (null, double u) => u,
            (double d, double u) => Math.Max(d, u),
        };

    /// <summary>Latency added by load (bufferbloat), or null when it can't be computed.</summary>
    [JsonIgnore]
    public double? BufferbloatMs =>
        IdleLatencyMs is double idle && LoadedLatencyMs is double loaded
            ? Math.Max(0, loaded - idle)
            : null;
}

public enum SpeedTestPhase
{
    Idle,
    Ping,
    Download,
    Upload,
    Complete,
    Failed
}

public class SpeedTestProgress
{
    public SpeedTestPhase Phase { get; init; }
    public double ValueMbps { get; init; }   // current sample
    public double PeakMbps { get; init; }    // highest seen so far
    public int Percent { get; init; }        // 0–100 within phase
    public string StatusText { get; init; } = string.Empty;

    /// <summary>
    /// True for a latency-probe sample (T25 live graph). Only Phase and
    /// LatencyMs are meaningful then — the page must not treat it as a
    /// throughput / progress-bar update.
    /// </summary>
    public bool IsLatencySample { get; init; }

    /// <summary>Probe round-trip in ms, or null for a lost probe.</summary>
    public double? LatencyMs { get; init; }
}
