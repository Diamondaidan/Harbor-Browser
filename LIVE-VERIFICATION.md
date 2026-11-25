# Harbor 0.7.1 installed checks and remaining coverage

Updated November 25, 2026 (America/Chicago). The installed RC1 executable was independently verified against SHA-256 4AD1CB6B33A43A1C20E6AB262E04283E4CBB2D626FB74ABCDEBAC342CE410EC4, ProductVersion 0.7.1-rc.1. The owner then confirmed the checks below.

| Installed RC1 check | Result and evidence |
| --- | --- |
| Basic use | User reported that it works. |
| Bookmarks/settings retained | User confirmed both after installation. |
| Proton browser restart | User confirmed reconnection without another sign-in. |
| Physical network recovery | User confirmed the supplied connection off/on check: browsing pauses, protection returns, and the page works after explicit Reload. |
| Sleep/wake | User confirmed the sleep/wake check works. |
| Clean/offline spare-PC installation | Pending; no result received. |

These are manual user-reported results. No independent request counters, route measurements or authentication data were collected for these checks. A general positive result does not establish a complete privacy/leak audit, long-duration reliability or every hardware configuration.

The full 0.7.1 executable is rebuilt with release identity and no RC label, using the same app/helper source as RC1. Its exact files receive new automated verification. The RC1 observations above are not claimed as an installed-full test. Preparation does not close, replace or restart the regular installed browser.

## Public updater testing

Production update-service/transport code checks the real public GitHub feed with the production trust root, downloads the published 0.7.0 installer, pauses it, recreates the updater and resumes at the exact saved offset. HTTP 206, a stable paused partial file, signed total size and SHA-256 are checked. PUBLIC-UPDATER-VERIFICATION.json records the latest run, offset and scope; RELEASE-VERIFICATION.md records the matching full-build checks.

The downloaded public installer is never executed. This test uses a disposable profile with no browser cookies/account credentials and does not change Windows network settings. It is public metadata/download/resume evidence, not a complete installed update or rollback test.

## Automatic coverage

Runtime fixtures inject owned profiles and use generated local websites/files and synthetic signing keys. They cover update signatures, same-version RC-to-full eligibility, parent-process handoff, backups and failure/recovery behavior; title-bar/themes/compact layouts; site loading and verification; Proton startup/request gates and off/on data retention; download filenames/pause/resume; and tab unloading with form/media/download protection. Simulated Windows network/power callbacks are separate from the manual RC1 checks above.

Native installer fixtures use owned folders, unique fixture AppIds and test registry prefixes. They verify original markerless identities, profile canaries, busy-profile/backup failures, uninstall and unchanged browser defaults. Dependency execution/restart cases use safe stubs. Microsoft offline prerequisites are independently hash/size/signature checked before packaging.

## Remaining scope

- Actual installation of the new full build and a complete installed updater/rollback flow with administrator prompts.
- Clean/offline spare-PC installation, including genuinely missing prerequisites and any requested reboot.
- Cold reboot, repeated/long-duration recovery, real monitor/DPI/input combinations and broader website compatibility.
- Protected IPv6 and a complete DNS/background-traffic/anonymity audit. Earlier 0.7.0 RC1 IPv4 results are historical; protected IPv6 was inconclusive then.
- Private tabs retain the explained normal-profile Proton routing limitation.

Historical 0.7.0 RC1 performance/live observations remain preserved in their original release packages. No new 0.7.1 memory/startup benchmark or fresh vulnerability audit is claimed. Windows installers remain unsigned. Preparing local full artifacts does not publish them or change the public stable feed.