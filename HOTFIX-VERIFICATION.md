# Harbor 0.5.5 Proton Trial — Hotfix 2 verification

November 15, 2026. Windows 10 x64, .NET Framework 4.8, WebView2 Runtime 157.0.4341.46. Assembly version 0.5.5.0; file version 0.5.5.2. This is a separate portable preview, not a production installer. Installed Harbor and earlier trial profiles/packages were preserved.

## Reproduced failures

**Startup:** The original Hotfix 1 started in a fresh synthetic profile, then failed after a full process restart. The exception was E_FAIL (0x80004005) from SetPermissionStateAsync in ResetNativePermissions. An isolated inspection identified the rejected entry as Notifications / Allow at Proton's chrome-extension origin. Harbor had incorrectly treated this internal extension grant as a saved website permission. Evidence is in Evidence/Hotfix1-reproduction.

**Spinner:** Two fresh, deeply nested synthetic profile locations showed the real Proton spinner with no sign-in action. The same compiled host and provider package reached the actual sign-in button using a short synthetic profile location and retained its own storage canary across a full restart. The compared profile-root lengths were 198 and 97 characters. This establishes a path-depth trigger in this controlled environment, not a claim that every long Windows path is unsupported. No OS path settings were changed. Evidence is in Evidence/Profile-depth-comparison.

These reproductions used empty signed-out profiles. No live TrialData, credentials, real authorization state or storage collection was inspected, copied, cleared or replayed.

## Corrections

- Native permission cleanup applies only to HTTP(S) website origins. Internal extension/engine grants are preserved. Every real website reset is still awaited and reset failures still propagate; no blanket exception suppression was added. Harbor's default-deny and per-site ask/allow/block policy is unchanged.
- Default Hotfix 2 data is stored at `%LOCALAPPDATA%\HarborTrials\055H2`, with Profile and Downloads subfolders. It is independent of the extraction directory and all earlier TrialData folders. All Hotfix 2 copies share this per-user root and its profile lock; it remains after deleting the extracted application. No old login profile is migrated automatically.
- Opening/reloading Proton controls prepares the provider in the correct profile. A valid current registration is reused. Moving the application can require registration at its new package path.
- Readiness requires an actual visible, enabled sign-in or logged-in action with no spinner. An in-script exact provider-origin guard prevents reading account-page DOM after a navigation race. No authentication, proxy or runtime state is queried by this readiness check.
- Native navigation and rendering waits are bounded. Errors identify the failed load and leave Reload available. Explicit retry replaces a failed controller; a healthy controller remains in place. Late completion cannot overwrite a newer load.
- Route checks require usable provider controls and recheck readiness before running. Account pages and stale/spinning views cannot initiate a route check.
- Hotfix 1's original NewWindow binding, opener/name/session-storage/POST handling, transient account-page policy and sleep protection remain included.

All 336 bundled Proton package files are byte-for-byte unchanged from Hotfix 1. The provider's permissions, account validation, cryptography, server responses and scripts were not patched or bypassed.

## Final executed suites

| Suite | Passed assertions |
| --- | ---: |
| Regression/services/settings/offline WPF layout | 384 |
| Ordinary browser/media/download/privacy/recovery | 360 |
| Address and Home suggestion clicks | 17 |
| Startup recovery and version consistency | 15 |
| Signed-out Proton controls, removal and account privacy | 30 |
| Synthetic native popup/context/POST/storage/sleep | 27 |
| Synthetic Proton pending-state storage continuity | 24 |
| New cold-restart and loading/retry recovery | 45 |
| **Total** | **902** |

These are assertions, not 902 independent user workflows or a privacy certification. Logs and exact counts are in Evidence/Final-suites. Initial development failures and repeated diagnostic runs are excluded from this total.

The 45 new checks include an enable process followed by two fully separate process/engine restarts; actual Home and signed-out controls; preserved Proton Notifications grant; reset of a seeded HTTP(S) website Geolocation grant; continuity of one explicitly named synthetic extension-local canary; failed-controller replacement; failed-provider restoration retry; a real owned DOM fixture with a visible spinner and footer links; bounded failure/reload; refusal to probe a different origin; stale-ready route refusal; and late-probe completion handling.

An additional smoke check loaded the exact staged application and provider assets from the deeply located portable directory through the test host, using an isolated short profile. Enable and full restart passed all 20 recorded boolean checks, including Home, actual sign-in, no spinner, permission separation, storage continuity and clean engine shutdown. These repeats are excluded from 902. The temporary host was removed; the distributed ZIP contains only the portable application. Evidence is in Evidence/Staged-portable.

Final application bytes match the main, regression, integration and portable copies. The portable ZIP passed 7-Zip integrity testing and contains no used profile, Tor runtime or test executable. Build/restore finished successfully; no package dependencies were changed. Final application/portable/source SHA-256 values are in HOTFIX-HASHES.txt.

## Remaining live checks

Open the Hotfix 2 window, enable Proton, and start a fresh sign-in from its controls. The user must complete account authentication, connect, and choose Check browser route. Do not reuse an earlier authorization/error tab or share profile files, passwords or codes.

Completed real sign-in (including whether the earlier Invalid state error is resolved), authenticated browser-only proxy operation, connected-Proton performance, route-failure behavior and a complete DNS/WebRTC/background-traffic audit remain unverified. Enabled alone still does not mean connected or protected. Route comparison is limited manual evidence, not a kill switch or privacy guarantee. No installer/clean-install/reboot test was performed for this portable hotfix. Older release and benchmark documents are historical and are not new Hotfix 2 results.

Reference: Microsoft's SetPermissionStateAsync documentation explains that it resets a permission for a specified origin: https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile.setpermissionstateasync?view=webview2-dotnet-1.0.4314.35 . The exact extension-origin E_FAIL above was reproduced locally, rather than inferred from that documentation.
