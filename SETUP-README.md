# Harbor Browser 0.7.1 setup

This is the local full unsigned 0.7.1 package. Preparing it does not install it or publish a GitHub release. The owner confirmed RC1's bookmarks/settings retention, Proton restart without signing in, network recovery and sleep/wake. The full build changes only the channel metadata/visible RC label from RC1 and is tested separately. Actual installation of this full package and clean/offline spare-PC setup remain separate checks. Automatic tests use owned profiles. Installed and portable launches share the regular profile. Preserve the working 0.7.0 and RC1 packages.

Close regular Harbor before running the installer. Handle Windows administrator approval yourself. Setup keeps the established Program Files installation and LocalAppData/HarborBrowser profile and backs up Harbor data files. Beta profiles remain separate. Windows packages are unsigned by the owner's choice; Windows may warn or block them. Do not bypass a Windows security block; report it for diagnosis.

The full installer embeds the pinned Microsoft-signed .NET Framework 4.8 and WebView2 x64 offline packages. No developer tools are needed on another PC. Usable dependencies are skipped, and setup never forces a reboot. Save work and restart yourself if Windows/setup requests it.

Supported package target: x64 Windows 10 build 19041+ / Windows 11, .NET Framework 4.8+, WebView2 157+. Older Windows, 32-bit Windows and ARM64 are outside this package. Clean installation and genuine dependency/restart behavior remain separate validation items.

The portable folder also uses the regular profile and single-instance identity. Close installed Harbor first; it is not an isolated test channel. Keep every extracted file, including HarborUpdateLauncher.exe, together.

Help > Check for updates exposes the updater. UPDATING.md explains the public GitHub feed, signing, consent, network behavior and recovery. The public latest remains full 0.7.0; this local full 0.7.1 build is ahead of it. When authenticated full 0.7.1 metadata is published, 0.7.1 RC1 can offer it. Windows signing itself has not been performed.

Proton remains optional. Connect yourself and wait for Protected. Disconnecting pauses supported browsing. Reconnect and explicitly Reload, or choose “Turn off protection — keep sign-in” to restore the regular connection, then Reload. Turning protection off leaves Proton installed but disabled and retains its saved data. Under Options, “Remove Proton and reset sign-in” uninstalls it and clears its extension sign-in; the website login remains separate. Proton may still require sign-in if its own saved session expires or is revoked. Update checks/downloads pause while Proton or Tor is enabled. Private tabs cannot coexist with an enabled browser connection. Coverage is limited to supported Harbor requests, not other apps or a complete leak audit.

Windows keeps its current default browser. Harbor advertises HTTP/HTTPS/HTML support; choose defaults yourself in Windows Settings. Uninstall retains profile data, cached update files/backups and Microsoft prerequisites. A failed update or rollback does not justify deleting the profile. Keep the working installation and its backups until an approved replacement is validated on your PC.
