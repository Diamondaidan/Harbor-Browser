# Harbor Browser 0.5.0 verification

Verified November 13, 2026 on the available Windows 10 Home build 19045 machine with .NET Framework 4.8 and WebView2 157.0.4341.46. App/assembly/installer file version: 0.5.0.0. Profile schema remains 9. Synced sources, earlier releases, the personal browser profile, HTTP/HTTPS default choices and real VPN connections were not changed.

## Executed checks

| Suite/check | Result |
| --- | --- |
| Regression/services/settings/layout | 384 passed |
| Full running-app/media/download/privacy/recovery | 360 passed |
| Real App startup/forwarding sub-suite | 4 passed, included in the full suite |
| New UI/IPC/VPN sub-suite | 17 passed, included in the full suite |
| Suggestion-click regressions | 17 passed |
| Installer-staged Home page in actual WebView2 | 38 passed |
| Isolated installer/upgrade/uninstall and browser registration | 19 passed |
| Live public-site navigation/icons | 6 passed across W3C, Wikipedia and Example Domain |
| Total without repeated sub-suites | 824 successful assertions |
| Release build | Passed, zero errors; NU1900 warnings because the online package vulnerability feed was unreachable |
| Production Inno Setup compile | Passed; .NET 4.8 and WebView2 x64 prerequisites embedded and Microsoft signatures/sizes verified |
| Compiled/tested/staged application | SHA-256 identical |

These are assertion counts, not 824 separate user workflows. Failed development attempts are not counted. Full suite and focused logs are included in the source package; generated profiles, binaries and installer fixtures are excluded from that ZIP.

## Private-tab indication

Normal tabs have no private marker. Private tabs have Private text, accessible names, a purple P badge over the favicon and a Private window-title prefix. The badge stays visible for pinned/icon-only tabs when the title is hidden. Light/dark renders retained the site icon and were visually inspected. Existing private-cookie/storage/permission/download/history/session exclusions passed the full suite. No new anonymity or network-privacy claim is made.

## Browser registration and activation

The normal installer now declares machine-wide registered browser capabilities, HTTP/HTTPS URL associations, .htm/.html file handling, owned ProgIDs, icons and quoted open commands. It does not edit UserChoice or set a default. Cog > Tools > Default browser opens the documented Windows Default apps URI; choosing Harbor remains a Windows user action. Windows 10 uses the general page; supported Windows 11 builds can navigate to the registered application page.

Executable installer tests used separate current-user fixture registration names/ProgIDs/AppId and workspace app/profile folders. They verified HTTP/HTTPS capabilities, RegisteredApplications, quoted executable/target arguments, HTML OpenWith/association entries, upgrade snapshots, byte-preserved profile files, uninstall cleanup and unchanged user HTTP/HTTPS defaults. These tests do not prove the production administrator/UAC path, missing-prerequisite installation or actual selection in the Windows Settings picker. The production machine-wide installer was compiled but not installed over personal Harbor.

Real child processes ran the production App startup code using injected disposable stores and a unique test pipe namespace. Cold activation opened one target without an extra Home tab. A second process forwarded its URL and exited. With a private tab selected, the forwarded link became a new normal tab. A local HTML file with spaces also forwarded correctly. Child applications closed cleanly. Normal session/recovery behavior remained covered by the full suite.

Argument tests covered quoted URLs, encoded spaces/fragments, file URI forwarding, missing/non-HTML files, remote shares, unsupported schemes, extra option-shaped association arguments and count limits. The forwarding pipe grants access to the current Windows user and bounds/validates its message. The tests did not use the personal browser's single-instance namespace.

Initial startup tests placed WebView2 profiles inside an excessively deep generated directory. Private-controller initialization/shutdown stalled there; using a short disposable temporary profile resolved the test. Cleanup experiments were discarded, and production private-cleanup code remains the 0.4.3 implementation. Long custom/test profile paths remain a known environment limitation. The ordinary installed profile uses LocalAppData, not the deep generated test path.

## VPN scope and validation

The native read-only discovery probe returned zero Windows VPN profiles and zero query errors. No real connection was connected, disconnected, created or edited. Unit/UI tests used injected Windows query/command results to verify distinct user/all-user scopes, observed status, connection/disconnection transitions, stale profiles, unknown states, query timeouts, name validation, quoted direct-process arguments, targeted disconnect and credential-error guidance. Compact light/dark VPN windows kept footer controls reachable.

The implementation uses Get-VpnConnection to discover current-user and all-user profiles and query Windows status. Actions recheck the selected profile and call Windows rasdial with its exact name and scope-specific phonebook. No username/password is collected, stored or passed. Commands run without a shell/visible console, drain output asynchronously and have time limits. A success result is followed by a status query; it is not represented as proof of routing/privacy. Windows profile/credential management stays in Windows settings.

Actual saved-credential/EAP/MFA authentication, server connectivity, routing, split-tunnel traffic, disconnect completion, provider-specific applications and policy behavior still require a configured Windows VPN. Harbor supplies no VPN server/service/subscription and no VPN performance/leak-test result. Status refreshing runs while the VPN window is open; broad performance benchmarks were not repeated.

## Existing behavior retained

Full checks exercised local streamed ZIP downloads with pause/resume/cancel and byte/filename checks, tab order/reordering/widths/overflow, bookmark order/drop handling, PNG/SVG/ICO icons, settings and themes, video fullscreen/PiP, actual permission timeout, private isolation, browsing-data cleanup, browser-engine recovery and resource monitoring. W3C and Wikipedia loaded live with their icons; Example Domain used the no-icon fallback. Home tests used staged assets without external requests. Historical 0.4.1 performance results remain in PERFORMANCE.md/Measurements and are not new 0.5.0 measurements.

## Remaining Windows-system checks

- Install the normal 0.5.0 setup, including prerequisites/UAC, on the target laptop; upgrade the actual installed Harbor and verify data preservation.
- Select Harbor in Windows Default apps and open links from another real application with Harbor closed/open/private-selected. Confirm HTML handling and desired default associations. No automated test selected a new default browser.
- Check private badges at actual 1366 x 768 scale settings, native mouse/trackpad bookmark/tab dragging, edge scrolling, normal suggestion-popup capture, monitor changes, taskbar access and real GPU/video/audio. Logical-size/native test windows are not physical display validation.
- Use a real Windows VPN profile to validate the remaining VPN behaviors above.
- Signing, broader extension/store support, security auditing, reliable updating and wider hardware/site compatibility remain pre-1.0 work. App and installer remain unsigned.

See DEFAULT-BROWSER.md, VPN.md, LAPTOP-CHECKS.md and SETUP-README.md. Close Harbor before running dist/HarborBrowserSetup-0.5.0.exe. Previous releases remain separate. The source archive is verified entry-by-entry against the source files; the external SHA-256 file records the installer, source archive and application hashes. Home source/staged assets are also compared by hash.
