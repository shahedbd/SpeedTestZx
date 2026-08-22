# SpeedTestService

`Service/SpeedTestService.cs` — runs the full Ping → Download → Upload test
and returns a `SpeedTestResult`. Called from `SpeedTestControl` via
`RunFullTestAsync(progress, ct)`; each phase reports live updates through
`IProgress<SpeedTestProgress>`.

## Phases

### Ping

`MeasurePingAsync` runs in two rounds: probe every endpoint once to find
the fastest, then take 6 timed samples against that one winner. `PingMs`
is the sample average; `JitterMs` is their standard deviation. A sample
that errors (timeout, DNS failure, etc.) is recorded as a 999ms outlier
rather than aborting the test — see Resilience below.

```
 ROUND 1 — probe every endpoint once (HEAD), keep the fastest
 ┌──────────────────────────────────────────────────────────┐
 │  speed.cloudflare.com  ──HEAD──▶  120 ms                   │
 │  www.cloudflare.com    ──HEAD──▶   95 ms                   │
 │  1.1.1.1               ──HEAD──▶   40 ms   ◀── fastest      │
 │  www.google.com        ──HEAD──▶   80 ms                   │
 │  www.microsoft.com     ──HEAD──▶  110 ms                   │
 │  www.bing.com          ──HEAD──▶  105 ms                   │
 └──────────────────────────────────────────────────────────┘
                            │
                            ▼   bestServer = 1.1.1.1
 ROUND 2 — 6 timed samples against the winner, 30ms apart
 ┌──────────────────────────────────────────────────────────┐
 │  #1  38 ms   #2  41 ms   #3  39 ms                          │
 │  #4  55 ms   #5  40 ms   #6  42 ms                          │
 │                                                              │
 │  PingMs   = average(samples)                                │
 │  JitterMs = stddev(samples)                                 │
 └──────────────────────────────────────────────────────────┘
```

### Download

`MeasureDownloadAsync` — a small warm-up GET, then **two concurrent GET
streams** against `speed.cloudflare.com/__down?bytes=50MB`, capped at 8
seconds. Each stream reads its response in 64KB chunks and reports a
fresh Mbps sample roughly every 200ms; both streams pool their samples
into one shared list.

```
 1. Warm-up — one GET, 512KB, failure is non-fatal
 ┌──────────────────────────────────────────────────────────┐
 │   client ──GET /__down?bytes=512KB──▶ speed.cloudflare.com │
 └──────────────────────────────────────────────────────────┘
                            │
                            ▼
 2. Measured transfer — 2 concurrent streams, 8s cap
 ┌──────────────────────────────────────────────────────────┐
 │  Stream A ──GET /__down?bytes=50MB──▶ ██████░░░░ reading   │
 │  Stream B ──GET /__down?bytes=50MB──▶ ████████░░ reading   │
 │                                                              │
 │  every ~200ms, each stream reports: mbps = bytes*8/elapsed  │
 │                                                              │
 │    A:  18.2  19.0  21.4  20.1  19.8 ...                     │
 │    B:  17.9  20.3  22.0  19.5  20.6 ...                     │
 │          \      |      /                                    │
 │           ▼     ▼     ▼                                     │
 │        shared samples[]  (both streams pool together)       │
 └──────────────────────────────────────────────────────────┘
                            │
                            ▼
 3. Result = average of the FASTEST 80% of samples
    (drops the slow ramp-up outliers at the bottom)  → DownloadMbps
```

### Upload

`MeasureUploadAsync` — a small warm-up POST, then **two concurrent
loops**, each repeatedly POSTing a fixed 4MB random payload to
`speed.cloudflare.com/__up` until the 8-second cap fires. Every completed
POST is one sample (not a periodic timer like download — the chunk size
is fixed, so each POST's own elapsed time gives the Mbps directly).

```
 1. Warm-up — one POST, 256KB, failure is non-fatal
 ┌──────────────────────────────────────────────────────────┐
 │   client ──POST /__up (256KB)──▶ speed.cloudflare.com      │
 └──────────────────────────────────────────────────────────┘
                            │
                            ▼
 2. Measured transfer — 2 concurrent chunk loops, 8s cap
 ┌──────────────────────────────────────────────────────────┐
 │  Stream A: [4MB]→POST→ack [4MB]→POST→ack [4MB]→POST→ack... │
 │  Stream B: [4MB]→POST→ack [4MB]→POST→ack [4MB]→POST→ack... │
 │                                                              │
 │  each POST: mbps = chunkSize*8 / elapsed   (1 sample/chunk) │
 │        \                              /                     │
 │         ▼                            ▼                      │
 │      shared samples[]  (both loops append, lock-guarded)    │
 │                                                              │
 │  loop stops when the 8s timer fires — not a fixed chunk count│
 └──────────────────────────────────────────────────────────┘
                            │
                            ▼
 3. Result = average of the FASTEST 80% of samples   → UploadMbps
```

Both transfer phases fall back to a plain average if fewer than 5 samples
came in (e.g. a very slow connection that barely completed one chunk).

## Resilience — 429 (Too Many Requests)

Cloudflare's endpoint rate-limits per IP, and this test hits it in a short
burst (ping + warm-up + 2 concurrent streams × 2 phases). Three layers
absorb that instead of failing the whole test on one rate-limited request:

1. **`SendWithRetryAsync`** — wraps every GET/POST to the transfer
   endpoints. On 429, waits for the server's `Retry-After` header (or a
   short fixed backoff) and retries, up to 2 times, before giving up.
2. **Warm-up failures are non-fatal** — both the download and upload
   warm-up calls are wrapped in try/catch; a failed warm-up doesn't block
   the real measurement loop that follows.
3. **Per-stream failure tolerance** — if one of the two concurrent
   streams hits a hard HTTP failure that outlasts its retries, the phase
   falls back to whatever samples the other stream collected instead of
   throwing.

If a 429 still survives all of that (sustained, not transient), it
surfaces as a plain-English message — *"The speed test server is
rate-limiting requests right now..."* — instead of the raw exception text.
Ping-phase failures never throw; each sample that errors is recorded as a
999ms outlier instead of aborting the test.

## Result — `Model/SpeedTestResult`

`DownloadMbps`, `UploadMbps`, `PingMs`, `JitterMs`, `ServerUsed`,
`TestedAt`, `Success`, `ErrorMessage`. On success, `SpeedTestControl`
persists it via `TestHistoryStore.Append()`.

## Notes

- Single shared `static HttpClient` (`AllowAutoRedirect = false`,
  20s timeout) — not per-call, per standard `HttpClient` guidance.
- The 8-second cap per phase is a `CancellationTokenSource` linked to the
  caller's token, not a fixed sleep — a fast connection still gets many
  samples; a slow one still gets cut off on time.
