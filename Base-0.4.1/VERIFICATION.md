# Harbor Browser 0.4.1 verification

Verified November 13, 2026 (America/Chicago). Work used isolated profiles and local fixtures. The installed Harbor, personal profile, 0.4.0 release and synced sources were not modified.

## Executed checks

| Check | Result |
| --- | --- |
| Regression, service, settings and logical-layout tests | 322 passed |
| Actual Windows app, interface, media, download, privacy and recovery tests | 281 passed |
| Packaged Home-page tests | 38 passed |
| Performance measurements | 14 measured runs, 138 raw samples; all expected suspension/wake/protection checks passed |
| Release build | Passed with zero errors; NU1900 warns that the online NuGet vulnerability feed was unreachable |
| Inno Setup / prerequisites | Passed; app/setup versions 0.4.1.0, embedded Microsoft prerequisites passed size/signature checks |

641 automated checks passed in total. Focused privacy checks were also rerun during development and are not counted twice. The regression/integration executable copies and staged executable have identical SHA-256 hashes; compiled/staged Home assets also match. Installer/source hashes are in HarborBrowser-0.4.1-SHA256.txt, distributed alongside the source archive.

## New behavior checked

- A tab's original inactivity deadline survives unrelated activity updates. An overdue real DispatcherTimer suspends the page.
- Background pages actually report native IsSuspended; selection wakes them with the same in-page token and no reload.
- Moon/dimming indicators, Keep awake, removing that exception, and disabling automatic sleep behave correctly.
- Switching to a tab during an asynchronous media check keeps it awake. Background navigation resets the old sleeping flag and restarts its timer.
- Resource Monitor row identity/selection survive refresh. Its sleep/wake/Keep awake controls target stable tab IDs. Light/dark views and the compact 520 × 340 layout were exercised and rendered previews visually inspected.
- Playing muted video in a separate-origin frame stays awake and remains playing. Real normal downloads and paused/resumable downloads protect their source tab.
- WebView2 processes report CPU samples; service tests cover first sample, normalization, PID reuse, counter rollback, clock boundaries and stale-process pruning.
- Failed browser-engine tabs are marked Needs reload rather than Sleeping, with unavailable wake controls.

## Privacy and recovery checked again

The existing camera/microphone/notification permission decisions, private rule/storage isolation, separate normal/private cleanup, time ranges, backup sanitation, bookmark/open-tab retention and real renderer/browser crashes were rerun. Physical devices were not accessed; camera allowance used the fake-device test option.

Additional checks exercised the configured 60-second permission timeout through a shortened test timer, actual timer-driven denial, and dismissal on navigation. This verifies the timeout path and configured interval without claiming a literal one-minute wall-clock test.

A real private download was paused as soon as Harbor tracked it. Declining last-private-tab closure retained the resumable job; confirming closure cancelled it, removed the row and ended the source connection. A separately completed private download retained its byte-identical file after private-session cleanup. The runtime can buffer the HTTP body before surfacing progress; this verifies a real unfinished paused job, not interruption at a guaranteed point in the network stream.

Two actual test-app runs verified clear-cookies/site-storage-on-exit: the first wrote cookies/local storage and closed through cleanup; the second reopened the same isolated profile and found them empty. Both closures completed and marked their journals clean. Cache clearing was requested, but individual cache-file erasure was not measured.

Earlier download name/extension/collision, real pause/range-resume/cancel, source-tab closure, ZIP byte integrity, tabs/groups/restoration, Settings, bookmark import/export, Home customization, suggestions, menu scrolling, logical scaling, fullscreen video and picture in picture were also retained in the suites.

## Performance evidence

See [PERFORMANCE.md](PERFORMANCE.md) and Measurements for the data, machine/runtime context, method and limits. The automatic-timer comparison reproduced 0.4.0 keeping all 10 tabs awake under repeated unrelated activity updates, while 0.4.1 slept 9. Both were repeated twice.

Longer 20-tab runs averaged 4.74% CPU awake and 2.81% with 19 tabs sleeping on this Windows 10/i7-7700HQ/12 GiB machine. WebView2 private commit averaged 976.3 MiB awake and 941.2 MiB sleeping. Short windows included transition spikes and mixed CPU results; no universal or version-wide speedup is claimed. The benchmark builds preceded a final failed-tab status correction; unchanged sleep/scheduling paths and runner hashes are documented in the performance report.

## Still requires user-system validation

- Fresh setup, upgrading an existing installed copy, missing-prerequisite/offline installation, restart handling and uninstall. No installer was run against the personal installation.
- Physical mouse/trackpad/keyboard use, Windows display/DPI/monitor transitions, actual taskbar/auto-hide behavior and hardware video decoding. Automated layout checks used logical sizes and injected monitor bounds.
- Real camera/microphone/location settings and hardware, delivered notifications, accessibility/screen readers and broad real-website workloads.
- Power loss, forced OS shutdown, disk-full failures, exported/manual copies outside managed backups and independent security auditing.

CPU/memory data is from one machine and synthetic local workloads. Memory remains best effort; sleeping retains page state rather than discarding tabs. Keep awake lasts only for the current tab/session. Full extension architecture and permission enforcement, signed releases, reliable updates and broad hardware/performance validation remain roadmap work.

Profile schema remains 7, as in 0.4.0. The installer identity is unchanged. Harbor/setup are not code-signed. Target packaging is x64 Windows 10/11; its configured minimum build is not a guarantee for every old runtime/Windows configuration.

## References

Implementation follows Microsoft's [suspend/resume behavior](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.trysuspendasync), [audio activity reporting](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.isdocumentplayingaudio) and [memory-target guidance](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.memoryusagetargetlevel). No forced working-set trim or combination of competing memory-management modes was introduced.