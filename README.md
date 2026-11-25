# Harbor Browser 0.3.6

## History scrollbar

History's vertical scrollbar now stays on the right in both light and dark mode. The cog's menus use the same right-side scrolling convention while retaining their existing left-opening flyouts.

## Small-screen layout

Harbor now fits its starting window inside the current monitor's usable area, leaving room for the Windows taskbar. Settings, history, cookie management, resource monitoring and text prompts also fit to the available space. Window fitting runs again when Windows reports a DPI, display or work-area change.

The toolbar adapts as the available width decreases:
- Below 900 logical pixels, the search-engine selector and Go button hide. Enter still searches or navigates using the selected engine.
- Below 760 logical pixels, the Beacon logo stays visible while the HARBOR wordmark hides.
- Below 650 logical pixels, the Home and bookmark shortcut buttons hide. Ctrl+T opens the configured home page, and bookmarking remains in the cog menu.
- Tab titles shorten on narrow windows. The tab strip keeps its existing overflow scrolling.

Settings content scrolls while Save settings and Cancel stay at the bottom. Long option labels wrap, and cookie/history actions can wrap onto another row. Picture-in-picture placement accounts for DPI and stays within the monitor's work area.

The primary target was 1366 × 768. Tests exercised logical layouts corresponding to 100%, 125%, 150%, 175% and 200% scaling without changing the user's Windows display settings.

## Compact cog menu

The cog menu and its flyouts use Harbor's current light/dark or custom palette throughout. Compact rows replace the default Windows menu styling and unused icon gutters. Long labels truncate, and long menus scroll within a height based on the available screen area.

## Address selection

The first left click into the address bar selects the entire URL. A subsequent click allows normal cursor placement and editing. Keyboard focus, including Ctrl+L, also selects the entire URL. Selected text uses a contrasting foreground color.

## Existing features

This update retains Beacon branding, Harbor Home, independent light/dark color customization, right-side new tabs, tab reordering, cog-based tab groups, corrected download filenames/extensions, video fullscreen, taskbar-aware browser fullscreen and picture in picture.

F11 toggles browser fullscreen. A website's video fullscreen button expands video to the monitor; Escape restores the previous mode. Picture in picture is available beside the cog, under Cog > Tools, or with Ctrl+Shift+P.

## Install and build

Close Harbor, then run dist/HarborBrowserSetup-0.3.6.exe over the existing installation. The installer AppId, profile location and schema 5 remain unchanged. The installer bundles the offline .NET Framework 4.8 and WebView2 x64 prerequisite packages.

No installation, upgrade or uninstall was performed on the user's installed browser during this work.

To build from source, follow SETUP-README.md. Source archives omit build outputs, test profiles and large redistributables; the build script obtains the official prerequisites when needed.

## Verification

See VERIFICATION.md for executed checks and limits. Test runners:
- Tests/Run-Tests.ps1: service, filename, settings, appearance, geometry and offline layout checks.
- HomeSmokeTests/Run-Browser-Tests.ps1: isolated WebView2 home-page checks.
- IntegrationTests/Run-Integration-Tests.ps1: the real app with an isolated profile, compact menus, address selection, small layouts and a generated cross-origin video.

Previews contains rendered interface previews and captured video viewports.
