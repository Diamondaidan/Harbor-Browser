# Harbor Browser 0.4.2 verification

Verified November 13, 2026 on the available Windows 10 build 19045 desktop with WebView2 157.0.4341.46 and .NET Framework 4.8. App/assembly/installer file version: 0.4.2.0. Profile schema: 8. Tests used isolated profiles and local fixtures. The personal Harbor installation/profile, synced sources and previous release artifacts were not changed. No installer was executed against the installed browser.

## Executed checks

| Check | Result |
| --- | --- |
| Regression/services/settings/layout suite | 345 passed |
| Full running-app/media/download/privacy/recovery suite | 308 passed |
| Focused 0.4.2 feature suite, including restart persistence | 32 passed; repeats 27 feature checks from the full suite and adds 5 theme/persistence checks |
| Suggestion-click hotfix suite | 17 passed after correcting repeated-cancelled-navigation test timing |
| Installer-staged Home page in actual WebView2 | 38 passed |
| Release build | Passed; zero errors; NuGet NU1900 warnings because the online vulnerability feed was unreachable |
| Inno Setup compile | Passed; installer file version 0.4.2.0, product 0.4.2 |
| Bundled .NET Framework 4.8 / WebView2 x64 prerequisites | Microsoft signatures and minimum sizes verified; 115.7 MB / 205.0 MB |
| Tested integration executable / compiled app / installer-staged app | SHA-256 identical |

There are 713 successful assertions across these suites after excluding the 27 repeated feature assertions in the focused suite. These are check counts, not 713 separate user workflows. Initial repeated-click failures are retained in diagnostic logs and are not counted as successful coverage.

## New behavior exercised

- Menu commands: one Pin/Unpin command, an intact New tab command, and pin/unpin changes affecting only the correct item.
- Tabs: actual WPF items use equal normal widths, shrink to share space, preserve compact pinned widths, and keep the + button within the title bar. Service tests cover the 54-pixel minimum and pinned-space reservation. Existing tab order/reorder/session checks passed.
- Images: a local PNG, dynamically changed SVG and ICO favicon all decoded to visible WPF tab images through WebView2. Returning Home restored the embedded Beacon image. Asynchronous results are guarded by view/navigation/revision so stale icons cannot replace a new page's icon.
- Bookmarks: old bookmarks receive unique stable identities; edits/merges preserve them; stale/duplicate saved order entries are ignored; bookmarks and folders can be interleaved. Real bar Move left and reorder actions updated the displayed buttons and saved settings. Saving appearance settings preserved the order. Drag/drop handlers and insertion/edge-scroll logic are implemented; physical press-drag-release and edge scrolling were not exercised by these routed/control tests.
- Modules: a real Manifest V3 extension installed into an isolated normal WebView2 profile, injected only into its declared test site, used an isolated script world, and left the page/browser usable after a deliberate exception. Unrelated sites, trusted Home and a private profile received no injected marker. Enable, disable, remove, automatic reload, managed-copy cleanup and preservation of the native PDF/clipboard components passed. Disabled state persisted through closing/reopening a separate test Harbor app and could then be enabled/removed. Both themes used Harbor colors/icon and were visually inspected. Invalid capabilities, frame/main-world injection, file/traversal access and a newer minimum Harbor version were refused.

## Regression scope and test limits

The full app suite includes local streamed downloads with pause/resume/cancel and byte/ZIP checks, filenames/extensions, tab movement, small-screen controls, video fullscreen/PiP, settings, optional online suggestions with injected replies, private cookies/storage/permissions/download cleanup, actual permission timeout, engine recovery and resource monitor behavior. Home browser tests used installer-staged assets without external network requests. Long performance benchmarks were not repeated; PERFORMANCE.md and Measurements contain historical 0.4.1 measurements.

The first suggestion-click runs timed out while repeatedly navigating to the same cancelled fixture URL. Diagnostic routing showed that the intended row was pressed and the popup closed, but a later expected NavigationStarting event did not arrive. The fixture now uses a separate destination for each sub-element and lets each cancelled navigation settle; all 17 checks passed. Repeated-cancelled-request coalescing/timing is the inferred cause, not a confirmed WebView2 defect. Production click handlers were not changed for this test adjustment. Address clicks are WPF routed input with native popup auto-capture disabled only in the offscreen host; Home suggestion checks use actual browser DevTools pointer events. These do not establish physical mouse/trackpad behavior with normal popup capture.

The source package contains build/test logs and representative previews. Full generated test profiles and binaries remain in the working folder and are excluded from the source ZIP. Source/payload Home assets are hash checked during packaging. Installer prerequisites are embedded; the source ZIP excludes those large redistributable executables and the build script downloads/verifies them when needed.

## Checks requiring the target Windows system

- Fresh installation, missing-prerequisite installation, upgrade from the installed Harbor, and uninstall on the other laptop. Installer compilation and static packaging checks do not prove these flows.
- Physical bookmark dragging, insertion feedback/edge scrolling, tab dragging/overflow scrolling, repeated suggestion clicks with normal popup capture and mouse/trackpad input.
- The user's 1366 x 768 display at their actual Windows scale, monitor transitions, native taskbar access, real video/GPU/audio behavior and broad website compatibility.
- Wider extension APIs/store compatibility, malicious-code auditing, signing and real-site/hardware performance measurements remain future work. Site-module permissions are limited to the implemented interface described in SITE-MODULES.md; they are not a completed security audit.

Close Harbor before running dist/HarborBrowserSetup-0.4.2.exe. This installer preserves the existing AppId and makes profile-file snapshots before replacing application files; the app snapshots migration to schema 8. Harbor and its installer are unsigned. See SETUP-README.md for build/install requirements and the release SHA-256 file for artifact hashes.
