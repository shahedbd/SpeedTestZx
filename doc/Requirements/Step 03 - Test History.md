# Step 3 — Test History

Covers build-order step 5 from the
[Development Plan](Internet%20Speed%20Checker%20Plus%20-%20Development%20Plan.md) —
the first genuinely new tab in this rebuild, not a port from
WiFiPasswordViewer.

## Persistence: `Helper/TestHistoryStore.cs`

Same JSON-in-AppData shape as `WifiProfileDataCache`, but **not a TTL
cache** — an append-only log, matching the dev plan's explicit
distinction. `SpeedTestResult` already had `TestedAt`, so no model changes
were needed.

- `Load()` / `Append(result)` / `Clear()`.
- **Retention: capped at 200 entries**, newest first, oldest trimmed off
  on append. This was one of the dev plan's flagged "open decisions" — went
  with a simple entry-count cap rather than a date-based one since there's
  no pagination in the History table (a day-based cutoff would need one
  eventually; a count cap doesn't). Easy to change the `MaxEntries`
  constant later if 200 turns out wrong in either direction.

## Speed Test hook

`SpeedTestControl.StartTestAsync()` now calls
`TestHistoryStore.Append(_lastResult)` right alongside the existing
in-memory `AppendHistory(_lastResult)` scrollback call, only on a
successful test. One-line change — the persistence and the on-page
scrollback are independent of each other now.

## New tab: `UserControls/Pages/TestHistoryControl.cs`

Full-page table — Date/Time, Download, Upload, Ping, Jitter, Server —
newest first. Built following this project's own established list-page
shape (header + column header + scrollable row panels + status bar), the
same pattern `NetworkInfoDetailsControl` and WiFiPasswordViewer's
`WifiProfilesControl` already use, rather than importing a different
table pattern from elsewhere. Includes:

- A count in the header ("Test History (N)") + a Refresh button (reloads
  from disk, in case the file changed outside this instance).
- An empty state ("No test results yet — Run a Speed Test...") when the
  store is empty.
- No search/filter and no per-row delete — kept deliberately minimal;
  "Clear History" in Settings covers bulk cleanup, and a simple
  chronological log doesn't need filtering yet.

## Settings dialog

Filled in the "Data" section TODO left in step 1 — a "Clear Test History"
button, same slot WiFiPasswordViewer's Settings dialog used for "Clear
WiFi Profile Cache". Destructive, so it's gated behind
`AlertService.Confirm(...)` first (same pattern as WiFiPasswordViewer's
"Forget Network" action), then shows a success toast via
`NotificationManager` on completion.

## AppConfig

- Added "Test History" to `NavItems` (`IconChar.ClockRotateLeft`),
  positioned right after Speed Test.
- Extended `AboutFeatures`.
- Cleaned up the now-stale nav-item comment (Test History/Network
  Info/Network Tools are done; only Dashboard and Export remain per the
  build order).

## Next

Step 6 per the dev plan: **Dashboard**, now unblocked since Test History
exists to feed its "last test" summary tiles. Will confirm before
starting.
