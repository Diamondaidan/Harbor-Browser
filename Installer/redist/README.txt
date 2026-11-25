Offline prerequisites are populated automatically by Build-Installer.ps1.

Expected files:
- ndp48-x86-x64-allos-enu.exe
  Official Microsoft .NET Framework 4.8 offline installer.
- MicrosoftEdgeWebView2RuntimeInstallerX64.exe
  Official Microsoft Edge WebView2 Evergreen Runtime x64 standalone (offline) installer.

The WebView2 standalone installer is a full runtime package. Do not substitute the small Evergreen Bootstrapper, which requires internet access while installing the runtime. If automatic download fails, use the official Microsoft WebView2 download page, select Evergreen Standalone Installer > x64, save the file under this exact name, and run Build-Installer.ps1 -SkipPrerequisiteDownloads. The build script verifies minimum file sizes and Microsoft Authenticode signatures before packaging.

Do not use third-party download sites, and do not disable signature verification.

Official Microsoft WebView2 Evergreen Standalone x64 download:
https://go.microsoft.com/fwlink/p/?LinkId=2124701
