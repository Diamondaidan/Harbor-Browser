# Harbor 0.6.2 runtime validation scope

Automated tests use owned, injected profiles and synthetic local pages. The regular installed Harbor profile and its saved accounts are not used. Older 0.5.7 account/restart observations and 0.6.1 reports remain historical; they do not establish a live 0.6.2 account connection. RELEASE-VERIFICATION.json identifies the exact application bytes and passing suites for this candidate.

The 0.6.2 site suite uses an owned HTTPS server and synthetic Cloudflare-style headers. It proves the notice/helper/return/reload flow and report redaction for controlled responses, without solving a real CAPTCHA or weakening certificate checks in production. A fixture-only certificate pin is passed solely to the test host. Layout coverage uses offscreen WPF windows and effective widths; it does not establish physical multi-monitor/DPI or mouse/trackpad behavior.

An isolated private-route probe found that native InPrivate views do not inherit the normal extension proxy in this WebView2 runtime, including with an authenticated fake proxy. Browser-only Proton remains incompatible with private tabs. The owner chose to retain the restriction and show an actionable explanation. Private tabs become available only with protection off, on the regular connection.

Synthetic network/wake events and injected clocks do not replace physical Wi-Fi interruption, sleep or reboot checks. Extension-local synthetic canaries prove local data retention, not that Proton will accept a live expired/revoked session. The earlier performance baseline is retained as history; it was not rerun for these changes and no speed/memory improvement is claimed.

Remaining: live signed-in Proton off/on/restart and website verification; actual clean/offline spare-PC install, real missing prerequisites and reboot; physical Wi-Fi/sleep/DPI and broader Windows hardware; approved GitHub hosting and a real newer-version update/rollback/UAC cycle. IPv6 protection remains unresolved. The owner accepts unsigned Windows distribution; Harbor's own update-record signatures and checksums remain required. Nothing is published by the local build.
