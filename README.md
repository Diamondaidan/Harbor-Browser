# Harbor Browser 0.3.1

Harbor is a compact Windows desktop browser shell built with WPF, .NET Framework 4.8, and Microsoft Edge WebView2.

## 0.3.1 highlights

- Show a visible download notification when a download starts, with live byte and percentage progress and clear completion or interruption messages.
- Use the server's Content-Disposition filename when available, fall back to the URL filename, and reserve paths across active downloads to prevent same-name collisions.
- Add cancel and resumable-download actions to the Downloads menu. Interrupted downloads now show WebView2's interruption reason.
- Ask before closing Harbor while downloads are active, since closing the browser can interrupt them.
- Smooth Settings mouse-wheel scrolling with eased movement while preserving normal scrollbar dragging and touch panning.
- Explain Developer Tools in Settings, including how to open them with F12 or Ctrl+Shift+I.
- Preserve the 0.3.0 modular architecture, menu cleanup, Settings layout, installer identity, and profile schema.

## Existing browser features

- Tabs sit above the address/search bar, with a compact + button immediately after the rightmost tab.
- A star next to the address box toggles the current page bookmark; right-click bookmark buttons to open, open in a new tab, rename, or remove them.
- Pinned tabs shrink to favicon-only chips. Tab context menus provide pinning, grouping, closing, close-other-tabs, private-tab, session-restore, and sleep commands.
- Tab groups can be created, renamed, removed, and saved with the browsing session.
- Sleeping tabs reduce background activity. Inactive tabs wake automatically when selected.
- History, Downloads, VPN, cookie management, and the Resource Monitor remain accessible from the gear menu.
- WebView2 provides native website context menus. Harbor keeps its own context menus for tabs and bookmarks.
- Privacy features include WebView2 tracking prevention, Harbor's supplemental tracker-host blocking, InPrivate tabs, cookie management, and optional cookie/cache cleanup on exit.

## Upgrade compatibility

Version 0.3.1 preserves installer AppId `{8D9A0F4C-2E3B-4F64-9C12-8B3F1C0E74A1}`, the existing installation directory behavior, and `%LOCALAPPDATA%\HarborBrowser`. Profile schema 5 is unchanged, so no profile migration is needed. Install 0.3.1 directly over the existing Harbor installation; do not uninstall first. The installer backs up the profile before replacing files.

## Build

On Windows, install Visual Studio 2022 Build Tools or Community with .NET desktop build tools and the .NET Framework 4.8 Developer Pack, plus Inno Setup 7. Run `MAKE-SETUP.bat`. The build script verifies Microsoft's signed .NET Framework 4.8 and WebView2 Evergreen x64 standalone prerequisite installers before embedding them.

The finished installer is written to `dist\HarborBrowserSetup-0.3.1.exe`. Previously downloaded, verified installers can be copied into `Installer\redist` to avoid downloading them again:

- `ndp48-x86-x64-allos-enu.exe`
- `MicrosoftEdgeWebView2RuntimeInstallerX64.exe`

## Roadmap

The 0.3.x series will continue separating feature ownership and lifecycle from the main window. The initial extraction establishes the pattern for future tab-management, privacy, bookmark/history, download, VPN, and resource-monitor modules.
