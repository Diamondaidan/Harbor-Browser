# Installing Harbor Browser 0.6.0

Close regular Harbor before running HarborBrowserSetup-0.6.0-release.exe. Setup requests Windows administrator approval, keeps the established Program Files installation and LocalAppData/HarborBrowser profile, and backs up Harbor data files. Beta profiles remain separate. The installer is unsigned until the owner obtains a Windows code-signing certificate. Do not bypass a Windows security block; report it for diagnosis.

The full installer embeds the pinned Microsoft-signed .NET Framework 4.8 and WebView2 x64 offline packages. No developer tools are needed on another PC. Usable dependencies are skipped, and setup never forces a reboot. Save work and restart yourself if Windows/setup requests it.

Supported package target: x64 Windows 10 build 19041+ / Windows 11, .NET Framework 4.8+, WebView2 157+. Older Windows, 32-bit Windows and ARM64 are outside this package. Clean installation and genuine dependency/restart behavior remain separate validation items.

The portable folder also uses the regular profile and single-instance identity. Close installed Harbor first; it is not an isolated test channel. Keep every extracted file, including HarborUpdateLauncher.exe, together.

Help > Check for updates exposes the new updater. UPDATING.md explains the GitHub feed, signing, consent, network behavior and recovery. Until release files are published, online checks cannot report a verified available release. Signing support is prepared; Windows signing itself has not been performed.

Proton remains optional. Connect yourself and wait for Protected. Disconnecting pauses supported browsing; reconnect and explicitly Reload. Update checks/downloads pause while Proton or Tor is enabled. Private tabs cannot coexist with an enabled browser connection. Coverage is limited to supported Harbor requests, not other apps or a complete leak audit.

Windows keeps its current default browser. Harbor advertises HTTP/HTTPS/HTML support; choose defaults yourself in Windows Settings. Uninstall retains profile data, cached update files/backups and Microsoft prerequisites. A failed update or rollback does not justify deleting the profile. Keep 0.5.7 and its backups until 0.6.0 is validated on your PC.
