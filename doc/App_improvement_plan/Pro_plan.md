# Pro Features — Development Plan

Parent plan: [Development_plan.md](Development_plan.md) · Tasks: [Task_tracker.md](Task_tracker.md) · Related: [Dashboard_plan.md](Dashboard_plan.md)

## Goal

Launch Pro with two new menu items that work as a pair:

1. **ISP Report Card** (headline): "Am I getting what I pay for?", with a PDF report you can send to your ISP.
2. **Connection Monitor**: "When is my internet bad?" Automatic tests, a latency and outage log, a time-of-day heatmap, and alerts.

The Monitor gathers the evidence and the Report Card turns it into proof, so they're sold as one Pro bundle.

3. **Advanced Test Lab** comes in a later update. It's outlined at the end of this doc and not scheduled yet.

### Not in scope (other apps in your catalogue cover these)
Who's On My Wi-Fi, saved Wi-Fi passwords, a taskbar speed meter, and per-app bandwidth.

---

## Free vs Pro (proposal for T10)

| Area | Free | Pro |
|---|---|---|
| Speed test, quality verdicts, share card, Dashboard | Everything, as today | — |
| **ISP Report Card** | Plan entry; "% of plan" on every test; last 7 days summary | 30 / 90-day and custom ranges; PDF complaint report; cost view ("$ of undelivered speed"); worst-hours breakdown |
| **Connection Monitor** | 1 automatic test a day; outages from the last 24 h; basic "internet down" alert | Automatic tests every 1, 3 or 6 h; continuous latency and loss log; heatmap; full outage history (90 days); smart alerts |
| Test History | Full history, as today | — |

The rule: **free users see their own problem, Pro gives them proof and automation.** Every Pro page shows a real preview built from the user's own data, never a blank lock screen.

---

## Menu placement

```
Dashboard
Speed Test
Test History
Network Info
Network Tools
ISP Report Card      ★ Pro      ← new (replaces the planned "ISP Plan")
Connection Monitor   ★ Pro      ← new
─────────────
Settings
Our Apps
About
```

- Nav items show a small "PRO" pill until the user unlocks Pro. The pages still open; Pro-only parts show an inline upsell card.
- "Go Premium" button in the header (T32), like Net Speed Meter Plus.

---

## Phase 0 — Pro foundations (prerequisites)

| ID | Task | Notes |
|---|---|---|
| T08 | Entitlement stub + "PRO" badge helper | `Helper/Entitlements.cs`: `IsPro` (a debug override in Settings for testing). `ProGate.Wrap(control)` shows an upsell overlay when not Pro. |
| T50 | Pro upsell card pattern | One reusable card: what you get, a preview from the user's data, and an "Unlock Pro" button. Used on every Pro page. |
| T49 | JSON storage for Pro data (no SQLite — decision 2) | See **Storage design** below: lift the 200-test cap, safe writes for `TestHistory.json`, daily JSON Lines files for the per-minute log, and a small outages file. |
| T06 | Background test runner | Already in the tracker. The Monitor runs tests when the Speed Test page isn't open, so the runner must leave the page. |
| T10 | Confirm the free vs Pro table above | Decision only. |
| T32 | Store add-on and licensing | **Durable add-on, one-time US$9.99** (decision 1). `Windows.Services.Store.StoreContext`. Replaces the stub. Cache the licence so Pro works offline. Can be built in parallel with Phases 1–2 and must land before release. |

### Storage design (T49) — JSON files, no database

Everything stays in the app's data folder as JSON, like today. Three kinds of data, each stored the way it's written:

| Data | File | How it's written | Size |
|---|---|---|---|
| **Test results** | `TestHistory.json` (existing) | Whole file rewritten after each test, as today | About 8 tests/day at the 3 h Pro default → under 3,000 a year, about 2 MB |
| **Per-minute latency and loss** | `Monitor/probes/2026-11-06.jsonl` — one file per day | **Appended**, one line per minute, never rewritten | About 150 KB/day → about 13 MB for 90 days |
| **Outages** | `Monitor/outages.json` | Whole file rewritten when an outage starts or ends | A few KB |

**Changes to the existing history store (`TestHistoryStore`):**
- **Remove the 200-test cap.** At 8 tests a day, 200 tests is only 25 days, which would break the 30 / 90-day reports. Test results are kept forever (decision 5).
- **Safe writes.** Today the file is overwritten in place, and a file that fails to load is treated as empty, so the next test would replace the whole history with one entry. Instead:
  - write to a temporary file, then swap it in (`File.Replace`), keeping `TestHistory.bak.json` as the previous copy;
  - if the file can't be read, load the backup; if that also fails, rename the bad file to `TestHistory.corrupt-<date>.json` and never overwrite it.
- **Compact JSON** (no indentation) to keep the file small.
- **Thread safety:** a lock around load and save, since scheduled tests save from the Monitor while the window may be open.

**Why daily JSON Lines files for the log:** appending one line a minute is cheap and crash-safe (a half-written last line is just skipped on read). Retention is deleting whole files: on startup, files older than 90 days (Pro) or 1 day (free) are removed.

**Reading for the heatmap:** the page reads only the days it shows. A small `Monitor/hourly.json` cache (median ↓ and ping per hour) is updated once an hour so the 90-day heatmap opens instantly without parsing 90 files.

**Test History page:** the table has no paging today, which was fine for 200 rows. With thousands, it needs virtual mode or paging. This is added to T21.

---

## Phase 1 — ISP Report Card

### What the page shows

```
┌ Your plan ─────────────────────────────────────────────┐
│ ISP: Business Zone Ltd   Plan: 100 / 50 Mbps   $25/mo  │  [Edit]
└────────────────────────────────────────────────────────┘
[ 61% of plan ] [ 38% of tests below 50% ] [ Worst: 8–11 pm ] [ Uptime 99.2% ]   ← DataCards
┌ % of plan over time (30 days) ─────────────────────────┐
│ daily median ↓ as % of plan, with a 100% line          │
└────────────────────────────────────────────────────────┘
┌ By hour of day ───────────┐┌ Report ───────────────────┐
│ bar per hour: median % of ││ Range: [30 days ▾]        │
│ plan (from Monitor data)  ││ [ Export PDF report ]  ★  │
└───────────────────────────┘└───────────────────────────┘
```

### Tasks

| ID | Task | Free / Pro | Notes |
|---|---|---|---|
| T33 | Plan checker | Free | Plan entry: ISP, download and upload Mbps, monthly price, currency. The ISP name is pre-filled from the last test. Every test gets `PctOfPlan` (download ÷ plan download). It's shown on the Speed Test result, the Dashboard Last test card and the history rows. |
| T51 | Report Card page | Free (7 days) · Pro (30 / 90 / custom) | Summary cards, the "% of plan over time" chart, and the "by hour of day" chart. Uses `HistoryStats` (from the Dashboard work), extended with medians and percentiles. |
| T52 | Cost view | Pro | "You paid $25 for 100 Mbps and got a median of 61 Mbps, about $9.75 of undelivered speed this month." Clearly labelled as an estimate. |
| T34 | PDF complaint report | Pro | See below. |

### Grading rules
- **% of plan** = that test's download ÷ the plan's download. Upload is graded separately.
- **Summary figures** use the **median**, not the average, so one bad test can't swing the result. Also shown: the 10th percentile ("worst 10% of tests") and the share of tests below 50% and 80% of plan.
- **Wi-Fi tests:** a slow Wi-Fi link isn't the ISP's fault. Each test records its adapter type (Ethernet or Wi-Fi), and the report can be limited to wired tests only. The page suggests this when both kinds exist.

### PDF report contents (T34)
1. **Cover:** ISP, plan, date range, the user's name or address if they enter one (optional, stored only on this PC).
2. **Summary:** median and 10th-percentile download and upload as % of plan, number of tests, share below 50% and 80%, uptime percentage and total outage time (from the Monitor).
3. **Charts:** % of plan over time, and by hour of day.
4. **Outage table:** start, end, duration, and cause ("Router unreachable" or "ISP / internet").
5. **Test table:** every test with time, ↓, ↑, ping, server, adapter type.
6. **Method note:** test server (Cloudflare), how speed is measured, and that wired tests were preferred.

**Library:** PDFsharp 6 (MIT licence, .NET 8, no native code). The charts are drawn with the same GDI+ code as on screen, rendered to a bitmap and embedded. This avoids QuestPDF's revenue-based licence and a WebView dependency.

---

## Phase 2 — Connection Monitor

### How it runs
- **Engine:** `Service/MonitorService.cs` runs in the app process, which stays alive in the tray when the window is closed (the tray already exists).
- **Start with Windows:** an MSIX `StartupTask`. It's never switched on silently: when the user turns on scheduled tests, a prompt offers it ("Start with Windows so tests run even after a restart?"). It can be changed later in Settings (decision 4).
- **No Windows service** and no admin rights. If the app isn't running, nothing is recorded, and the page says so ("Not recorded: PC off or app closed").

### Three data streams

| Stream | How | Frequency | Data use |
|---|---|---|---|
| **Scheduled tests** | A *light* speed test: the same method, but capped at about 4 s and 25 MB per direction | Free: daily · Pro: every 1, 3 or 6 h, **default 3 h** (decision 3) | About 50 MB per test: about 400 MB/day at 3 h, 1.2 GB/day at hourly. Shown in Settings. |
| **Latency and loss log** | `LatencyProbe` to 1.1.1.1 **and** the router (default gateway) | One probe every 10 s, stored as per-minute aggregates (median, max, loss %) | Negligible |
| **Outage detection** | Derived from the log: 3 lost probes in a row to 1.1.1.1 starts an outage; 2 successes end it | Live | — |

**Outage cause:** if the router doesn't answer either, the cause is "Router / Wi-Fi". If the router answers but the internet doesn't, it's "ISP / internet". This is what makes a report credible.

**Data safety:**
- Skip scheduled tests on **metered connections** (`NetworkCostType` via WinRT) and on battery saver, unless the user allows it.
- Skip a scheduled test if a manual test or a large download (from the Dashboard's traffic monitor) is running, and retry 10 minutes later.

### What the page shows

```
[ Uptime 24 h 99.8% ] [ Outages 7 d: 3 · 47 min ] [ Median ping 18 ms ] [ Next test 14:00 ]
┌ Heatmap: weekday × hour, colour = median ↓ % of plan (or ping) ──── [Speed ▾] ┐
└───────────────────────────────────────────────────────────────────────────────┘
┌ Latency & loss, last 24 h ───────────────────────────────────────────────────┐
│ per-minute median ping line, loss as red ticks, outages as shaded bands      │
└───────────────────────────────────────────────────────────────────────────────┘
┌ Outages ─────────────────────────────┐┌ Schedule & alerts ──────────────────┐
│ start · end · duration · cause       ││ Test every [3 h ▾]  ☐ on metered    │
│                                      ││ Alerts: ☑ down ☑ slow (<50% plan)   │
└──────────────────────────────────────┘└─────────────────────────────────────┘
```

### Tasks

| ID | Task | Free / Pro | Notes |
|---|---|---|---|
| T53 | Monitor engine and scheduler | Free (daily) · Pro (1 / 3 / 6 h) | Light test mode in `SpeedTestService`, the metered / battery / busy checks, and a catch-up run after the PC wakes. Builds on T22 (daily auto-test). |
| T54 | Latency and loss log | Pro | Dual-target probe every 10 s, one line per minute appended to the day's `.jsonl` file, 90-day retention (free keeps 24 h). |
| T35 | Outage log | Free (24 h) · Pro (90 days) | Moved here from the ISP Plan table. Detection and cause as described above. A tray toast when the internet comes back: "Internet was down 6 min (ISP)." |
| T55 | Heatmap | Pro | 7 × 24 grid. Colour scale: % of plan (when a plan is set), otherwise Mbps or ping. Shows "Not enough data" for cells with fewer than 2 samples. |
| T56 | Smart alerts | Free (internet down) · Pro (all) | Down, slow (below X% of plan for N hours), high latency, back online. Once per incident, with quiet hours. Extends T23. |
| T57 | Monitor page | Free / Pro | Layout as above, reusing `DataCardGrid` and `LiveGraphControl`. |
| T58 | Run in tray and start with Windows | Free | Close-to-tray setting, `StartupTask` offered when scheduled tests are turned on, and a tray tooltip showing monitor status. |

---

## Phase 3 — Pro launch

| ID | Task | Notes |
|---|---|---|
| T32 | Store add-on live | Durable add-on, one-time US$9.99, with Partner Center's local price tiers for other markets. Test with a test account. |
| T61 | Store listing, screenshots and privacy | New "What's new", Pro screenshots (report PDF page, heatmap). The privacy statement covers background probes, scheduled tests and that all data stays on this PC (T26). |
| T62 | Upsell touchpoints | "Go Premium" header button; Pro preview cards; one gentle prompt after the 5th test that's below 80% of plan. No nagging. |

---

## Later — Advanced Test Lab (not scheduled)

A Pro menu item for enthusiasts. Most of the code already exists, so it suits a "Pro got better" update.

| ID | Task | Notes |
|---|---|---|
| T36 | Accurate Mode | Single vs multi-connection; run 3× and take the median; selectable test length. |
| T37 | DNS Race + connectivity checks | Race 1.1.1.1, 8.8.8.8, 9.9.9.9 and the ISP's DNS and recommend the fastest; IPv6, NAT type, MTU. |
| T59 | Game & meeting readiness | Latency, jitter and loss to Steam, Xbox Live, PlayStation, Teams, Zoom and Meet endpoints. |
| T60 | Advanced Test Lab page | Menu item and layout. |

---

## Testing checklist (manual)

| Check | Expected |
|---|---|
| Enter a plan, run tests | % of plan on the result, Dashboard and history; median used in summaries |
| Mix Wi-Fi and Ethernet tests | Report offers "wired tests only"; the numbers change accordingly |
| Export a PDF (Pro) | Opens in Edge or Acrobat; charts sharp; tables page-break cleanly; no data from outside the range |
| Free user opens a Pro page | Preview from their own data and an upsell card, no crash, no blank page |
| Unplug the router for 2 min | Outage logged as "Router / Wi-Fi"; toast when back |
| Disconnect the router's WAN cable | Outage logged as "ISP / internet" |
| Metered Wi-Fi with hourly tests | Tests skipped and logged as skipped; latency log continues |
| Sleep the PC overnight | Gap shown as "Not recorded"; one catch-up test after wake, not a burst |
| Close the window with close-to-tray on | Monitor keeps recording; tray tooltip shows status |
| Licence offline | Pro stays unlocked from the cached licence |
| 90 days of per-minute data | Page opens in under 1 s; `Monitor` folder about 15 MB; files older than 90 days deleted on startup |
| Kill the app while a test is saving | History intact on restart (from the file or its backup); never replaced by a single entry |
| More than 200 tests | All kept; Test History still scrolls smoothly |

## Risks

| Risk | Mitigation |
|---|---|
| Hourly tests use about 1.2 GB/day | Light test, metered and battery skips, the usage shown in Settings, 3 h as the Pro default |
| ISPs dispute the report because of Wi-Fi | Record the adapter type; "wired only" option; method note in the PDF |
| The app isn't running, so there are gaps | Start with Windows and close-to-tray; gaps labelled honestly |
| A corrupt or half-written `TestHistory.json` wipes history | Safe writes (temp file + swap), backup copy, corrupt files set aside and never overwritten |
| Heatmap over 90 daily files is slow to read | Hourly cache file; only the days shown are read |
| Store review flags background network use | Scheduled tests off until the user turns them on; explained in the listing and the privacy statement |

## Decisions (agreed)

| # | Decision | Choice |
|---|---|---|
| 1 | Pricing | **One-time unlock, US$9.99** (durable Store add-on) |
| 2 | Storage | **Stay with JSON files** — no SQLite. See the storage design under Phase 0 |
| 3 | Default Pro test interval | **Every 3 hours** (about 400 MB/day); 1 h and 6 h selectable |
| 4 | Start with Windows | **Offered when the user turns on scheduled tests**; never switched on silently |
| 5 | Pro history retention | **90 days of per-minute data; every test result kept** |
