# Harbor 0.6.3 live validation scope

Automatic checks use separate injected profiles, local HTTPS/proxy/HTTP servers, generated files and synthetic account-state canaries. They do not use the installed Harbor profile or sign in to a real Proton account. Exact current app/helper hashes and suite results are in RELEASE-VERIFICATION.json.

The route stress test reproduces the earlier response-read stall and checks the new original-network reader without increasing the timeouts. Page-text changes, malformed/oversized replies, redirects, failed halves, cancellation and subsequent recovery are covered. The download test checks exact generated file bytes and explicitly injected private-session separation. Neither test establishes a live provider session, physical network behavior, every possible leak or every Windows computer.

Required manual validation remains: retained Proton sign-in and route checks after off/on/restart; a real website challenge; physical network interruption, sleep/wake/reboot and monitor/input transitions; actual clean/offline spare-PC setup with missing prerequisites and administrator prompts; and a hosted GitHub newer-version update/rollback after approved publication. IPv6 protection remains unresolved. Earlier 0.5.7 account observations and older performance reports are historical, not new 0.6.3 results.

Windows packages are intentionally unsigned. Harbor's own release-record signatures and checksums remain required. The regular portable EXE shares the established Harbor profile when launched normally; automatic runtime tests must use the owned test hosts. No installed browser upgrade or remote publication is performed by these local checks.
