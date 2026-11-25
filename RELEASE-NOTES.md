# Harbor Browser 0.6.2

- Show a compact notice when a website requests verification, refuses first-party page files or returns missing/server-error files. The live page stays visible.
- Offer a separate same-origin HTTPS site-check tab when appropriate. Complete checks yourself, return to the original page, then reload explicitly. Forms and downloads are not resumed automatically, and browser protections remain enabled.
- Explain address lookup, connection, proxy authentication and certificate errors more clearly. Harbor's own tracker and protection denials are counted separately from website errors.
- Add Help > Page loading details, with an explicit Copy report action. Reports contain categories, counts and version numbers; URLs, hosts/IPs, page titles, account data and profile paths are excluded. Nothing is uploaded.
- Explain why private tabs are unavailable while browser-only Proton/Tor is enabled. Proton's Turn off protection — keep sign-in allows private tabs on the regular connection; Disconnect continues to pause browsing. The current private-route restriction remains for safety.

The Windows app and installer remain intentionally unsigned. Microsoft prerequisites retain their verified signatures; Harbor's signed update metadata is separate. This is a local development package, not a public GitHub release. No installed profile is upgraded automatically.

The lightweight resource notice covers observed first-party GET page files in regular, non-Proton-account tabs. Private/Proton-account inspection, third-party failures, native tracker prevention and every website feature are not fully covered. No new performance improvement or complete privacy audit is claimed. Live Proton/account/site-check behavior, actual clean/offline setup and prerequisites/restart, physical network/sleep/DPI and a hosted updater cycle still require manual validation. See RELEASE-VERIFICATION.md and SITE-0.6.2-VERIFICATION.md.
