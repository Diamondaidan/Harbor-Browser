# Harbor roadmap

Updated November 13, 2026. Current release: 0.5.0 (build 0.5.0.0), including the earlier browsing, organization and personalization features.

This combines the original milestones with the newer usability and compatibility work. Milestone numbers are planning labels; completion is tracked by delivered behavior and verification. Unfinished work from earlier milestones carries forward.

| Milestone | Scope | Current position |
| --- | --- | --- |
| 0.1.1 — Working installer and stability | Self-contained setup, prerequisite handling, safe upgrades, profile preservation, reliable startup and shutdown. | Installer built; installation, upgrade and uninstall checks on a separate Windows laptop remain. |
| 0.2.0 — Memory optimization and tab suspension | Sleeping/waking tabs, sensible exceptions for active media and downloads, memory/CPU measurements, resource-monitor improvements. | 0.4.1 adds reliable inactivity timing, media/download protection, tab controls, per-process CPU sampling and repeatable local measurements. Broader hardware/site tuning remains. |
| 0.3.0 — Modular extension architecture | A defined module/extension interface, loading and lifecycle, enable/disable controls, version checks and permissions. | 0.4.2 adds reviewed local Manifest V3 site modules, lifecycle controls, API/minimum-version checks, native site scope and isolated execution. Broader extension APIs/store compatibility remain. |
| 0.3.6 — Compatibility and interface validation | 1366 × 768 layouts, Windows scaling, compact menus, address selection, download filenames, tab controls, fullscreen and picture in picture. | Implemented with automated checks; physical laptop, display/scale and installation checks remain. |
| 0.3.7 — Everyday browsing | Reopen closed tabs, address suggestions from bookmarks/history, a dedicated Downloads page with progress and controls. | Implemented and checked in the isolated app; physical shortcut/desktop and laptop installation checks remain. |
| 0.3.8 — Organization and personalization | Bookmark folders and import/export, editable home shortcuts, custom home backgrounds and saved appearance presets. | Implemented with folder import/export, editable shortcuts, custom backgrounds, presets and optional online completion; automated checks passed. |
| 0.4.0 — Privacy and security | Per-site permission controls, browsing-data cleanup, private-browsing verification, extension permission enforcement and stronger crash/session recovery. | Site-permission controls, scoped browsing-data cleanup, private-session isolation and crash recovery are implemented and tested. 0.4.2 adds restricted site-module permission enforcement and private/Home isolation. Broader extension capabilities and independent security review remain. |
| 0.4.1 — Performance and stability | Independent sleep deadlines, Keep awake, tab-state resource monitor, repeatable measurements and privacy edge tests. | Implemented; 641 automated checks and 14 measured runs passed. Installer/upgrade and physical-system validation remain. |
| 0.4.2 — Extension foundation and organization fixes | Reviewed site-module lifecycle, enable/disable, version checks, capabilities and failure isolation; tab/menu/icon fixes and bookmark ordering. | Implemented and checked with real WebView2 modules. Normal tabs share widths and shrink; bookmark-bar order persists. Physical drag/trackpad and installed upgrade checks remain. |
| 0.4.3 — Reliability and extension controls | Saved site-pattern choices, clearer module status/errors, compact dialogs, bookmark drop surfaces and overflow-tab visibility; installation/upgrade validation. | Implemented with real native grant/revocation checks, live-site checks and isolated installer/upgrade/uninstall fixtures. Physical input, actual target-laptop scaling and the normal administrator installer remain validation gates. |
| 0.5.0 — Windows VPN and browser integration | Scoped Windows VPN profiles/status/actions, clearer private tabs, Windows default-browser registration and external-link activation. | Implemented. Read-only Windows discovery, simulated VPN actions, real App startup/forwarding, private marker checks and isolated registration/upgrade/uninstall fixtures passed. Real VPN server/routing, default selection in Windows UI and target-laptop validation remain. |
| 1.0.0 — Polished, stable release | Consistent interface and accessibility, reliable recovery, measured performance, broad website compatibility, signed installer, reliable updates and installation/recovery documentation. | Release goal; depends on earlier milestones passing their checks. |

## Recommended sequence

1. Validate 0.5.0 on the target Windows 10 laptop, including a fresh installation and an upgrade, 1366 × 768 at the user's actual scale setting, downloads, mouse/trackpad scrolling, tab dragging, taskbar access and video playback.
2. Close unfinished foundation work from 0.1.1–0.3.0, especially broader extension compatibility and hardware/site memory measurements. Track the work explicitly as it is completed in subsequent builds.
3. Validate the delivered 0.3.7 and 0.3.8 browsing, organization and customization features with real user data and desktop interactions.
4. Follow 0.5.0 with real VPN/server and Windows-default-selection validation, target-laptop/input checks, broader extension capability review and real-site/hardware measurements. The full security milestone remains open.
5. Use the 1.0.0 criteria as the final release gate.

## Verification gates

- Installer: fresh setup and upgrades preserve user data and handle missing prerequisites; uninstall behaves as documented.
- Memory: record repeatable results with multiple active, sleeping and media-playing tabs before choosing optimization targets.
- Extensions: a sample extension loads, can be disabled, receives only its allowed capabilities and has defined failure handling.
- Everyday browsing: closed tabs reopen in the expected order, private history stays excluded from suggestions, and download actions handle completion and interruption clearly.
- Privacy: site decisions can be reviewed/revoked, cleanup affects the chosen data, and private-session isolation is verified.
- VPN: status reflects the real connection, controls affect the selected profile, and failures leave the browser usable.
- Stable release: no known release-blocking defects, repeatable installation/recovery checks and a documented support scope.

The current verification report is in HarborBrowser-0.5.0/VERIFICATION.md. The first site-module foundation is delivered; broader extension APIs, installer/hardware validation and performance tuning remain tracked separately from implemented browsing, personalization and privacy/recovery features.
