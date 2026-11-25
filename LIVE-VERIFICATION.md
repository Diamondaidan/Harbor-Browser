# Harbor 0.7.1 RC1 live-test scope and historical checks

Updated November 25, 2026 (America/Chicago). No installed 0.7.1 upgrade or real-account Proton test is claimed. The historical installed 0.7.0 RC1 checks below remain relevant background only.

## Public updater check on 0.7.1 code

Production update-service and transport code verified public 0.7.0 metadata (sequence 7), paused its real installer download at 20,805,982 bytes, and recreated the updater using a disposable profile. The partial file stayed unchanged while paused. The next request sent that exact offset and received HTTP 206. The completed 352,941,844-byte file matched the signed SHA-256 DAAEDB4736FF4459B34116BC2E06E80882E843B0FC9C16E83B0F87BFB6A5D8D1. All nine checks passed. The downloaded installer was never executed; no regular profile, browser cookies, account credentials or Windows network settings were used/changed. This is a live public download/recovery result, not proof of a complete installed update, rollback or physical connection-loss recovery.

## Installed RC1 checks

The installed RC1 executable independently matched SHA-256 4D68688B270E3A5CBE653C280CA66E62905F7231FDB994FC54ADF76338C65883. The following results apply to those bytes.

| Check | Result and evidence |
| --- | --- |
| Upgrade from 0.6.3 | User confirmed 0.7.0 opened with bookmarks/settings retained. |
| Proton startup and restart | User reported Protected automatically without sign-in. After closing/reopening Harbor, the generated page stayed paused until explicit Reload; Protected returned automatically after waiting, without sign-in. |
| Off/on | User confirmed VPN Off and direct controls completed; re-enabling retained extension sign-in and reached Protected. |
| Disconnect while protection enabled | User confirmed Disconnected and blocked Reload. Helper run count stayed at six. Reconnect reached Protected without replaying the paused page. |
| Protected routes | Five completed sweeps: IPv4 main-page, worker and service-worker comparisons differed from direct. Secure WebSocket echo worked; no local STUN packets/candidates in those sweeps. |
| IPv6 | Browser requests unavailable while direct controls worked. Inconclusive; a protected IPv6 route is not established. WebSocket echo also does not measure its server-observed route. |
| Clean tab unload/reload | User confirmed Unloaded in Resource Monitor. Selecting the tab restored its page without Reload; clean-page count increased from two to three. |
| Draft/media guards | User confirmed Unsaved input and Playing media, with both tabs retained. Only a fixed generated draft and muted canvas video were used; their load counts stayed unchanged during clean-tab reload. |
| Download filename, pause and resume | Extensionless Content-Disposition produced the expected .zip filename. User confirmed Pause/Resume controls. Partial file stayed 294,912 bytes over six seconds. Resume completed the exact 2,097,597-byte ZIP, SHA-256 9988DD277B3CAE9F910526EC11B80ECD433F5D682221B188CD7EC557B6D406AE. One request and zero Range requests: this checks same-transfer resume; restart/ranged resume is covered separately by owned fixtures. |

Windows desktop control returned stale state and a handle-quota error after retry/reset. Actual controls above were operated by the user, with independent process identity, generated-request counters and file hashes where described. No login details, cookies, profile databases or public IP addresses were collected. Earlier 0.6.3 observations are historical, not additional 0.7.0 results.

## Automatic testing and behavior limits

Automatic tests use injected owned profiles, generated files and local fixture servers. They do not open or copy the installed profile, sign in to a real Proton account, change real VPN/network/power settings or install over the user's browser.

The unloading tests check actual view disposal/reload; edited inputs including cross-origin frames and open shadow roots; contenteditable pages; native POST documents; media; private/pinned/kept-awake tabs; active/paused downloads; selection/navigation/settings/close races; the automatic timer; resource controls in both themes; and restoration of twenty tabs with only the saved selection initialized.

Unloading is off by default and loses in-page state, scroll position and the tab's Back/Forward history. Page-state detection is best effort, not a guarantee for all JavaScript/custom controls. A missing, failed, timed-out or oversized activity scan retains the page. Keep important tabs awake.

The final VPN label check renders VPN Off, Connecting, Protected and Disconnected in light and dark themes at six logical widths: 1366, 1093, 911, 683, 600 and 480. It checks natural text fits and address/menu controls remain usable. These are controlled WPF renders, not physical monitor/DPI switching tests.

## Still untested

- Physical internet interruption/recovery, sleep/wake and reboot on the final build.
- Actual clean/offline spare-PC setup, including real missing-prerequisite/admin/restart flows. A matching kit is prepared; current-PC preflight is not a clean install.
- Physical monitor/DPI/input combinations, broad real-site challenges, and a complete DNS/background-traffic/anonymity audit.
- Protected IPv6. Private tabs retain the explained browser-only Proton limitation.
- Complete installed update/rollback and administrator-prompt behavior on 0.7.1. Public metadata/download/resume was tested separately as described above.

Windows installers are intentionally unsigned. Harbor's signed update record is separate. Source/build copies and automated fixtures do not replace the installed browser. The regular portable EXE uses the regular profile when launched normally; owned test hosts are required for isolated testing.
