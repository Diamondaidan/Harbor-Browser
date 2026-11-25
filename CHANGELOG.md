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
