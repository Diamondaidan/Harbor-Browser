# Harbor Browser 0.4.0 verification

Verified November 12, 2026 (America/Chicago). No installation, upgrade or uninstall was performed. The installed browser, personal profile and synced project sources were not modified.

## Executed checks

| Check | Result |
| --- | --- |
| .NET Framework 4.8 Release build | Passed, zero errors. NuGet reported NU1900 because its vulnerability feed was unreachable; an online package vulnerability audit was not completed. |
| Inno Setup compilation | Passed. App and setup file versions: 0.4.0.0. |
| Embedded .NET Framework 4.8 and WebView2 x64 installers | Size checks and Microsoft Authenticode signature verification passed. |
| Tested, compiled and staged Harbor executable | SHA-256 identical across regression/integration copies and installer payload. |
| Compiled and staged Home assets | All file hashes match. |
| Regression, service, settings and logical-layout checks | 310 passed. |
| Isolated actual-app, native-window, media and privacy checks | 242 passed. |
| Packaged Home-page WebView2 checks | 38 passed. |
| Total automated checks | 590 passed. |

Logs included beside this report: harbor-0.4.0-regression.log, harbor-0.4.0-integration.log, harbor-0.4.0-home.log and harbor-0.4.0-build.log. Source and installer hashes are distributed separately in HarborBrowser-0.4.0-SHA256.txt.

## Privacy tests exercised

Permission service checks covered URL normalization, rejection of credentials/unsafe schemes, protocol/port/requesting-site/embedding-site separation, independent snapshots, Ask removing a stored rule, unsupported kinds failing closed, date filtering and ignoring a corrupt permission primary rather than restoring old backup grants.

Actual WebView2 requests exercised camera denial once, remembered microphone denial, notification permission granted once, denial when switching away from a pending prompt, remembered camera allowance, and native-profile grant-cache inspection. Camera allowance used Chromium's fake-device test option; no physical camera or microphone was accessed. Notification testing checked the JavaScript permission result, not Windows toast delivery.

Two simultaneous private tabs shared private cookies, local storage and in-memory permission choices. They could not see normal storage. Private choices did not overwrite normal saved permission rules. Normal cleanup cleared normal cookies/local storage while leaving private storage intact; private-scoped cleanup did the reverse. Closing the last private tab removed its rules/download rows and the next session received a new identity with empty data.

The private last-tab download confirmation was tested with a synthetic active download row. Real pause, range-resume, cancellation, source-tab closure, filename preservation and ZIP byte integrity were tested separately with normal downloads from a loopback HTTP fixture.

History cleanup was checked against recent/old entries, atomic backup fallback, automatic snapshots and installer UpgradeBackups. Recovery copies and closed-tab lists were reset while current open tabs and bookmarks remained. Permission-backup sanitation was also checked. Cookie/site-storage/cache clearing calls ran against real profiles; individual disk-cache files were not measured for secure erasure.

Both new dialogs were exercised in light and dark mode, including core permission rows, resetting while a grid cell was being edited, chosen cleanup categories/time range, palette and Beacon icon. The resulting rendered previews were visually inspected.

Existing private-history exclusion and address/Home online-suggestion suppression checks also passed. Online tests used an injected deterministic transport; no live provider-connectivity probes were repeated for this release.

## Recovery and resource monitor

An isolated child process wrote a normal session and was abruptly terminated. The next session detected the unclean journal and retained URL, pin, group and selection. Exclusive writer locking, clean shutdown, dismissal, corrupt-journal fallback and corrupt-session backup recovery were checked.

The actual app was opened with an unclean test profile. It showed recovery before reopening saved pages, restored the saved tab/group/pin on request, retained the dismissed checkpoint for Tools, and marked a subsequent close clean. Preference migration was verified to save only after the writer lock was obtained.

The isolated app's renderer was deliberately crashed through DevTools. Reload restored the original tab and address; entering a new address also recovered the page. A separate browser-engine crash preserved normal session URLs, marked broken controls for recreation, created a fresh control after the browser-exit event and successfully navigated again.

The resource monitor displayed actual Harbor/WebView2 process information and tab counts, refreshed on demand and remained usable after engine recovery. These are functionality checks, not repeatable memory/performance benchmarks.

## Earlier behavior rechecked

The suites retained download extension/collision handling; real HTTP transfer controls and byte integrity; tab ordering, reordering, groups and restoration; Downloads filtering; compact menus/right-side scrollbars; Settings; bookmark folders/import/export; Home shortcuts/backgrounds/presets; address selection and online-result cancellation; fullscreen video and picture in picture.

The Home suite ran against the staged Home assets and blocked external page-resource requests. It verified light/dark/custom scenes, search actions, keyboard navigation, private behavior and suggestion rendering.

## User-system checks still needed

- Fresh installation, upgrading an existing installation, missing-prerequisite installation, offline setup, restart handling and uninstall on the target Windows laptop.
- Actual Windows display scaling/monitor transitions, physical tab dragging, mouse/trackpad scrolling, taskbar/auto-hide behavior and hardware video playback. Resolution checks used logical layouts and injected monitor/work-area bounds; Windows display settings were unchanged.
- Physical camera/microphone and Windows location/privacy settings, location service results, delivered notifications, embedded-site requests on real websites and accessibility with keyboard/screen reader.
- The full one-minute permission timeout, actual private transfer cancellation on last-tab closure, clear-on-exit behavior across forced OS shutdowns, power loss and disk-full failures.
- Broad website compatibility, third-party auditing, measured memory optimization and extension permission enforcement.

Private browsing is not anonymous networking or secure disk erasure. Open pages may recreate data after clearing. Exported/manual copies outside Harbor-managed backups are not removed. Unsaved forms and in-page state are not crash-restored. Unsupported permission kinds are denied.

The installer and Harbor application are not code-signed. Target packaging is x64 Windows 10/11; the configured minimum Windows build is 16299, not a guarantee of compatibility with every old Windows installation or runtime. Schema 7 preserves older data through snapshots; older Harbor builds protect newer-schema data by disabling saves. Close Harbor before upgrading.

Extension architecture/permission enforcement, broader security review, installation validation and memory benchmarking remain unfinished roadmap work. This release does not mark the complete security milestone or 1.0.0 readiness.

## API references

[WebView2 request-only permission decisions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2permissionrequestedeventargs.savesinprofile), [profile browsing-data cleanup](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile.clearbrowsingdataasync), [process failure/recovery guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-related-events), and [browser-process exit event](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2environment.browserprocessexited) informed the implementation. Isolated crash fixtures use the documented [Page](https://chromedevtools.github.io/devtools-protocol/tot/Page/) and [Browser](https://chromedevtools.github.io/devtools-protocol/tot/Browser/) DevTools crash commands.