# Harbor Browser 0.6.1 Proton off/on verification

Passed **1389 assertions across 18 suites** against matching app/helper copies.

App SHA-256: 324C32D3CD1366AE49220FEC3A8E7397FAA2E96E02292D4868781C405FEF3827
Update launcher SHA-256: 3828316155D7116A3CB22BE54A3036FC4FC4A235817A2715E88C6A0FB6E0E7D2

| Suite | Assertions | Passed |
| --- | ---: | --- |
| regression | 384 | True |
| full | 360 | True |
| suggestions | 17 | True |
| recovery | 15 | True |
| proton | 34 | True |
| auth-handoff | 27 | True |
| proton-pending | 24 | True |
| proton-hotfix2 | 45 | True |
| proton-beta | 109 | True |
| proton-fallback | 19 | True |
| proton-guard-handoff | 12 | True |
| proton-r4 | 34 | True |
| release057 | 9 | True |
| updates | 120 | True |
| updates-ui | 8 | True |
| environment061 | 67 | True |
| ui061 | 46 | True |
| proton-off061 | 59 | True |

The new 59-check off/on suite verifies that both ordinary off actions disable the native provider without uninstalling it, restore actual local HTTP browsing only after explicit Reload, and preserve a synthetic extension-local marker through off/on cycles and full browser/engine restart. It covers persistence failures, superseded operations, delayed native registration, explicit reset and retained website cookies. See PROTON-OFF-0.6.1-VERIFICATION.md.

All listed results are current for this executable. Existing coverage includes browsing, downloads/filenames, tabs, media, bookmarks, settings, light/dark captions, simulated size/scaling, private profiles, account-handoff fixtures, request protection, network/clock recovery and updater integrity/routing. The 46-check UI suite still passes with the new normal off button. Off, enabled and provider renderer captures were visually inspected.

One initial R4 local route-comparison request timed out after three successful comparisons. Its log is retained under Evidence/ProtonOff/initial-route-timeout.log. A fresh complete 34-check R4 run passed without application changes or a relaxed timeout. An older pending-state test was updated to use the explicit Remove action for cleanup rather than ordinary Off.

No real account was signed in during these tests. Synthetic extension data retention does not guarantee validity of a live Proton session; an owner-run off/on and restart check remains. Physical monitor/DPI/input, Wi-Fi/sleep, actual clean/offline prerequisites/UAC/reboot and hosted update/rollback are separate manual checks. The retained performance baseline identifies its preceding executable and was not rerun for these corrections.
