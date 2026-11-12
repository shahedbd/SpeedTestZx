using System.Diagnostics;
using System.Net;
using SpeedTestZx.Model;

namespace SpeedTestZx.Service
{
    public class SpeedTestService
    {
        // ── Endpoints ────────────────────────────────────────────────────────
        // Spread across providers (Cloudflare, Microsoft, Google) so one
        // slow/blocked host or provider doesn't skew the "best server" pick
        // — MeasurePingAsync HEAD-tests all of these once and runs its 6
        // timed samples against whichever responds fastest.
        private static readonly string[] PingEndpoints =
        [
            "https://speed.cloudflare.com/__down?bytes=1",
            "https://www.cloudflare.com",
            "https://1.1.1.1",
            "https://www.google.com",
            "https://www.microsoft.com",
            "https://www.bing.com",
        ];

        // ── Shared HttpClient ────────────────────────────────────────────────
        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseProxy = false,
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        // ── Latency probe timing ─────────────────────────────────────────────
        // Idle: 40 probes 50 ms apart ≈ 2 s (+ one timeout at worst) — enough
        // for loss to resolve to 2.5% steps. Loaded: one probe every 250 ms
        // for the 8 s transfer ≈ 30 samples per direction.
        private const int IdleProbeCount = 40;
        private const int IdleProbeIntervalMs = 50;
        private const int LoadedProbeIntervalMs = 250;

        private readonly ConnectionInfoService _connectionInfo = new();

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINT
        // ══════════════════════════════════════════════════════════════════════
        /// <param name="light">
        /// Scheduled-test mode (Pro_plan.md T53): same method, but each
        /// direction stops after <see cref="LightWindow"/> or
        /// <see cref="LightByteCap"/>, whichever comes first (~50 MB a test).
        /// </param>
        public async Task<SpeedTestResult> RunFullTestAsync(
            IProgress<SpeedTestProgress>? progress = null,
            CancellationToken ct = default,
            bool light = false)
        {
            var window = light ? LightWindow : TransferWindow;
            long cap = light ? LightByteCap : 0;

            var result = new SpeedTestResult
            {
                TestedAt = DateTime.Now,
                SchemaVersion = SpeedTestResult.CurrentSchemaVersion,
            };
            var probe = new LatencyProbe();

            // ISP / location lookup runs alongside the ping phase — it's one
            // small request and shouldn't add to the test time.
            var infoTask = _connectionInfo.GetAsync(ct);

            try
            {
                // ── Phase 1: Ping ───────────────────────────────────────────
                Report(progress, SpeedTestPhase.Ping, 0, 0, "Measuring ping...");
                var (ping, jitter, server) = await MeasurePingAsync(ct);
                result.PingMs = ping;
                result.JitterMs = jitter;
                result.ServerUsed = server;
                Report(progress, SpeedTestPhase.Ping, 60, 0,
                    $"Ping: {ping:F0} ms  ·  Jitter: {jitter:F0} ms");

                ct.ThrowIfCancellationRequested();

                // ── Phase 1b: Idle latency + packet loss ────────────────────
                Report(progress, SpeedTestPhase.Ping, 70, 0, "Checking packet loss...");
                var (idleMs, lossPct) = await probe.MeasureIdleAsync(
                    IdleProbeCount, IdleProbeIntervalMs,
                    LatencyReporter(progress, SpeedTestPhase.Ping), ct);
                result.IdleLatencyMs = idleMs;
                result.PacketLossPct = lossPct;
                result.LatencyMethod = probe.Method;
                Report(progress, SpeedTestPhase.Ping, 100, 0,
                    $"Ping: {ping:F0} ms  ·  Packet loss: {lossPct:0.#}%");

                ct.ThrowIfCancellationRequested();

                // ── Phase 2: Download (+ latency under load) ────────────────
                Report(progress, SpeedTestPhase.Download, 0, 0, "Starting download test...");
                using (var loadCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    var loaded = probe.MeasureLoadedAsync(LoadedProbeIntervalMs,
                        LatencyReporter(progress, SpeedTestPhase.Download), loadCts.Token);
                    try
                    {
                        (result.DownloadMbps, result.DownloadStabilityPct) = await MeasureDownloadAsync(progress, window, cap, ct);
                    }
                    finally
                    {
                        loadCts.Cancel();
                        result.LoadedLatencyDownMs = await loaded;
                    }
                }

                ct.ThrowIfCancellationRequested();

                // ── Phase 3: Upload (+ latency under load) ──────────────────
                Report(progress, SpeedTestPhase.Upload, 0, 0, "Starting upload test...");
                using (var loadCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    var loaded = probe.MeasureLoadedAsync(LoadedProbeIntervalMs,
                        LatencyReporter(progress, SpeedTestPhase.Upload), loadCts.Token);
                    try
                    {
                        result.UploadMbps = await MeasureUploadAsync(progress, window, cap, ct);
                    }
                    finally
                    {
                        loadCts.Cancel();
                        result.LoadedLatencyUpMs = await loaded;
                    }
                }

                await ApplyConnectionInfoAsync(result, infoTask);

                result.Success = true;
                Report(progress, SpeedTestPhase.Complete, 100, result.DownloadMbps,
                    $"Done  ·  ↓ {result.DownloadMbps:F1} Mbps  " +
                    $"↑ {result.UploadMbps:F1} Mbps  " +
                    $"Ping {result.PingMs:F0} ms");
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Test cancelled.";
                Report(progress, SpeedTestPhase.Failed, 0, 0, "Cancelled.");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Survives past SendWithRetryAsync's own backoff only if the
                // server kept rate-limiting through every retry — a real,
                // sustained 429, not a one-off blip. Worth a plain-English
                // message instead of the raw exception text.
                result.Success = false;
                result.ErrorMessage = "The speed test server is rate-limiting requests right now. Please wait a bit before testing again.";
                Report(progress, SpeedTestPhase.Failed, 0, 0, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Report(progress, SpeedTestPhase.Failed, 0, 0, $"Error: {ex.Message}");
            }

            return result;
        }

        // ── ISP / location — attach whatever the lookup found ───────────────
        // By now the lookup has normally long finished; if it hasn't, give
        // it a couple of seconds more rather than holding the result back.
        private static async Task ApplyConnectionInfoAsync(SpeedTestResult result, Task<ConnectionInfo> infoTask)
        {
            try
            {
                var done = await Task.WhenAny(infoTask, Task.Delay(TimeSpan.FromSeconds(2)));
                if (done != infoTask) return;

                var info = await infoTask;
                result.Isp = info.Isp;
                result.ClientCity = info.City;
                result.ClientCountry = info.Country;
                result.ServerColo = info.Colo;
            }
            catch
            {
                // No ISP on this result — not a reason to fail the test.
            }
        }

        // ── Latency samples → progress (live graph) ─────────────────────────
        private static Action<double?>? LatencyReporter(
            IProgress<SpeedTestProgress>? progress, SpeedTestPhase phase)
        {
            if (progress == null) return null;

            return rtt => progress.Report(new SpeedTestProgress
            {
                Phase = phase,
                IsLatencySample = true,
                LatencyMs = rtt,
            });
        }

        // ── Ping + jitter (fast — 6 samples) ────────────────────────────────
        private async Task<(double ping, double jitter, string server)> MeasurePingAsync(
            CancellationToken ct)
        {
            string bestServer = PingEndpoints[0];
            double bestPing = double.MaxValue;

            foreach (var url in PingEndpoints)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    using var req = new HttpRequestMessage(HttpMethod.Head, url);
                    using var res = await _http.SendAsync(req,
                        HttpCompletionOption.ResponseHeadersRead, ct);
                    sw.Stop();

                    if (sw.Elapsed.TotalMilliseconds < bestPing)
                    {
                        bestPing = sw.Elapsed.TotalMilliseconds;
                        bestServer = url;
                    }
                }
                catch { /* try next */ }
            }

            var samples = new List<double>();
            for (int i = 0; i < 6; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var sw = Stopwatch.StartNew();
                    using var req = new HttpRequestMessage(HttpMethod.Head, bestServer);
                    using var res = await _http.SendAsync(req,
                        HttpCompletionOption.ResponseHeadersRead, ct);
                    sw.Stop();
                    samples.Add(sw.Elapsed.TotalMilliseconds);
                }
                catch { samples.Add(999); }

                await Task.Delay(30, ct);
            }

            double avg = samples.Average();
            double jitter = Math.Sqrt(samples.Sum(s => Math.Pow(s - avg, 2)) / samples.Count);

            return (Math.Round(avg, 1), Math.Round(jitter, 1), bestServer);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  THROUGHPUT (download / upload)
        //
        //  Several streams run in parallel and every byte they move goes into
        //  one shared counter. A single sampler reads that counter every
        //  SampleIntervalMs, so each sample — and the gauge, live graph and
        //  stability figure built on them — is the COMBINED rate of all
        //  streams. (The old code averaged per-stream rates, which reported
        //  roughly 1 / TransferStreams of the real speed on a saturated line.)
        //
        //  Streams restart as soon as a request finishes, so fast lines keep
        //  the pipe full for the whole window. The final figure is bytes ÷
        //  time over the steady part of the window (TCP ramp-up excluded).
        // ══════════════════════════════════════════════════════════════════════

        private const string DownloadBaseUrl = "https://speed.cloudflare.com/__down?bytes=";
        private const string UploadUrl = "https://speed.cloudflare.com/__up";

        /// <summary>Parallel connections per direction (tune under T04).</summary>
        private const int TransferStreams = 2;
        private static readonly TimeSpan TransferWindow = TimeSpan.FromSeconds(8);

        /// <summary>Light (scheduled) test: shorter window and a byte cap per direction.</summary>
        private static readonly TimeSpan LightWindow = TimeSpan.FromSeconds(4);
        private const long LightByteCap = 25L * 1024 * 1024;
        private const int SampleIntervalMs = 200;

        /// <summary>Leading share of the window treated as TCP ramp-up and left out of the result.</summary>
        private const double RampUpShare = 0.2;

        private const int UploadRequestBytes = 4 * 1024 * 1024;

        // ── Download ─────────────────────────────────────────────────────────
        private async Task<(double Mbps, double? StabilityPct)> MeasureDownloadAsync(
            IProgress<SpeedTestProgress>? progress,
            TimeSpan transferWindow, long byteCap,
            CancellationToken ct)
        {
            Report(progress, SpeedTestPhase.Download, 5, 0, "Warming up...");
            try
            {
                await DownloadOnceAsync(512 * 1024, _ => { }, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Warm-up failure (e.g. a transient rate limit) is non-fatal —
                // the measurement below still runs and reports its own errors.
            }

            Report(progress, SpeedTestPhase.Download, 10, 0, "Measuring download...");

            return await MeasureThroughputAsync(
                SpeedTestPhase.Download, "Download",
                (onBytes, token) => DownloadChunkAsync(onBytes, token),
                progress, transferWindow, byteCap, ct);
        }

        // ── Upload ───────────────────────────────────────────────────────────
        private async Task<double> MeasureUploadAsync(
            IProgress<SpeedTestProgress>? progress,
            TimeSpan transferWindow, long byteCap,
            CancellationToken ct)
        {
            Report(progress, SpeedTestPhase.Upload, 5, 0, "Warming up upload...");
            try
            {
                await UploadOnceAsync(new byte[256 * 1024], _ => { }, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Warm-up failure is non-fatal.
            }

            Report(progress, SpeedTestPhase.Upload, 10, 0, "Measuring upload...");

            // One random payload shared (read-only) by every stream.
            var payload = new byte[UploadRequestBytes];
            Random.Shared.NextBytes(payload);

            var (mbps, _) = await MeasureThroughputAsync(
                SpeedTestPhase.Upload, "Upload",
                (onBytes, token) => UploadOnceAsync(payload, onBytes, token),
                progress, transferWindow, byteCap, ct);
            return mbps;
        }

        // ── Shared measurement loop ──────────────────────────────────────────
        private static async Task<(double Mbps, double? StabilityPct)> MeasureThroughputAsync(
            SpeedTestPhase phase,
            string label,
            Func<Action<long>, CancellationToken, Task> runOneRequest,
            IProgress<SpeedTestProgress>? progress,
            TimeSpan transferWindow, long byteCap,
            CancellationToken ct)
        {
            var counter = new ByteCounter();

            using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
            window.CancelAfter(transferWindow);

            var workers = Enumerable.Range(0, TransferStreams)
                .Select(_ => KeepTransferringAsync(runOneRequest, counter.Add, window.Token))
                .ToArray();
            var allWorkers = Task.WhenAll(workers);

            var samples = new List<double>();                         // combined Mbps per interval
            var points = new List<(double Sec, long Bytes)> { (0, 0) }; // cumulative, for the result
            var clock = Stopwatch.StartNew();

            while (true)
            {
                await Task.WhenAny(Task.Delay(SampleIntervalMs, window.Token), allWorkers);

                // Window over, or every stream gave up: stop sampling. The
                // last partial interval is dropped — it would just be noise.
                if (window.IsCancellationRequested || allWorkers.IsCompleted)
                    break;

                double sec = clock.Elapsed.TotalSeconds;
                long bytes = counter.Total;
                var (lastSec, lastBytes) = points[^1];

                double mbps = ToMbps(bytes - lastBytes, sec - lastSec);
                samples.Add(mbps);
                points.Add((sec, bytes));

                int pct = Math.Min(95, 10 + (int)(sec / transferWindow.TotalSeconds * 85));
                Report(progress, phase, pct, mbps, $"{label}: {mbps:F1} Mbps");

                // Light test: enough data moved for this direction.
                if (byteCap > 0 && bytes >= byteCap)
                    break;
            }

            window.Cancel();
            Exception?[] errors = await allWorkers;

            ct.ThrowIfCancellationRequested();

            // Nothing got through: fail the test (with the stream's error when
            // there is one) instead of reporting 0 Mbps as a success.
            if (counter.Total == 0)
            {
                if (errors.FirstOrDefault(e => e != null) is Exception first)
                    throw first;
                throw new HttpRequestException($"The {label.ToLowerInvariant()} test couldn't reach the speed test server.");
            }

            double result = SteadyMbps(points);
            Report(progress, phase, 100, result, $"{label}: {result:F1} Mbps");

            return (Math.Round(result, 2), Stability(samples));
        }

        /// <summary>
        /// Runs requests back to back on one stream until the window closes.
        /// Returns the error that stopped it early, or null.
        /// </summary>
        private static async Task<Exception?> KeepTransferringAsync(
            Func<Action<long>, CancellationToken, Task> runOneRequest,
            Action<long> onBytes,
            CancellationToken window)
        {
            while (!window.IsCancellationRequested)
            {
                try
                {
                    await runOneRequest(onBytes, window);
                }
                catch (OperationCanceledException) when (window.IsCancellationRequested)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    // A hard failure that outlasted SendWithRetryAsync (e.g.
                    // sustained rate limiting). This stream stops; the others
                    // keep going and the result uses whatever got through.
                    return ex;
                }
            }
            return null;
        }

        /// <summary>
        /// Bytes ÷ time over the window after the ramp-up share. Falls back to
        /// the whole window when the steady part is too short to trust.
        /// </summary>
        private static double SteadyMbps(List<(double Sec, long Bytes)> points)
        {
            if (points.Count < 2) return 0;

            var end = points[^1];
            var start = points[(int)((points.Count - 1) * RampUpShare)];

            if (end.Sec - start.Sec < 1.0)
                start = points[0];

            return ToMbps(end.Bytes - start.Bytes, end.Sec - start.Sec);
        }

        private static double ToMbps(long bytes, double seconds) =>
            seconds > 0 ? bytes * 8.0 / seconds / 1_000_000 : 0;

        /// <summary>
        /// 100 − coefficient of variation (%), over the samples after the
        /// first 20% (TCP ramp-up is slow by design and isn't instability).
        /// Null when there are too few samples to say.
        /// </summary>
        private static double? Stability(List<double> samples)
        {
            var steady = samples.Skip(samples.Count / 5).ToList();
            if (steady.Count < 5) return null;

            double mean = steady.Average();
            if (mean <= 0) return null;

            double stddev = Math.Sqrt(steady.Sum(s => Math.Pow(s - mean, 2)) / steady.Count);
            return Math.Round(Math.Clamp(100 - stddev / mean * 100, 0, 100), 0);
        }

        // ── Chunk fix: download in sizes Cloudflare still serves ────────────
        //    Cloudflare now answers large single downloads (≥ ~10 MB) with
        //    429 and "Retry-After: 3600". Waiting that out stalled the whole
        //    window. Instead, step down to a smaller size at once.

        /// <summary>
        /// Per-request download sizes, largest first. Large requests keep a
        /// fast line busy with few round trips; after a 429 the next request
        /// uses the next size down. The working size is remembered for the
        /// rest of the session.
        /// </summary>
        public static readonly int[] DownloadChunkSizes =
        {
            25 * 1024 * 1024,
            10 * 1024 * 1024,
            5 * 1024 * 1024,
            2 * 1024 * 1024,
            1024 * 1024,
        };

        private static int _downloadChunkIndex;

        /// <summary>Size used for the next download request.</summary>
        public static int CurrentDownloadChunk => DownloadChunkSizes[Volatile.Read(ref _downloadChunkIndex)];

        /// <summary>
        /// Moves to the next smaller size after a 429 at <paramref name="refusedSize"/>.
        /// Returns false when already at the smallest. Two streams refusing
        /// the same size step down once, not twice.
        /// </summary>
        public static bool StepDownFrom(int refusedSize)
        {
            int index = Array.IndexOf(DownloadChunkSizes, refusedSize);
            if (index < 0 || index >= DownloadChunkSizes.Length - 1) return false;

            Interlocked.CompareExchange(ref _downloadChunkIndex, index + 1, index);
            return true;
        }

        public static void ResetDownloadChunk() => Volatile.Write(ref _downloadChunkIndex, 0);

        /// <summary>One download request at the current chunk size, stepping down on 429.</summary>
        private static async Task DownloadChunkAsync(Action<long> onBytes, CancellationToken ct)
        {
            while (true)
            {
                int size = CurrentDownloadChunk;
                try
                {
                    await DownloadOnceAsync(size, onBytes, ct);
                    return;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests
                                                      && StepDownFrom(size))
                {
                    // Refused at this size: retry straight away, smaller.
                }
            }
        }

        // ── Low-level: one download request, counting bytes as they arrive ──
        private static async Task DownloadOnceAsync(
            int bytes,
            Action<long> onBytes,
            CancellationToken ct)
        {
            // No waiting on a refused large size (DownloadChunkAsync steps
            // down instead); one short retry at the smallest size.
            using var res = await SendWithRetryAsync(
                c => _http.GetAsync(DownloadBaseUrl + bytes, HttpCompletionOption.ResponseHeadersRead, c),
                ct, maxRetries: bytes == DownloadChunkSizes[^1] ? 1 : 0);
            res.EnsureSuccessStatusCode();

            var buffer = new byte[65536];
            await using var stream = await res.Content.ReadAsStreamAsync(ct);

            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
                onBytes(bytesRead);
        }

        // ── Low-level: one upload request, counting bytes as they're sent ───
        private static async Task UploadOnceAsync(
            byte[] payload,
            Action<long> onBytes,
            CancellationToken ct)
        {
            // Fresh content per attempt — SendWithRetryAsync may resend.
            using var res = await SendWithRetryAsync(
                c => _http.PostAsync(UploadUrl, new CountingContent(payload, onBytes), c), ct);
            res.EnsureSuccessStatusCode();
        }

        /// <summary>Thread-safe running byte total shared by all streams.</summary>
        private sealed class ByteCounter
        {
            private long _total;
            public long Total => Interlocked.Read(ref _total);
            public void Add(long bytes) => Interlocked.Add(ref _total, bytes);
        }

        /// <summary>
        /// Request body that writes the payload in 64 KB pieces and reports
        /// each piece as it's handed to the connection, so upload is measured
        /// while it happens rather than per whole request (which also timed
        /// the server's reply and the round trip).
        /// </summary>
        private sealed class CountingContent : HttpContent
        {
            private const int PieceSize = 64 * 1024;
            private readonly byte[] _payload;
            private readonly Action<long> _onBytes;

            public CountingContent(byte[] payload, Action<long> onBytes)
            {
                _payload = payload;
                _onBytes = onBytes;
                Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
                SerializeToStreamAsync(stream, context, CancellationToken.None);

            protected override async Task SerializeToStreamAsync(
                Stream stream, TransportContext? context, CancellationToken cancellationToken)
            {
                for (int offset = 0; offset < _payload.Length; offset += PieceSize)
                {
                    int count = Math.Min(PieceSize, _payload.Length - offset);
                    await stream.WriteAsync(_payload.AsMemory(offset, count), cancellationToken);
                    _onBytes(count);
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _payload.Length;
                return true;
            }
        }

        // ── Retry wrapper — absorbs 429s with a short backoff before they
        //    become a hard failure. Honors the server's Retry-After header
        //    when present, otherwise a short fixed backoff; gives up after
        //    maxRetries and returns the last (failing) response as-is.
        /// <summary>Longest wait for one retry, whatever Retry-After says
        /// (Cloudflare sends 3600 s, which outlasts the whole test).</summary>
        public static readonly TimeSpan MaxRetryWait = TimeSpan.FromSeconds(2);

        public static TimeSpan RetryWait(TimeSpan? retryAfter, int attempt)
        {
            var wait = retryAfter ?? TimeSpan.FromSeconds(1.5 * (attempt + 1));
            return wait > MaxRetryWait ? MaxRetryWait : wait;
        }

        private static async Task<HttpResponseMessage> SendWithRetryAsync(
            Func<CancellationToken, Task<HttpResponseMessage>> send,
            CancellationToken ct,
            int maxRetries = 2)
        {
            for (int attempt = 0; ; attempt++)
            {
                var res = await send(ct);
                if (res.StatusCode != HttpStatusCode.TooManyRequests || attempt >= maxRetries)
                    return res;

                var wait = RetryWait(res.Headers.RetryAfter?.Delta, attempt);
                res.Dispose();
                await Task.Delay(wait, ct);
            }
        }

        // ── Progress helper ──────────────────────────────────────────────────
        private static void Report(
            IProgress<SpeedTestProgress>? progress,
            SpeedTestPhase phase,
            int percent,
            double valueMbps,
            string statusText)
        {
            progress?.Report(new SpeedTestProgress
            {
                Phase = phase,
                Percent = Math.Clamp(percent, 0, 100),
                ValueMbps = valueMbps,
                PeakMbps = valueMbps,
                StatusText = statusText
            });
        }
    }
}
