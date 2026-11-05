using TestNetSpeed.Model;

namespace TestNetSpeed.Helper
{
    public enum QualityLevel { Great, Good, Fair, Poor }

    /// <summary>One use-case verdict, e.g. "Gaming: Fair — latency jumps to 180 ms under load".</summary>
    public record QualityVerdict(string UseCase, QualityLevel Level, string Reason);

    /// <summary>Streaming estimate (T29) — derived, not a real video test.</summary>
    public record StreamingEstimate(string Resolution, string BufferingRisk);

    public record QualityReport(
        QualityVerdict Streaming,
        QualityVerdict Gaming,
        QualityVerdict Calls,
        StreamingEstimate StreamingEstimate,
        string BufferbloatGrade)
    {
        public IEnumerable<QualityVerdict> Verdicts => new[] { Streaming, Gaming, Calls };
    }

    /// <summary>
    /// Turns a <see cref="SpeedTestResult"/> into plain answers (T13, T15, T29).
    ///
    /// Modelled on Cloudflare AIM: each use case checks the metrics that
    /// matter to it, every metric gets a level, and the use case takes the
    /// worst one — with that metric's reason, so the verdict always says
    /// what is holding it back. Pure functions, no I/O: safe to call for
    /// any history row. Metrics an old (v1) result doesn't have are simply
    /// not checked.
    ///
    /// All thresholds live in the tables below — tune them here only.
    /// </summary>
    public static class QualityScorer
    {
        // ── Threshold tables: (Great, Good, Fair) limits; anything worse is Poor ──

        // Higher is better (Mbps).
        private static readonly (double Great, double Good, double Fair) StreamDown = (25, 10, 5);
        private static readonly (double Great, double Good, double Fair) GameDown   = (10, 5, 3);
        private static readonly (double Great, double Good, double Fair) CallDown   = (10, 3, 1.5);
        private static readonly (double Great, double Good, double Fair) CallUp     = (10, 3, 1.5);

        // Lower is better (ms / %).
        private static readonly (double Great, double Good, double Fair) GameLatency   = (30, 60, 100);
        private static readonly (double Great, double Good, double Fair) GameJitter    = (10, 20, 30);
        private static readonly (double Great, double Good, double Fair) GameLoss      = (0.5, 1, 2.5);
        private static readonly (double Great, double Good, double Fair) CallLatency   = (100, 150, 300);
        private static readonly (double Great, double Good, double Fair) CallJitter    = (20, 30, 50);
        private static readonly (double Great, double Good, double Fair) CallLoss      = (0.5, 1, 3);
        private static readonly (double Great, double Good, double Fair) StreamLoss    = (1, 2, 5);
        private static readonly (double Great, double Good, double Fair) StreamUnsteady = (20, 35, 50); // 100 − stability

        // Streaming resolutions: minimum *sustained* Mbps for each step.
        private static readonly (double MinMbps, string Label)[] Resolutions =
        [
            (100, "Up to 8K"),
            (25,  "Up to 4K"),
            (8,   "Full HD 1080p"),
            (5,   "HD 720p"),
            (3,   "SD 480p"),
            (0,   "Low quality only"),
        ];

        // Bufferbloat (latency added under load, ms) → grade.
        private static readonly (double MaxMs, string Grade)[] BloatGrades =
        [
            (30,  "A"),
            (60,  "B"),
            (200, "C"),
            (400, "D"),
        ];

        // ══════════════════════════════════════════════════════════════════════
        //  ENTRY POINT
        // ══════════════════════════════════════════════════════════════════════

        public static QualityReport Score(SpeedTestResult r)
        {
            // Latency a game or call actually feels: under load when we have it.
            double latency = r.LoadedLatencyMs ?? r.IdleLatencyMs ?? r.PingMs;
            string latencyWhere = r.LoadedLatencyMs != null ? " under load" : "";

            var streaming = Worst("Streaming",
                HigherIsBetter(r.DownloadMbps, StreamDown, v => $"download is {v:0.#} Mbps"),
                LowerIsBetter(r.PacketLossPct, StreamLoss, v => $"{v:0.#}% packet loss"),
                LowerIsBetter(100 - r.DownloadStabilityPct, StreamUnsteady, _ => "download speed is unsteady"));

            var gaming = Worst("Gaming",
                LowerIsBetter(latency, GameLatency, v => $"latency is {v:0} ms{latencyWhere}"),
                LowerIsBetter(r.JitterMs, GameJitter, v => $"jitter is {v:0} ms"),
                LowerIsBetter(r.PacketLossPct, GameLoss, v => $"{v:0.#}% packet loss"),
                HigherIsBetter(r.DownloadMbps, GameDown, v => $"download is {v:0.#} Mbps"));

            var calls = Worst("Video calls",
                HigherIsBetter(r.UploadMbps, CallUp, v => $"upload is {v:0.#} Mbps"),
                HigherIsBetter(r.DownloadMbps, CallDown, v => $"download is {v:0.#} Mbps"),
                LowerIsBetter(latency, CallLatency, v => $"latency is {v:0} ms{latencyWhere}"),
                LowerIsBetter(r.JitterMs, CallJitter, v => $"jitter is {v:0} ms"),
                LowerIsBetter(r.PacketLossPct, CallLoss, v => $"{v:0.#}% packet loss"));

            return new QualityReport(streaming, gaming, calls, EstimateStreaming(r), BufferbloatGrade(r.BufferbloatMs));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUFFERBLOAT (T13)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>A–F, or "—" when loaded latency wasn't measured.</summary>
        public static string BufferbloatGrade(double? bloatMs)
        {
            if (bloatMs is not double ms) return "—";

            foreach (var (max, grade) in BloatGrades)
                if (ms < max) return grade;

            return "F";
        }

        public static Color GradeColor(string grade) => grade switch
        {
            "A" => LevelColor(QualityLevel.Great),
            "B" => LevelColor(QualityLevel.Good),
            "C" => LevelColor(QualityLevel.Fair),
            "D" or "F" => LevelColor(QualityLevel.Poor),
            _ => Color.FromArgb(148, 163, 184),
        };

        // ══════════════════════════════════════════════════════════════════════
        //  STREAMING ESTIMATE (T29)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Highest resolution the connection should hold, from download speed
        /// discounted by how unsteady it was, plus a buffering-risk level. An
        /// estimate — there is no real video stream behind it.
        /// </summary>
        public static StreamingEstimate EstimateStreaming(SpeedTestResult r)
        {
            double stability = r.DownloadStabilityPct ?? 100;

            // A line that swings ±40% only reliably holds the bottom of its
            // range, so plan on the lower part of the measured speed.
            double sustained = r.DownloadMbps * (0.5 + 0.5 * stability / 100);

            string resolution = Resolutions.First(x => sustained >= x.MinMbps).Label;

            double loss = r.PacketLossPct ?? 0;
            string risk = stability >= 75 && loss < 1 ? "Low"
                        : stability >= 50 && loss < 3 ? "Medium"
                        : "High";

            return new StreamingEstimate(resolution, risk);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DISPLAY HELPERS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Short jitter note for the Dashboard's Jitter card, from the same
        /// gaming / call jitter thresholds the verdicts use.
        /// </summary>
        public static (string Note, QualityLevel Level) JitterNote(double jitterMs)
        {
            if (jitterMs <= GameJitter.Good) return ("Good for gaming & calls", jitterMs <= GameJitter.Great ? QualityLevel.Great : QualityLevel.Good);
            if (jitterMs <= CallJitter.Good) return ("OK for calls, high for gaming", QualityLevel.Fair);
            return ("High for gaming & calls", QualityLevel.Poor);
        }

        public static Color LevelColor(QualityLevel level) => level switch
        {
            QualityLevel.Great => Color.FromArgb(16, 185, 129),  // green
            QualityLevel.Good => Color.FromArgb(59, 130, 246),   // blue
            QualityLevel.Fair => Color.FromArgb(245, 158, 11),   // amber
            _ => Color.FromArgb(239, 68, 68),                    // red
        };

        // ══════════════════════════════════════════════════════════════════════
        //  RULES
        // ══════════════════════════════════════════════════════════════════════

        private record Check(QualityLevel Level, string Reason);

        private static Check? HigherIsBetter(
            double? value, (double Great, double Good, double Fair) t, Func<double, string> reason)
        {
            if (value is not double v) return null;

            var level = v >= t.Great ? QualityLevel.Great
                      : v >= t.Good ? QualityLevel.Good
                      : v >= t.Fair ? QualityLevel.Fair
                      : QualityLevel.Poor;

            return new Check(level, reason(v));
        }

        private static Check? LowerIsBetter(
            double? value, (double Great, double Good, double Fair) t, Func<double, string> reason)
        {
            if (value is not double v) return null;

            var level = v <= t.Great ? QualityLevel.Great
                      : v <= t.Good ? QualityLevel.Good
                      : v <= t.Fair ? QualityLevel.Fair
                      : QualityLevel.Poor;

            return new Check(level, reason(v));
        }

        /// <summary>Worst level wins; its reason explains the verdict.</summary>
        private static QualityVerdict Worst(string useCase, params Check?[] checks)
        {
            var measured = checks.Where(c => c != null).Select(c => c!).ToList();
            if (measured.Count == 0)
                return new QualityVerdict(useCase, QualityLevel.Poor, "Not enough data");

            var worst = measured.OrderByDescending(c => c.Level).First();

            string reason = worst.Level == QualityLevel.Great
                ? "Plenty of headroom"
                : Capitalise(worst.Reason);

            return new QualityVerdict(useCase, worst.Level, reason);
        }

        private static string Capitalise(string s) =>
            s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }
}
