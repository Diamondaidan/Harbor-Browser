# Harbor Browser 0.6.1 development notes

- Add “Turn off protection — keep sign-in” in Proton controls and paused pages. This restores the regular connection after explicit Reload while preserving the installed extension and its saved data.
- Keep “Remove Proton and reset sign-in” as a separate action under Options. Disconnect still pauses browsing; unfinished or failed off operations cannot silently open the request gate.
- Match Harbor-owned dialog title bars to the browser's light/dark palette, including live theme changes on Windows 10.
- Use a compact Proton window with the extension given most of the space; place secondary actions in a collapsed options panel and scale the provider controls on small windows.
- Keep the new-tab button directly after the last tab, with the button still accessible when the tab strip overflows.
- Invalidate Proton route evidence as soon as a network/address/suspend/resume callback arrives, even while the interface is busy.
- Cancel obsolete route checks and schedule a fresh check after a transition; keep paused pages and downloads from resuming automatically.
- Use monotonic elapsed time and UTC bounds for route evidence. Moving the system clock backward cannot prolong an old Protected claim.
- Preserve the established profile format and pinned provider files. Add synthetic event/clock coverage and startup measurements to the owned-profile harness.

The owner has chosen unsigned Windows packages after SignPath declined the application. Public installer publication still requires final release review and validation. These fixes are validated with synthetic callbacks and local fixtures; actual physical sleep/Wi-Fi and signed-in IPv6 tests remain open.

## Retained update and recovery features from 0.6.0

- Check for updates from the Help menu; updates and backup recovery are also available under Tools.
- Read release notes, download a verified update and explicitly choose when to close Harbor and open setup. There are no automatic startup checks or silent installations.
- Update records use Harbor's embedded release public key. Expired, altered, foreign, inconsistent or older release records are refused. Installer size and SHA-256 must match the signed record.
- Interrupted transfers can resume with matching strong server validators and exact byte ranges. Changed or ignored ranges restart safely or are refused; failed verification never opens the installer.
- The update helper rechecks the signed record and installer, holds the package against modification, waits for Harbor to exit, then snapshots compatible profile data before opening setup. Windows approval and restart remain user choices.
- A compatible previous version can be offered only when explicitly approved in the current signed record. Profile format remains 9, shared with 0.5.7. Bookmarks/settings and other Harbor data files are snapshotted; account databases remain in place and are not copied.
- Update checks/downloads use the Windows connection and pause while Proton or Tor is enabled. Enabling a browser connection cancels pending update traffic.
- Prepared GitHub Releases hosting for Diamondaidan/Harbor-Browser and optional Windows code-signing tools. Current packages are intentionally unsigned and will be labeled accordingly. Harbor's own update signatures remain required.

The application retains the established Harbor installation/profile and browser registration. Proton behavior and its limited supported-browser-request scope remain as in 0.5.7; this release does not add system-wide VPN, DNS or anonymity guarantees.

This build targets x64 Windows 10 build 19041+ / Windows 11 with .NET Framework 4.8+ and WebView2 157+. Actual live hosting, a real newer-release self-update/rollback, genuine missing-prerequisite setup, clean spare-PC installation, network interruption and Windows sleep/reboot remain manual validation items.
