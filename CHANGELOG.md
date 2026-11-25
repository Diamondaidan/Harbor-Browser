# 0.7.0

- Optional inactive-tab unloading with conservative page-state/media/download checks; off by default.
- Separate unloaded/sleeping/deferred states, unload reasons and Reload controls in Resource Monitor; clarify process memory accounting.
- Avoid the unused background initialization attempt during restored-session startup.
- Record matching 0.6.3/0.7.0 performance results and native lifecycle/protection-transition coverage.
- Let the VPN status button size to its label so Disconnected fits in light/dark and compact layouts.
- Prepare full unsigned 0.7.0 after installed RC1 upgrade, Proton lifecycle, unloading and live download checks. Physical recovery and clean/offline spare-PC checks remain pending; live IPv6 protection is unconfirmed.
# Harbor 0.6.1 development

- Close the thread-safe Proton request gate directly when a network/address/suspend/resume notification arrives, before interface work is queued.
- Cancel obsolete verification and clear its cooldown after the transition; reject route results from before the change and keep paused-page retry explicit.
- Bound route evidence by elapsed time as well as UTC; clock rollback cannot extend a Protected claim.
- Add synthetic callback and clock-change coverage, preserve the pinned provider package, and record startup readiness in the existing owned-profile performance harness.
- Carry forward the separately prepared signing metadata and licensing files. Signing, public installer release and physical spare-PC checks remain separate work.

# Harbor 0.5.6 Proton Beta R4 (historical)

- Keep an aborted request from unloading unrelated pages; retain immediate pauses for proxy/privacy changes and unsafe errors, existing lease/ownership checks and the native rejecting fallback.
- Correct stale Protected descriptions and keep hidden/background startup status out of the active tab.
- Reuse one verification view and replace per-check direct-browser launches with a credential-free HTTP comparison; validate native cancellation and recovery.
- Pass 1,080 assertions across twelve suites. Complete both owned component benchmarks; live Proton retry and broader manual gates remain pending.

# Harbor 0.5.6 Proton Beta R3

- Separate 056G profile and beta identity; preserve the user's working Hotfix 2 and earlier development data.
- Allow the exact HTTPS verification origin in trusted authentication and native fallback; exclude it from history/session storage and managed modules.
- Deny embedded unrelated pages without replacing their parent sign-in tab. Retain the prepared native popup handoff from R2.
- Pass all eleven Windows automation/test suites, totaling 1,046 assertions; retain live sign-in, connection, network-failure and performance gates.
- Add host protection states, expiring route evidence, current proxy ownership checks, explicit paused-page retry and fail-closed removal handling.
- Correct unnamed versus named default-profile selection for verification controllers.
- Expand native server-counted tests across startup, workers, downloads, connection loss, expiry and network changes. Failed protection checks block packaging.
- Include pinned dependency license/source notices and record remaining public-distribution review.

# Harbor 0.5.5 Proton trial (portable, experimental)

- Separate application/pipe identity and portable TrialData folders; production installer disabled.
- Host unmodified official Proton1.3.6 controls persistently; bounded loading/reload/removal and safe diagnostics.
- Keep extension-enabled intent distinct from connected/verified state; add a limited manual same-profile/direct-control route comparison.
- Exclude Proton account handoff tabs from Harbor history, sessions, recently closed tabs and managed site-module injection.
- Add signed-out compatibility/reversal checks and controlled direct/Tor/signed-out-Proton component measurements.
- Live account authentication, proxy authentication, leak/failure behavior and connected-Proton performance remain separate validation gates.

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
