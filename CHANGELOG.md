# Harbor Browser 0.5.4

- Match native History, Bookmarks, Settings, resource monitor and other dialog title bars to Harbor's light/dark appearance, including live changes.
- Add an always visible Tor connection indicator and last-successful-check information.
- Require actual same-profile HTTPS Tor verification before showing a verified connection; inspect the actual proxy/WebRTC/prediction settings.
- Add five-minute health checks, network/wake invalidation and recovery, manual recheck and reconnect controls.
- Pause new managed HTTP/HTTPS page requests while Tor is unverified. Resume a recorded cancelled top-level navigation after verification; retain explicit Disconnect and restore for regular browsing.
- Cancel connection/check work on disconnect or disposal and reject stale positive results.
- Add native caption, failed-routing, cancellation, recovery, SOCKS hostname and local STUN positive-control tests.

Tor remains experimental; these checks are not a complete DNS/background-traffic/anonymity audit. Setup remains Windows x64 with bundled offline prerequisites. See VERIFICATION.md for final results and untested cases.

# Harbor Browser 0.5.3

- Use compact random GUID-based private-session names, reducing the profile paths that stalled WebView2 initialization in the reproduced deep-folder case.
- Bound engine/controller/permission/module/video startup waits and offer Reload tab in the affected tab instead of a modal error or indefinite wait. Keep extra time for Tor bootstrap.
- Cancel abandoned startup work on close/replacement. Ignore late completion from an old view. Skip recreating private controllers that never started or held page data during cleanup.
- Bound hidden profile-controller access and private-data clearing while preserving the existing cleanup failure behavior.
- Derive application, Home, About, status, module compatibility and installer versions from VERSION.txt and the compiled assembly.
- Record prerequisite detection, skipped/run helpers, exit codes and restart requests in setup logs.
- Wait for private Home's initial document in suggestion-click tests before observing the user's navigation.

The installer remains Windows x64 with bundled offline prerequisites. No new VPN provider, system networking changes, or expanded extension permissions are introduced. See VERIFICATION.md for executed checks and remaining limits.

- Guard queued tab-strip scrolling against a later selection change; check actual header visibility in overflow tests.
- Exclude generated TestResults source from test-project compilation so installer fixtures cannot introduce duplicate test entry points.
