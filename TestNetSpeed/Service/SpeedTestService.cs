using System.Diagnostics;
using System.Net;
using TestNetSpeed.Model;

namespace TestNetSpeed.Service
{
    public class SpeedTestService
    {
        // ── Endpoints ────────────────────────────────────────────────────────
        private static readonly string[] PingEndpoints =
        [
            "https://speed.cloudflare.com/__down?bytes=1",
            "https://www.google.com",
            "https://www.cloudflare.com",
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

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINT
        // ══════════════════════════════════════════════════════════════════════
        public async Task<SpeedTestResult> RunFullTestAsync(
            IProgress<SpeedTestProgress>? progress = null,
            CancellationToken ct = default)
        {
            var result = new SpeedTestResult { TestedAt = DateTime.Now };

            try
            {
                // ── Phase 1: Ping ───────────────────────────────────────────
                Report(progress, SpeedTestPhase.Ping, 0, 0, "Measuring ping...");
                var (ping, jitter, server) = await MeasurePingAsync(ct);
                result.PingMs = ping;
                result.JitterMs = jitter;
                result.ServerUsed = server;
                Report(progress, SpeedTestPhase.Ping, 100, 0,
                    $"Ping: {ping:F0} ms  ·  Jitter: {jitter:F0} ms");

                ct.ThrowIfCancellationRequested();

                // ── Phase 2: Download ───────────────────────────────────────
                Report(progress, SpeedTestPhase.Download, 0, 0, "Starting download test...");
                result.DownloadMbps = await MeasureDownloadAsync(progress, ct);

                ct.ThrowIfCancellationRequested();

                // ── Phase 3: Upload ─────────────────────────────────────────
                Report(progress, SpeedTestPhase.Upload, 0, 0, "Starting upload test...");
                result.UploadMbps = await MeasureUploadAsync(progress, ct);

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

        // ── Download — live Mbps reported every ~200 ms ─────────────────────
        private async Task<double> MeasureDownloadAsync(
            IProgress<SpeedTestProgress>? progress,
            CancellationToken ct)
        {
            const int totalBytes = 50 * 1024 * 1024;
            var url = $"https://speed.cloudflare.com/__down?bytes={totalBytes}";

            Report(progress, SpeedTestPhase.Download, 5, 0, "Warming up...");

            try
            {
                await DownloadStreamAsync(
                    $"https://speed.cloudflare.com/__down?bytes={512 * 1024}", null, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Warm-up failure (e.g. a transient rate limit) is non-fatal
                // — same tolerance the upload warm-up below already has via
                // UploadChunkAsync's own try/catch. The real measurement
                // loop still runs and produces its own samples.
            }

            Report(progress, SpeedTestPhase.Download, 10, 0, "Measuring download...");

            var samples = new List<double>();
            double peak = 0;

            var cts2 = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts2.CancelAfter(TimeSpan.FromSeconds(8));

            var liveProgress = new Progress<double>(mbps =>
            {
                samples.Add(mbps);
                peak = Math.Max(peak, mbps);

                int pct = Math.Clamp(10 + samples.Count * 3, 10, 95);
                Report(progress, SpeedTestPhase.Download, pct, mbps, $"Download: {mbps:F1} Mbps");
            });

            try
            {
                var t1 = DownloadStreamAsync(url, liveProgress, cts2.Token);
                var t2 = DownloadStreamAsync(url, liveProgress, cts2.Token);
                await Task.WhenAll(t1, t2);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Time limit hit — not a user cancel
            }
            catch (HttpRequestException) when (!ct.IsCancellationRequested)
            {
                // One stream hit a hard HTTP failure that outlasted
                // SendWithRetryAsync's retries (e.g. sustained rate
                // limiting) — fall back to whatever samples the other
                // stream already collected instead of failing the test.
            }

            if (ct.IsCancellationRequested)
                ct.ThrowIfCancellationRequested();

            double result = samples.Count > 4
                ? samples.OrderDescending().Take((int)(samples.Count * 0.8)).Average()
                : samples.DefaultIfEmpty(0).Average();

            Report(progress, SpeedTestPhase.Download, 100, result, $"Download: {result:F1} Mbps");

            return Math.Round(result, 2);
        }

        // ── Upload — live Mbps reported every ~200 ms ───────────────────────
        private async Task<double> MeasureUploadAsync(
            IProgress<SpeedTestProgress>? progress,
            CancellationToken ct)
        {
            const string uploadUrl = "https://speed.cloudflare.com/__up";

            Report(progress, SpeedTestPhase.Upload, 5, 0, "Warming up upload...");
            await UploadChunkAsync(uploadUrl, 256 * 1024, ct);

            Report(progress, SpeedTestPhase.Upload, 10, 0, "Measuring upload...");

            var samples = new List<double>();
            double peak = 0;

            var cts2 = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts2.CancelAfter(TimeSpan.FromSeconds(8));

            try
            {
                var t1 = UploadStreamAsync(uploadUrl, samples, ct: cts2.Token,
                    onSample: mbps =>
                    {
                        peak = Math.Max(peak, mbps);
                        int pct = Math.Clamp(10 + samples.Count * 3, 10, 95);
                        Report(progress, SpeedTestPhase.Upload, pct, mbps, $"Upload: {mbps:F1} Mbps");
                    });

                var t2 = UploadStreamAsync(uploadUrl, samples, ct: cts2.Token,
                    onSample: mbps =>
                    {
                        peak = Math.Max(peak, mbps);
                        int pct = Math.Clamp(10 + samples.Count * 3, 10, 95);
                        Report(progress, SpeedTestPhase.Upload, pct, mbps, $"Upload: {mbps:F1} Mbps");
                    });

                await Task.WhenAll(t1, t2);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Time limit hit
            }
            catch (HttpRequestException) when (!ct.IsCancellationRequested)
            {
                // Same tolerance as the download side above.
            }

            if (ct.IsCancellationRequested)
                ct.ThrowIfCancellationRequested();

            double result = samples.Count > 4
                ? samples.OrderDescending().Take((int)(samples.Count * 0.8)).Average()
                : samples.DefaultIfEmpty(0).Average();

            Report(progress, SpeedTestPhase.Upload, 100, result, $"Upload: {result:F1} Mbps");

            return Math.Round(result, 2);
        }

        // ── Low-level: streaming download with live sampling ───────────────
        private async Task DownloadStreamAsync(
            string url,
            IProgress<double>? liveProgress,
            CancellationToken ct)
        {
            using var res = await SendWithRetryAsync(
                c => _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, c), ct);
            res.EnsureSuccessStatusCode();

            var buffer = new byte[65536];
            await using var stream = await res.Content.ReadAsStreamAsync(ct);

            long totalRead = 0;
            var sw = Stopwatch.StartNew();
            var sampleSw = Stopwatch.StartNew();
            long sampleRead = 0;

            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
            {
                totalRead += bytesRead;
                sampleRead += bytesRead;

                if (liveProgress != null && sampleSw.Elapsed.TotalMilliseconds >= 200)
                {
                    double mbps = sampleRead * 8.0 / sampleSw.Elapsed.TotalSeconds / 1_000_000;

                    liveProgress.Report(mbps);
                    sampleRead = 0;
                    sampleSw.Restart();
                }
            }
        }

        // ── Low-level: upload stream — sends 4 MB chunks in a loop ─────────
        private async Task UploadStreamAsync(
            string url,
            List<double> sharedSamples,
            CancellationToken ct,
            Action<double>? onSample = null)
        {
            const int chunkSize = 4 * 1024 * 1024; // 4 MB
            var payload = new byte[chunkSize];
            Random.Shared.NextBytes(payload);

            while (!ct.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();

                using var content = new ByteArrayContent(payload);
                content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                try
                {
                    using var res = await SendWithRetryAsync(c => _http.PostAsync(url, content, c), ct);
                    sw.Stop();

                    double mbps = chunkSize * 8.0 / sw.Elapsed.TotalSeconds / 1_000_000;

                    lock (sharedSamples)
                        sharedSamples.Add(mbps);

                    onSample?.Invoke(mbps);
                }
                catch (OperationCanceledException) { break; }
                catch { break; }
            }
        }

        // ── Upload chunk (warm-up only) ─────────────────────────────────────
        private async Task UploadChunkAsync(string url, int bytes, CancellationToken ct)
        {
            var payload = new byte[bytes];
            Random.Shared.NextBytes(payload);
            using var content = new ByteArrayContent(payload);
            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            try { using var res = await SendWithRetryAsync(c => _http.PostAsync(url, content, c), ct); }
            catch { /* warm-up failure is non-fatal */ }
        }

        // ── Retry wrapper — absorbs 429s with a short backoff before they
        //    become a hard failure. Honors the server's Retry-After header
        //    when present, otherwise a short fixed backoff; gives up after
        //    maxRetries and returns the last (failing) response as-is.
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

                var wait = res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1.5 * (attempt + 1));
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
