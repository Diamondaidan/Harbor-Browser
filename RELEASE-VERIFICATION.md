# Harbor Browser 0.6.1 application verification

Passed **1330 assertions across 17 suites** against matching app/helper copies.

App SHA-256: DB64E68769CF9DBBE48C3BBF3FBDB5A0A2152E1176AE7E7EEE0322668BB3EDD0
Update launcher SHA-256: 79EEB61EA18234A170085475C2011DFEF8AF542361B60031E43D303719E16ABC

| Suite | Assertions | Passed |
| --- | ---: | --- |
| regression | 384 | True |
| full | 360 | True |
| suggestions | 17 | True |
| recovery | 15 | True |
| proton | 34 | True |
| auth-handoff | 27 | True |
| proton-pending | 24 | True |
| proton-hotfix2 | 45 | True |
| proton-beta | 109 | True |
| proton-fallback | 19 | True |
| proton-guard-handoff | 12 | True |
| proton-r4 | 34 | True |
| release057 | 9 | True |
| updates | 120 | True |
| updates-ui | 8 | True |
| environment061 | 67 | True |
| ui061 | 46 | True |

All results are current for this executable. They cover local WebView2 browsing, downloads and filename handling, media, bookmarks, tab organization, settings, private profiles, synthetic account handoff, request protection, recovery and the updater. No real account sign-in or live protected-route claim is included.

The new 46-assertion UI suite checks app-rendered captions and live light/dark changes, including History; closing cancellation; compact genuine signed-out Proton controls and footer fit at three window sizes; collapsed Options with a visible privacy notice; and a trailing plus button with spare room and overflow. Dark/light captions, the provider renderer and tab-strip captures were visually inspected. See UI-0.6.1-VERIFICATION.md.

The full suite includes ten simulated size/scaling cases, including 1366 x 768 at 100/125/150/175/200 percent. These are offscreen WPF checks rather than physical-monitor certification. The environment suite injects network, wake and clock events; it does not change Windows networking, time or power settings.

Installer/package results are separate. The performance report identifies the preceding executable and was not rerun for these UI changes. Physical caption/mouse/DPI tests, signed-in Proton controls/reconnection, actual clean/offline prerequisite/reboot tests and hosted update/rollback remain manual validation items.
