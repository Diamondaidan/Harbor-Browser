# Harbor 0.5.6 R3 — continued live testing

November 16, 2026, America/Chicago. Executable SHA-256: D10B7330E2151293A5D5E0F0B4274A700E064BF145B2FF6D36CB0CBEC6B6F604. The Harbor application source, executable, portable archive and source archive were not changed. The earlier eleven-suite / 1,046-assertion automated result belongs to this same executable; those suites were not rerun during this live session.

## Executed results

| Check | Result |
| --- | --- |
| Signed-in operation and closing provider controls | PASS: Harbor continued showing Protected |
| Main-page IPv4 route | PASS: completed and differed from the separate direct control |
| Web Worker and Service Worker IPv4 routes | PASS: both completed and differed from direct controls |
| Fresh signed-out direct browser | PASS positive control: all six IPv4/IPv6 comparisons matched the direct controls |
| Direct local STUN and WSS controls | PASS: five UDP packets, two host candidates and a successful secure WebSocket echo |
| Full connected sweep | INCOMPLETE twice: the browser paused/unloaded the test page |
| Connected sweep without WSS | INCOMPLETE: three IPv4 comparisons passed and zero local STUN packets/candidates were reported before interruption |
| IPv6-only sweep, with no media or WSS probe | REPRODUCED INTERRUPTION: page paused before completed route results were saved |
| Full process restart | PASS: original process exited; the same verified executable reopened using the retained beta profile |
| First observed restored tab before reconnection | PASS for the observed UI behavior: Disconnected and Browsing paused; harness-run count stayed at five |
| Retained sign-in and manual reconnect | PASS: user reported Connected without another sign-in; Protected was observed |
| Explicit Reload after reconnect | PASS: a new run completed all three IPv4 comparisons with routes different from direct controls |
| Reconnection status text | FINDING: a startup-timeout status appeared even while Protected was displayed; explicit Reload cleared it |
| Intentional disconnect | PASS for observed behavior: user and UI confirmed Disconnected / Browsing paused |
| Reload while disconnected | PASS: pause remained and local harness-run count stayed at six |
| Recovery after intentional disconnect | DEFERRED: user is away from the computer |
| Disconnected status description | FINDING: correct Disconnected state is followed by the previous Protected verification text |
| Real Wi-Fi/proxy outage, sleep/wake and reboot | Not executed |

The first restart observation was about fourteen seconds after process launch. An unchanged harness-run count establishes that no new test run began before the observed retry; it does not independently prove zero startup packets. Automatic reconnection was not demonstrated: the user clicked Connect. The sensitive account tab was excluded from restored tabs as designed; Home and the owned test tab remained.

## Reproduced IPv6 interruption

The main IPv4-only and three-context IPv4-only checks complete. Adding IPv6 reproduces a paused test tab, including an IPv6-only scenario without WebRTC or WebSockets. Harbor later shows Protected again and requires explicit Reload. The full IPv4/IPv6/WSS sweep completes in the separate direct browser, so the live harness and direct IPv6 path have positive controls.

This does not show an IPv6 leak, nor establish the exact provider/engine error. No completed protected IPv6 comparison was recorded. Source review shows that Harbor handles proxy changes, privacy-policy changes and proxy errors with the same invalidation message. A request-level proxy error is a hypothesis, not a confirmed cause. The event should be classified in an owned fixture before changing the guard. Chrome documents proxy error reporting separately from settings changes in its [proxy error event documentation](https://developer.chrome.com/docs/extensions/reference/api/proxy#event-onProxyError).

The live interruption, reconnect startup status and stale disconnected description need development follow-up before a stable shipping claim. The application was left unchanged because this session is testing.

## Resource observations

Three thirty-sample process-tree observations were collected. They are separate workload snapshots, not a matched VPN-on/off comparison. The first had two tabs and open provider controls; the second included a probe/interruption; the post-restart snapshot has two restored tabs and closed controls. Periodic route verification also creates temporary browser processes.

The first sample recorded median private commit of 568.305 MiB, summed working set 806.957 MiB, median CPU 0% and p95 CPU 13.615% across eight logical processors. Process counts ranged from ten to seventeen. The probe/interruption sample recorded median private commit of 694.306 MiB but had unreadable/exiting processes, so it is a partial observation.

The post-restart two-tab sample had all processes readable: median private commit 368.953 MiB, median summed working set 670.588 MiB and median/p95 CPU 0/0%, with a maximum sampled CPU of 20.801%. Process counts ranged from nine to sixteen. The process/CPU bursts and differing workloads mean these snapshots should not be subtracted to estimate VPN overhead.

Private commit is not physical RAM; summed working sets can count shared pages twice. These observations cannot establish isolated VPN overhead or general lightweight behavior. The historical Hotfix 2 +8.242 MiB comparison is not a result for R3.

## Evidence and privacy

Evidence is in ../Harbor-Connected-Checks/Results/beta056-r3/live-20261116-134332 and its isolated-probes subfolder. Resource JSON/CSV files are in the parent beta056-r3 folder. Original full checks and scenario-selection checks use separate local fixture processes. Scenario support was added only to a copy of the test harness under that evidence folder; Harbor and the earlier harness files were preserved.

The harness compares public addresses in memory and saves family/status/equality, elapsed time, candidate types/counts and local STUN counts. It does not save public IP values, credentials or account state. No profile files, authentication storage, proxy credentials or broad packet trace were inspected. User authentication and VPN/network controls remain manual.

## Handoff while the user is away

The user reported being away from the computer and chatting remotely. Recovery after the deliberate disconnect and real network/sleep/reboot tests were deferred. The final desktop/process check at 17:05 America/Chicago found no R3 beta process or window; it was not reopened. Installed Harbor was left untouched.

The two owned local fixture servers were stopped after verifying their exact process paths. Reports and test profiles were retained. Restart the local test fixtures before retrying their loopback test page in a later session. No connection, Windows networking or privacy setting was changed by the agent.
