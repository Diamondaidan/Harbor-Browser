# Harbor Browser 0.6.1 application verification

Passed **1284 assertions across 16 suites** against matching app/helper copies.

App SHA-256: 87155D68CFD41C49B927518A2635D11AA4BB06299763612CFF5E1285BE31E136
Update launcher SHA-256: BD0A5F9C0266534600A6893E3C386146E755C84B16184035E7F5035F583456AD

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

The new 67-check suite covers injected UTC/elapsed clocks and synthetic network lost/returned, address change, suspend and resume callbacks. It holds the interface queue busy, verifies immediate request denial and stale-proof rejection, checks obsolete-work cancellation and cooldown reset, counts blocked requests at a local server, then requires explicit Reload after fresh evidence. No Windows network, power or clock setting was changed.

The prior 15 suites were run against this candidate or reused only from the same matching executable after successful builds. They include real WebView2 local browsing, downloads, media, private profiles, account-handoff fixtures, request gates and updater/native helper fixtures. No account sign-in or live protected-route claim is included.

The full suite passed 10 simulated size/scaling cases, including 1366 x 768 at 100/125/150/175/200 percent, plus light/dark menu/dialog checks. Header renders at 100 and 200 percent were visually inspected. These are offscreen WPF layout checks, not physical-monitor certification.

Installer fixtures and performance measurements are reported separately. See DEVELOPMENT-0.6.1.md and LIVE-VERIFICATION.md for remaining physical, provider and public update checks.
