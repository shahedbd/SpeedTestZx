namespace TestNetSpeed.Model;

public class SpeedTestResult
{
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public double PingMs { get; set; }
    public double JitterMs { get; set; }
    public string ServerUsed { get; set; } = string.Empty;
    public DateTime TestedAt { get; set; } = DateTime.Now;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
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
}
