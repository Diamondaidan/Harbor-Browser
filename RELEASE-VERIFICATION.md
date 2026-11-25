# Harbor 0.7.1 RC1 automated release verification

Application SHA-256: 4AD1CB6B33A43A1C20E6AB262E04283E4CBB2D626FB74ABCDEBAC342CE410EC4
Update launcher SHA-256: ADCFB10124EA75BFEB2DB167647F6CD47E16A5982358255169667A221E4F1CA2

All 1656 assertions across 22 suites passed on November 25, 2026 against matching 0.7.1 RC1 app/helper copies.

| Suite | Assertions |
| --- | ---: |
| regression | 384 |
| full | 360 |
| suggestions | 17 |
| recovery | 15 |
| proton | 34 |
| auth-handoff | 27 |
| proton-pending | 24 |
| proton-hotfix2 | 45 |
| proton-beta | 109 |
| proton-fallback | 19 |
| proton-guard-handoff | 12 |
| proton-r4 | 34 |
| release057 | 9 |
| updates | 164 |
| updates-ui | 8 |
| environment061 | 69 |
| ui061 | 94 |
| proton-off061 | 59 |
| site062 | 72 |
| route063 | 36 |
| downloads063 | 17 |
| unloading070 | 48 |

The update suite includes build parsing, actual RC/stable executable resources, same-version handoff rules, full-release availability in the updater, read-only profile behavior and network gating. Existing download/recovery, themed UI, site loading, Proton lifecycle and unloading checks remain included.

Native installer fixtures passed 296 checks, including all 25 pinned original application identities. The altered-file case is refused without replacement. Vendor prerequisites are stubbed; real profile data and Windows defaults are preserved. Full staged-payload installation and smoke results are recorded separately after staging in PACKAGED-PAYLOAD-VERIFICATION.json.

All 336 official Proton asset sizes/hashes remain unchanged. No new vulnerability audit or startup/memory benchmark is claimed. Historical measurements are in PERFORMANCE-0.7.0.md.

Nine live public updater checks verified signed metadata, a stable paused partial file, exact HTTP 206 resume after service recreation and final signed file size/SHA256. The downloaded installer was never run. PUBLIC-UPDATER-VERIFICATION.json records the public facts without credentials or real profile paths.

All runtime fixtures use owned injected profiles. Real installed 0.7.1 upgrade, real-account Proton checks, physical network/sleep/wake/reboot/monitor checks and clean/offline spare-PC setup remain manual. LIVE-VERIFICATION.md distinguishes historical 0.7.0 RC1 observations. Local packaging does not publish or activate 0.7.1.
