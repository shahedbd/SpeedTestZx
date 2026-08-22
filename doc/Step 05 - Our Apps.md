# Step 5 — Our Apps

Not part of the original build order — a cross-promo tab added on request,
listing the developer's other Store apps.

## Name

**"Our Apps"** — considered "Other Apps" and "Try Our Apps" too, but "Our
Apps" reads friendliest as a one-line sidebar label and doesn't imply a
sales pitch the way "Try..." does.

## Placement

Added last in `AppConfig.NavItems` (`Helper/AppConfig.cs`), after About —
sits at the very bottom of the sidebar, as requested, separated from the
app's actual functionality above it.

## Data — `AppConfig.OurApps`

A `List<OurApp>` (`record OurApp(Name, Description, Icon, IconColor,
StoreLink, Acquisitions, IsPaid)`), sourced from `doc/MS App Product
Identity.md` — 17 entries, excluding this app's own listing (Internet
Speed Checker Plus). Each app's `StoreLink` is its
`ms-windows-store://pdp/?productid=...` protocol link straight from that
doc.

**Ordered `IsPaid` first, then by `Acquisitions`** (lifetime installs,
descending) within each group — paid apps get first billing, then most
proven apps lead the free ones. `Acquisitions` also drives the
install-count badge on each card ("1M+ installs", "27K+ installs", etc. —
see `FormatInstallCount`). WiFi Password Viewer and Invoice Gen aren't on
the developer's tracking sheet, so their `Acquisitions` is `0`; they sort
last and skip the badge entirely rather than showing "0".

**Descriptions are inferred one-liners**, not pulled from the real Store
listings (no live web access when this was built) — worth a pass to
tighten these against the actual listing copy before shipping.

**Worth a second look:** the list includes *Net Speed Meter*, *Net Speed
Meter Plus*, and *CPU ZX* / *CPU ZX Pro* — near-duplicate pairs (older vs.
newer versions of the same product) and, in the Net Speed Meter case, the
same category as this app itself. Cross-promoting a predecessor product
from inside its replacement is an unusual choice — worth confirming
that's intended rather than trimming the list.

## Page — `UserControls/Pages/OurAppsControl.cs`

Same list-page shape as `TestHistoryControl` (header + scrollable row
list, alternating row shading, hover highlight) but each row is a richer
"card": a colored icon circle standing in for the app's logo (no real
logo art available locally — reuses the colored-circle language already
established by `StatTileControl` and the sidebar's icon colors), name,
description, and a **"Get from Microsoft Store"** button carrying
`AppConfig.MsStoreIconPath` as its `Button.Image`.

**Bug fixed:** the action column (badge + button) was rendering as a
sliver on every row. Root cause — `RenderRows()`'s 17-iteration loop
added rows to `_listContainer.Controls` without `SuspendLayout()` /
`ResumeLayout()`, unlike `TestHistoryControl`'s equivalent loop, which
has always had it. Added the missing Suspend/Resume pair, and while
rebuilding this section also set `UseVisualStyleBackColor = false` on the
button (WinForms can otherwise ignore an explicit `BackColor` on some
flat-styled buttons depending on the active visual style).

**Button click** → `Process.Start(new ProcessStartInfo { FileName =
app.StoreLink, UseShellExecute = true })`, same pattern `AboutForm` already
uses for its website/email links — `UseShellExecute = true` is what lets
Windows hand the `ms-windows-store://` URI to the Store app's protocol
handler.

**Icon** — `AppConfig.MsStoreIconPath` (`Resources/ms_store_48x48.ico`)
loaded once via `DpiAwareService.ScaleIcon` and cached in a static field
(`GetStoreIcon()`), reused across all 17 button instances rather than
re-reading the file per row.

## Not touched

`DeviceData/StartupService.cs`'s silent background Store-page opens (on
first install, and every 15 days) are a separate, pre-existing mechanism —
unrelated to this visible gallery tab, left as-is.
