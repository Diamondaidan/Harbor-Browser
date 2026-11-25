# Harbor 0.6.3 local package verification

Prepared locally on November 23, 2026. The 0.6.3 installer was built without running it over regular Harbor. Earlier builds and the installed browser profile/accounts remain preserved. Nothing was uploaded or published.

## Completed checks

- 1,516 application assertions passed across 21 suites against matching final app/helper copies. This includes 36 route-reliability checks, 17 download-recovery checks, 69 network/wake checks and the retained browsing, filename, media, tabs, bookmarks, settings, appearance, privacy, account-handoff, protection, off/on, recovery and updater suites.
- 94 core installer fixture checks and 103 full-payload installer fixture checks passed. All 399 payload/manifest files matched after temporary installation and upgrade. The installed test copy passed 307 assertions across seven smoke suites, including route and download recovery. Install/profile paths, AppIds and registration were isolated; actual HTTP/HTTPS defaults remained unchanged.
- Both embedded Microsoft prerequisites matched pinned hashes/sizes and valid Microsoft publisher signatures. Vendor installers were embedded, not executed. Prerequisite behavior used harmless stubs. The production installer is intentionally unsigned for Windows.
- All 336 pinned provider files remained unchanged. The local Harbor-signed update record uses sequence 6 and approves the preserved 0.6.2 installer as the compatible previous version. Two production-trust roundtrip checks passed. The protected private key is excluded from deliverables. The record expires and is not a live hosted feed.
- Light/dark download-recovery renders from the installed test copy were inspected. Generated downloads completed with exact source bytes, and explicitly injected independent private sessions retained separate rows/files and cleared private records on close.

App SHA-256: 103840485A9B49B361B632A22BDD8CF3747AFAB4A77D05DEABE7E2B528D097C1

Update launcher SHA-256: 113A4D459E6BDF98FA61421ED29F27982B34D73CE142E9E4C6C02A8ED4145312

Installer SHA-256: F0DB3548D44462DD0A5B2F5918F99E1EADED3372FACDAB8C642CEE0D665A8E37

## Scope and remaining validation

The baseline native verification stalled after successful response/loading events. Asynchronous scheduling and a deferred content read still failed. The final network reader passed immediate sequential comparisons without extending timeouts and rejected controlled page-text forgery, invalid replies, redirects and cancellation. Failed comparisons cancel the unfinished half and release the verifier. The development download test also caught and corrected a pending-row marker being removed before a replacement operation could rebind. See RELIABILITY-0.6.3-VERIFICATION.md for details and earlier log locations.

NuGet vulnerability metadata was unavailable during the builds (NU1900), so no fresh online dependency-vulnerability scan is established. No new whole-browser performance improvement or complete leak audit is claimed. IPv6 protection remains unresolved.

Still manual: live Proton session validity after off/on/restart and a real site verification; physical network/sleep/reboot/DPI/input; clean/offline spare-PC setup with real missing prerequisites and administrator prompts; and approved GitHub hosting with an actual newer-version update/rollback cycle. No real account credentials, tokens or authentication databases were read or copied. Private tabs remain blocked while browser-only protection is enabled.

The matching source archive and spare-PC kit are local deliverables. Archive/member hashes and packaged helper checks are recorded in LOCAL-DELIVERY-CHECKS.json beside the artifacts. Current-PC preflight results are kept outside the kit. A normally launched portable app shares the regular Harbor profile; automatic runtime checks must use owned test hosts. GitHub's dated inventory is in GITHUB-UPLOAD-STATUS.md.
