## 0.4.3

- Add persisted, native-enforced site-pattern choices for local modules and a deny-all state that Enable cannot bypass.
- Preserve original requested permissions separately from the active manifest; refuse grants beyond the module's request.
- Keep disabled modules unregistered after access edits until explicit Enable, avoiding the native API default-enabled interval.
- Revalidate managed permission state at startup; disable modules when permission metadata is damaged, with actionable status.
- Show module status in list rows, retain selection after changes, use separate Enable/Disable and Site access controls, and make short dialogs scroll/wrap.
- Explain that access changes reload pages and may reset a module's own preferences.
- Accept bookmark drops into empty bar space; keep the drop surface sized to the window.
- Bring selected overflow tabs into view.
- Add schema 9 snapshots for module access metadata, isolated installer/upgrade/uninstall fixtures, live website checks, and targeted regressions.

## 0.4.2

- Fix the duplicate Pin command by tracking the actual menu item instead of a numeric menu position.
- Load favicons through WebView2 GetFaviconAsync, with navigation/view guards, buffered decoding, Home Beacon and clean fallback handling.
- Give normal tabs equal widths and shrink them as space fills; keep compact pinned tabs, icon-only overflow and an always-visible + button.
- Add bookmark-bar drag ordering, insertion feedback, edge scrolling and Move left/right commands for bookmarks and folders.
- Persist stable bookmark identities and bar order, preserve them through editing/settings, and migrate to profile schema 8 with snapshots.
- Add reviewed local Manifest V3 site modules, native isolated execution/site scope, Home exclusion, normal-profile-only installation, API/minimum-version gates and enable/disable/remove controls.
- Preserve engine PDF/clipboard components, isolate module script failures, and include an opt-in Beacon demonstration module.
- Keep the 0.4.1 Hotfix 1 suggestion-click behavior and existing browsing/privacy/performance controls.

## 0.4.1 Hotfix 1 — build 0.4.1.1

- Resolve address-bar mouse clicks from the pressed/released row, including title, URL, source and padding.
- Preserve address-field focus; explicitly select on mouse-down without requiring focusable suggestion rows.
- Cancel pending online completion on press to avoid replacing the row mid-click.
- Ignore release outside the pressed row and clicks on the list background.
- Replace the earlier preselected-list mouse test with row-targeted mouse routing; add focused online/private/keyboard/Home pointer regressions.
- Preserve the original 0.4.1 artifacts, profile schema and installer identity.
## 0.4.1

- Track inactivity independently per tab instead of restarting every sleep timer on unrelated tab activity.
- Protect active pages, audible/detected playing media, picture in picture, pending permission requests, and active or paused/resumable downloads from sleep.
- Recheck selection, navigation and view identity after asynchronous checks; avoid overlapping suspend attempts and retry busy pages with backoff.
- Reset sleep state/timing after background navigation and preserve in-page state on wake.
- Add moon/dimming indicators and a per-tab Keep awake choice for the current session.
- Add a Resource Monitor Tabs view with status/reasons, switch/sleep/wake/Keep awake actions, stable selection/scrolling during refresh and a compact layout for short windows.
- Add CPU samples for WebView2 processes, monotonic timing, PID-reuse handling and cleanup of stale samples.
- Add repeatable local benchmarks and tests for muted cross-origin playback, real downloads, private transfer cancellation, permission timeout/navigation, and clear-on-exit across reopening a test app.
- Correct release metadata to state schema 7. The profile schema and installer identity stay unchanged from 0.4.0.
## 0.4.0

- Add a site-permission banner with one-time and remembered Allow/Block choices, exact requesting/embedding-site scopes and cancellation on navigation, tab changes, closing and timeout.
- Add an editor for camera, microphone, location and notification rules; reset native cached permissions and reload matching sites when rules change.
- Add time-scoped Clear browsing data for history, cookies/site storage, cache and remembered permissions, separately scoped to normal browsing or the current private session.
- Sanitize removed history/permission data and obsolete recovery copies in Harbor-managed automatic and installer upgrade backups.
- Rotate private-session profile identities, keep private choices in memory, clear private site data/download rows on last-tab closure, and confirm cancellation of unfinished private downloads.
- Include site storage with the clear-cookies-on-exit preference.
- Add an exclusive profile writer lock, an unclean-shutdown journal, a retained recovery checkpoint and explicit restoration controls.
- Recover from renderer failures and recreate WebView2 controls after browser-engine termination.
- Advance profile schema 6 to 7 with a snapshot before migration; preserve earlier features.
- Keep extension permission enforcement and measured memory optimization on the roadmap; they are not completed by this release.

## 0.3.8

- Add a bookmark manager with folder hierarchy, search, editing, ordering and HTML/JSON import/export.
- Show bookmark folders in the bar and cog menu; retain empty folders and support safe folder renaming/removal.
- Add independent editable Home shortcuts, automatic bookmark shortcuts and a 12-shortcut limit.
- Add fixed/built-in Home scenes and validated profile-managed custom background images.
- Add named light/dark appearance presets with draft save/cancel isolation.
- Lighten search/address text selection and Home suggestion highlights.
- Add optional online completion from the selected provider in the address bar and Home.
- Block online lookup in private tabs and for address-shaped inputs; cancel stale requests and avoid browser cookies.
- Keep provider completions search-only.
- Migrate profile schema 5 to 6 with a backup before writing new preferences.
- Retain all 0.3.7 everyday-browsing, download, media and compatibility behavior.

## 0.3.7

- Reopen up to 20 closed normal tabs with Ctrl+Shift+T and menu commands, preserving position, pin state and existing group.
- Exclude private tabs from the in-memory closed-tab list.
- Add local address suggestions from bookmarks/history, keyboard and mouse selection, and bookmark-only suggestions in private tabs.
- Preserve address edits while navigation finishes.
- Add a native Downloads browser tab at harbor://downloads, Ctrl+J and the cog menu.
- Add filename filtering, live byte/percentage progress, pause/resume/cancel and file/list actions.
- Separate normal/private download lists and keep completed files when clearing entries.
- Preserve the original filename/list entry when resume raises a new download-start event.
- Retain closed source controls while downloads need them, then release them.
- Add real loopback HTTP download checks, including interrupted range resume, cancellation, source-tab closure and byte-for-byte ZIP integrity.
- Carry forward all 0.3.6 compatibility, menu and media fixes.

## 0.3.6

- Keep cog menu and submenu scrollbars on the physical right side.
- Preserve left-opening flyouts and menu content direction.
- Verify overflowing History menus with synthetic history in light and dark mode, including scrolling to the bottom.

- Fit normal windows and dialogs to monitor work areas at startup and after display/DPI/work-area changes.
- Adapt toolbar and branding to narrow logical widths; shorten tab titles and preserve essential navigation.
- Keep Settings actions visible while content scrolls; wrap long labels and cookie/history action rows.
- Make picture-in-picture placement account for DPI and available work area.
- Replace native menu templates with compact, palette-aware cog menus and flyouts.
- Remove unused icon gutters, truncate long menu labels and bound/scroll long menus.
- Select the full URL on initial address-bar mouse focus and keyboard focus; preserve normal editing on subsequent clicks.
- Use a contrasting foreground for selected text.
- Add resolution geometry, real-app layout, native fitting, menu and address-selection checks.

## 0.3.5

- Connect HTML fullscreen changes to native full-monitor video presentation.
- Add browser fullscreen with F11 and restore the previous placement and resize constraints on exit.
- Limit normal maximization to monitor work areas; provide taskbar-edge space during video fullscreen.
- Add a persistent-player picture-in-picture window with play/pause, mute and return-to-tab controls.
- Support video discovery and layout restoration across nested/cross-origin frames, and keep pop-out tabs awake.
- Combine tabs, branding and window controls into one title row.
- Replace the group bar with cog-menu filtering, assignment and management.
- Add explicit Windows app/shortcut identity, versioned Beacon shortcut icons and Explorer icon refresh.
- Add isolated app/media integration tests and test-profile injection.

## 0.3.4

- Append regular new tabs at the right edge.
- Add tab dragging, insertion indicators, overflow scrolling and context-menu/keyboard moves.
- Respect pinned-tab boundaries and group filters; persist reordered sessions.
- Embed Beacon as a window/app icon and place it beside HARBOR.
- Add shared dynamic interface palettes, Light/Dark settings, an HSV color wheel, brightness and hex entry.
- Customize accent, window, panel, input and text colors independently for each mode.
- Add local Settings preview, save/cancel isolation, per-color/per-mode reset and contrast guidance.
- Synchronize normal home-page theme changes with app mode and preserve private-tab isolation.
- Apply WebView2's preferred color scheme.
- Register home-page message handlers once instead of adding them again on sleep/wake.

## 0.3.3

- Add Harbor Home at harbor://home with local morning and blue-hour coastal backgrounds.
- Add search/address entry using the selected search engine and up to eight saved-bookmark shortcuts.
- Remember the home-page theme in normal tabs without saving private-tab theme changes.
- Add the selected Beacon lighthouse identity as the application and installer icon.
- Include the three identity explorations, selected PNG/ICO, wallpapers and release banner in Artwork.
- Upgrade the former DuckDuckGo default once; preserve custom home URLs and later explicit choices.
- Carry forward the 0.3.2 download and compact-window fixes.

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
