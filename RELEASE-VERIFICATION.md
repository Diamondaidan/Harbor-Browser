# Harbor Browser 0.6.0 application verification

Passed 1217 assertions across 15 suites.
App SHA-256: 5CAC10A130A1E026DC593E86E8FA96998124BFED9FE7A1B2E8F0CC804122146B
Update launcher SHA-256: 03C09A9EA33414079DC952F854C4351E8CABE1DF802C6DE6C3A370CE283DBB2D

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

The updater suite covers signature/expiry/sequence checks, package bounds and hashes, interrupted and changed downloads, strong-ETag resume, real loopback HTTP redirects/range headers, synthetic snapshots, native process identity/exit handoff, installer read locks, backup failure and record expiry during shutdown. Light/dark compact layouts were rendered offline. Native menu integration confirms enabling Proton cancels update traffic and prevents a paused updater from issuing a request.

Full run: T/1116204532. Windows 10 build 19045, WebView2 157.0.4341.46. Matching app/helper copies were checked across all three runners. No real account credentials were used.

See INSTALLER-VERIFICATION.md and LIVE-VERIFICATION.md for separate native installation scope and outstanding manual/live checks. Public update hosting and Windows installer signing have not been performed.
