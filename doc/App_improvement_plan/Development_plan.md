# Test Net Speed — Improvement Development Plan

Source: `Initial_draft.txt` (same folder), checked against the current code on 2026-11-05.

Effort sizes: **S** = 1–2 days · **M** = 3–5 days · **L** = 1–2 weeks (one developer).

---

## 1. Review of the draft

The draft is solid. Its idea, *"understand your internet" rather than "another speed test"*, is the right way to stand out, and its catalog check is correct: none of the 15 items repeats what WiFi Password Viewer, Net Speed Meter or Net Monitor already do. Checking it against the code turned up the corrections below. The plan in section 3 already includes them.

### Confirmed by the code

| Draft claim | Code evidence |
|---|---|
| No Jitter card | `SpeedTestResult.JitterMs` is already measured (stddev of 6 ping samples). `DashboardControl` creates only Download / Upload / Ping / Internet / Last Tested tiles. The fix is just UI. |
| Settings is 2nd in the sidebar | `AppConfig.NavItems`: Dashboard, **Settings**, Speed Test, … |
| No server / ISP shown | `SpeedTestResult.ServerUsed` holds only the fastest *ping* URL. Nothing resolves the ISP. `DashboardControl` even has a comment saying it leaves ISP out on purpose because there is no geolocation source. |
| History data exists but has no charts | `TestHistoryStore` saves a JSON list (`TestHistory.json`). `TestHistoryControl` shows it only as a table. |

### Corrections and risks

1. **Server Selection (#4) is not possible with the current backend.** All download and upload traffic goes to `speed.cloudflare.com`, which is anycast. There is exactly one logical server, so a manual picker or favourite server has nothing to choose between. **Change:** ship only the *ISP + server location* half. Cloudflare's `speed.cloudflare.com/meta` (ASN org, city, country, colo) or `cdn-cgi/trace` (colo) gives ISP name and test location for free. Drop the picker unless a second backend is added (see section 4).
2. **Packet loss cannot be measured over HTTP.** TCP hides loss by retransmitting. Cloudflare's own test uses WebRTC/TURN for this. **Change:** measure loss with ICMP (`System.Net.NetworkInformation.Ping`, 50–100 echoes to 1.1.1.1 / 8.8.8.8). If ICMP is blocked, fall back to TCP-connect timeouts on :443, and label the result "approximate" in that case.
3. **Quality Score (#1) depends on #2.** Its gaming and calls verdicts need loaded latency and loss to be meaningful. Build #2 first, in the same release.
4. **"Ping at 3 stages" (#3) is a by-product of #2.** Loaded latency *is* ping during download and upload. Ship the 3-stage numbers with #2. Only the live graph is extra work.
5. **The Video Streaming Test (#5) has no backend.** Ookla streams from real video CDNs. Without one, the app can only *estimate* the highest stable resolution from sustained throughput, how much it varies, and loaded latency. **Change:** demote it to "Streaming estimate" inside the Quality Score, or leave it out. Don't present it as a real video test.
6. **Gaming Ping Board (#9).** Many game and voice servers drop ICMP. Use TCP-connect time to known hosts and ports, with ICMP where it answers. Keep the host list in a JSON file so it can be updated without code changes.
7. **"Pro" gating has no infrastructure yet.** There is no Store add-on or licensing code in the project. A one-day entitlement stub should come early (Phase 0) so features can be marked Pro-ready, and the real Store add-on lands in Phase 4.
8. **The test runner lives in the UI.** `SpeedTestControl` calls `RunFullTestAsync` and `TestHistoryStore.Append` directly. Scheduled tests (#8), the tray quick test (#11) and the outage log (#13) all need to run headless, so this has to be pulled out into a service first.
9. **The history schema will grow.** Loaded latency, loss, ISP, colo, connection type, tags and plan % all add fields to `SpeedTestResult`. Add a `SchemaVersion` now, keep old files readable, and consider moving to JSON Lines or SQLite once scheduled tests multiply the row count.
10. **Backend dependency.** All tests rely on Cloudflare's public speed endpoints. Scheduled tests add steady background load. Keep the default schedule light (daily), add backoff on 429/5xx, and see section 4 for a fallback.

### Scope verdict

The draft's tight next release (#1 + #2 + #6 + Jitter) is right. I'd add three cheap items: the **sidebar reorder**, **ISP + location** (half a day once the `/meta` call exists), and **3-stage ping** (free from #2).

---

## 2. Release map

| Phase | Theme | Items | Tier | Est. |
|---|---|---|---|---|
| 0 | Foundations + quick fixes | Jitter tile, sidebar order, ISP/location, test-runner service, schema v2, entitlement stub | Free | ~1.5 wk |
| 1 | "Understand your internet" (v-next) | #2 Loaded latency / loss / bufferbloat, 3-stage ping, #1 Quality Score, #6 Share card | Free | ~2.5 wk |
| 2 | Retention | #7 History charts, #8 Scheduled tests + alerts, #11 Tray quick test, #3 Live graph | Free | ~3 wk |
| 3 | Diagnostics | #9 Gaming & Meeting Ping Board, #10 Wi-Fi vs Ethernet insight, #5 Streaming estimate | Free | ~2 wk |
| 4 | Pro launch | Store add-on, #12 ISP Plan Checker + report, #13 Outage log, #14 Accurate Mode, #15 DNS Race bundle | Pro | ~4 wk |

Each phase ends with a Store release and new Store screenshots.

---

## 3. Phases

### Phase 0 — Foundations and quick fixes

**Goal:** fix the visible gaps the draft found, and lay the plumbing every later phase needs.

| # | Task | Files | Effort |
|---|---|---|---|
| 0.1 | **Sidebar order.** Dashboard, Speed Test, Test History, Network Info, Network Tools, then Settings, Our Apps, About at the bottom. | `Helper/AppConfig.cs` (`NavItems`) | S (minutes) |
| 0.2 | **Jitter tile** on the dashboard. The value is already stored. | `UserControls/Pages/DashboardControl.cs` | S |
| 0.3 | **ISP + test location.** New `ConnectionInfoService` that calls `speed.cloudflare.com/meta` (fallback: `cdn-cgi/trace`) once per test. Save `Isp`, `ServerCity`, `ServerColo` on the result. Show an ISP tile on the dashboard and on the Speed Test result. | new `Service/ConnectionInfoService.cs`, `Model/SpeedTestResult.cs`, `DashboardControl.cs`, `SpeedTestControl.cs` | S |
| 0.4 | **Headless test runner.** New `SpeedTestRunner` that owns run → enrich (ISP, connection type) → save to history → raise `TestCompleted`. `SpeedTestControl` switches to it, and it allows one test at a time across the app. | new `Service/SpeedTestRunner.cs`, `SpeedTestControl.cs` | M |
| 0.5 | **Result schema v2.** Add `SchemaVersion`, plus nullable fields for the later phases (`LoadedPingDownMs`, `LoadedPingUpMs`, `PacketLossPct`, `Isp`, `ServerColo`, `ConnectionType`, `Tag`, `Trigger` [manual/scheduled/tray]). Old files load with the new fields left empty. | `Model/SpeedTestResult.cs`, `Helper/TestHistoryStore.cs` | S |
| 0.6 | **Entitlement stub.** `Entitlements.IsPro` (always false for now) and a `ProGate` helper that shows a "Coming in Pro" badge or upsell, so later phases can mark features without a refactor. | new `Helper/Entitlements.cs` | S |

**Done when:** the sidebar is in the new order, the dashboard shows Jitter and ISP, old `TestHistory.json` files still load, and a test can be started from code without the Speed Test page open.

---

### Phase 1 — "Understand your internet" (next Store release)

**Goal:** change the headline Store screenshot from four numbers to a verdict.

#### 1.1 Loaded latency, packet loss, bufferbloat grade (#2), M–L

- `LatencyProbe`: an ICMP ping loop (about 4 per second) that runs **at the same time as** the download and upload phases in `SpeedTestService`. It falls back to TCP-connect probes if ICMP is blocked.
- New idle loss phase: 50 ICMP echoes before the download phase, about 3 seconds. The result is `PacketLossPct`.
- Bufferbloat = loaded latency (median) − idle latency. Grades follow the common scale: A < 30 ms, B < 60, C < 200, D < 400, F ≥ 400.
- **3-stage ping display:** idle / download / upload on the result card.
- Files: `Service/SpeedTestService.cs`, new `Service/LatencyProbe.cs`, `Model/SpeedTestResult.cs`, `SpeedTestControl.cs`, `TestHistoryControl.cs` (new columns).
- **Done when:** loaded and idle ping, loss %, and an A–F grade appear on every result and are saved to history. Total test time grows by no more than about 4 seconds.

#### 1.2 Connection Quality Score (#1), S

- `QualityScorer`: a pure function from `SpeedTestResult` to three verdicts (**Streaming**, **Gaming**, **Video calls**), each Great / Good / Fair / Poor with a one-line reason ("Poor for gaming: latency jumps to 180 ms under load").
- Thresholds go in one table, modelled on Cloudflare AIM's points per metric (latency, loss, download, upload, loaded latency, jitter). Add unit tests per band.
- A Streaming line inside it: "Up to 4K" / "HD" / "SD", based on sustained download and variance. This replaces #5 (see correction 5).
- UI: a verdict card on the Speed Test result and the Dashboard.
- **Done when:** every result shows three verdicts and the scorer has unit tests for each threshold edge.

#### 1.3 Shareable result card (#6), S

- `ResultCardRenderer` draws a branded PNG (orange `AppConfig.PrimaryColor`, logo, Download/Upload/Ping, Quality verdicts, ISP, date, app name/Store link).
- Actions: **Copy image**, **Save PNG…**, and Windows Share via `DataTransferManager` where the packaged app supports it.
- **Done when:** one click puts the card on the clipboard, and it renders correctly at 100–200% DPI in both themes.

**Release checklist:** new Store screenshots (verdict card, bufferbloat grade, share card), a Store description update, and "What's new" text.

---

### Phase 2 — Retention

**Goal:** give users a reason to keep the app installed and open it again.

| # | Task | Notes | Effort |
|---|---|---|---|
| 2.1 | **History charts & insights (#7)** | 7 / 30-day trend lines (download, upload, ping). Avg / best / worst. Stability % (share of tests within ±20% of the median). Compare 2 tests side by side. Tag a test. CSV export. Draw the chart with GDI+, like `SpeedTestUIHelper`, to avoid adding a dependency. | M–L |
| 2.2 | **Scheduled auto-tests + alerts (#8)** | Uses `SpeedTestRunner`. Free tier: daily at a user-chosen time while the app runs in the tray (`MinimizeToTrayOnClose` already exists). Alerts through `NotificationManager` when download is below a user threshold or the internet is unreachable. Skip on metered connections and on battery saver. Back off on server errors. | M |
| 2.3 | **Tray quick test (#11)** | "Run quick test" tray menu item plus a global hotkey. A small flyout near the tray shows live progress and the result, with an "Open full results" link. | S–M |
| 2.4 | **Live test graph (#3)** | Real-time download/upload Mbps curve plus loaded-ping overlay in the empty dashboard and Speed Test area. Data already comes from `IProgress<SpeedTestProgress>`. Extend it with timestamped samples. | M |

**Settings additions:** Schedule (off / daily + time), alert threshold (Mbps), alert on outage (on/off). These go in `AppSettingsVm` and `BehaviorSettings.cs`.

**Done when:** a 30-day history renders in under 200 ms, scheduled tests run with the window closed and show up in history as `Trigger = scheduled`, and alerts fire once per incident, not every test.

---

### Phase 3 — Diagnostics

**Goal:** explain *why* the connection is slow, not just that it is.

| # | Task | Notes | Effort |
|---|---|---|---|
| 3.1 | **Gaming & Meeting Ping Board (#9)** | A list of targets (Discord, Zoom, Teams, Google, Cloudflare, Steam/Riot/Xbox regions) with ping, jitter and loss each, run in parallel. Uses `NetworkToolsService` ping plus TCP-connect fallback. Targets come from a bundled `ping_targets.json`. A new tab, or a sub-view of Network Tools. | S–M |
| 3.2 | **Wi-Fi vs Ethernet insight (#10)** | Current connection only: SSID, band, signal %, link rate, channel, through the Native Wi-Fi API (`wlanapi.dll`). Check WiFiPasswordViewer for reusable interop. After a slow test, show a rule-based tip ("2.4 GHz at 43% signal is your bottleneck"). Save `ConnectionType` and band on the result. | M |
| 3.3 | **Streaming estimate polish (#5)** | If Phase 1's estimate tests well, add a short dedicated "Streaming check" (a 20-second sustained download with variance) that shows a resolution and a buffering-risk level. Label it an estimate. | S |

**Done when:** the ping board finishes in under 5 seconds, and a slow test on weak Wi-Fi produces the correct tip.

---

### Phase 4 — Pro launch

**Goal:** the first paid tier, with the ISP Plan Checker as its headline.

| # | Task | Notes | Effort |
|---|---|---|---|
| 4.1 | **Store add-on + licensing** | A durable add-on or subscription through `Windows.Services.Store.StoreContext`. Replace the `Entitlements` stub. Add a "Go Premium" header button, like the one in Net Speed Meter Plus. Cache the license check for offline use. | M |
| 4.2 | **ISP Plan Checker + complaint report (#12)** | Plan entry (down/up Mbps, ISP, price). Every test is graded as "% of plan" (free shows the grade). **Pro:** 30-day PDF report with charts, percentiles, test times and server location, ready to send to the ISP. | M–L |
| 4.3 | **Outage / uptime log (#13)** | Background connectivity probe every 30 seconds (cheap HEAD request or ICMP) while in the tray. Records start, end and duration. Daily summary ("down 3×, 47 min"). Included in the 4.2 report. | M |
| 4.4 | **Accurate Mode (#14)** | Single vs multi-connection option (`SpeedTestService` stream count is a parameter), plus "Run 3× and take the median". | S–M |
| 4.5 | **DNS Race + connectivity bundle (#15)** | Race 1.1.1.1 / 8.8.8.8 / 9.9.9.9 / the ISP's DNS (cold and warm lookups) and recommend the fastest. Also IPv6 reachability, NAT type (STUN), and MTU path check. Extends the existing DNS Lookup. | M |

**Done when:** purchase, restore and offline license all work, gated features show the upsell for free users, and the PDF report is checked with a real 30-day dataset.

---

## 4. Open decisions

1. **Second test backend?** Server selection (#4) and resilience against Cloudflare throttling both need one. Options: self-hosted LibreSpeed, M-Lab NDT7 (free, many servers, needs a WebSocket client), or stay on Cloudflare only. **Recommendation:** evaluate NDT7 during Phase 2. Until then, Cloudflare plus ISP/location display is enough.
2. **Pro pricing model:** ~~one-time unlock or subscription~~ **Decided: one-time unlock, US$9.99** (see [Pro_plan.md](Pro_plan.md)).
3. **Should history move to SQLite?** **Decided: no, stay with JSON files.** Pro data uses daily JSON Lines files for the per-minute log; see the storage design in [Pro_plan.md](Pro_plan.md).
4. **Privacy text.** Phase 0.3 and the outage log send requests to Cloudflare in the background. Update the Store privacy statement and the About page before Phase 2 ships.
