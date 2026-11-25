# Build and install Harbor Browser 0.4.0

1. Install Visual Studio 2022 Build Tools or Community with the .NET desktop build tools workload and the .NET Framework 4.8 Developer Pack.
2. Install Inno Setup 7.
3. Run `MAKE-SETUP.bat` from this folder.
4. The installer is created at `dist\HarborBrowserSetup-0.4.0.exe`.

Previously downloaded, verified prerequisite installers can be copied into `Installer\redist` to avoid downloading them again:

- `ndp48-x86-x64-allos-enu.exe`
- `MicrosoftEdgeWebView2RuntimeInstallerX64.exe`

Close Harbor and install 0.4.0 directly over the existing installation. The stable installer AppId and previous-directory settings enable an in-place upgrade; the installer makes a profile snapshot before replacing application files. Profile schema upgrades from 6 to 7 with a snapshot before migration. Newer-schema save protection remains enabled.

The built installer embeds the .NET Framework 4.8 and WebView2 x64 packages, installs missing prerequisites, and requires administrator approval. The source archive excludes those large prerequisite executables; the build script downloads and verifies them when needed. The packaged application and installer are not code-signed.

Windows scope: x64 Windows 10/11, with installer minimum build 16299. This is a packaging minimum, not a claim that every Windows 10 laptop or old WebView2 runtime has been tested. Verify a fresh install, upgrade and uninstall on the target laptop. No installer was run against the development machine's installed Harbor/profile during this release's verification.
