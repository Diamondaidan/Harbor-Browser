## 0.3.2

- Replace the separate Windows title bar with window controls in Harbor's existing top row.
- Move the gear menu to the right end of the address bar.
- Retain native caption dragging, double-click maximize/restore, resize borders and Alt+Space system menu.
- Preserve WebView2-suggested filenames and recover missing/generic extensions from known MIME types.
- Recover ZIP, PDF, image, archive and executable extensions from completed file signatures when needed.
- Distinguish common ZIP-based Office, EPUB, JAR and APK formats.
- Clean Windows reserved device names, trailing dots/spaces and long filenames; avoid file and folder collisions.
- Preserve the installer identity, profile location and schema 5.

## 0.3.1

- Add a visible download-start notification and live download progress feedback independent of the status bar.
- Prefer the response Content-Disposition filename, safely sanitize it, and reserve names across in-progress downloads to prevent concurrent path collisions.
- Report completed/interrupted download states and expose cancel/resume actions when WebView2 supports them.
- Warn before closing Harbor while downloads remain active, reducing accidental interruptions during shutdown.
- Add eased mouse-wheel scrolling to Settings and explain Developer Tools, their inspection features, and keyboard shortcuts.
- Preserve the profile schema, installer AppId, installation directory behavior, and existing profile location.

## 0.3.0

- Begin the modular architecture refactor: move browser-tab runtime state and tab-group actions out of `MainWindow` into dedicated view-layer types.
- Extract Harbor's supplemental tracker-host list, WebView2 request-filter registration, and host matching into `TrackerProtectionService`.
- Remove the redundant File, View, and Tabs top-level sections from the gear menu. Existing tab operations remain available through tab-strip/context actions and keyboard shortcuts; view preferences live in Settings.
- Reorder the gear menu so Help appears immediately before Settings, with Settings as the final item.
- Raise the New Tab button by four layout units relative to its prior position so its top edge aligns with the tab headers.
- Rebuild Settings as a resizable window with a vertically scrollable content area and a fixed footer. Cancel and Save settings remain visible at all window heights; Save settings is also the default action.
- Preserve profile schema 5, installer AppId, install directory behavior, and `%LOCALAPPDATA%\HarborBrowser` so this update does not require a data migration.

## 0.2.3

- Removed the sidebar and moved Bookmarks, History, Downloads, and VPN functions into the gear menu.
- Added a direct Settings entry to the gear menu and removed the obsolete View > Sidebar toggle.
- Changed gear-menu submenu flow so flyouts open left and disclosure arrows point left.
- Raised the New Tab button slightly to align it with the tabs.
- Replaced deprecated Inno Setup `x64` identifiers with `x64compatible` and renamed `FileCopy` to `CopyFile`.
- Preserved the existing installer AppId, install directory behavior, profile path, and profile schema 5.

# Changelog

## 0.2.2

- Move the tab strip above the address/search bar and add a compact new-tab button immediately after the rightmost tab.
- Move File, View, Bookmarks, Tabs, Tools, and Help commands into a top-right gear menu.
- Add a star toggle at the right edge of the address bar to add or remove the current page bookmark; remove the old sidebar "Add current page" action.
- Add favicon-only compact pinned tabs.
- Add tab context menus (new, pin/unpin, move to group, close, close other tabs) and context menus for bookmarks, history, downloads, and configured VPN profiles. WebView2 keeps its native page/link/image context menu.
- Add create, rename, and remove operations for tab groups; group names are persisted in session data and survive restart/upgrade.
- Fix the Resource Monitor Close button for its modeless window by explicitly closing the window and stopping its refresh timer.
- Preserve the stable installer AppId and profile location. Upgrade profile schema to 5 with an automatic pre-migration backup to protect custom tab groups from older Harbor builds.

## 0.2.1



- Add a live Resource Monitor with Harbor CPU sampling, Harbor working/private memory, WebView2 process memory, process kinds, tab counts, sleeping/pinned/private counts, and tracker-block count. WebView2 memory is reported by process because renderer processes may be shared across tabs.
- Add pinned tabs, pinned-first ordering, and tab-group filtering for Work, Personal, Research, Shopping, and Ungrouped tabs. Pinned tabs remain visible in each group.
- Add session restore for normal tabs, including URLs, selected tab, pin state, and group assignment. Private tabs are deliberately excluded. Session restore suppresses intermediate saves so a restore cannot overwrite its source session while tabs are being recreated.
- Add real WebView2 InPrivate tabs. Private tabs are not recorded in Harbor history or session restore; the UI warns that downloads and explicitly saved bookmarks may persist.
- Add WebView2 native tracking prevention levels (Off, Basic, Balanced, Strict) plus Harbor's supplemental starter host-block list.
- Use the non-deprecated WebResourceRequested filter overload with document request sources so iframe/document requests are included.
- Add a Cookie Manager to inspect cookie metadata for the current site or profile, delete an individual cookie, or clear all cookies. Cookie values are not displayed.
- Add optional cookie and disk-cache cleanup on exit.
- Preserve the existing installer AppId, previous installation directory behavior, and LocalAppData profile location. The installer creates a pre-upgrade snapshot including session files.
- Upgrade the profile schema to 4 with a pre-migration backup; existing bookmarks, settings, history, tab sessions, and 0.2.0 performance settings remain readable.

## 0.2.0

- Add automatic sleeping for inactive tabs using WebView2's TrySuspendAsync API.
- Add configurable inactive-tab sleep delay (30 seconds, 1, 2, 5, or 10 minutes).
- Resume sleeping tabs when selected and show a sleep marker in the tab title.
- Add a Tools command to put all eligible inactive tabs to sleep immediately.
- Retain lazy initialization so a new tab does not start the WebView2 engine until selected.
- Upgrade the profile schema from 1 to 2 with an automatic pre-migration snapshot. Existing 0.1.1 bookmarks, history, and settings are retained.

## 0.1.1

- Preserve a stable installer identity and reuse the previous installation directory for in-place upgrades.
- Back up settings, bookmarks, history, and recovery files before installing an update.
- Keep the browser profile in LocalAppData, separate from installed program files.
- Add a data-schema marker and pre-migration snapshots.
- Preserve previous-good `.bak` copies when saving JSON data and recover from them if the current file is unreadable.
- Refuse to write a profile marked with a newer data schema.
- Fix WPF/.NET Framework 4.8 build error MC1000 by explicitly referencing System.Web.
- Improve prerequisite acquisition with partial-file handling, package-size checks, and Microsoft signature verification.

## Next: 0.3.2

- Move browser features into optional modules with explicit enable/disable state and a stable module contract.
- Separate tab management, privacy, downloads, bookmarks, VPN, and resource monitoring from the main window.
