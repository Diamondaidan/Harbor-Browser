# Harbor 0.7.1 full automated release verification

Application SHA-256: 45F8D1700A19C34D013C2492869614573180F7A957D05B5C95B91C4BAA548571
Update launcher SHA-256: 08305B95314AB133F4AA115008B3967EEE5B50E582E71F12A06C4C192716ACF5

All 1656 assertions across 22 suites passed on November 25, 2026 against matching full 0.7.1 app/helper copies.

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

The full build uses release metadata and plain version labels. Runtime source, Directory.Build.props and VERSION.txt match tested RC1 commit 8db48920262ce4552cc6a8a5149c8ef6726c7f23; only RELEASE-CHANNEL.txt changes the compiled app/helper identity. FULL-PROMOTION-VERIFICATION.json records this comparison.

Core installer fixtures passed 296 native checks against the new full binaries, including all 25 pinned original application identities, retained profile canaries, changed-file refusal, backup failures, busy profiles, uninstall and unchanged Windows defaults. Full staged-payload installed-file/application smoke evidence is recorded separately after staging in PACKAGED-PAYLOAD-VERIFICATION.json. Vendor prerequisites are stubbed in these fixtures.

All 336 pinned official Proton assets remain unchanged. Microsoft offline prerequisites are size/hash/signature checked before packaging. No new vulnerability audit or startup/memory benchmark is claimed.

Nine live public updater checks passed with production transport/trust and an owned profile: public signed metadata, pause, updater recreation, exact HTTP 206 resume and final signed package size/hash. The downloaded installer was never executed. See PUBLIC-UPDATER-VERIFICATION.json.

The owner separately confirmed installed RC1 bookmarks/settings retention, Proton restart without another sign-in, physical network recovery and sleep/wake. Those are user-reported RC1 results, not new installed-full results or independent route measurements. Clean/offline spare-PC installation, missing-prerequisite/admin/reboot behavior and installation of the new full package remain separate checks. See LIVE-VERIFICATION.md.

Local packaging does not publish a GitHub release, change main or replace the public update record. Previous full and RC1 artifacts remain preserved.
