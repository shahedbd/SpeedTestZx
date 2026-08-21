# Step 1 — Bootstrap + Speed Test

Covers build-order steps 1 and 2 from the
[Development Plan](Internet%20Speed%20Checker%20Plus%20-%20Development%20Plan.md):
bootstrapping the copied WiFiPasswordViewer project into Test Net Speed's
own identity, and porting the Speed Test tab as the first real, working
page.

## Bootstrap

The project was already namespace-renamed (`TestNetSpeed` throughout) when
copied — this step wired up `AppConfig.cs`'s actual *content* and removed
everything with no reuse value for a speed-test app.

**`Helper/AppConfig.cs`:**
- Identity: `AppName` → "Test Net Speed", `AppSubtitle` → "Test your
  internet speed, anytime", `AppDescription` rewritten.
- `AppVersion` / `AppReleaseVersion` reset to `1.0.0.0` / `1000` —
  **placeholder only**. This rebuild replaces an existing Store listing;
  set these to the real next Store version before publishing, not left
  at the reset value.
- `AboutFeatures` rewritten to list only what's actually implemented so
  far (just Speed Test + the shared shell features) — extend it as each
  further tab lands, same as the WiFi build's list grew incrementally
  rather than describing unbuilt features.
- `PrimaryColor` — **placeholder**, `#005FD2`. `Resources/180x180.png`
  and `favicon.ico` are still WiFiPasswordViewer's lock icon (carried
  over from the copy) — this color was *not* sampled from a real icon,
  since none exists yet. Once real app art is supplied, replace both
  resource files and re-sample this value (see WiFiPasswordViewer's
  `AppConfig.cs` for the sampling approach) — `TitleBarColor` and
  `HeaderButtonService`'s brand colors both derive from it, so fixing it
  here re-brands the whole app.
- `NavItems` — reduced to just `Speed Test` + `About`. The rest
  (Dashboard, Test History, Network Info, Network Tools, Export) get
  added one at a time as each is built.

**Deleted** (WiFi-specific, zero reuse value per the dev plan's own reuse
table — none of these were listed there):
`Model/WiFiProfile.cs`, `Service/WiFiProfileService.cs`,
`Service/QrCodeService.cs`, `Service/ExportService.cs`,
`Helper/WifiProfileDataCache.cs`,
`UserControls/Pages/Wifi/{QrCodeControl,SavedWifiControl,WifiProfilesControl,WifiControl,WifiHeaderControl,ExportControl}.cs`,
`UserControls/Pages/DashboardControl.cs` (unused template stub — the real
Dashboard is custom-built at step 6, once Test History exists to feed it).

**Fixed** (referenced deleted code): `Forms/SettingsDialog.cs` had a "Clear
WiFi Profile Cache" action in its Data section — removed for now (left a
`TODO` marking the same slot for a future "Clear Test History" action once
the Test History tab exists). Appearance (Dark Mode) and Behavior
(minimize-to-tray) sections are untouched and fully functional.

**Left in place, untouched, not yet wired to nav** (still sitting under
`UserControls/Pages/Wifi/`, awaiting step 3's move+rename per the dev
plan): `NetworkDetailsControl.cs`, `WifiNetworkDetailsControl.cs`,
`NetworkToolsControl.cs`. Verified they have zero references to anything
deleted above, so they're safe to leave as-is until step 3.

## Speed Test (direct port)

Moved `UserControls/Pages/Wifi/SpeedTestControl.cs` →
`UserControls/Pages/SpeedTestControl.cs`, namespace
`TestNetSpeed.UserControls.Wifi` → `TestNetSpeed.UserControls.Pages`. No
other change — confirmed it had zero WiFi-specific coupling, matching the
dev plan's "None — copy as-is" assessment. `Service/SpeedTestService.cs`
and `Helper/SpeedTestUIHelper.cs` needed no changes at all (already
namespace-correct from the project copy).

The in-memory `_historyBox` scrollback inside `SpeedTestControl` is
unchanged for now — hooking completed test results into the persistent
Test History store is step 5's job, not this one.

## Verified

- Grepped the whole project for every deleted type name — no remaining
  references anywhere.
- Grepped the untouched `Wifi/` leftover files specifically — confirmed
  no broken references to anything deleted.
- `.csproj` uses SDK-style default globbing (no explicit `<Compile>`
  items), so no stale file-list entries to clean up.

## Next

Step 3 per the dev plan: port Network Info (merge
`NetworkDetailsControl.cs` + `WifiNetworkDetailsControl.cs`, flatten out
of the `Wifi/` folder, rename) and Network Tools (move only, zero content
change). Will confirm before starting.
