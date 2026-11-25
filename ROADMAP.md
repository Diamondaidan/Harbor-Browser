# Harbor roadmap

Updated November 12, 2026. Current release: 0.3.7, including the 0.3.6 compatibility and History scrollbar fixes.

This combines the original milestones with the newer usability and compatibility work. Milestone numbers are planning labels; completion is tracked by delivered behavior and verification. Unfinished work from earlier milestones carries forward.

| Milestone | Scope | Current position |
| --- | --- | --- |
| 0.1.1 — Working installer and stability | Self-contained setup, prerequisite handling, safe upgrades, profile preservation, reliable startup and shutdown. | Installer built; installation, upgrade and uninstall checks on a separate Windows laptop remain. |
| 0.2.0 — Memory optimization and tab suspension | Sleeping/waking tabs, sensible exceptions for active media and downloads, memory/CPU measurements, resource-monitor improvements. | Tab suspension and resource reporting exist; broader performance measurements and tuning remain. |
| 0.3.0 — Modular extension architecture | A defined module/extension interface, loading and lifecycle, enable/disable controls, version checks and permissions. | Feature services are separated; a complete extension system still needs implementation and verification. |
| 0.3.6 — Compatibility and interface validation | 1366 × 768 layouts, Windows scaling, compact menus, address selection, download filenames, tab controls, fullscreen and picture in picture. | Implemented with automated checks; physical laptop, display/scale and installation checks remain. |
| 0.3.7 — Everyday browsing | Reopen closed tabs, address suggestions from bookmarks/history, a dedicated Downloads page with progress and controls. | Implemented and checked in the isolated app; physical shortcut/desktop and laptop installation checks remain. |
| 0.3.8 — Organization and personalization | Bookmark folders and import/export, editable home shortcuts, custom home backgrounds and saved appearance presets. | Planned; extends existing bookmarks, home page and light/dark customization. |
| 0.4.0 — Privacy and security | Per-site permission controls, browsing-data cleanup, private-browsing verification, extension permission enforcement and stronger crash/session recovery. | Existing privacy features provide a starting point; fuller controls and verification remain. |
| 0.5.0 — VPN integrations | Clear connection status, profile management, profile-specific connection controls, useful error handling and tested supported integrations. | Basic Windows VPN profile connection commands exist; deeper integration remains planned. |
| 1.0.0 — Polished, stable release | Consistent interface and accessibility, reliable recovery, measured performance, broad website compatibility, signed installer, reliable updates and installation/recovery documentation. | Release goal; depends on earlier milestones passing their checks. |

## Recommended sequence

1. Validate 0.3.7 on the target Windows 10 laptop, including a fresh installation and an upgrade, 1366 × 768 at the user's actual scale setting, downloads, mouse/trackpad scrolling, tab dragging, taskbar access and video playback.
2. Close unfinished foundation work from 0.1.1–0.3.0, especially extension architecture and memory measurements. Track the work explicitly as it is completed in subsequent builds.
3. Validate the delivered 0.3.7 everyday browsing features, then deliver the organization improvements in 0.3.8.
4. Complete privacy/security before expanding VPN integrations.
5. Use the 1.0.0 criteria as the final release gate.

## Verification gates

- Installer: fresh setup and upgrades preserve user data and handle missing prerequisites; uninstall behaves as documented.
- Memory: record repeatable results with multiple active, sleeping and media-playing tabs before choosing optimization targets.
- Extensions: a sample extension loads, can be disabled, receives only its allowed capabilities and has defined failure handling.
- Everyday browsing: closed tabs reopen in the expected order, private history stays excluded from suggestions, and download actions handle completion and interruption clearly.
- Privacy: site decisions can be reviewed/revoked, cleanup affects the chosen data, and private-session isolation is verified.
- VPN: status reflects the real connection, controls affect the selected profile, and failures leave the browser usable.
- Stable release: no known release-blocking defects, repeatable installation/recovery checks and a documented support scope.

The current verification report is in HarborBrowser-0.3.7/VERIFICATION.md. The earlier extension-architecture and performance milestones remain unfinished and are tracked separately from the delivered 0.3.7 browsing features.
