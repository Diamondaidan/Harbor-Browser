# Harbor Browser 0.5.4

Connection monitoring and a consistent dark interface for Windows x64. A persistent Tor toolbar button shows off, connecting, checking, verified and failed states. History, Bookmarks, Settings and other Harbor dialogs now apply the selected light/dark mode to their native Windows title bars, including changes while open.

Use dist/HarborBrowserSetup-0.5.4.exe to install or upgrade. Setup includes Microsoft's signed offline .NET Framework 4.8 and WebView2 x64 runtime installers, the optional Tor client and matching source/notices. Administrator access is required. Harbor and setup remain unsigned.

The normal profile stays in %LOCALAPPDATA%\HarborBrowser. Setup snapshots existing core profile files before upgrade and keeps the profile on uninstall. Browser registration leaves the selected Windows default unchanged. Previous release packages are preserved separately.

Tor is optional, off by default and experimental. Before showing Tor On, Harbor checks its actual browser route against the Tor Project. It checks again every five minutes and after network/wake notifications. A failed check pauses new managed page requests until successful verification or explicit Disconnect and restore. The connection panel provides Check connection now and Reconnect with Tor. See VPN-PREVIEW-README.md for scope and limitations.

VERIFICATION.md records executed tests and remaining clean-PC, reboot, hardware and privacy-audit work. Earlier spare-PC results apply to 0.5.2. Automated network/wake simulations do not certify every laptop's sleep or network behavior.

Build with Visual Studio 2022 desktop build tools, .NET Framework 4.8 Developer Pack and Inno Setup 7. Run Build-Installer.ps1. VERSION.txt controls release versions. SETUP-README.md describes prerequisite downloads for rebuilding; the completed installer includes those prerequisites.
