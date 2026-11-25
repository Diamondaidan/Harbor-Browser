# Harbor 0.7.1 full local package verification

Prepared November 25, 2026. The owner confirmed installed RC1 bookmarks/settings retention, Proton restart without another sign-in, physical network recovery and sleep/wake. These are user-reported RC1 results. Full 0.7.1 changes compiled channel identity and visible RC labels, with unchanged app/helper source. Its new binaries receive separate automatic verification. Preparing them does not replace the installed RC1 or publish a release.

All 1,656 browser/updater assertions across 22 suites passed on matching full app/helper copies. Core native installer fixtures passed 296 checks. Full staged-payload fixtures passed 306 checks, verified all 400 payload/manifest files and ran 403 application assertions across eight packaged smoke suites. The 25 approved original app identities were upgraded in disposable markerless folders. Historical applications/installers were never run. Personal profiles and actual Windows browser defaults were preserved. Vendor dependency result/restart cases used safe stubs.

Eleven full-package identity checks passed, including rejection of false RC labels and candidate metadata disguised as a release. No real private key was accessed. The actual full installer was inspected without executing it: embedded app, helper, manifest and both offline Microsoft prerequisites matched the verified inputs. The real prerequisite packages passed pinned size/hash and valid Microsoft-signature checks before packaging. All 336 official Proton assets remain unchanged.

Nine live public updater checks passed on the full code with an owned profile. The public 0.7.0 installer paused at 20,116,241 bytes, remained unchanged while paused, and resumed at that exact offset with HTTP 206 after updater recreation. The completed 352,941,844-byte installer matched its signed hash. The downloaded public installer was never run. This is metadata/download/resume evidence, not an installed update/rollback result.

Actual installation of this new full executable remains pending. Clean/offline spare-PC installation, genuinely missing prerequisites, administrator/reboot flows and a complete installed updater/rollback flow are separate checks. No new memory/startup benchmark, vulnerability audit or complete leak/anonymity audit is claimed. Protected IPv6 remains unconfirmed. LIVE-VERIFICATION.md and INSTALLED-RC1-VERIFICATION.json separate manual RC1 observations from automatic full-build evidence.

Windows installers remain intentionally unsigned. No new signed public update record was prepared and no release/main/feed was changed. Published 0.7.0 and earlier packages and the local RC1 artifacts remain preserved. Normal installed and portable launches use the regular profile; automatic runtime checks inject owned stores.

Save work, finish downloads, close Harbor and run HarborBrowserSetup-0.7.1-release.exe to install the full build. Handle Windows prompts yourself. Build-Spare-PC-Kit.ps1 can bundle this installer, matching source and check tools for later. A current-PC read-only preflight is not a clean/offline or missing-prerequisite test.

Final installer/portable/source/kit hashes and source commit are recorded in local LOCAL-DELIVERY-CHECKS.json and dist/SHA256SUMS-0.7.1-release.txt after packaging. Source archives exclude private keys, profiles, caches, generated test data, Microsoft dependency executables and extracted inspection tools. Historical identity fixtures are included only as source test inputs, not in the browser payload.
Application SHA-256: 45F8D1700A19C34D013C2492869614573180F7A957D05B5C95B91C4BAA548571
Update launcher SHA-256: 08305B95314AB133F4AA115008B3967EEE5B50E582E71F12A06C4C192716ACF5
Installer SHA-256: BF6FF63D59463F78858DF6D1AF6394C2BB66FF4571F204A9811D07579AF18A92
