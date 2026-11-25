# Harbor Browser 0.5.3

A stability release for Windows x64, with a shorter random private-session identity, bounded startup waits, an in-tab Reload recovery action, and version labels generated from VERSION.txt and the running application.

Use dist/HarborBrowserSetup-0.5.3.exe to install or upgrade. Setup bundles Microsoft's signed offline .NET Framework 4.8 and WebView2 x64 runtime packages, plus the optional Tor client and its matching source/notices. Administrator access is required. Setup logs prerequisite detection, skipped installers, helper exit codes and restart requests.

The regular Harbor profile remains %LOCALAPPDATA%\HarborBrowser. Setup backs up existing core profile files before upgrade and retains profile data on uninstall. It advertises Harbor in Windows Default apps without changing your selected default. The 0.5.2 source and packages are preserved separately.

Tor remains experimental and off by default. It routes normal Harbor pages and browser-managed downloads, with private tabs and native online typing suggestions paused while enabled. See VPN-PREVIEW-README.md. This release does not claim a complete DNS/WebRTC or anonymity audit.

VERIFICATION.md distinguishes executed tests from untested clean-computer prerequisite, reboot and hardware cases. The earlier spare-PC results apply to 0.5.2 and do not prove a missing-prerequisite installation for 0.5.3. Harbor and setup remain unsigned, so Windows may display publisher/reputation prompts.

Build with Visual Studio 2022 desktop build tools, the .NET Framework 4.8 Developer Pack and Inno Setup 7. Run Build-Installer.ps1. VERSION.txt is the release version source; the builder refuses a conflicting -Version argument. See SETUP-README.md for prerequisite download instructions. The finished installer needs no separate prerequisite download before setup.
