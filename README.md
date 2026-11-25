# Harbor Browser 0.3.2

Harbor is a compact Windows desktop browser shell built with WPF, .NET Framework 4.8, and Microsoft Edge WebView2.

## 0.3.2 highlights

- Replace the separate Windows title bar with window controls in Harbor's existing top row.
- Move the gear menu to the right end of the address bar.
- Retain native caption dragging, double-click maximize/restore, resize borders and Alt+Space system menu.
- Preserve WebView2-suggested filenames and recover missing/generic extensions from known MIME types.
- Recover ZIP, PDF, image, archive and executable extensions from completed file signatures when needed.
- Distinguish common ZIP-based Office, EPUB, JAR and APK formats.
- Clean Windows reserved device names, trailing dots/spaces and long filenames; avoid file and folder collisions.
- Preserve the installer identity, profile location and schema 5.

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

Version 0.3.2 preserves installer AppId `{8D9A0F4C-2E3B-4F64-9C12-8B3F1C0E74A1}`, the existing installation directory behavior, and `%LOCALAPPDATA%\HarborBrowser`. Profile schema 5 is unchanged, so no profile migration is needed. Install 0.3.2 directly over the existing Harbor installation; do not uninstall first. The installer backs up the profile before replacing files.

## Build

On Windows, install Visual Studio 2022 Build Tools or Community with .NET desktop build tools and the .NET Framework 4.8 Developer Pack, plus Inno Setup 7. Run `MAKE-SETUP.bat`. The build script verifies Microsoft's signed .NET Framework 4.8 and WebView2 Evergreen x64 standalone prerequisite installers before embedding them.

The finished installer is written to `dist\HarborBrowserSetup-0.3.2.exe`. Previously downloaded, verified installers can be copied into `Installer\redist` to avoid downloading them again:

- `ndp48-x86-x64-allos-enu.exe`
- `MicrosoftEdgeWebView2RuntimeInstallerX64.exe`

## Roadmap

The 0.3.x series will continue separating feature ownership and lifecycle from the main window. The initial extraction establishes the pattern for future tab-management, privacy, bookmark/history, download, VPN, and resource-monitor modules.
