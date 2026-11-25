# Harbor Browser 0.5.4 - release verification

November 14, 2026. Tested on Windows 10 Home x64 build 19045, WebView2 157.0.4341.46, .NET Framework 4.8, Intel Core i7-7700HQ, 8 logical processors and 12 GiB memory. Tests used isolated profiles, downloads, application instances and installer identities. The installed browser, personal profile, browser defaults and Windows proxy configuration were preserved.

## Final selected suites

| Suite | Result |
| --- | ---: |
| Regression/services/settings/offline layouts | 384 passed |
| Complete running browser/UI/media/download/privacy/recovery | 360 passed |
| Focused address/Home suggestion clicks | 17 passed |
| Staged Home page in actual WebView2 | 39 passed |
| Live Tor connection, download, failure and reversal | 20 passed |
| Module integrity plus real Tor restart/crash lifecycle | 26 passed |
| Live W3C/Wikipedia/Example Domain navigation and icons | 6 passed; none unverified |
| Startup recovery, cancellation, isolation and versions | 15 passed |
| Native caption attributes and compact toolbar | 34 passed |
| Tor monitoring, faults, recovery, disconnect and disposal | 28 passed |
| Bounded SOCKS hostname and local WebRTC/STUN controls | 9 passed |
| Isolated 0.5.3-to-0.5.4 installer upgrade/uninstall | 26 passed |
| Simulated prerequisite skip/success/failure/restart | 41 passed |
| Isolated shortcuts and cleanup | 23 passed |
| **Total final selected assertions** | **1,028 passed** |
| Four additional deep-profile suggestion/private replays | 68 repeated assertions; all passed |
| Performance scenarios | 10 passed; 90 samples; see PERFORMANCE-0.5.4.md |
| Builds | App and four test projects; zero errors |
| Current direct/transitive NuGet advisory listing | Completed; no vulnerable packages reported by configured sources |

Counts are assertions, not 1,028 independent workflows. Development attempts and repeated assertions are excluded from the total. The deep-profile replays used WebView2 roots 183 characters long and the final packaged binary. Restricted restores initially warned that the advisory feed was unavailable; the later network-enabled advisory listing completed. This is not a native-code vulnerability audit.

## Native dark title bars

A shared service applies Harbor's selected appearance to native captions on Harbor-owned WPF windows. Actual DWM attributes were read back for History, Bookmarks, Settings, Bookmark editor, Resource monitor, Clear browsing data, Site permissions, Site modules, Module site access and Tor controls. Each was checked dark, light and dark again while open. Settings' local preview resources are respected. Main window custom chrome is retained. System-owned file pickers and message boxes use Windows' own controls.

The toolbar indicator was also checked at widths 480, 600 and 1366. All 34 focused native-attribute/toolbar assertions passed. These are native API/layout checks, not pixel-level visual verification. Desktop capture failed twice with `window capture timed out: timed out waiting on channel`, including a fresh window selection and retry. No desktop screenshot pass is claimed. Review the actual title-bar appearance on the target PC; Windows 11 caption/text color attributes were not exercised on this Windows 10 host. Older Previews images are historical component/browser captures and omit native title bars.

## Tor monitoring and recovery

Tor now separates enabled intent, bootstrap, verified browser route and last successful check. The routing worker reads back actual SOCKS endpoint/ownership/bypass settings, WebRTC IP policy and disabled prediction. A hidden controller in the same normal profile verifies the raw response to a nonce-bearing HTTPS Tor Project API request, including HTTP status and a strict boolean IsTor. Rendered page text is not used as the health proof.

The toolbar displays off, connecting, checking, verified or failed. Checks run at connection time, every five minutes while enabled, and after network/wake notifications. An ordinary periodic check keeps an already verified route active while pending. Network changes and failures invalidate verification. New managed HTTP/HTTPS page requests pause until verification or explicit Disconnect and restore. Local Home stays available. Only a recorded cancelled top-level navigation is automatically resumed; failed subresources may require a reload.

The focused live tests exercised actual connection and recheck, non-Tor fault injection, missing/disabled routing, a native 503 response, cancelled navigation, reconnect-before-resume, simulated network loss/return and suspend/resume, actual helper termination/reconnect, prompt cancellation on Disconnect, rejection of late positive results, disposal, helper cleanup, direct-route restoration and unchanged Windows proxy settings. Simulated notifications do not prove physical adapter switching or real sleep/wake on every computer.

Development testing caught a reconnect stall caused by re-adding an existing extension; setup now reuses/re-enables it. A prior lifecycle test caught cancelled lifetime state preventing later explicit cleanup after disposal. Removal now has its own bounded cleanup path, including late-registration cleanup after explicit removal, while disposed services cannot enable routing again. Final suites exercise both corrected paths.

## Bounded privacy probes and limits

A fresh browser profile used Harbor's actual routing extension with a local SOCKS5 fixture. A unique .invalid hostname reached SOCKS as a domain and its fixture response rendered successfully. This demonstrates hostname forwarding for that request; it does not prove the absence of all local DNS traffic.

A secure-context RTCPeerConnection gathered candidates without requesting camera/microphone access. Protected gathering sent no UDP to the local STUN listener. After removing the extension, the direct positive control reached that same listener. Evidence records packet counts and candidate types, without candidate addresses. These nine assertions are bounded local controls, not a complete WebRTC/DNS leak audit.

With the routing extension deliberately disabled, a cancelled local navigation opened a speculative TCP connection before the navigation guard acted; no HTTP bytes were observed during the short local observation (200 ms plus up to 500 ms per accepted connection). The guard is not a packet-level firewall. Existing sockets, all Chromium background services, fingerprinting, DNS prefetch initiated by pages and anonymity were not certified. Tor remains experimental, off by default and limited to normal Harbor page traffic/browser-managed downloads. Private tabs and native online typing suggestions remain paused while enabled. This is not Tor Browser or a conventional VPN.

## Functional and installer coverage

The full browser suite covers filename/extension recovery (including signature filenames), unsafe/duplicate names, interruption/cancel/resume, private transfers, history/storage isolation, site permissions, modules and grants/integrity, tab/bookmark ordering, resource monitoring, menus, appearance customization, responsive layouts, session/crash/startup recovery, video fullscreen and picture in picture. Monitor/fullscreen cases use isolated layout fixtures and do not replace physical multi-monitor/video checks. The live Tor .asc download retained its full name and PGP signature text; detached signature verification was not performed.

An initial full-suite attempt mistook cancellation of a new private tab's initial Home load for the module fixture's navigation. The test now matches the expected target/navigation ID. Initial failures are retained separately; only final successful selected runs are counted.

Setup contains signed offline .NET Framework 4.8 and WebView2 x64 packages, optional Tor 0.4.9.16, component notices and matching source. Cached vendor packages passed size and Microsoft-signature checks. Setup requires WebView2 157 or later; the included package supplies 157.0.4341.46. Target is Intel/AMD Windows x64; administrator access is required. Harbor and its installer remain unsigned.

Isolated Inno variants exercise actual file copying, 0.5.3-to-0.5.4 upgrade, core-profile backup/preservation, browser registration, shortcuts and uninstall. All 35 installed payload files matched their staged hashes. Test registrations, links and install folders were removed without changing default-browser choices. Production setup was compiled but not installed under its production identity on this host.

Prerequisite cases use harmless stand-ins instead of running Microsoft installers. They cover existing-component skips, success, both restart-required (3010) paths, both failure (1603) paths, silent arguments, runtime-version comparisons and setup logs. These simulations do not establish genuine missing-prerequisite installation or reboot success.

## Remaining checks requiring another Windows environment

- Install this exact 0.5.4 setup on the spare PC and inspect native title bars, toolbar layout, downloads and real sleep/network recovery.
- In a disposable clean Windows environment, test genuinely missing prerequisites, offline setup and an actual requested restart. Shared Windows components were not removed from this working computer to force those conditions.
- Exercise Windows 11, additional display/scaling/GPU/audio/input configurations, multi-monitor fullscreen/taskbar behavior and prolonged sessions.
- Complete packet-level DNS/WebRTC/background-traffic and independent security/anonymity reviews before expanding privacy claims.

The previously supplied spare-PC results apply to 0.5.2; they are not 0.5.4 physical-system results. See LAPTOP-CHECKS.md for the existing user checklist. The earlier 0.5.3 installer/source hashes were rechecked and remain unchanged.

## Primary implementation references

- [Microsoft DWM window attributes](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)
- [Chrome extension privacy settings and limits](https://developer.chrome.com/docs/extensions/reference/api/privacy)
- [Microsoft network availability notifications](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkchange.networkavailabilitychanged)
- [Microsoft power-mode notifications](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.powermodechanged)

## Build identity

- Assembly/file version: 0.5.4.0; product version: 0.5.4.
- Final tested and staged application SHA-256: 83EA457909C821074D301F1B70854843464ED1378D43D84671B37C8F9A808E40.
- Final installer SHA-256: 6BBD6069AE8ECCB3CE6886B081739EB34798711A5033F55BAB79E42484EE6AB7.
- Installer size: 358,812,070 bytes.
- Source/installer/application hashes are recorded externally in PACKAGE-HASHES.txt to avoid a source archive self-hash cycle.

Evidence in the source ZIP includes final suite logs/counts, earlier failed attempts, build logs, payload hashes, the desktop-capture failure and bounded privacy results. No browser profiles, caches or download folders are included.
