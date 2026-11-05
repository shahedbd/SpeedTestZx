# Test Net Speed — Improvement Task Tracker

Plan: [Development_plan.md](Development_plan.md)

**Status:** To Do · In Progress · Done · Blocked · Dropped  
_In Progress = code written, waiting for build and review._
**Priority:** High · Medium · Low
**Type:** New feature · Bug · Improvement · Other

---

## 1. Core features (by side menu item)

### Speed Test

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T05 | 0 | Show ISP name and test location (Cloudflare `/meta`), also on Dashboard | New feature | High | In Progress |
| T11 | 1 | Measure latency under load during download and upload (bufferbloat) | New feature | High | In Progress |
| T12 | 1 | Measure packet loss with ICMP, TCP fallback when blocked | New feature | High | In Progress |
| T13 | 1 | Bufferbloat grade A–F on every result | New feature | High | In Progress |
| T15 | 1 | Quality Score: Streaming / Gaming / Video calls verdicts, with unit tests | New feature | High | In Progress |
| T16 | 1 | Shareable result card: copy / save PNG / Windows Share | New feature | High | In Progress |
| T25 | 2 | Live download / upload graph during the test | New feature | Medium | In Progress |
| T29 | 3 | Streaming resolution estimate (labelled as an estimate) | New feature | Low | In Progress |
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
| T37 | 4 | DNS Race + IPv6, NAT type and MTU checks | New feature | Low | To Do |

### ISP Plan (new menu item, Pro)

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T33 | 4 | ISP Plan Checker: "% of your plan" grade on every test | New feature | Medium | To Do |
| T34 | 4 | 30-day ISP complaint PDF report (Pro) | New feature | Medium | To Do |
| T35 | 4 | Outage / uptime log (24 h free, full history Pro) | New feature | Medium | To Do |

### System tray and header (not a menu item)

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T22 | 2 | Scheduled daily auto-test in the tray | New feature | High | To Do |
| T23 | 2 | Slow-speed and internet-down alerts (once per incident) | New feature | High | To Do |
| T24 | 2 | Tray quick test flyout + keyboard shortcut | New feature | Medium | To Do |
| T32 | 4 | Store add-on / subscription and licensing; "Go Premium" header button | New feature | Medium | To Do |

---

## 2. Bugs and improvements

| ID | Phase | Task | Type | Priority | Status |
|---|---|---|---|---|---|
| T03 | 0 | Drop failed ping samples instead of recording 999 ms (inflates jitter) | Bug | High | To Do |
| T01 | 0 | Move Settings to the bottom of the sidebar (above Our Apps / About) | Improvement | High | To Do |
| T02 | 0 | Add Jitter tile to the Dashboard | Improvement | High | To Do |
| T04 | 0 | Validate accuracy against Cloudflare / Ookla; tune stream count and test length | Improvement | High | To Do |
| T14 | 1 | Show ping at 3 stages: idle / download / upload | Improvement | High | In Progress |
| T17 | 1 | Keep full test under ~25 s; add Quick vs Full option if needed | Improvement | Medium | To Do |
| T18 | 1 | Clear error states: offline, captive portal, VPN / proxy, blocked ICMP | Improvement | Medium | To Do |
| T21 | 2 | Compare 2 tests side by side, tag a test, CSV export | Improvement | Medium | To Do |
| T36 | 4 | Accurate Mode: single vs multi-connection, run 3× and take median | Improvement | Low | To Do |

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
