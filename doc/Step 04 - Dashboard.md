# Step 4 — Dashboard

Covers build-order step 6 from the
[Development Plan](Internet%20Speed%20Checker%20Plus%20-%20Development%20Plan.md) —
the app's landing page, unblocked now that Test History exists to feed it.

## New tab: `UserControls/Pages/DashboardControl.cs`

Not a WiFi port — no equivalent existed in WiFiPasswordViewer. Two-row
layout: a fixed-height stat-tile row on top, a fill welcome card below.

**Stat tiles** (`StatTileControl`, reused as-is from WiFiPasswordViewer,
same pattern `WifiHeaderControl` used there):

- **Last Download / Last Upload / Last Ping** — read from
  `TestHistoryStore.Load()`'s first entry (already newest-first). Show
  `—` placeholders when history is empty rather than a zero, so an unused
  app doesn't look broken.
- **Internet Status** — `NetworkDetailsService.GetPublicIpAsync()`;
  "Connected" + the IP on success, "Offline" when it returns its
  `"Unavailable"` sentinel. Same connectivity-check approach
  `NetworkInfoDetailsControl` already uses.
- **Last Tested** — timestamp of the most recent history entry.

**Welcome card** — app name, a one-line description, and a
"▶ Start Speed Test" button that calls
`NavigationManager.Instance.NavigateTo("Speed Test")`, the same
cross-page nav pattern used elsewhere in this app.

**Omitted: ISP name.** The dev plan listed this as a possible Dashboard
tile, but it needs IP-geolocation lookup logic ported from the old
`NetSpeedMeterPlus` app (`test_code/NetSpeedMeterPlus/Data/Model/IPGeolocationViewModel.cs`),
which hasn't been reviewed yet. Left out for now rather than guessing at
an implementation — can be added as a 6th tile later if wanted.

## AppConfig

- Added **"Dashboard"** to `NavItems`, placed **first** (`IconChar.House`)
  so it becomes the landing page — `NavigationManager`'s startup logic
  auto-clicks the first non-modal nav item.
- Extended `AboutFeatures`.

## Next

Step 7 per the dev plan: **Export** — the last tab in the build order.
Will confirm before starting.
