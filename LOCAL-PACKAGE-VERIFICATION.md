# Harbor 0.6.1 Proton off/on package verification

Prepared locally November 17, 2026. This candidate adds a normal “Turn off protection — keep sign-in” action, with a separate “Remove Proton and reset sign-in” action under Options. Earlier local installers are preserved. Nothing was published and this production installer was not run over the regular browser.

## Executed against this build

- **1,389 application assertions across 18 suites passed** against matching app/helper copies. This includes 59 new data-retention/off/reset checks, the existing 46 UI checks and all prior browsing, downloads/filename, media, tab, bookmark, settings, privacy, account-handoff, request-protection, recovery and updater suites.
- **94 core installer fixture checks** and **100 full-payload installer fixture checks passed**. All **400 payload/manifest files** matched after initial installation and upgrade. The fixture-installed app passed **180 assertions**: 67 environment/recovery, 8 updater UI, 46 UI and 59 off/on tests, including a full browser/engine restart while off. Test registration and install/profile paths were isolated; actual HTTP/HTTPS defaults were unchanged.
- Both embedded Microsoft prerequisites matched pinned sizes/hashes and valid Microsoft publisher signatures. The full production installer compiled successfully and remains intentionally unsigned for Windows. Microsoft prerequisite installers were embedded rather than executed; fixture prerequisites were harmless stubs.
- The provider's 336 pinned files remain unmodified. A fresh local update record uses sequence 4 and the existing protected Harbor metadata key. **Two production-trust roundtrip checks passed** against this installer and the approved 0.5.7 previous installer. This is separate from Windows publisher signing. The record expires and is not an active hosted feed; the private key is excluded from deliverables.
- The off window, normal off action and provider renderer were visually inspected. The new suite retains a synthetic extension-local marker across off/on cycles, failures and a complete process restart, and confirms that only explicit reset clears it while retaining a separate website cookie.

App SHA-256: 324C32D3CD1366AE49220FEC3A8E7397FAA2E96E02292D4868781C405FEF3827

Installer SHA-256: 60B9D5AC8C6DAE1D86615670FD69B4540FE21EBF9AC99CBED37EE8ADB8D13222

## Verification qualifications

An initial R4 local route comparison timed out; a fresh complete 34-check run passed without application changes or relaxed timeouts. The initial packaged retention test could not write its canary under a longer generated profile path. Repeating that suite with a shorter owned test profile passed, with unchanged application bytes. Both initial logs are retained under Evidence/ProtonOff. The fixture points to its short off/on evidence path and verifies ownership boundaries.

The performance baseline identifies its preceding executable and was not rerun for this fix. No performance improvement is claimed. The matching source and a local spare-PC kit accompany the installer; final archive/member and packaged-helper results are in LOCAL-DELIVERY-CHECKS.json. Generated PC-check Results are retained outside the distributed kit.

## Still requires manual testing

No real account credentials, tokens or authentication databases were read or copied. Artificial data retention does not prove that Proton will consider a live saved session valid. After installation, turn protection off, explicitly Reload, enable/connect again and check retained sign-in; repeat after restarting Harbor while off. Proton can still require sign-in for an expired or revoked session.

Actual clean/offline setup with genuinely missing prerequisites, production UAC/reboot, physical monitor/DPI/input and Wi-Fi/sleep transitions, signed-in IPv6 behavior and a hosted update/rollback remain separate manual checks. Synthetic supported-request tests do not establish a complete leak audit or a system-wide kill switch. No Windows security setting, real account profile or default-browser choice was changed by fixture checks.