# Harbor Browser 0.5.7 application verification

Passed 1089 assertions across 13 suites against the regular release executable.
App SHA-256: 349A49B8B835C9F6BE206312EE19BDDF859EEDFB1360FDEAB7373C9ACB4A122B

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

Native WebView2 tests use owned synthetic profiles. Coverage includes browsing/download naming, suggestions, tabs, settings, recovery, proxy fallback and actual owned browser-engine crash recovery. No real account credentials were used in automated tests.

Live candidate results are recorded separately in LIVE-VERIFICATION.md. Identity/label changes were checked here; actual production installation and final manual smoke checks are distinct. Windows 10 build 19045, WebView2 157.0.4341.46. A pass is not certification of every Windows system or a complete leak audit.
