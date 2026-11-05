# Test Net Speed — Improvement Task Tracker

Plan: [Development_plan.md](Development_plan.md)

**Status:** To Do · In Progress · Done · Blocked · Dropped  
_In Progress = code written, waiting for build and review._
**Priority:** High · Medium · Low
**Type:** New feature · Bug · Improvement · Other

---

## 1. Core features (by side menu item)

### Dashboard

Plan: [Dashboard_plan.md](Dashboard_plan.md)

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T41 | 1 | Traffic monitor service: live ↓ / ↑ from network adapter counters | New feature | High | In Progress |
| T42 | 1 | Live latency sampler: one ping to 1.1.1.1 every 2 s | New feature | High | In Progress |
| T43 | 1 | Rolling 60 s mode for the live graph (both lines at once) | Improvement | High | In Progress |
| T44 | 2 | Jitter card replaces "Last tested" (closes T02) | Improvement | High | In Progress |
| T45 | 2 | Live traffic panel: header + rolling graph + latency dots | New feature | High | In Progress |
| T46 | 2 | Last test card: time, numbers, quality verdicts, Run test button | New feature | High | In Progress |
| T47 | 2 | Recent tests card: last 10 tests chart, avg / best / worst | New feature | Medium | In Progress |
| T48 | 2 | Welcome card only when history is empty; stack below 700 px | Improvement | Medium | In Progress |

### Speed Test

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T05 | 0 | Show ISP name and test location (Cloudflare `/meta`), also on Dashboard | New feature | High | Done |
| T11 | 1 | Measure latency under load during download and upload (bufferbloat) | New feature | High | Done |
| T12 | 1 | Measure packet loss with ICMP, TCP fallback when blocked | New feature | High | Done |
| T13 | 1 | Bufferbloat grade A–F on every result | New feature | High | Done |
| T15 | 1 | Quality Score: Streaming / Gaming / Video calls verdicts, with unit tests | New feature | High | In Progress |
| T16 | 1 | Shareable result card: copy / save PNG / Windows Share | New feature | High | In Progress |
| T25 | 2 | Live download / upload graph during the test | New feature | Medium | Done |
| T29 | 3 | Streaming resolution estimate (labelled as an estimate) | New feature | Low | Done |
| T31 | 3 | Manual server picker (only if a second backend is added) | New feature | Low | Blocked |

### Test History

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T20 | 2 | History charts: 7 / 30-day trends, avg / best / worst, stability % | New feature | High | To Do |

### Network Info

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T28 | 3 | Wi-Fi vs Ethernet insight with slow-test tips | New feature | Medium | To Do |

### Network Tools

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T27 | 3 | Gaming & Meeting Ping Board (ping, jitter, loss per target) | New feature | Medium | To Do |

### Pro foundations and launch

Plan: [Pro_plan.md](Pro_plan.md). Phases in these Pro tables are the Pro plan's phases (P0–P3).

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T49 | P0 | JSON storage for Pro: lift 200-test cap, daily .jsonl probe logs, outages file (safe writes done in T63) | Other | High | To Do |
| T50 | P0 | Pro upsell card pattern: preview from the user's own data + Unlock button | New feature | High | To Do |
| T61 | P3 | Store listing, Pro screenshots, privacy statement for background probes | Other | High | To Do |
| T62 | P3 | Upsell touchpoints: header button, preview cards, one gentle prompt | Improvement | Medium | To Do |

_Also needed for Pro: T06 (background runner), T08 (entitlement stub), T10 (free vs Pro line), T32 (Store add-on)._

### ISP Report Card (new menu item, Pro headline)

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T33 | P1 | Plan checker: plan entry + "% of plan" on every test (free) | New feature | High | To Do |
| T51 | P1 | Report Card page: summary cards, % of plan over time, by hour (7 days free, 30 / 90 / custom Pro) | New feature | High | To Do |
| T52 | P1 | Cost view: "$ of undelivered speed" estimate (Pro) | New feature | Medium | To Do |
| T34 | P1 | PDF complaint report with charts, outages and test table (Pro, PDFsharp) | New feature | High | To Do |

### Connection Monitor (new menu item, Pro)

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T53 | P2 | Monitor engine + scheduler: light test, daily free / 1-3-6 h Pro, skip on metered / battery / busy | New feature | High | To Do |
| T54 | P2 | Latency and loss log: router + 1.1.1.1 every 10 s, per-minute rows, 90 days Pro | New feature | High | To Do |
| T35 | P2 | Outage log with cause (router vs ISP); 24 h free, 90 days Pro | New feature | High | To Do |
| T55 | P2 | Weekday × hour heatmap (Pro) | New feature | Medium | To Do |
| T56 | P2 | Smart alerts: down (free); slow, high latency, back online (Pro) | New feature | Medium | To Do |
| T57 | P2 | Connection Monitor page | New feature | High | To Do |
| T58 | P2 | Close to tray + start with Windows (StartupTask, offered when scheduled tests are turned on) | New feature | Medium | To Do |

### Advanced Test Lab (new menu item, Pro, later)

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T36 | Later | Accurate Mode: single vs multi-connection, run 3× and take median | Improvement | Low | To Do |
| T37 | Later | DNS Race + IPv6, NAT type and MTU checks | New feature | Low | To Do |
| T59 | Later | Game & meeting readiness: latency / jitter / loss to Steam, Xbox, Teams, Zoom | New feature | Low | To Do |
| T60 | Later | Advanced Test Lab page | New feature | Low | To Do |

### System tray and header (not a menu item)

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T22 | 2 | Scheduled daily auto-test in the tray | New feature | High | To Do |
| T23 | 2 | Slow-speed and internet-down alerts (once per incident) | New feature | High | To Do |
| T24 | 2 | Tray quick test flyout + keyboard shortcut | New feature | Medium | To Do |
| T32 | 4 | Store add-on (one-time US$9.99) and licensing; "Go Premium" header button | New feature | Medium | To Do |

---

## 2. Bugs and improvements

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T03 | 0 | Drop failed ping samples instead of recording 999 ms (inflates jitter) | Bug | High | To Do |
| T39 | 0 | Download / upload averaged per-stream rates instead of summing them (≈ half real speed); fast lines ran out of data | Bug | High | In Progress |
| T63 | 0 | History could be wiped: crash mid-save left a broken file, which loaded as empty and was overwritten | Bug | High | In Progress |
| T01 | 0 | Move Settings to the bottom of the sidebar (above Our Apps / About) | Improvement | High | In Progress |
| T02 | 0 | Add Jitter tile to the Dashboard (done by T44) | Improvement | High | In Progress |
| T40 | 0 | Dashboard data cards and larger sidebar icons, following NetPulseApp | Improvement | Medium | In Progress |
| T04 | 0 | Validate accuracy against Cloudflare / Ookla; tune stream count and test length | Improvement | High | To Do |
| T14 | 1 | Show ping at 3 stages: idle / download / upload | Improvement | High | Done |
| T17 | 1 | Keep full test under ~25 s; add Quick vs Full option if needed | Improvement | Medium | To Do |
| T18 | 1 | Clear error states: offline, captive portal, VPN / proxy, blocked ICMP | Improvement | Medium | To Do |
| T21 | 2 | Compare 2 tests side by side, tag a test, CSV export; paging for thousands of rows | Improvement | Medium | To Do |

---

## 3. Others

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T06 | 0 | Move test runner out of the Speed Test page into a background service | Other | High | To Do |
| T07 | 0 | Add a version number to the saved history format and new optional fields | Other | High | To Do |
| T08 | 0 | Add a Pro placeholder flag and "Coming in Pro" badge helper | Other | Medium | To Do |
| T09 | 0 | Add local counters for success measures (shares, scheduled tests, return visits) | Other | Medium | To Do |
| T10 | 0 | Decide the free vs Pro line for every feature | Other | Medium | To Do |
| T19 | 1 | New Store screenshots, keywords and "What's new" text | Other | High | To Do |
| T26 | 2 | Update the privacy statement for background requests | Other | Medium | To Do |
| T30 | 3 | Evaluate a second test backend (M-Lab NDT7 / LibreSpeed) | Other | Low | To Do |
| T38 | — | Remove the leftover "Who's On My Wi-Fi" line from the draft | Other | Low | To Do |
