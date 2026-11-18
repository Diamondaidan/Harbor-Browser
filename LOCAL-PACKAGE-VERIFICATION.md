# Harbor 0.6.2 local package verification

Prepared locally on November 17, 2026. The production installer was built without running it over the regular Harbor installation. Earlier installers, source folders and the installed account/profile remain preserved. Nothing was published.

## Completed for these application bytes

- 1,461 application assertions passed across 19 suites, including 72 new site/diagnostic/private-limit checks. All test app/helper copies matched the final compiled binaries.
- 94 core installer fixture checks and 101 full-payload installer fixture checks passed. All 399 payload/manifest files matched after fixture installation and upgrade. The fixture-installed application passed another 252 assertions: 67 environment/recovery, 8 updater UI, 46 UI, 59 off/on retention and 72 site/private-limit checks. Install/profile paths, AppIds and registration were isolated; actual HTTP/HTTPS defaults remained unchanged.
- Both embedded Microsoft prerequisites matched pinned hashes/sizes and valid Microsoft publisher signatures. Actual vendor installers were not executed; prerequisite behavior was tested using harmless stubs. The full production installer compiled and is intentionally unsigned for Windows.
- All 336 pinned provider asset hashes remained unchanged. The local update record uses sequence 5 and the existing protected Harbor metadata key. Two production-trust roundtrip checks passed against this 0.6.2 installer and the preserved 0.6.1 previous installer. The private key is excluded from deliverables. This record expires and is not an active hosted feed.
- Generated light/dark report windows and compact notices were visually inspected. The site fixture verifies a same-profile HTTPS helper, query stripping, explicit return/reload and retained page content. A real CAPTCHA was not solved. The private route limitation remains: Turn off protection — keep sign-in permits private tabs on the regular connection; Disconnect keeps protection enabled and browsing paused.

App SHA-256: EFE5565D4A6F13E260E5AF953D2EE18F2A53912C3EEE771B857EC5815A5F87BC

Update launcher SHA-256: 5064FA58D5BB6CED7EBDA204679C1E0D276D6470B8A81A2DD121F84E1387386D

Installer SHA-256: F9377BA9687035DF770ED36BAC004A4D5C07AC80F9CD0D944FF6FEF5992F9931

## Qualifications and remaining work

Two initial R4 route fixture attempts timed out; subsequent full runs passed without relaxing timeouts or changing the production route verifier. This remains an intermittent test/runtime observation. A private-report startup-label race was fixed by initializing privacy at tab creation, and the final native test checks it before first navigation. Earlier logs are retained under Evidence/062-initial. NuGet vulnerability metadata was unavailable (NU1900), so this build does not establish a fresh online dependency-vulnerability scan. The older performance baseline is historical; no speed/memory improvement is claimed.

Manual checks remain: a real website challenge through signed-in Proton; live account off/on/restart session validity; actual clean/offline spare-PC setup and missing Microsoft prerequisites/UAC/reboot; physical network/sleep/DPI/input and broader Windows hardware; and an approved hosted GitHub update/rollback cycle. IPv6 protection remains unresolved. No actual account credentials, tokens or authentication databases were read or copied.

The matching source and spare-PC kit are local deliverables. Their archive/member checks and the packaged connection-helper checks are recorded in LOCAL-DELIVERY-CHECKS.json alongside the artifacts. Generated current-PC preflight Results are kept outside the distributed kit. A normally launched portable app uses the regular Harbor profile; automated runtime checks must use the owned test hosts.
