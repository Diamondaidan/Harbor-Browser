# Harbor Browser 0.5.2 - release verification

Prepared November 14, 2026. The regular release was built and tested on Windows 10 x64 build 19045 with WebView2 157.0.4341.46, Intel Core i7-7700HQ, 8 logical processors and 12 GiB installed memory. Tests used isolated profiles, download folders, local fixtures and owned test processes. The installed Harbor, personal profile and browser defaults were preserved.

## Executed checks

| Suite | Final result |
| --- | --- |
| Release app and all four test-project builds | Passed, zero build errors |
| Regression, services, settings and offline WPF layout | 384 assertions passed |
| Running browser, media, downloads, UI, privacy and recovery | 360 assertions passed |
| Focused address and Home search-suggestion clicks | 17 assertions passed on final run |
| Staged Home page in real WebView2 | 38 assertions passed |
| Live Tor connection, download, failure, isolation and reversal | 20 assertions passed |
| Extension integrity plus real Tor restart/crash lifecycle | 26 assertions passed |
| Live W3C, Wikipedia and Example Domain navigation and favicon behavior | 6 assertions passed; no UNVERIFIED sites |
| Isolated 0.5.0 install to 0.5.2 upgrade and uninstall | 26 assertions passed |
| Simulated missing prerequisite success, reboot and failure branches; native runtime-version checks | 25 assertions passed |
| Total, excluding repeated development runs and benchmarks | **902 successful assertions** |
| Performance scenarios, counted separately | 10 passed; 90 recorded samples; see PERFORMANCE-0.5.2.md |
| Network-enabled package restore and NuGet vulnerability listing | Passed; no vulnerable packages reported by configured sources |

These counts are assertions, not 902 independent user workflows. The final logs are in Evidence in the source package. All final browser suites used the same application executable as the staged installer payload.

The first focused suggestion run passed its first 13 assertions but timed out while creating a private tab. That isolated process was stopped. All 17 checks then passed alone with a fresh, shorter profile path; no application code changed for the rerun. Broader private-tab cases also passed in the complete suite. The precise cause of the initial timeout is not established. suggestions-initial-timeout052.log is retained for review. The reusable release-test runner now uses shorter fixture paths.

Initial non-network build restores warned that the vulnerability feed was unavailable. A subsequent network-enabled restore and explicit vulnerable-package listing succeeded without reported advisories. This checks the managed package feed; it is not an audit of all native third-party code.

## Functional coverage

The running-browser suite exercised download naming and extension recovery, unsafe filename cleanup, duplicate filenames, cancellation, paused/resumed transfers, private download handling and stored download state. A live Tor download retained its .asc filename/extension and expected signature text; that check did not validate the detached signature.

UI checks covered new tabs on the right, tab reordering and equal shrinking widths, bookmark/folder reordering, private-tab identity, Beacon icons, title-bar controls, compact cog menus, URL selection, history placement, light/dark appearance, custom colors, responsive dialogs and resource monitoring. Browser fullscreen used the monitor work area, video fullscreen used the full monitor, and iframe video/popout controls were exercised. WPF and browser captures are from isolated test windows, including simulated monitor/scaling layouts; they are not a manual check on every physical display or laptop.

Search tests covered row text, URL, source label, empty surface, keyboard selection, private bookmark suggestions, delayed online replies and real browser pointer events on Home suggestions. The Home suite also confirmed no external request was needed to load local Home assets.

Modules were tested with site grants, disabled state, removal, private/internal-page exclusion, missing native registration, changed package files and corrupt saved permissions. Enabled restoration requires an explicit saved choice and an unchanged reviewed package. Legacy records can require saving Site access again and explicitly enabling the module; they are not silently trusted.

## Installer checks and fixes

The finished installer is the regular **0.5.2** x64 release, using the same production upgrade identity as 0.5.0 and %LOCALAPPDATA%\HarborBrowser. It contains the complete application, Tor client, matching Tor source, original component notices, and Microsoft's full offline prerequisite packages. The staged payload has 35 files before Inno's versioned icon and uninstaller additions.

The bundled Microsoft packages have valid Microsoft Corporation Authenticode signatures:

- .NET Framework 4.8 offline: 121,346,568 bytes.
- WebView2 x64 Evergreen standalone, runtime 157.0.4341.46: 214,990,032 bytes.

The release requires WebView2 major version 157 or later. Native setup checks reject missing/invalid/156 version values and accept 157/newer. Older runtimes trigger the included standalone package. No separate prerequisite download is needed before running the finished installer. Administrator access is required; Windows may require a restart.

A discovered setup defect was fixed: prerequisite exit 3010 previously did not trigger the final restart notification. Setup now preserves it through NeedRestart. Both .NET and WebView restart branches return the configured restart exit code in the fixture. Simulated exit 1603 aborts before application copying; successful helpers receive the intended silent arguments. Real Microsoft prerequisite installers were not executed on this already configured computer.

Lifecycle fixtures compiled the real installer script into isolated variants with a distinct app identity, per-user test registration, test install/profile folders, and no shortcuts or automatic app launch. The lifecycle variant reused existing prerequisites; the prerequisite variant substituted harmless test executables. The production AppId and real Windows components were never installed/replaced by these tests.

The actual Inno file-copy, upgrade backup, browser capability registration and uninstall logic ran. Every installed payload file matched its staged SHA-256. Settings, bookmarks, module permissions, saved Tor choice and startup state survived upgrade and were included in the snapshot. Uninstall preserved profile data, removed fixture registrations and left HTTP/HTTPS default choices unchanged. The production setup compiled successfully, but its actual elevated installation under the production identity was not run on the host.

Harbor and the finished installer are unsigned. Windows may show unknown-publisher or reputation prompts. The Microsoft packages inside setup are signed; that does not sign Harbor itself.

## Tor evidence and limits

An actual Harbor page loaded Tor's API with IsTor=true; a separate direct environment returned false with a different address. Killing the owned helper made tested page requests fail rather than fall back directly. Disconnect removed routing and restored direct browsing. Normal restart restored the requested connection before the test page navigated. Normal close and forced browser termination ended the owned helper; subsequent crash recovery reconnected successfully. Disposal/cancellation left no helper running. Windows proxy values stayed unchanged.

Tor remains experimental, off by default, and limited to normal Harbor page traffic and browser-managed downloads. Private tabs and native online typing suggestions pause while enabled. Other apps and external programs opened from downloads remain outside the connection. Harbor is not Tor Browser or a conventional VPN. Full packet-level DNS/WebRTC, background-service, anonymity and fingerprinting audits were not performed.

## Build identity and preservation

- Application file/assembly version: 0.5.2.0; product version: 0.5.2.
- Tested and packaged HarborBrowser.exe SHA-256: BAF5DF1B09C8EA4C77D39F0DBACA1C3467D5061F1E979613892F0A5730EAB42F.
- Installer SHA-256: B07CFEEB213858969A10679B26D90DA9EC78FE5B63689061F7E9C665B6D59F7B.
- Installer size: 358,806,138 bytes (approximately 342.2 MiB).
- Tor/source provenance is recorded in ThirdParty/TOR-PROVENANCE.md and installed Licensing/TOR-PROVENANCE.md.
- Original 0.5.0 installer hash remains AE09E05BF5F14BE53C4A52ABAB9175BE1DBAC6DE0D03206ABCA6396968FE8683.
- Earlier 0.5.2 preview portable hash remains C026DFA2D7FBADE31616750D69DFA70A7AD2439E1A13B3FC34B5C5089FB7658A.

The synced sources directory was not edited. Old release folders/packages were preserved. Final cleanup found no owned Tor helper or fixture registration. The package-audit tool's first launch created an untrusted localhost development certificate; only that newly identified certificate was removed afterward. Browser defaults and system proxy settings were not changed.

## Requires a clean Windows system or user testing

- Real missing-prerequisite installation, elevation, Windows servicing failures, actual reboot/resume and interactive setup/shortcut behavior on a fresh computer.
- Windows 11, other Windows 10 builds, different hardware, actual multiple monitors, DPI/accessibility settings and taskbar configurations. This installer targets Intel/AMD x64 Windows 10 version 1709 or later; 32-bit Windows and ARM64 are excluded.
- Long sessions, website/account-specific behavior, protected streaming/DRM, every download server, security-software interaction and all physical video/taskbar arrangements.
- Real configured Windows VPN authentication, routing and MFA. Those controls were tested with simulated outcomes; no system VPN was connected.
- The complete Tor privacy audits described above.

Source-package rebuild instructions are in SETUP-README.md. Build the app/test projects in Release before running Run-Full-Release-Tests.ps1. Installer fixture scripts and PerformanceTests/Run-Measurements.ps1 are separate. Do not rebuild binaries while a test process is using them. Package checksums are an external sidecar to avoid hashing an archive that contains its own checksum.
