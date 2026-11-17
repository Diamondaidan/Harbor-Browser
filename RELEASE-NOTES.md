# Harbor Browser 0.6.0 release notes

- Check for updates from the Help menu; updates and backup recovery are also available under Tools.
- Read release notes, download a verified update and explicitly choose when to close Harbor and open setup. There are no automatic startup checks or silent installations.
- Update records use Harbor's embedded release public key. Expired, altered, foreign, inconsistent or older release records are refused. Installer size and SHA-256 must match the signed record.
- Interrupted transfers can resume with matching strong server validators and exact byte ranges. Changed or ignored ranges restart safely or are refused; failed verification never opens the installer.
- The update helper rechecks the signed record and installer, holds the package against modification, waits for Harbor to exit, then snapshots compatible profile data before opening setup. Windows approval and restart remain user choices.
- A compatible previous version can be offered only when explicitly approved in the current signed record. Profile format remains 9, shared with 0.5.7. Bookmarks/settings and other Harbor data files are snapshotted; account databases remain in place and are not copied.
- Update checks/downloads use the Windows connection and pause while Proton or Tor is enabled. Enabling a browser connection cancels pending update traffic.
- Prepared GitHub Releases hosting for Diamondaidan/Harbor-Browser and Windows code-signing tools. Windows signing remains pending a certificate. Unsigned installers are identified clearly.

The application retains the established Harbor installation/profile and browser registration. Proton behavior and its limited supported-browser-request scope remain as in 0.5.7; this release does not add system-wide VPN, DNS or anonymity guarantees.

This build targets x64 Windows 10 build 19041+ / Windows 11 with .NET Framework 4.8+ and WebView2 157+. Actual live hosting, a real newer-release self-update/rollback, genuine missing-prerequisite setup, clean spare-PC installation, network interruption and Windows sleep/reboot remain manual validation items.
