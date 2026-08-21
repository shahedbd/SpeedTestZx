# Internet Speed Checker Plus — Development Plan

Rebuild of the existing 12k-customer Store app (old source:
`test_code/NetSpeedMeterPlus/`) on `DesktopAppTemplate`'s sidebar+tabs
shell, reusing as much of `WiFiPasswordViewer`'s already-built code as
directly applies.

## Base

- Template: `DesktopAppTemplate` (sidebar + content panel shell,
  `AppConfig.cs` single-source config, `ThemeManager`/`NavigationManager`
  singletons, DPI-aware controls).
- Reference implementation: `WiFiPasswordViewer` — same shell, same
  patterns, several tabs port near-directly.

## Sidebar structure

| # | Tab | Old app equivalent |
|---|---|---|
| 1 | Dashboard | (new — last-test summary tiles) |
| 2 | Speed Test | "Test Internet Speed" |
| 3 | Test History | "History" |
| 4 | Network Info | "IP Info" + "NIC Setup" |
| 5 | Network Tools | *(new)* |
| 6 | Export | *(new)* |
| — | Settings (header button → dialog) | "basic settings" |
| — | About (modal) | "About" |

## Per-tab scope

- **Dashboard** — stat tiles: last test's download/upload/ping, connection
  status, ISP name, last-tested timestamp. Mirror `WifiHeaderControl`'s
  tile row layout.
- **Speed Test** — port directly (see below).
- **Test History** — table of past results (date, down, up, ping, jitter,
  server). New persistence layer, same *shape* as `WifiProfileDataCache`
  (JSON in `%LocalAppData%`, not TTL-cached — append-only log instead).
- **Network Info** — adapter name, local/public IP, gateway, DNS, MAC,
  link speed. Port `WifiNetworkDetailsControl` directly, drop the WiFi-only
  fields (nothing here is WiFi-specific).
- **Network Tools** — Ping / DNS Lookup / IP Lookup / Traceroute. Port
  directly, zero WiFi coupling.
- **Export** — history → CSV/JSON. Adapt `ExportControl`'s shape; simpler
  than the WiFi version since there's only one data set to export (no
  password/network-details toggle).

## Reusing WiFiPasswordViewer code

| Source (WiFiPasswordViewer) | Target | Change needed |
|---|---|---|
| `Service/SpeedTestService.cs` | `Service/SpeedTestService.cs` | None — copy as-is |
| `Helper/SpeedTestUIHelper.cs` | `Helper/SpeedTestUIHelper.cs` | None — copy as-is |
| `UserControls/Pages/Wifi/SpeedTestControl.cs` | `UserControls/Pages/SpeedTestControl.cs` | Drop `WifiProfileDataCache`/history-log references specific to that build; feed results into the new Test History store instead |
| `UserControls/Pages/Wifi/NetworkDetailsControl.cs` + `WifiNetworkDetailsControl.cs` | `UserControls/Pages/NetworkInfoControl.cs` | Rename, drop nothing (all fields are generic network info already) |
| `UserControls/Pages/Wifi/NetworkToolsControl.cs` | `UserControls/Pages/NetworkToolsControl.cs` | None — copy as-is |
| `Service/NetworkToolsService.cs` + `Helper/InputValidator.cs` | same | None — copy as-is |
| `Service/NetworkDetailsService.cs` | same | None — copy as-is |
| `Forms/AlertDialog.cs` + `Managers/AlertService.cs` | same | None — copy as-is |
| `Managers/NotificationManager.cs` (toast) | same | None — copy as-is |
| `Forms/SettingsDialog.cs` | same | Swap WiFi-cache-clear action for history-clear/export actions |
| `Forms/AboutForm.cs` + `Helper/HeaderButtonService.cs` | same | Re-point `AppConfig` fields (name, description, features list, brand color from *this* app's icon) |
| `Managers/ThemeManager.cs` + dark-mode/tray persistence pattern | same | None — copy as-is, same `SettingsService`/`AppSettingsVm` shape |
| `Service/ExportService.cs` (pattern, not the file) | `Service/ExportService.cs` | Rewrite for history rows instead of WiFi profiles — same CSV/TXT/JSON approach |

Net effect: Speed Test, Network Info, Network Tools, Settings, About, and
the whole theming/DPI/tray/toast infrastructure are largely **copy-paste
with renames** — the only genuinely new build is **Test History**
(+ its Export variant) and the **Dashboard**.

## Reusing NetSpeedMeterPlus (old app)

Keep: whatever IP-geolocation/ISP-lookup logic already works in
`Data/Model/IPGeolocationViewModel.cs` and `Services/NetworkInterfaceService.cs`
/`NICService.cs` — don't re-derive that from scratch, port the working
lookup logic into the new `NetworkInfoControl`. Rebuild everything UI-side
on the template rather than porting old WinForms layout code.

## New work: Test History persistence

- Model: `SpeedTestResult` (already exists) + `TestedAt` — append each
  completed run to a JSON list, same read/write shape as
  `WifiProfileDataCache` minus the TTL (history is a log, not a cache).
- History tab: sortable table + a "Clear History" action in Settings.
- Consider a retention cap (e.g. last N results or last N days) so the
  file doesn't grow unbounded — old app's `TextFileDB.cs`/`ReportDataLIB`
  may already have a policy worth matching.

## Build order

1. Bootstrap project from `DesktopAppTemplate`, wire `AppConfig` (name,
   icon, brand color sampled from the app icon, nav items).
2. Speed Test (direct port) — validates the shell end-to-end fastest.
3. Network Info + Network Tools (direct ports).
4. Settings + About (direct ports, re-pointed content).
5. Test History (new) — unblocks Dashboard and Export.
6. Dashboard (depends on History existing).
7. Export (depends on History existing).

One tab per approval, same as the WiFiPasswordViewer build.

## Open decisions before starting

- Confirm Network Tools is wanted (flagged earlier as the one feature
  beyond old-app parity).
- History retention policy (unlimited vs. capped).
- Whether IP geolocation/ISP lookup is worth porting from
  `NetSpeedMeterPlus` now or deferring to a later pass.
