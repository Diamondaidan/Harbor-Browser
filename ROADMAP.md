# Harbor 0.6.1 development roadmap

Delivered development scope: explicit GitHub update checks, signed release records, verified/resumable downloads, consent before setup, compatible previous-version recovery, data snapshots and future Windows signing tools.

0.6.1 adds immediate route-evidence invalidation on network/wake callbacks, clock-safe verification leases and 67 targeted regression checks. All 1,284 app assertions and 94 isolated installer checks passed. Owned-profile startup/CPU/memory measurements are recorded separately. Physical hardware and signed public-update gates remain open.

Next validation: prepare clearly labeled unsigned packages for final review, then publish owner-approved assets and test an actual hosted newer-release update and rollback; validate an approved candidate on the spare PC, including genuine offline/missing prerequisites, sleep/reboot and network interruption. Keep broader privacy, accessibility and hardware/site testing as separate gates toward 1.0. SignPath is no longer a dependency. The frozen 0.6.0 release stays draft while this separate development branch is reviewed.

No automatic public upload, silent install, key rotation or complete TUF implementation is included. See UPDATING.md, LIVE-VERIFICATION.md and the final verification reports.
