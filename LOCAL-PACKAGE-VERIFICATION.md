# Harbor 0.6.1 local package verification

Prepared November 17, 2026. The full Windows installer and portable package were built locally and remain intentionally unsigned. No GitHub release was published, and production setup was not run against the regular installation.

## Executed in this packaging pass

- Verified the app/helper hashes against the completed 1,284-assertion application record and the original 94-check native installer record. Runtime application code was not changed or rebuilt for this packaging pass.
- Verified both bundled Microsoft prerequisites against pinned sizes, SHA-256 hashes and valid Microsoft publisher signatures; compiled the full Inno Setup installer with those offline packages.
- Ran the production installer template in an isolated fixture with the complete staged payload, unique install/profile paths and test registration. All **98 fixture checks** passed, including install, upgrade, uninstall, data preservation/snapshots, busy-file and backup failures, older-version recognition and simulated dependency/restart outcomes. All **398 payload/manifest files** matched after initial installation and upgrade.
- Ran the test host from the fixture-installed app folder using injected test profiles. Its **67 network/clock recovery checks and 8 updater UI/routing checks** passed against the packaged application and dependencies. It did not launch the regular Harbor entry point or sign in to an account.
- Rebuilt the spare-PC connection helper from source. **Eight local HTTP checks** passed for asset delivery, empty status and unauthorized host/origin/capability rejection. No external connection-check services or account were contacted.
- Prepared a local Harbor-signed update record with the existing protected metadata key, then passed **two production-trust roundtrip checks** against the actual 0.6.1 installer and approved 0.5.7 previous installer. This is separate from Windows publisher signing. The private key is not included in any deliverable.

The earlier 12-case/120-sample performance baseline and 1,284-assertion functional results remain applicable to the unchanged executable. They were not rerun merely to rebuild archives. See RELEASE-VERIFICATION.md and PERFORMANCE-0.6.1.md for their scope.

## Kit and final integrity checks

The spare-PC kit contains the current installer, portable archive, matching source, locally verified update record, source-built connection helper, instructions and verification reports. Its read-only preflight checks the host platform and every listed file. Packaging preserves that preflight outside the distributable kit; the kit is shipped without prefilled user Results. Archive/member hashes and exclusion of profiles/private keys are checked separately in the final local delivery receipt.

## Still requires another Windows system or manual actions

Actual clean/offline installation with genuinely missing Microsoft prerequisites; the production administrator prompt and reboot; physical Wi-Fi interruption and sleep/wake; signed-in Proton reconnect/IPv6 behavior; physical monitor/input/taskbar/video checks; and a real hosted update/rollback after approved publication.

Fixture dependency installers were harmless stubs. Microsoft prerequisites were verified and embedded, not executed during fixture tests. Supported-request tests are not a complete leak audit or system-wide kill switch. No Windows security setting, default browser or real account/profile was changed by the fixture checks.

The local update record does not activate hosting and expires. Prepare a fresh higher-sequence record if it changes or expires before publication. Existing verified development archives are preserved separately; the release package includes the current unsigned-distribution policy.
