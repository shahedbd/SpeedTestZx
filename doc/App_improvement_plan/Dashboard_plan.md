# Dashboard Improvement — Development Plan

Parent plan: [Development_plan.md](Development_plan.md) · Tasks: [Task_tracker.md](Task_tracker.md) (T41–T48)

## Goal

The Dashboard is the landing page. It should answer two questions without running a test:

1. **How is my connection right now?** A live traffic graph and live latency.
2. **How has it been lately?** The last test's verdicts and a trend of recent tests.

The big welcome card stays only for first-time users who have no tests yet.

## Decisions (agreed)

| # | Decision | Choice |
|---|---|---|
| D1 | What the Dashboard graph shows | **Option A — live traffic** on this PC (all apps), not a speed test |
| D2 | Live latency (ping 1.1.1.1 every 2 s, amber dots) | **Included in the first phase** |
| D3 | Card row | **Jitter replaces "Last tested"**; the test time moves to the Last test card (closes T02) |
| D4 | Recent tests card | **Last 10 tests**, plus average / best / worst |
| D5 | Graph time window | **Fixed 60 seconds**, rolling |

### Defaults I chose (change any of these if you disagree)

- **Which adapters are counted:** Physical Ethernet and Wi-Fi adapters that are up are summed. Loopback, tunnel, VPN and virtual adapters (Hyper-V, VirtualBox, WSL) are skipped. VPN traffic still travels over the physical adapter, so it's counted once, with a small encryption overhead.
- **Units:** The header shows Kbps below 1 Mbps and Mbps above it. The graph's y-axis never drops below 1 Mbps, so near-idle traffic draws as a low line rather than noise filling the whole chart.
- **Latency target:** 1.1.1.1, using the same probe as the speed test (ICMP, falling back to TCP port 443 when ICMP is blocked).
- **Free vs Pro:** Everything on this page is free. This doesn't settle T10.

## Target layout

```
[ Download ][ Upload ][ Ping ][ Internet ][ Jitter ]          ← card row (existing DataCards)
┌──────────── Live traffic · last 60 s ─────────────── ● Live ┐
│ ↓ 2.4 Mbps   ↑ 310 Kbps   ⏱ 18 ms                            │
│  blue ↓ line · green ↑ line · amber latency dots · red = lost│
└──────────────────────────────────────────────────────────────┘
┌──── Last test · 14:32, 06 Nov ─┐┌──── Recent tests (last 10) ───┐
│ ↓ 94 Mbps  ↑ 41 Mbps  27 ms    ││ ▂▄▆▅▇▆▅▆▇█  ↓ / ↑ bar pairs    │
│ Streaming  Great               ││ Avg ↓ 88 · Best 97 · Worst 61 │
│ Gaming     Good                ││                               │
│ Video calls Good · Bufferbloat B││         View history →        │
│ ISP · Server SIN               ││                               │
│ [ Run speed test ]             ││                               │
└────────────────────────────────┘└───────────────────────────────┘
```

- **No tests yet:** The two bottom cards are replaced by the existing welcome card ("Welcome to …" with the Start Speed Test button). The card row and the live graph still show.
- **Narrow windows (below about 700 px):** The two bottom cards stack vertically, and the page scrolls.
- **Rough heights at 96 DPI:** Card row 90, graph 220, bottom cards fill the rest (at least 200).

---

## Phase 1 — Building blocks

Code with no visible change on its own.

### T41 · Traffic monitor service
New `Service/TrafficMonitor.cs`:
- A 1-second timer reads `NetworkInterface.GetIPStatistics()` (`BytesReceived` / `BytesSent`) on the counted adapters (see the defaults above).
- Each tick, it raises `Sample(double downMbps, double upMbps)`, computed as the change in bytes × 8 ÷ the real time elapsed.
- If a counter goes backwards (adapter reset, or Wi-Fi reconnect), that tick is skipped. The adapter list is rebuilt on `NetworkChange.NetworkAddressChanged`.
- `Start()` / `Stop()`. It sends nothing over the network.

### T42 · Live latency sampler
- Expose one public probe on `LatencyProbe`: `ProbeAsync()` returns the ms, or null when the probe is lost. It keeps the existing ICMP → TCP fallback.
- The Dashboard calls it every 2 s, one probe at a time. A lost probe is drawn as a red mark.

### T43 · Rolling mode for `LiveGraphControl`
- **New:** `WindowSeconds` (0 = current behaviour, fill until Freeze; 60 = rolling).
- **New:** `AddTraffic(down, up)` adds to both lines at the same timestamp.
- **Rolling mode behaviour:**
  - Points older than the window are dropped.
  - The x-axis shows the last 60 s, with "now" at the right edge.
  - The y-axis auto-scales to what's on screen.
- The Speed Test page is unchanged: it keeps the default `WindowSeconds = 0`.

## Phase 2 — Dashboard layout

### T44 · Card row: Jitter replaces Last tested (closes T02)
- New card: JITTER, `WaveSquare` icon, amber accent. It shows the last test's jitter, with a short note such as "Good for calls" or "High for gaming" taken from the Quality Score thresholds.
- Ping keeps its "Grade X" bufferbloat badge.

### T45 · Live traffic panel
- **Header:** title "Live traffic · last 60 s", a "● Live" marker, and the current ↓ / ↑ values plus latency in ms.
- **Body:** `LiveGraphControl` in rolling mode, fed by T41 every 1 s and T42 every 2 s.
- **When it runs:** It starts when the page is shown and stops in `Dispose`. Pages are disposed when you navigate away, so it never runs in the background. It also pauses while the main window is minimised.
- **Offline:** The graph keeps drawing (traffic will be about zero), latency points show as lost, and the header shows "Offline".

### T46 · Last test card
- **Shows:**
  - Test time and date (moved here from the old card).
  - ↓ / ↑ / ping.
  - The three Quality Score verdicts with their colours, plus the bufferbloat grade.
  - ISP and server.
- **Reuses** the verdict rows from `SpeedTestInsightsPanel`, moved into `UserControls/Lib` so both pages share them.
- **Buttons:** "Run speed test" opens Speed Test, replacing the welcome card's button.
- **Old tests:** v1 tests (saved before latency was measured) show the numbers only, plus "Run a new test for quality scores".

### T47 · Recent tests card
- A small owner-drawn bar chart: the last 10 tests, oldest on the left, with a ↓ / ↑ bar pair for each.
- **Below the chart:** average, best and worst download.
- **Clicking a bar or the link:** opens Test History.
- **Fewer than 2 tests:** a short "Run more tests to see a trend" note.
- The averages use the same calculation T20 (History charts) will need, so it goes in a shared helper.

### T48 · Welcome card only when empty, plus responsive layout
- Show the welcome card only when the history is empty. Otherwise show the two bottom cards.
- Side by side at 700 px wide or more, stacked below that.

## Phase 3 — Polish and release

- Light and dark theme check for every new part. The data cards already update their colours when the theme changes.
- Check at 100%, 150% and 200% display scaling.
- Add the live latency ping to the privacy statement (T26): it's a background request while the Dashboard is open.
- Check the packaged (MSIX) build: ICMP should work, with the TCP fallback otherwise.

---

## Testing checklist (manual — there's no test project)

| Check | Expected |
|---|---|
| Play a YouTube video or start a large download | The ↓ line rises within 1–2 s, and the header matches Task Manager → Performance → Wi-Fi/Ethernet to within about 10% |
| Leave the PC idle | The line sits near zero and the header shows Kbps |
| Turn on a VPN | Traffic isn't doubled |
| Turn Wi-Fi off and on | No crash or negative values; the graph carries on after reconnecting |
| Block ICMP or go offline | Latency shows lost marks or the TCP fallback, and the header says "Offline" |
| Navigate away, then back | The graph restarts empty; nothing runs while you're on another page |
| Minimise for 1 minute | Sampling paused; the graph resumes on restore |
| Empty history | Welcome card shown; no Last test or Recent tests cards |
| Only v1 (old) history | Last test card shows numbers and the "run a new test" note |
| Window narrower than 700 px | Bottom cards stack; no clipped text |

## Risks

| Risk | Mitigation |
|---|---|
| Adapter counters differ between drivers or include virtual traffic | Count only physical Ethernet and Wi-Fi; compare against Task Manager during testing |
| A ping every 2 s looks like background traffic to privacy-minded users | Only while the Dashboard is visible; say so in the privacy statement |
| Graph repainting every second costs CPU | One `Invalidate` per tick, at most 60 points per line; already double-buffered |

## Out of scope

- Per-app traffic (which program is using the bandwidth). That needs ETW (Windows event tracing) or administrator rights.
- Live traffic in the system tray. Could come after T22–T24 and reuse T41.
- Choosing the window length (decision D5).
