# Harbor Browser 0.6.2 release verification

Passed **1461 assertions across 19 suites** against matching app/helper copies.

App SHA-256: EFE5565D4A6F13E260E5AF953D2EE18F2A53912C3EEE771B857EC5815A5F87BC
Update launcher SHA-256: 5064FA58D5BB6CED7EBDA204679C1E0D276D6470B8A81A2DD121F84E1387386D

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
| site062 | 72 | True |

The new 72-check site suite covers challenge/refusal classification, stale responses, report redaction, bounded observation, native HTTPS helper/return/reload behavior, retained document content, responsive notices, themed details windows, native Tor-block classification, protected private-tab refusal and an accurate private label before first navigation. Private tabs remain unavailable with browser-only Proton/Tor enabled; Off permits native InPrivate on the regular connection. See SITE-0.6.2-VERIFICATION.md for implementation scope, the private-route probe, initial failures/corrections and limits.

Other coverage includes actual local browsing/downloads/filename handling, tabs, media, bookmarks, settings, title bars, simulated sizes/scaling, private cleanup, synthetic account handoff, request guards, clock/network recovery, off/on retention and updater integrity. Native tests use owned profiles and synthetic canaries, not the installed user profile or account tokens. Generated light/dark report and notice renders were visually inspected.

Initial R4 fixture runs intermittently timed out; fresh full checks passed without relaxing timeouts or changing the production verifier. The startup privacy-label race was corrected before this final receipt. Earlier logs remain in Evidence/062-initial. NuGet vulnerability metadata was unavailable (NU1900); a fresh online dependency-vulnerability scan is not established. All 336 pinned provider hashes passed.

Still manual: real website verification through signed-in Proton, live account/session off/on/restart validity, actual clean/offline spare-PC setup with missing Microsoft prerequisites/UAC/reboot, physical network/sleep/DPI/input and hosted GitHub update/rollback. The retained older performance baseline is historical; no new speed/memory gain or complete privacy audit is claimed. Windows packages remain intentionally unsigned. No publication or production installation was performed.
