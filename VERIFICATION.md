# Harbor Browser 0.3.7 verification

Verified November 12, 2026 (America/Chicago).

## Executed results

- Windows .NET Framework 4.8 Release build: passed, zero warnings and zero errors.
- Inno Setup installer compilation: passed.
- Compiled app and installer file versions: 0.3.7.0.
- Compiled application and staged installer application SHA-256 hashes: identical.
- Bundled .NET Framework 4.8 and WebView2 x64 offline prerequisite size and Microsoft signature validation: passed.
- 253 regression/service/settings/offline-layout checks: passed.
- 167 isolated real-app/interface/media/download checks: passed.
- 28 isolated WebView2 home-page checks: passed.
- Total: 448 passed checks.

## New behavior verified

Closed tabs:
- Private tabs and unsafe URLs are excluded.
- The list is bounded to 20 entries and restores newest first.
- Snapshot copying preserves title, pin, group and original position.
- The real app restores the selected tab with its pin/group/position and makes it visible.

Address suggestions:
- Bookmarks/history match locally, duplicates are removed, unsafe and credential-bearing URLs are excluded, and case-sensitive paths remain distinct.
- Empty queries do not expose the browsing list.
- Private tabs use bookmark-only suggestions and private navigation is excluded from saved history.
- Routed Up/Down, Enter and Escape events select/navigate/dismiss correctly.
- A routed mouse-selection event opens the selected result.
- The selected suggestion uses the current interface palette.

Downloads:
- A native Downloads tab opens without starting a WebView for that page and reuses the page within the same normal/private context.
- The page participates in normal session saving and can navigate to a normal web page afterward.
- A real local HTTP server supplies a valid roughly 2 MB ZIP using an extensionless attachment filename.
- Live bytes/progress appear in the page.
- Pause stops progress; Resume succeeds after the test server deliberately closes the transfer connection.
- The resumed request uses an HTTP byte range, completes with one original list entry/filename, matches the source byte-for-byte and opens as a ZIP.
- Cancellation and subsequent row removal work.
- Search filters rows; clearing completed rows retains their disk files.
- Normal and private pages show only their respective entries.
- Closing the source tab does not stop the transfer. The retained source is released after completion.
- At a 659×320 logical window size, all page content scrolls and transfer controls remain reachable.
- Light/dark page previews and suggestion previews were inspected.

The forced interruption test exposed a second download-start event during resume. The implementation now binds that event back to the original entry and path instead of allocating a duplicate filename. The source-tab closure test also exposed lost lifecycle updates; source controls now remain alive for active/resumable transfers and are released afterward.

## Previous behavior rechecked

The suites retain filename/extension recovery, collision protection, settings save/cancel, light/dark/custom appearance, tab ordering, local home behavior, 1366×768 logical-layout coverage, themed right-side menu scrollbars, window geometry, fullscreen video, picture-in-picture controls/restoration, tab groups and saved tab order.

## Environment and limits

All app tests used an isolated profile and synthetic fixtures. Download HTTP traffic stayed on loopback; home-page tests confirmed no external requests from Harbor Home. The user's installed browser and personal profile were not changed.

No installation, upgrade or uninstall was performed. The available desktop reports 1920×1080; Windows resolution/scale settings were not changed. Resolution/scale-specific checks used corresponding logical layouts and injected monitor/work-area rectangles.

Remaining user-system checks:
- Installer/upgrade/uninstall on the target Windows laptop.
- Physical Ctrl+Shift+T and Ctrl+J input, keyboard focus transitions, real mouse/trackpad scrolling, dragging and taskbar-edge interaction.
- Opening downloaded files in their external handlers and showing them in Explorer; availability/guards were checked without launching those applications.
- Actual display/DPI transitions and additional real-world websites, download servers and protected video players.

Closed-tab restoration and download lists are intentionally limited to the current browser session. Completed files remain on disk. Resume availability depends on the browser engine and server.

API references: [WebView2 download operation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2downloadoperation) and [WebView2 download integration](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/overview-features-apis#downloads).

The source archive excludes build outputs, test profiles and prerequisite executables. Installer/source hashes are in HarborBrowser-0.3.7-SHA256.txt.
