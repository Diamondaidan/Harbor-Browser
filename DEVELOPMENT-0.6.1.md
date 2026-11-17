# Harbor 0.6.1 development

Started November 17, 2026 from the separate signing-preparation branch. The 0.6.0 draft and the installed browser remain preserved. No Windows signing account, paid resource, public binary release or updater key rotation is part of this work.

## Corrections

An isolated negative control reproduced a network-notification race: while the interface dispatcher was held busy, a simulated network-loss callback returned with the request gate still open. The correction invalidates thread-safe route evidence synchronously, then queues visual updates, cancellation and scheduling on the interface thread. Older route results cannot reopen the gate.

A second negative control moved an injected UTC clock backward five minutes. The old state continued to report Protected. Route evidence now expires using monotonic elapsed time as well as UTC, and backward UTC changes invalidate it. Tests change injected clocks only, never the Windows clock.

The new integration suite exercises network lost/returned, address changes, suspend and resume callbacks against local fixture pages, including a busy dispatcher, stale results, canceled work, server-counted request denial, fresh verification and explicit reload. The account provider package is unmodified. These are synthetic events and tests of supported browser requests, not a system-wide kill-switch or complete leak audit.

## Automatic verification

The UI candidate passed **1,330 application assertions across 17 suites** and **94 isolated native installer assertions**. The **12 performance cases with 120 samples** in the retained baseline describe the preceding executable; they were not rerun for the UI changes. See RELEASE-VERIFICATION.md, INSTALLER-VERIFICATION.md and PERFORMANCE-0.6.1.md for hashes, results and limits. Startup measurements cover creation of the owned test window until its first browser tab is initialized; they are not a cold Windows boot or the installed production profile.

Layout coverage uses offscreen WPF windows with simulated available sizes and scaling. Memory/CPU measurements use synthetic local workloads and separate profiles. They describe this computer and runtime, and do not establish performance improvements over a prior build without a matching comparison.

## Remaining physical and external checks

- Actual spare-PC clean setup, missing/offline Microsoft prerequisites and restart behavior.
- Physical Wi-Fi transitions, suspend/resume and reboot, using a signed-in Proton account.
- Live IPv6 behavior; the prior unavailable result is unresolved.
- Real monitor/DPI transitions, mouse/trackpad tab dragging, taskbar access and fullscreen video on target hardware.
- An actual public GitHub update/rollback cycle after approved publication. The owner chose unsigned Windows packages after SignPath's rejection; provider approval and Windows signatures are no longer release gates. A future signing provider would need separate signature/clean-PC validation.

Do not interpret older inherited 0.5.x/0.6.0 verification documents as results for this candidate. The regular application shares the established Harbor profile; automated runtime checks must continue to use injected owned stores rather than launching Harbor normally against a pretend profile.

Platform references used for the event/elapsed-time handling:

- https://learn.microsoft.com/dotnet/api/system.net.networkinformation.networkchange.networkaddresschanged
- https://learn.microsoft.com/dotnet/api/microsoft.win32.systemevents.powermodechanged
- https://learn.microsoft.com/dotnet/api/system.diagnostics.stopwatch
