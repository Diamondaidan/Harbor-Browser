# Harbor 0.7.0 automated release verification

Application SHA-256: 2E955A82B7A015BD277EF53B89B1EE61BBCCD87530DAD06E0E57D37223F2819B
Update launcher SHA-256: 7B43E61623BC4500B6E1C88AD8A265A582DDD0E19BB8472F742D9C5A8020D047

All 1,612 assertions across 22 suites passed against matching final application/helper copies on November 24, 2026 (America/Chicago). This includes 48 new VPN label layout checks across four statuses, six logical widths and both themes. The unloading suite contributes 48 native checks covering actual disposal/reload, form/frame/media/download protection, lifecycle races, settings, resource controls and deferred restoration.

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
| ui061 | 94 |
| proton-off061 | 59 |
| site062 | 72 |
| route063 | 36 |
| downloads063 | 17 |
| unloading070 | 48 |

The final full matrix passed without failed suites. The preserved RC1 fixture correction controls UTC and monotonic clocks in its synthetic lease test; application lease/timeouts were not changed.

Native installer core fixtures passed 94 checks. Full staged-payload installation and smoke evidence is recorded separately after staging in PACKAGED-PAYLOAD-VERIFICATION.json. Microsoft prerequisite packages are hash/signature verified; vendor installers are stubbed in automatic fixture flows.

All 336 official Proton asset sizes/hashes remain unchanged. Restore/build completed; no separate fresh online vulnerability audit is claimed.

Performance results for RC1 and their scope are in PERFORMANCE-0.7.0.md. These automatic tests use owned injected profiles and synthetic state, not the installed profile or a signed-in provider account. Installed RC1 upgrade, Proton lifecycle, unloading and download results are in LIVE-VERIFICATION.md. Final installed upgrade, physical network/sleep/wake/monitor and clean/offline spare-PC checks remain unperformed. Local checks do not publish a release, activate the public feed or replace the installed browser.

Additional request-counter tests verify that selecting an unloaded tab while Proton is unverified sends no requests, fresh synthetic proof does not replay the address, and explicit Retry restores the recreated page. The suspected stuck-view path did not reproduce under this normal lifecycle; no product change was needed.
