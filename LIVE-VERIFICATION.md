# Harbor 0.5.7 live validation

Build-time record, November 16, 2026. Verified candidate app SHA-256: 11618CD58F707AFABAFD985AE785485C2BEF5EB5F49B39ACB4CC9556A4E345E6. Regular release app SHA-256: 349A49B8B835C9F6BE206312EE19BDDF859EEDFB1360FDEAB7373C9ACB4A122B.

Executed on the separate candidate: user completed actual Proton sign-in/connect; Harbor's Protected state was observed. Two completed route runs (initial and after an actual graceful process restart) each passed IPv4 and IPv6 in the page, worker and service-worker contexts: all six differed from their corresponding direct control. Both runs observed zero local STUN UDP packets and a successful functional secure WebSocket echo. Each also deliberately requested an unreachable reserved hostname before subsequent requests, with the parent page remaining usable and Protected.

After restart, sign-in remained available; the user clicked Connect without signing in again. The restored test page stayed paused until explicit Reload, after which all route checks passed. No forms or downloads were replayed. Results contain booleans/status/timing, never saved public addresses or authentication data.

The regular release changes identity, profile selection, interface text and installer channel; its complete native suites passed independently. These candidate results do not claim an observed connection against the existing production profile.

Pending at packaging: intentional live disconnect/reconnect, real network interruption, sleep/wake/reboot, final installed-release smoke check, clean/offline spare-PC installation, genuine missing dependencies/restart and actual Windows default-browser choice. Separate updated observations may be recorded beside this immutable package after installation.

Scope: request-level route comparison, limited local WebRTC observation and functional WebSocket echo. This is not a packet-level DNS/protocol/background-traffic audit or a system-wide kill switch certification.
