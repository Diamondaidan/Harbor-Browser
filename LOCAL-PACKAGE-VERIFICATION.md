# Harbor 0.6.1 UI package verification

Prepared locally November 17, 2026. This new 0.6.1 candidate fixes Harbor-owned dialog captions, the compact Proton window and the trailing new-tab button. Earlier local builds remain preserved. Nothing was uploaded to GitHub, and the new production installer was not run against the regular installation.

## Executed against this build

- **1,330 application assertions across 17 suites passed** with matching app/helper copies. This includes all 46 new UI checks, existing browsing/download/media/tab/bookmark/settings checks, synthetic account handoff and protection recovery, and update integrity/routing. See RELEASE-VERIFICATION.json and UI-0.6.1-VERIFICATION.md.
- **94 core native installer fixture checks passed**, followed by **99 complete-payload installer fixture checks**. The latter verified all **399 payload/manifest files** after installation and upgrade, plus **121 application assertions** run from the fixture-installed folder: 67 environment/recovery, 8 updater UI and 46 new UI checks. Registration and install/profile paths were isolated. Actual HTTP/HTTPS defaults were unchanged.
- Both embedded offline Microsoft prerequisites matched pinned sizes and SHA-256 hashes and had valid Microsoft publisher signatures. The full production Inno Setup installer compiled successfully and is intentionally unsigned for Windows.
- A fresh local update record uses sequence 3 and the existing protected Harbor metadata key. **Two production-trust roundtrip checks passed** against this actual installer and the approved 0.5.7 previous installer. The record is for future hosting, expires and does not activate an update feed. Private signing material is excluded from deliverables.
- Dark/light dialog renders, the full signed-out extension renderer and overflowing tab-strip renders were visually inspected. The provider's 336 pinned files remained unmodified.

App SHA-256: DB64E68769CF9DBBE48C3BBF3FBDB5A0A2152E1176AE7E7EEE0322668BB3EDD0

Installer SHA-256: 6459F023DEF1A0536D41D4298299F98695AD63A4FA5AA3E8508305DDC9B26E47

The performance report belongs to the preceding executable. No performance improvement or current performance benchmark is claimed for these UI corrections.

## Distribution checks

The local spare-PC kit accompanies the installer with the portable build, matching source, local update record, connection-check helper and test instructions. Its read-only preflight validates its listed files and platform requirements; this computer's generated Results are kept outside the distributed kit. The final delivery receipt records archive/member integrity and the packaged helper smoke checks.

## Still requires manual or another-system testing

Actual clean/offline setup with genuinely missing Microsoft prerequisites; the production administrator prompt and reboot; physical mouse/trackpad caption movement, resizing, snapping, tab dragging and monitor/DPI transitions; signed-in Proton controls/reconnect and IPv6; physical network/sleep transitions; and a hosted update/rollback after approved publication.

Fixture dependency installers were harmless stubs. Real Microsoft prerequisites were verified and embedded rather than executed. Synthetic supported-request tests are not a complete leak audit or a system-wide kill switch. No real account profile, Windows security setting or default-browser choice was changed by these fixture checks.