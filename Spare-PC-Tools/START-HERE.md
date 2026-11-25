# Harbor 0.6.1 spare-PC checks

Extract the whole kit into a writable folder. This regular release uses the existing Harbor installation/profile; choose a spare PC for fresh install/uninstall tests.

1. Run Check-PC.cmd for read-only package and platform checks. Save the Results folder.
2. On the spare PC, run HarborBrowserSetup-0.6.1-release.exe. This installer is intentionally unsigned; Windows may warn or block it. Report a security block rather than disabling Windows protections. Handle any administrator prompt yourself. Check offline setup and, if required, the requested restart. No developer tools are needed.
3. Open Harbor Browser. Check tabs, bookmarks, history, downloads, settings, dark/light mode and resource monitor using test data.
4. Enable optional Proton, sign in yourself and connect. Wait for Protected. Run Connection-Checks.cmd and paste its displayed local address into Harbor. Unavailable IPv6 results are inconclusive.
5. Close/reopen Harbor. Check retained sign-in, Connect if needed, wait for Protected, then Reload the paused test tab.
6. Disconnect Proton. Confirm Disconnected / Browsing paused and blocked Reload. Reconnect and verify before explicitly reloading.
7. Check a brief network interruption, sleep/wake and reboot when convenient. Record both paused state and recovery. Avoid forms and active downloads during these tests.
8. Close Harbor and rerun setup to check upgrade/repair and saved test data. Uninstall on the spare PC and confirm data remains. Do not use an uninstall test on an everyday profile without a separate backup.

Optional: choose Harbor in Windows Default apps yourself, test a web link and local HTML file, then restore your preferred browser.

The helper records route equality/status and timing without saving public addresses or account data. It requests no camera/microphone. Its results do not establish complete DNS/background-traffic leak protection.

Before reverting to an older app, explicitly turn off and remove Proton, then close Harbor. Keep previous setup and data snapshots. Never copy authentication databases between beta and regular profiles.

Also check Help > Check for updates. With Proton/Tor enabled, checks must pause. GitHub assets remain unpublished until owner approval; unavailable hosting must not be shown as an up-to-date result. Do not attempt an actual rollback until the matching Harbor-signed release record and previous installer are available. These update signatures are separate from Windows installer signing. Never copy private signing keys into this kit.

The included harbor-update.json is a locally verified record for future hosting, not an active update feed. The previous installer is not included unless explicitly added to this kit. Matching Harbor source accompanies this kit; it contains historical test fixtures and upstream references, which are not additional apps to install. Local automated results are in LOCAL-PACKAGE-VERIFICATION.md. Run Check-PC.cmd on the spare PC to create its own Results; the kit is shipped without this computer's prefilled results.
