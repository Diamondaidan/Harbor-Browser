# Harbor Browser 0.7.1

A Windows WebView2 browser with tabs, bookmarks, history, downloads, customization, loading diagnostics, optional browser-only Proton protection and verified updates.

This version improves older-install upgrades and distinguishes test builds from full releases. Exact original executable hashes identify markerless Harbor installations. The updater can offer a full release to an RC of the same base version, while keeping signature, expiry, profile, routing and installer checks. See RELEASE-NOTES.md and UPDATING.md.

VERSION.txt supplies the numeric version; RELEASE-CHANNEL.txt supplies release or rcN. App and helper resources, visible labels, package manifests and installer names must agree. BUILDING.md explains verification and packaging. The normal installed and portable executables share the established regular profile. Automatic runtime tests inject disposable profiles.

The public repository and latest full release are at https://github.com/Diamondaidan/Harbor-Browser . Recovered original full versions are indexed on archive/full-releases; missing originals are disclosed there. Preparing 0.7.1 locally does not publish it or change the public feed. See GITHUB-UPLOAD-STATUS.md.

Windows installers remain intentionally unsigned. Harbor's separate RSA-signed release metadata authenticates update package bytes; it does not provide a Windows publisher certificate. RELEASE-VERIFICATION.md, INSTALLER-VERIFICATION.md and LIVE-VERIFICATION.md distinguish automated results from physical Windows checks.

Inactive-tab unloading remains optional and off by default. It loses page state and that tab's Back/Forward history; keep important tabs awake. PERFORMANCE-0.7.0.md contains historical measurements, not new 0.7.1 benchmarks.

Harbor-authored source/artwork: GPL-3.0-only. Third-party licenses/notices remain in LICENSE-NOTICE.md, THIRD-PARTY-NOTICES.md and ThirdParty/PROTON-BUILD.md. Proton is optional browser-only protection. Its normal-profile route cannot protect private tabs; Harbor explains that limitation and blocks them while protection is enabled.
