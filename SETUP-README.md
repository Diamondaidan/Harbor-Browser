# Build and install Harbor Browser 0.3.3

1. Install Visual Studio 2022 Build Tools or Community with the .NET desktop build tools workload and the .NET Framework 4.8 Developer Pack.
2. Install Inno Setup 7.
3. Run `MAKE-SETUP.bat` from this folder.
4. The installer is created at `dist\HarborBrowserSetup-0.3.3.exe`.

Previously downloaded, verified prerequisite installers can be copied into `Installer\redist` to avoid downloading them again:

- `ndp48-x86-x64-allos-enu.exe`
- `MicrosoftEdgeWebView2RuntimeInstallerX64.exe`

Install 0.3.3 directly over the existing Harbor Browser installation. Do not uninstall the previous version first. The stable installer AppId and previous-directory settings enable an in-place upgrade; the installer makes a profile snapshot before replacing application files. Profile schema 5 is unchanged.
