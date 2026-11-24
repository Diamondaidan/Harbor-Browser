# Harbor 0.6.3 reliability verification

Harbor 0.6.3 replaces a verification-reply read that could stall after a successful request. It also stops verification attempts while Windows reports the adapter unavailable and makes download recovery clearer. The 0.6.2 package and installed Harbor profile remain separate from these owned test profiles.

## Verification reply correction

The baseline stress fixture failed after five immediate comparisons: the sixth local proxy request returned HTTP 200 and NavigationCompleted reported success, but the response-content task did not complete. Additional asynchronous task scheduling and moving GetContentAsync after navigation still reproduced the stall. Those unsuccessful approaches are retained in local test logs; they are not claimed as fixes.

The final reader uses the engine's network protocol on the existing dedicated verifier renderer. It matches the fresh request identifier and exact verification URL, waits for network and navigation completion, validates HTTP 200 and JSON content type, then reads the original network bytes. The buffer is capped at 64 KiB and the reply at 16 KiB. Page scripts remain disabled; cached/service-worker replies cannot establish a proof. The renderer denies requests outside its canonical ipify verification endpoint, including redirect destinations. Proxy ownership and supported settings are checked before and after the comparison, and the existing timeouts and epoch/lease checks remain in force. A failed half cancels the unfinished comparison and discards the verifier view. No DOM text, account database, normal website response or public address is logged.

The native route suite performs thirty immediate comparisons without console pacing and checks reuse of one verifier controller and fresh independent direct replies. It deliberately changes the displayed JSON while direct and original browser replies match: that page change cannot forge Protected. Malformed/empty/oversized replies, HTML, unsuccessful status, redirects, a failed comparison half and cancellation all reject proof. A fresh comparison must recover afterward. These are local proxy/HTTP fixtures and synthetic addresses, not a live Proton account test or a complete leak audit.

## Network and download recovery

Network callbacks close the protection gate immediately and remember whether the adapter is available. An offline state prevents manual and automatic checks and cannot accept a route proof. Network return schedules a fresh check; stale monitor failures do not replace newer state. Paused pages and downloads still require explicit user action to resume. Failure messages use preset text rather than arbitrary exception details.

Downloads explain when Resume is available in the current session, when the server may restart a transfer, and when a fresh website download or sign-in is needed. Pending Resume clicks share one validation. A replacement operation remains associated with its original row and file until rebinding completes; private-session matching prevents selection of a different private session's row. During development, the native test caught an early removal of that pending marker, and the marker lifetime was corrected before packaging.

The download suite exercises real generated ZIP transfers, protection-blocked and duplicate Resume, interrupted connections, exact completed bytes and two explicitly injected independent native private sessions. Closing those sessions clears private rows and retains downloaded files. Ordinary private tabs continue to share their logical private session; this test does not introduce a new session selector. Nonresumable-range, sign-in and disk-space explanations are inspected in light/dark WPF renders.

## Release evidence and remaining checks

RELEASE-VERIFICATION.json binds the full application suite results to app/helper hashes. INSTALLER-VERIFICATION.json and PACKAGED-PAYLOAD-VERIFICATION.json identify isolated installer and installed-payload checks. Generated current-PC results are not shipped as spare-PC results. Old version-numbered reports are historical.

Still manual: live Proton off/on/restart validity and a real website verification flow; physical adapter interruption, sleep/reboot and monitor/input changes; clean/offline spare-PC setup with actual missing prerequisites and administrator prompts; a hosted GitHub update/rollback after approved publication. Private tabs remain unavailable while browser-only Proton/Tor is enabled. NuGet's vulnerability feed was unavailable during earlier builds; current build logs state whether a fresh scan was possible. No new whole-browser speed/memory benchmark is claimed.

GitHub upload status is recorded separately in GITHUB-UPLOAD-STATUS.md. Development and packaging here do not publish a release.

Primary references: [WebView2 threading](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model), [WebView2 protocol API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.calldevtoolsprotocolmethodasync), [Chromium Network protocol](https://chromedevtools.github.io/devtools-protocol/tot/Network/).
