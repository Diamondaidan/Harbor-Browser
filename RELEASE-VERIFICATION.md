# Harbor 0.7.0 automated release verification

Application SHA-256: 4D68688B270E3A5CBE653C280CA66E62905F7231FDB994FC54ADF76338C65883
Update launcher SHA-256: 8AB7D82592438F50FCFCA541ECAB87824E17BF18F339A4F86A0FB6ECBF425DCB

All 1,564 assertions across 22 suites passed against matching application/helper copies. The unloading suite contributes 48 native checks, including unloading from sleep, actual disposal/reload, form/frame/media/download protection, lifecycle races, settings, resource controls and deferred restoration.

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
| updates | 120 |
| updates-ui | 8 |
| environment061 | 69 |
| ui061 | 46 |
| proton-off061 | 59 |
| site062 | 72 |
| route063 | 36 |
| downloads063 | 17 |
| unloading070 | 48 |

The first broad run hit a timing-sensitive existing one-second simulated lease check. It passed in isolation; the fixture now controls both UTC and monotonic test clocks and passed again. The application lease/timeouts were not changed.

Native installer core fixtures passed 94 checks. Full staged-payload installation and smoke evidence is recorded separately after staging in PACKAGED-PAYLOAD-VERIFICATION.json. Microsoft prerequisite packages are hash/signature verified; vendor installers are stubbed in automatic fixture flows.

All 336 official Proton asset sizes/hashes remain unchanged. The NuGet vulnerability feed was unreachable (NU1900), so this is not a fresh online vulnerability audit.

Performance results and scope are in PERFORMANCE-0.7.0.md. These tests use owned injected profiles and synthetic state, not the installed profile or a signed-in provider account. Installed 0.7.0 live-account, physical network/sleep/wake/monitor and clean/offline spare-PC checks remain pending. No public release, update feed activation or real installed upgrade is performed by local checks.

Additional request-counter tests verify that selecting an unloaded tab while Proton is unverified sends no requests, fresh synthetic proof does not replay the address, and explicit Retry restores the recreated page. The suspected stuck-view path did not reproduce under this normal lifecycle; no product change was needed.
