# Harbor Browser 0.5.3 setup

Use dist/HarborBrowserSetup-0.5.3.exe for installation. It contains Harbor, Microsoft's .NET Framework 4.8 offline installer, the Evergreen WebView2 x64 standalone installer, the optional Tor client, notices, and matching Tor source.

Supported target: Intel/AMD x64 Windows 10 version 1709 or later, or Windows 11 x64. Administrator access is required. Setup installs missing prerequisites and honors their restart-required result. WebView2 157 or later is required for this tested release: older or missing runtimes trigger the bundled Microsoft standalone installer. The included runtime is 157.0.4341.46; Evergreen normally keeps it updated afterward.

Setup offers a desktop shortcut, registers HTTP/HTTPS and HTML capabilities, and leaves your default-browser choice to Windows. Choose Harbor in Windows Settings > Apps > Default apps if desired. It does not add a system VPN or change Windows proxy settings.

Upgrade uses the regular Harbor profile in %LOCALAPPDATA%\HarborBrowser. Existing core settings, bookmarks, history, session, module permissions, connection choice and startup metadata are copied to UpgradeBackups before application files change. Uninstall removes the app and its registrations while keeping the profile. Keep your own backup before downgrading; compatibility with earlier data formats is not guaranteed.

To rebuild: install Visual Studio 2022 Build Tools with .NET desktop tools and the .NET Framework 4.8 Developer Pack, and Inno Setup 7. Run .\Build-Installer.ps1. It restores NuGet packages, builds Release, checks payload completeness, and downloads official signed offline prerequisites. For already cached packages use -SkipPrerequisiteDownloads; their sizes and Microsoft signatures are still checked. -SkipCompile builds and stages the payload but skips the final Inno compiler.

Official prerequisite sources:
- https://go.microsoft.com/fwlink/?linkid=2088631 (.NET Framework 4.8 offline)
- https://go.microsoft.com/fwlink/p/?LinkId=2124701 (WebView2 x64 Evergreen standalone)

Keep these filenames under Installer/redist when using cached packages: ndp48-x86-x64-allos-enu.exe and MicrosoftEdgeWebView2RuntimeInstallerX64.exe. Do not substitute the small online WebView2 bootstrapper. Source archives omit cached Microsoft installers; the builder obtains them from Microsoft.

See VERIFICATION.md for executed tests. Clean-machine prerequisite installation, other Windows versions and hardware remain separate validation work. This unsigned Harbor installer may produce Windows reputation prompts.

Setup's log now records detected prerequisite versions, whether each bundled installer was skipped or run, helper exit codes, and restart requests. Keep that log when reporting setup problems.
