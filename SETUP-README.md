# Harbor Browser 0.6.1 development setup scope

This is a development candidate, not an installed or signed public release. The automated checks use separate test profiles and do not upgrade the regular browser. A portable candidate still shares the regular profile when launched normally. Use the documented owned test hosts for automatic runtime checks.

For an approved future installer, close regular Harbor before running setup. It requests Windows administrator approval, keeps the established Program Files installation and LocalAppData/HarborBrowser profile, and backs up Harbor data files. Beta profiles remain separate. Windows packages are unsigned by the owner's choice; Windows may warn or block them. Do not bypass a Windows security block; report it for diagnosis.

The full installer embeds the pinned Microsoft-signed .NET Framework 4.8 and WebView2 x64 offline packages. No developer tools are needed on another PC. Usable dependencies are skipped, and setup never forces a reboot. Save work and restart yourself if Windows/setup requests it.

Supported package target: x64 Windows 10 build 19041+ / Windows 11, .NET Framework 4.8+, WebView2 157+. Older Windows, 32-bit Windows and ARM64 are outside this package. Clean installation and genuine dependency/restart behavior remain separate validation items.

The portable folder also uses the regular profile and single-instance identity. Close installed Harbor first; it is not an isolated test channel. Keep every extracted file, including HarborUpdateLauncher.exe, together.

Help > Check for updates exposes the new updater. UPDATING.md explains the GitHub feed, signing, consent, network behavior and recovery. Until release files are published, online checks cannot report a verified available release. Signing support is prepared; Windows signing itself has not been performed.

Proton remains optional. Connect yourself and wait for Protected. Disconnecting pauses supported browsing; reconnect and explicitly Reload. Update checks/downloads pause while Proton or Tor is enabled. Private tabs cannot coexist with an enabled browser connection. Coverage is limited to supported Harbor requests, not other apps or a complete leak audit.

Windows keeps its current default browser. Harbor advertises HTTP/HTTPS/HTML support; choose defaults yourself in Windows Settings. Uninstall retains profile data, cached update files/backups and Microsoft prerequisites. A failed update or rollback does not justify deleting the profile. Keep the working installation and its backups until an approved replacement is validated on your PC.
