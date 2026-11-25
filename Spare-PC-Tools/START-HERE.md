# Harbor 0.7.1 spare-PC checks

Extract the whole kit into a writable folder. This regular release uses the existing Harbor installation/profile; choose a spare PC for fresh install/uninstall tests.

1. Run Check-PC.cmd for read-only package and platform checks. Save the Results folder.
2. On the spare PC, run HarborBrowserSetup-0.7.1-release.exe. This installer is intentionally unsigned; Windows may warn or block it. Report a security block rather than disabling Windows protections. Handle any administrator prompt yourself. Check offline setup and, if required, the requested restart. No developer tools are needed.
3. Open Harbor Browser. Check tabs, bookmarks, history, downloads, settings, dark/light mode and resource monitor using test data. Check History/Bookmarks/Settings title bars in both modes; move/resize those windows. Confirm the plus button follows the last tab and stays accessible in a crowded strip. Resize Proton's compact window and check its controls, footer and Options panel, including after signing in.

   Check Help > Page loading details in regular and private tabs. The report must contain categories and counts, not URLs, IP addresses or account data. On a site that requests verification, complete it yourself; the helper should return to the original page without reloading automatically. Use Reload explicitly. If no challenge occurs, mark the live challenge check as not exercised.
4. Enable optional Proton, sign in yourself and connect. Wait for Protected. Run Connection-Checks.cmd and paste its displayed local address into Harbor. Unavailable IPv6 results are inconclusive.
5. Close/reopen Harbor. Check retained sign-in, Connect if needed, wait for Protected, then Reload the paused test tab.
6. Disconnect Proton. Confirm Disconnected / Browsing paused and blocked Reload. Choose “Turn off protection — keep sign-in,” confirm VPN Off, then explicitly Reload. Enable Proton again and check that its sign-in is retained; connect, verify and Reload. Repeat after closing/reopening Harbor while protection is off. “Remove Proton and reset sign-in” under Options is a separate uninstall/reset action and is expected to clear the extension login while retaining the website login.

   While protection is enabled, try a private tab: Harbor must explain the private-route limitation and offer connection controls without creating an unprotected tab. With protection off, a private tab should open on the regular connection. Close private tabs before enabling Proton again.
7. Check a brief network interruption, sleep/wake and reboot when convenient. Record both paused state and recovery. Avoid forms and active downloads during these tests.

   While Windows reports no network adapter, route checks should stay paused. After the network returns, wait for a fresh Protected result and explicitly Reload. In Downloads, check the recovery explanation and Resume button. A second click while a resume check is pending must not queue a second request. Transfers that cannot resume should direct you back to the website for a fresh download. Use generated test files, and keep normal/private download lists separate.
8. Close Harbor and rerun setup to check upgrade/repair and saved test data. Uninstall on the spare PC and confirm data remains. Do not use an uninstall test on an everyday profile without a separate backup.

Optional: choose Harbor in Windows Default apps yourself, test a web link and local HTML file, then restore your preferred browser.

The helper records route equality/status and timing without saving public addresses or account data. It requests no camera/microphone. Its results do not establish complete DNS/background-traffic leak protection.

Before reverting to an approved older app, explicitly turn off protection and close Harbor. Keep previous setup and compatible data snapshots. Remove/reset Proton only when you intend to clear its extension sign-in. Never copy authentication databases between beta and regular profiles.

Also check Help > Check for updates. With Proton/Tor enabled, checks must pause. Full 0.7.1 assets are local until separately published; the public feed currently offers 0.7.0, so this build should describe itself as newer than that release. Unavailable hosting must not be shown as an up-to-date result. Do not attempt an actual rollback until the matching Harbor-signed release record and previous installer are available. These update signatures are separate from Windows installer signing. Never copy private signing keys into this kit.

If harbor-update.json is provided, it is a locally verified record for future hosting, not an active update feed. This local full 0.7.1 kit does not activate or include a new signed public update record. The previous installer is not included unless explicitly added to this kit. Matching Harbor source accompanies this kit; it contains historical test fixtures and upstream references, which are not additional apps to install. Local automated results are in LOCAL-PACKAGE-VERIFICATION.md. Run Check-PC.cmd on the spare PC to create its own Results; the kit is shipped without this computer's prefilled results.

For 0.7.1, enable optional unloading only with generated test pages. Confirm unsaved input, private/pinned tabs, media, and active/paused downloads stay loaded. Check the unloaded marker and Resource Monitor Reload. Returning to an unloaded page reloads its address and loses page state/Back-Forward history; keep important tabs awake. Restore a session with the last tab selected and confirm background tabs remain not loaded until selected.
