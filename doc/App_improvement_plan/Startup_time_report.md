# Start-up Time — Before / After

Date: 2026-11-06 · Release build · this PC (warm start, 12 runs each, first run dropped, builds alternated).

## Results

| Measure | Before | After | Change |
|---|---|---|---|
| Window shown (in-app timer from process start) | 786 ms | 472 ms | −40% |
| Ready: window painted, app idle (in-app timer) | 901 ms | 583 ms | −35% |
| Ready (external: launch → window up and idle) | 749 ms | 408 ms | −46% |
| Same, with ReadyToRun (Store publish) | — | 396 ms | −47% |

ReadyToRun alone, before the code fixes, took the in-app "ready" time from about 1.1 s to about 0.87 s.

## What was slow and what changed

| Cost on the UI thread at launch | Fix |
|---|---|
| Dashboard live-traffic sampler, first adapter read (~120 ms) | Baseline read on a worker; timer starts when it's done |
| Tray icon and menu built at launch (~100 ms) | Built on first use (hide to tray, Monitor on, alert) |
| First WinRT call: "started by Windows?" check (~65 ms) | Read on a worker at launch, answer cached |
| Store context created during load (~65 ms) | Created after the first paint, with the licence check and Monitor start |
| Icon font loaded on first icon (~65 ms) | Loaded on a worker at launch |
| Public-IP request set up on the UI thread (~45 ms) | Set up on a worker; result still lands on the UI thread |
| Settings + history JSON first use | Read on a worker at launch; the UI thread reuses it |
| Single-instance check listed all processes | Named mutex (same rule: one instance per exe path) |
| JIT compiling the app's code | `PublishReadyToRun` for runtime-specific publishes (Store x64 / ARM64) |

Checked after the change: the Store licence check completes, the Monitor starts, traffic samples
arrive every second, and the internet tile fills in.

## Not changed

- About 100 ms to show the window and 100 ms for its first paint: normal WinForms work.
- ReadyToRun applies only when the package is built for x64 / ARM64. An AnyCPU / neutral package skips it.
