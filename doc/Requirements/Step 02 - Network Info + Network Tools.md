# Step 2 — Network Info + Network Tools

Covers build-order step 3 from the
[Development Plan](Internet%20Speed%20Checker%20Plus%20-%20Development%20Plan.md).
Both tabs came from WiFiPasswordViewer with zero WiFi-specific coupling —
confirmed before porting (neither ever referenced the WiFi profile model or
services), matching the dev plan's own assessment.

## Network Info (new tab)

Merged and renamed WiFiPasswordViewer's `NetworkDetailsControl.cs` +
`WifiNetworkDetailsControl.cs` → `NetworkInfoControl.cs` +
`NetworkInfoDetailsControl.cs`, flattened out of the `UserControls/Pages/Wifi/`
folder into `UserControls/Pages/` directly. No field or logic changes —
every row (adapter name, connection type, local/public IP, subnet, gateway,
DNS, MAC, link speed, IPv6) was already generic network info, not WiFi
data. Nav label: **"Network Info"**.

## Network Tools (moved, unchanged)

Moved `NetworkToolsControl.cs` out of `Wifi/` into `UserControls/Pages/`
directly — namespace change only, zero content change, matching the dev
plan's "None — copy as-is" assessment. Ping / DNS Lookup / IP Lookup /
Traceroute, unchanged.

The `UserControls/Pages/Wifi/` folder is now empty and has been removed —
every page lives flat under `UserControls/Pages/` going forward, since
nothing in this app is WiFi-specific anymore.

## AppConfig

- Added both to `NavItems`, after Speed Test: Network Info
  (`IconChar.NetworkWired`), Network Tools (`IconChar.Screwdriver`) — same
  icons WiFiPasswordViewer used for the equivalent tabs.
- Extended `AboutFeatures` with both tabs' descriptions.

## Verified

- Grepped both new/moved files' old class names and the old
  `TestNetSpeed.UserControls.Wifi` namespace across the whole project —
  zero remaining code references (only the two intentional "ported from"
  doc-comment mentions).
- Confirmed no other file anywhere referenced `NetworkDetailsControl` or
  `WifiNetworkDetailsControl` before renaming.

## Next

Step 4 per the dev plan: Settings + About re-pointing is effectively
already done (both were re-pointed to `TestNetSpeed`'s `AppConfig` in step
1). What's left before Test History (step 5) is confirming there's nothing
else to port for Settings/About — will move straight to **Test History**
next unless you'd like a closer look at Settings/About first.
