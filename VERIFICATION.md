# Harbor Browser 0.5.3 - release verification

November 14, 2026. Built and tested on Windows 10 Home x64 build 19045 with WebView2 157.0.4341.46, an Intel Core i7-7700HQ, 8 logical processors and 12 GiB memory. Browser tests used isolated profiles/download folders and owned test processes. The installed Harbor, personal profile, browser defaults and Windows proxy settings were preserved.

## Final selected suites

| Suite | Result |
| --- | --- |
| Regression/services/settings/offline WPF layouts | 384 passed |
| Running browser/UI/media/download/privacy/recovery | 360 passed |
| Focused address/Home suggestion clicks | 17 passed |
| Staged Home page in actual WebView2 | 39 passed, including the current version footer |
| Live Tor connection, download, failure and reversal | 20 passed |
| Module integrity plus real Tor restart/crash lifecycle | 26 passed |
| W3C, Wikipedia and Example Domain navigation/icon checks | 6 passed; no unverified sites |
| New startup recovery, cancellation, late completion, isolation and version checks | 15 passed |
| Isolated 0.5.2 to 0.5.3 installer upgrade/uninstall | 26 passed |
| Simulated prerequisite skip/success/failure/restart and setup-log checks | 41 passed |
| Isolated Start menu/desktop shortcut behavior and cleanup | 23 passed |
| Total across the final selected suites | **957 successful assertions** |
| Four additional deep-profile suggestion/private-tab replays | All passed; 68 repeated assertions, counted separately |
| Performance scenarios | 10 passed; 90 samples; see PERFORMANCE-0.5.3.md |
| Builds | Release app and all four test projects built with zero errors |
| Current NuGet advisory listing | Completed; no vulnerable packages reported by the configured sources |

These are assertion counts, not 957 independent workflows. Repeated development runs are not included in the total. Evidence in the source package includes final logs, counts, file hashes and performance measurements. Some initial restricted restores warned that the advisory feed was unavailable; the later network-enabled advisory check completed successfully. This is not a native-code security audit.

## Private-tab startup and recovery

The 0.5.2 follow-up reproduced a private controller startup stall with a WebView2 data root 181 characters long. In this release, the private profile name is a compact prefix plus a full random GUID, reducing its path length without moving the normal profile or weakening session uniqueness. Four final fresh-profile replays used data roots 183 characters long and passed all 17 checks each. This demonstrates the fix for the reproduced condition; it does not promise unlimited path-length support.

The suggestion test now waits for the private Home document to finish loading before observing the user's click navigation. Previously, controller initialization could be true while the initial document was still about:blank. This removes a race in that test. The original 0.5.2 unexpected navigation URL was not recorded, so its precise historical value cannot be reconstructed.

Normal startup steps have 45-second deadlines; Tor connection preparation has a separate three-minute allowance. A failed/timed-out tab displays a Reload recovery action, rather than indefinitely waiting or presenting a startup modal. The failed controller is disposed and replaced by an uninitialized placeholder so toolbar/resource queries remain safe. Retry creates a fresh controller. A cancelled or replaced view cannot apply late startup completion to the new tab.

Fault-injection tests verified timeout-to-recovery, Reload producing a usable private controller, late completion leaving the replacement intact, closing a never-started private tab without recreating its failed controller, fresh-session identity/storage isolation, and continued use of a normal tab. The test timeout is shortened only through an internal test hook; the shipping default remains 45 seconds per normal step.

Private sessions that did initialize still receive their existing data cleanup. A session whose controller never started has never navigated; cleanup skips recreating that controller. Hidden profile-controller access and private data clearing also have bounded waits. Failed cleanup still prevents a clean shutdown journal. No silent direct-routing fallback, shared normal/private cookies or relaxed extension permissions was added.

## Tabs and version consistency

Queued new-tab scrolling now checks that its tab is still selected before moving the strip. The overflow-tab test checks actual header visibility rather than relying on 80/100-millisecond sleeps. An initial full-suite scroll assertion failed before that correction; full-initial-scroll-timing053.log is retained. The final complete suite passed both overflow-selection checks.

VERSION.txt is the release version source for the project and builder. Home receives the running assembly version through its trusted state message. About, the status bar, branding and module compatibility use the same assembly version. Direct Inno compilation reads VERSION.txt unless a version is explicitly supplied; the standard builder rejects a version that conflicts with VERSION.txt. The obsolete 0.5.0 Home footer is gone. Home/browser and version checks passed with 0.5.3.

Test projects exclude generated TestResults files. This fixes a build problem where a generated prerequisite test helper's C# entry point was inadvertently compiled into the next integration-test build.

## Functional and installer coverage

The complete suite exercised download filename/extension recovery, unsafe/duplicate names, cancel/resume, private transfers, stored state, normal/private history and storage, site permissions, module grants/integrity, session recovery, resource monitoring, tab/bookmark organization, menus, light/dark appearance, colors and responsive layouts. Browser fullscreen/work-area behavior, video fullscreen and picture in picture were exercised in isolated monitor/layout fixtures. A live Tor .asc download retained its expected complete name and contained PGP signature text; detached-signature verification was not performed.

Setup includes the app, Microsoft-signed offline .NET Framework 4.8 and WebView2 x64 standalone packages, the optional Tor client, its matching source and original component notices. WebView2 157 or later is required; the included runtime is 157.0.4341.46. The normal production upgrade identity and %LOCALAPPDATA%\HarborBrowser profile remain unchanged. Setup snapshots core profile metadata before upgrade and preserves profile data on uninstall.

Installer variants exercised actual Inno file copying, registration, backups, shortcuts and uninstall in isolated folders and per-user fixture registrations. Every installed payload file matched its staged hash. Upgrade preserved settings, bookmarks, module permissions, startup state and saved Tor choice. Shortcut targets, working folders, versioned Beacon icons and application identity were checked, with the optional desktop task both off and on. Fixture uninstall removed its links/registration and preserved browser defaults.

Prerequisite tests ran harmless stand-ins instead of the actual Microsoft installers. They checked existing-prerequisite skips, zero exits, both 3010 restart paths, both 1603 abort paths, silent arguments, native runtime-version comparisons and log contents. The new setup log records detected versions, skipped/run helpers, exit codes and restart requests. These simulations do not substitute for genuine clean-Windows installation or a real reboot.

The production installer compiled successfully, but was not run under its production identity on this host. No system prerequisite was uninstalled or replaced to force a test. Harbor and its installer remain unsigned; Microsoft signatures on the bundled prerequisites do not sign Harbor.

## Tor scope

Actual browser checks verified Tor's IsTor response, a separate direct route, the expected exit-address difference, helper-failure blocking, disconnect restoration, requested-state restart, normal shutdown, forced termination and crash recovery. No owned helper remained after the tests. Native online suggestions and private tabs remain paused while Tor is enabled.

Tor remains optional, off by default and experimental. It covers normal Harbor page traffic and browser-managed downloads, not other apps or external programs opened from downloads. This is not Tor Browser or a conventional VPN. Complete packet-level DNS/WebRTC, background-service, fingerprinting and anonymity audits were not performed.

## Build identity

- Application file/assembly version: 0.5.3.0; product version: 0.5.3.
- Final tested/staged application SHA-256: BF36D03382CA42EF331F400768DE354DA88928EABDD5AD7073372E48E6121EF6.
- Final installer SHA-256: EBFFA135C1B8B766B5EEDAC9F1FDF23D237AA1CD55107907D0F39321A41AD408.
- Installer size: 358,790,945 bytes, approximately 342.2 MiB.
- Both offline Microsoft packages passed the builder's size and Authenticode publisher checks.
- Test-project copies of HarborBrowser.exe match the staged application hash. The source ZIP is checked separately after creation; PACKAGE-HASHES.txt is external to avoid a self-referential archive hash.

The original 0.5.2 installer/source packages and synced sources were preserved. Earlier 0.5.2 spare-PC reports remain in that release folder; they are not counted as physical tests of 0.5.3.

## Remaining external validation

- A disposable Windows environment genuinely missing a prerequisite, offline installation, elevation/servicing errors and an actual installer-requested restart.
- Physical mouse/trackpad, screen/DPI/taskbar/video checks of this 0.5.3 build on the spare PC, plus Windows 11 and other hardware. Native screenshot/pointer automation was unavailable in the earlier follow-up; this release does not claim a successful manual desktop pass from that tool.
- The spare PC's exact downloaded filename, previously marked Unknown, remains a separate manual result. Automated local and live Tor filename checks passed.
- Long sessions, protected streaming/DRM, account/site-specific behavior, security-software interaction, real configured Windows VPN authentication/routing/MFA, and the Tor audits above.

This installer targets Intel/AMD x64 Windows 10 version 1709 or later and Windows 11 x64; 32-bit Windows and ARM64 are excluded. Source build and test instructions are in SETUP-README.md and the supplied test scripts. Build all test projects before starting tests; do not rebuild loaded binaries during a run.
