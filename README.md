# Harbor Browser 0.7.0

A Windows WebView2 browser with tabs, bookmarks, history, downloads, customization, page-loading diagnostics, optional browser-only Proton protection and local update verification.

This version adds opt-in inactive-tab unloading, clearer resource states and selected-tab-only session initialization. Sleeping preserves the page; unloading disposes its view and reloads the address later, losing page state and the tab's Back/Forward history. Detected edits, media, downloads and protected tabs prevent unloading. Keep important tabs awake because some website state cannot be detected.

See PERFORMANCE-0.7.0.md for measurements and their limits, RELEASE-VERIFICATION.md for automated checks, and LIVE-VERIFICATION.md for the remaining installed-account/hardware checks. Earlier 0.6.3 packages remain preserved separately. The installed browser is not changed by local development.

Build with BUILD.bat or MSBuild after installing the tools in BUILDING.md. Build-Release.ps1 and Build-Installer.ps1 require current matching verification records; official Proton asset hashes and signed offline Microsoft prerequisites remain checked. The normal portable executable uses the established regular profile. Automatic tests use injected owned profiles instead.

The Windows installer is intentionally unsigned under the owner's distribution choice. Harbor's RSA-signed release record and exact checksums authenticate update packages separately. No public release or update feed is activated by this source checkout. GitHub currently holds a 0.6.3 source branch and unpublished release drafts; see GITHUB-UPLOAD-STATUS.md.

Harbor-authored source/artwork: GPL-3.0-only. Third-party components retain their original licenses and notices. See LICENSE, LICENSE-NOTICE.md, THIRD-PARTY-NOTICES.md and ThirdParty/PROTON-BUILD.md. Proton is optional browser-only protection, not a system VPN or a complete leak/anonymity guarantee. Private tabs cannot use that extension's normal-profile route; turn protection off while keeping sign-in to use private tabs on the regular connection.