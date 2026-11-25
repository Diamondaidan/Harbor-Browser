# Harbor Browser 0.5.2

Windows x64 release with the Beacon identity, customizable light/dark appearance, movable tabs and bookmarks, downloads, history, site modules, private tabs and an optional free Tor connection.

Run HarborBrowserSetup-0.5.2.exe to install or upgrade Harbor. Setup bundles Microsoft-signed .NET Framework 4.8 and the WebView2 x64 offline runtime packages, plus the Tor client. No separate prerequisite download is needed before setup. Administrator access is required; Windows may request a restart. Windows 10 version 1709 or later on an Intel/AMD 64-bit PC is required. Windows ARM64 and 32-bit PCs are not supported by this installer.

This regular release uses the existing %LOCALAPPDATA%\HarborBrowser profile. Setup makes an UpgradeBackups snapshot of existing core profile files, preserves browser data on uninstall, and advertises Harbor in Windows Default apps without changing your choice. Close Harbor before upgrading. Keep a separate backup of important data.

The free Tor connection remains experimental and off by default. It routes normal Harbor pages and browser-managed downloads. Private tabs and online typing suggestions pause while connected. Disconnect and restore returns to direct browsing. See VPN-PREVIEW-README.md for scope and limits; this is not Tor Browser or a conventional VPN.

VERIFICATION.md records tests performed for this release and remaining limits. Third-party sources and notices are included in Licensing in the installed application and ThirdParty in the source package. Harbor and its setup executable are not code-signed, so Windows may display an unknown-publisher or SmartScreen prompt.

Build from source with Visual Studio 2022 desktop build tools, the .NET Framework 4.8 Developer Pack and Inno Setup 7. Run Build-Installer.ps1; see SETUP-README.md. The source package is for development and requires build tools and package downloads; the finished installer is for other computers.
