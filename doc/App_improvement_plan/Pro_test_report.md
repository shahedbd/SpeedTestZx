# Pro Features — Pre-release Test Report

Date: 2026-11-06 · Build: Debug, net8.0-windows10.0.19041.0 · Plan: [Pro_plan.md](Pro_plan.md)

Scope: **ISP Report Card** and **Connection Monitor** (Pro_plan Phases 0–2).
Method: a QA harness referencing the real `TestNetSpeed` assembly, with an isolated data folder
(`%LOCALAPPDATA%\TestNetSpeed_QA`, deleted afterwards). Live tests ran on this PC's Wi-Fi.

## Results

| Suite | What it covers | Result |
|---|---|---|
| Logic | Stats, grading, report building, history store (cap, corrupt-file recovery), plan store, licence cache, outage detection, Monitor storage and retention, uptime, heatmap, alerts, scheduler, startup task | **106 / 106 pass** |
| Live network | Full vs light test through the runner, one-test-at-a-time, % of plan and adapter stamping, latency log writes a minute, scheduler runs a test by itself, loops stop when switched off | **25 / 26 pass** (see note 1) |
| PDF export | 90-day, wired-only, no-plan and 7-day reports; readable; page breaks; range filtering; pages inspected visually | **8 / 8 pass** |
| UI smoke | Both pages in Free / Pro, dark / light, narrow window, upgrade prompt; screenshots inspected | **11 / 11 pass** |

Note 1 — "light test within ±40% of full" failed once (light 111 vs full 67.5 Mbps). Six alternating
runs showed no bias (light avg ≈ 56, full ≈ 61 Mbps); this Wi-Fi line swings 17–111 Mbps between runs.
Light tests are noisier by design; the report uses medians. Not a code defect.

Live figures: full test 22 s / ~184 MB; light (scheduled) test 8.5 s / ~61 MB.

## Defects found and fixed

| # | Area | Defect | Fix |
|---|---|---|---|
| 1 | Alerts | "Slow" alert could never fire on the default 3 h schedule (3 h window held only 1 test) | Window = max(slow hours, interval) + 30 min; counts manual tests too |
| 2 | Heatmap | Ping heatmap stayed empty for 2 weeks (needed 2 samples per weekday-hour) | Ping cells need 1 hourly sample; speed keeps 2 tests |
| 3 | Uptime | App restart mid-hour replaced that hour's roll-up, losing recorded minutes | Hourly roll-ups merge |
| 4 | Report | "Worst hours" showed hours with no tests ("6–9 pm" for 8 pm tests) and only 3 h of a longer slump | Trimmed to hours with data, then grown over equally slow neighbours (max 8 h) |
| 5 | Report | Cost view rounded $x.xx5 down (banker's rounding) | Money rounds half away from zero |
| 6 | Report | Wi-Fi tests could be labelled "Ethernet" when a Hyper-V / VPN adapter has a gateway | Adapter = Windows' best route to 1.1.1.1 (`GetBestInterface`) |
| 7 | PDF | Section headings stranded at page bottom, chart/table on next page | Headings kept with the following chart / rows |
| 8 | PDF + page | Last date / time label clipped ("Nov (", "19:1(") | Edge labels aligned inward |
| 9 | Pages | Phantom scrollbar on the Monitor page (stale scroll extent from pre-dock layout) | Scroll extent recomputed after each layout |
| 10 | Pages | Custom-range pickers / "wired only" / heatmap combo could be left unplaced (layout read `Visible`) | Layout uses its own flags |
| 11 | Monitor | Free users' heatmap preview parsed the whole history on the UI thread every minute | Moved to the background load |
| 12 | Monitor | "Monitor latency & outages" lost its "&"; Schedule card cut off "Quiet hours"; white list header and dropdowns in dark theme | `UseMnemonic=false`; taller card; themed header and inputs |
| 13 | Upsell | Upsell description cut off with "…" | Wrapped height measured correctly |

## Release blockers (not code defects — settings)

- `AppConfig.TestAccess` is committed as **`FullAccess`** — this gives Pro away to everyone. Set to `Off`.
- `AppConfig.LifetimeProStoreId` is still the placeholder — add the Partner Center add-on ID.

## Not covered by automation — please check manually on the packaged (MSIX) build

| Check | Expected |
|---|---|
| Buy Pro with a Store test account | Prompt → Store dialog → unlocks without restart; PRO pills disappear |
| Restart offline after buying | Still Pro (14-day cached licence) |
| Turn on automatic tests | "Start with Windows?" asked once; after sign-in the app starts in the tray |
| Unplug the router 2 min / pull the WAN cable | Outage logged as "Router / Wi-Fi" / "ISP / internet"; tray toasts down and back online |
| Metered Wi-Fi, battery saver | Scheduled test skipped with the reason on the Next test card; latency log continues |
| Sleep overnight | One catch-up test after wake; gap hatched as "not recorded" |
| Close with close-to-tray on | Monitor keeps recording; tray tooltip shows ping and next test |
| 125% / 150% / 200% DPI | Pages, charts and prompt scale; nothing clipped |
