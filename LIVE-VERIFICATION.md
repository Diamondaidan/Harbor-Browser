# Harbor 0.6.1 runtime validation scope

The development build passed 1,284 assertions across 16 owned-profile suites, including actual WebView2 local browsing/download/recovery/Proton tests and native updater routing-gate checks. Updater fixtures used ephemeral synthetic signing keys, local HTTP, synthetic profile data and harmless native parent/setup programs. No new production update record was signed for this candidate.

The installed user browser was not upgraded or used as a test profile. Earlier 0.5.7 live connection/disconnect/restart observations remain historical and do not establish an actual 0.6.1 installed sign-in or a live GitHub update. Native 0.6.1 setup fixtures passed 94 checks using isolated paths, AppIds and registration; actual Windows default choices remained unchanged.

Synthetic network/wake callbacks and injected clock changes passed, including a deliberately busy interface dispatcher. They do not replace physical Wi-Fi interruption, sleep or reboot tests. Simulated size/scaling checks and inspected offline header renders do not establish actual multi-monitor/DPI or physical mouse/trackpad behavior. Twelve owned local performance cases completed with 120 samples; see PERFORMANCE-0.6.1.md for the results and limits.

Remaining: signing approval; owner-approved public GitHub assets; actual hosted download/resume; real newer-version self-update and previous-version rollback; updater-driven UAC/setup; live 0.6.1 Proton account/routing checks; clean/offline spare-PC install and real missing dependencies/restart; physical Wi-Fi interruption, sleep/reboot and broader Windows 11/hardware checks. IPv6 protection remains unresolved; no new live IPv6 claim is made.

Native fixture success is not a complete privacy/security audit or certification of every Windows PC. This build preserves the documented limited browser connection scope.
