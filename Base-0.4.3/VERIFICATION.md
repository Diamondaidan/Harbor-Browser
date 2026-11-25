# Harbor Browser 0.4.3 verification

Verified November 13, 2026 on Windows 10 Home build 19045, WebView2 157.0.4341.46, .NET Framework 4.8. Application/assembly/installer file version: 0.4.3.0. Profile schema: 9. The personal Harbor installation/profile, synced sources and previous release artifacts were not changed. Browser checks used separate test profiles.

## Executed checks

| Suite or check | Result |
| --- | --- |
| Regression/services/settings/layout | 353 passed |
| Full running-app/media/download/privacy/recovery | 339 passed |
| Focused 0.4.3 access/drop/overflow checks | 26 passed; these are also included in the full suite |
| Suggestion-click hotfix | 17 passed |
| Installer-staged Home in actual WebView2 | 38 passed |
| Isolated installer, upgrade and uninstall fixtures | 12 passed |
| Public live-site navigation/icon checks | 6 passed across W3C, Wikipedia and Example Domain |
| Release build | Passed, zero errors; NU1900 warnings because the online NuGet vulnerability feed was unreachable |
| Production Inno Setup compile | Passed; normal administrator installer built, prerequisites embedded |
| .NET Framework 4.8 / WebView2 x64 prerequisite packages | Minimum sizes and Microsoft signatures verified |
| Compiled / tested / staged application | SHA-256 identical |

The non-repeated total is 765 successful assertions; the 26 focused assertions are repeated in the full suite and are not added again. Check counts are not counts of separate user workflows. Initial fixture failures were corrected in test setup: synthetic drag events needed their routed-event identifiers, and the installer test expected the wrong backup-folder name. No failed check is counted as successful coverage.

## Implemented and exercised

**Module access.** Real native extension scripts ran at an allowed local test site and did not run at an unchecked site. Attempts to grant undeclared patterns were rejected without widening existing grants. Deny all retained a visible disabled module and Enable could not bypass it. Editing disabled access left the extension unregistered until explicit Enable; selected grants then took effect. Previous permissions were revoked, including clearing injected effects when an actually sleeping tab woke. Original requested patterns and allowed patterns remained separate. Grants/enabled state survived closing and reopening the test browser. A corrupt permission file disabled the managed extension. Removal cleaned the saved policy. Existing private-tab and trusted-Home exclusion tests also passed.

**Module interface.** Enabled/disabled/blocked state is visible in the list, selection survives actions, and the editor explains wildcard overlap, page reloads and possible loss of the module's own saved preferences when access changes. Light/dark dialogs at 360 x 300 kept footer controls reachable with scrolling/wrapping; representative renders were visually inspected. Profile schema 8-to-9 migration retained snapshots of module access metadata. This release chooses among declared site patterns; arbitrary narrower host grants, full Chrome Web Store compatibility, background/toolbar/native extension APIs and independent security auditing are not delivered.

**Organization.** The empty portion of the bookmark bar is a full-width hit-test/drop surface, including after resize. Routed drag-over/drop tests exercised moving a bookmark to the end and saving the resulting order; foreign text payloads were refused. Existing bookmark identity/order/settings and folder-order tests passed. Selecting an overflow tab scrolled it into view; returning to an earlier tab scrolled back. Equal tab sizing, pin/unpin, new-tab order, favicon formats and session reordering regressions passed.

**Browsing.** W3C loaded with its PNG favicon, Wikipedia with its ICO favicon, and Example Domain used the clean no-icon fallback. These were live HTTPS pages in a disposable profile, not a broad website-compatibility survey. Local streamed downloads passed pause/resume/cancel, filename/extension, byte and ZIP-content checks. Existing video fullscreen/PiP, privacy, actual permission timeout, engine-crash recovery, resource-monitor and sleeping-tab checks passed. Search tests used controlled replies rather than personal queries; Home pointer tests used actual browser DevTools pointer events. Physical Windows address-popup capture is not established by routed WPF input.

## Installer fixture scope

The normal production installer was compiled and statically checked, but was not installed over the user's Harbor installation. For executable installer tests, IntegrationTests/Run-Installer-Fixtures.ps1 generated separate fixture installers from the real 0.4.2 and 0.4.3 scripts and release payloads. The fixture changes are explicit: a unique fixture AppId/name, workspace installation/profile paths, lowest/current-user privileges, no shortcuts, no application launch, no closing other apps and no prerequisite installation. Registry writes belong only to the fixture's uninstall identity and were removed afterward.

The 0.4.2 fixture installed the matching payload; the 0.4.3 fixture upgraded it to the matching new payload. Bookmarks, settings, module metadata and schema marker stayed byte-identical, and the real profile-backup logic created an UpgradeBackups snapshot including site-access metadata. Uninstall removed the fixture app and its registration while preserving its separate profile. The fixture cannot establish UAC, a missing-prerequisite install, real personal-profile upgrades, machine-wide installation or behavior on a different laptop. Both prerequisites were already installed here. No Windows Sandbox/VM test tool was available.

To rerun these installer fixtures, keep complete HarborBrowser-0.4.2 and HarborBrowser-0.4.3 sibling folders with their dist/payload directories. The source ZIP alone excludes those generated payloads and the large prerequisite executables; build/stage them first.

## Physical checks and remaining limits

A dedicated visible Harbor test window was opened with disposable bookmarks. The desktop helper could read its accessibility tree, but screenshot capture failed with `FrameArrived timed out: timed out waiting on channel`; selecting the window again and retrying failed with `window capture timed out: timed out waiting on channel`. No coordinate drag was attempted without a usable capture. Only that test process was closed. Physical bookmark/tab dragging, edge scrolling, trackpad input and normal address-popup capture remain unverified.

The 1366 x 768 layout was exercised at logical sizes corresponding to 100%, 125%, 150%, 175% and 200% Windows scale, alongside the existing other-size cases. These are logical layout/native test-window checks, not changes to the actual Windows scale setting or validation of the user's laptop display. Actual monitor transitions, taskbar access, real GPU/video/audio and laptop installation remain in LAPTOP-CHECKS.md.

Long performance benchmarks were not repeated; PERFORMANCE.md and Measurements retain historical 0.4.1 results. No new speed or RAM-reduction claim is made. The browser and production installer remain unsigned. The limited module controls are not a completed security review.

## Release files

Close Harbor before using dist/HarborBrowserSetup-0.4.3.exe. The production installer keeps the original AppId and profile location; prerequisites are bundled and profile files are snapshotted before upgrade. The application snapshots migration to schema 9. Previous releases remain separate.

The source archive includes current code, sample module, build/test scripts, logs and representative previews, and excludes generated test profiles, binaries, installer output and redistributable executables. Each archived file is compared to its source by SHA-256 during packaging. The external HarborBrowser-0.4.3-SHA256.txt records the source archive, installer and application hashes. Source/payload Home assets are also hash checked.
