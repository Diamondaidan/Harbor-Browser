# Harbor Browser 0.6.3 release verification

Passed **1516 assertions across 21 suites** against matching app/helper copies.

App SHA-256: 103840485A9B49B361B632A22BDD8CF3747AFAB4A77D05DEABE7E2B528D097C1
Update launcher SHA-256: 113A4D459E6BDF98FA61421ED29F27982B34D73CE142E9E4C6C02A8ED4145312

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
| environment061 | 69 | True |
| ui061 | 46 | True |
| proton-off061 | 59 | True |
| site062 | 72 | True |
| route063 | 36 | True |
| downloads063 | 17 | True |

The new route suite performs thirty immediate comparisons in one existing verifier controller, checks original response bytes despite controlled page changes, rejects malformed/oversized replies and redirects, cancels failed comparison halves, and recovers after cancellation. Native destination counters verify redirect denial. Offline adapter state cannot accept a proof or start a manual check, and network return schedules fresh verification. No timeout was lengthened.

The download suite checks generated ZIP bytes, protection-blocked and duplicate Resume, original row/file retention, explicitly injected independent native private sessions, cleanup and recovery explanations. The existing network/wake suite now includes rejection of proof until an unavailable adapter returns. Other suites cover browsing, media, filename handling, tabs, bookmarks, settings, appearance, privacy, account-handoff fixtures, request guards, off/on retention, startup/engine recovery and updater integrity.

Baseline and intermediate test failures are documented in RELIABILITY-0.6.3-VERIFICATION.md. Only the final app/helper hashes above are represented by this table. NuGet vulnerability metadata remained unavailable in the build logs (NU1900); no fresh online dependency-vulnerability scan is established. All 336 pinned provider asset hashes passed. Generated light/dark downloads and diagnostic layouts were inspected as offline renders.

Still manual: live Proton off/on/restart and real site verification; clean/offline spare-PC setup with actual missing Microsoft prerequisites/UAC/reboot; physical network/sleep/DPI/input and broader hardware; hosted GitHub update/rollback after approved publication. Private tabs remain blocked with browser-only protection enabled. No complete leak audit or whole-browser performance improvement is claimed. Windows packages are intentionally unsigned. No installed user profile or account tokens were used, and nothing was published.
