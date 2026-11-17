# Harbor 0.5.6 R4 — verification

November 16, 2026, America/Chicago. All twelve automated suites passed 1,080 assertions. Executable SHA-256: 027C3C6AA782FAAA9D7D4BAFC92710921FC117828AAC16D1D6986C939161A81C. R3 builds, archives and the retained 056G profile are preserved. All 336 provider files are unchanged. This remains a private portable beta; no production installer or public release is included.

## Request failure and connection protection

An isolated native HTTPS proxy reproduced the R3 failure: rejecting an owned CONNECT destination generated net::ERR_TUNNEL_CONNECTION_FAILED with fatal=true and unloaded its parent tab. The original failure log and redacted native-event result are retained in Evidence/056-r4/r3-native-error-baseline.*.

R4 distinguishes an aborted transaction from changed proxy ownership, changed WebRTC policy and unsafe/unknown proxy errors. A failed transaction leaves unrelated pages usable under their existing verification lease and fresh ownership checks. It schedules a bounded route refresh without renewing evidence. Repeated errors cannot queue unlimited immediate checks. Settings changes, unsafe errors, failed verification, evidence expiry and network changes retain their pause behavior. The native rejecting fallback remains in place.

The owned proxy test now requires the rejected request's parent to remain alive and a following request to pass through the same HTTPS proxy. Real native proxy clearing and privacy-policy changes must still pause the parent. An unknown or nonfatal proxy error still closes the gate. These checks do not establish live Proton IPv6 support; the user's IPv6 retry remains necessary.

## Status corrections

The indicator describes its current protection reason instead of appending an old successful check to Disconnected. Starting or timing out a background/hidden sign-in view cannot overwrite the selected page's status. A forced hidden-view timeout verifies that the visible page and its status survive.

## Route-check resource change

R3 created a separate browser environment and two controllers for every comparison. R4 reuses one verifier controller in the existing environment/profile and uses a direct, credential-free HTTP request for the comparison. Production contacts the same HTTPS ipify endpoint; no account cookies, credentials or public address values are retained by the host. The user-facing disclosure already states that verification contacts ipify through the browser and separately without Proton.

The verifier is restricted to the expected HTTPS origin/path, with script execution, web messages, permissions, new windows and downloads disabled. Every comparison checks native ownership/policy before and after the address comparison. Same-address replies cannot verify a route. Cancellation releases the cached native view; a later check creates one replacement. Completion waits for the native navigation as well as its response body before reusing the view.

The automated resource comparison runs two owned component scenarios with local fake-address endpoints: a reproduction of the prior controller/environment lifecycle and the R4 session. It uses the current SDK/parser in both phases. It is not a full R3/R4 live VPN overhead benchmark.

Both scenarios completed three comparisons and their thirty-sample process-tree measurements:

| Owned component scenario | Extra direct browser processes | Median / p95 private commit | p95 CPU | Maximum read process count |
| --- | ---: | ---: | ---: | ---: |
| Earlier lifecycle reproduction | 3 | 369.965 / 404.094 MiB | 14.968% | 15 |
| R4 reused view + HTTP control | 0 | 328.932 / 350.723 MiB | 0% | 10 |

The earlier-lifecycle measurement had exiting/unreadable processes, so it can undercount memory and CPU; no precise overhead reduction is claimed. R4's sample had all processes readable and maximum sampled CPU of 1.564%. CPU is normalized across eight logical processors; private commit is not physical RAM. The reliable structural result is three distinct extra browser processes in the earlier lifecycle versus none and one reused native verifier in R4. Full evidence and scope are in Evidence/056-r4/resource-comparison.json.

## Validation status

Desktop MSBuild Release builds passed. The full run in T/1116173250 passed all twelve suites, including 34 R4 assertions and the previously failing native case. BETA-VERIFICATION.json binds the passing logs and both runner copies to the executable hash. Each separate component benchmark passed five native assertions; those ten checks are additional to the 1,080 release-suite assertions.

Remaining live checks: R4 sign-in/reconnection and IPv6 retry, recovery after the final R3 intentional disconnect, real Wi-Fi/proxy outage, sleep/wake, reboot, matched connected workload measurements and production installer/clean-install coverage. The user is currently away from the computer. No user profile data, authentication state or connection controls were accessed by automation.

Technical references: [Chrome proxy errors](https://developer.chrome.com/docs/extensions/reference/api/proxy#event-onProxyError), [WebView2 performance guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance).
