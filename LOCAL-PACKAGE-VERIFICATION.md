# Harbor 0.7.1 RC1 local package verification

Prepared November 25, 2026. This local candidate improves recognition of older installations and carries explicit RC build metadata throughout the app, updater, helper and installer. Published 0.7.0 and earlier packages remain unchanged; 0.7.1 has not been published or installed over the regular browser.

All 1,656 browser/updater assertions across 22 suites passed on matching app/helper copies. Core native installer fixtures passed 296 checks. Full staged-payload fixtures passed 306 checks, verified all 400 payload/manifest files, and ran 403 application assertions across eight installed smoke suites. All 25 approved original app identities were upgraded in owned markerless folders; historical applications/installers were never run. Actual browser defaults and personal profiles were preserved. Vendor prerequisite behavior was simulated with safe stubs.

Eleven package-identity checks passed. A candidate renamed to a release filename is still rejected by stable-feed signing before any real private-key access. The actual RC installer was separately unpacked without running it: embedded app, helper, package manifest and both offline Microsoft dependencies exactly matched the verified inputs. The prerequisite packages also passed hash/size and Microsoft-signature checks before packaging. All 336 official Proton asset hashes remain unchanged.

Nine live public updater checks passed using a disposable profile and the production trust root/transport. The public 0.7.0 installer paused at 20,805,982 bytes, remained unchanged while paused, and resumed from that exact offset with HTTP 206 after updater recreation. Final signed size/SHA256 matched. The downloaded public installer was never executed. This is not a complete installed updater/rollback test.

Application SHA-256: 4AD1CB6B33A43A1C20E6AB262E04283E4CBB2D626FB74ABCDEBAC342CE410EC4
Update launcher SHA-256: ADCFB10124EA75BFEB2DB167647F6CD47E16A5982358255169667A221E4F1CA2
Installer SHA-256: B21800642F3ED1C054324A25C700997B2373570AF0FE4C2EB657404026C955A5
Portable SHA-256: 31E610A2A4FA2C7B155F783B5140E2875DCF67CB9BF09B2E4B61395624B3C983

Regular installed upgrade/retention and real-account Proton checks on 0.7.1 are still pending. Physical connection loss/recovery, sleep/wake/reboot, monitor/DPI checks, and clean/offline spare-PC installation remain manual. Earlier installed 0.7.0 RC1 observations are historical. Protected IPv6 remains unconfirmed. No new startup/memory benchmark, vulnerability audit or complete privacy/leak audit is claimed.

Windows installers remain intentionally unsigned. RC1 uses the regular installation/profile; portable launches do too. Save forms and finish downloads, close Harbor, run the installer and handle Windows prompts yourself to begin installed testing. Publication and stable-feed signing are separate later steps. Do not rename RC artifacts to promote them.

Exact source commit/archive and final delivery hashes are recorded in local LOCAL-DELIVERY-CHECKS.json and dist/SHA256SUMS-0.7.1-rc1.txt after source packaging. Source packages exclude private keys, profiles, generated test data, caches, Microsoft dependency executables and extracted inspection tools. Original identity fixtures remain in source as test inputs; they are excluded from the installed browser payload.