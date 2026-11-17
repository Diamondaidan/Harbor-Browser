# Building Harbor 0.6.1 development

This checkout is separate from the frozen 0.6.0 draft. Do not replace its signed update metadata or existing installers. The 0.6.1 candidate must receive its own current verification records before packaging and must remain unsigned/unpublished until the signing process is approved.

Use Windows desktop MSBuild, .NET Framework 4.8 targeting tools, the pinned WebView2 NuGet package and Inno Setup 7.

1. Build UpdateTests/HarborUpdateTests.csproj, IntegrationTests/HarborIntegrationTests.csproj and Tests/HarborRegressionTests.csproj in Release. The app project builds and copies the separate update launcher.
2. Run Run-Full-Release-Tests.ps1, then Record-Release-Verification.ps1. Tests use owned synthetic profiles and test signing keys. Native fixture programs do not install Harbor or change Windows defaults.
3. Run IntegrationTests/Run-Release-Installer-Fixtures.ps1 for isolated native setup/upgrade/uninstall and simulated dependency cases. Its AppId, paths and registry namespace are separate from the actual app.
4. Acquire the pinned Microsoft dependencies using Installer/Get-Prerequisites.ps1. Build-Installer.ps1 checks hashes, sizes and Microsoft signatures.
5. Run Build-Installer.ps1 -SkipBuild -Candidate release. Existing stages/artifacts are never overwritten. Optional SigningThumbprint and TimestampUrl sign the final installer with an existing certificate. See UPDATING.md before signing.
6. Create a signed update record with ReleaseTools/New-Update-Record.ps1 after installer signing/finalization. The app's update release key is separate from Windows Authenticode signing. Never put private keys in this source tree. Use the existing owner key; do not regenerate the trust root for a routine release.
7. Validate the prepared record using UpdateTests/bin/Release/net48/HarborUpdateTests.exe --verify-record RECORD INSTALLER [PREVIOUS-INSTALLER]. This reads/hashes files without executing installers.

The regular app uses the existing production profile. Development/runtime tests must inject owned stores and must not launch the regular app against a pretend profile. Preserve working release packages. Source ZIPs omit private keys, profiles, build/test output, caches, dependency executables and extracted inspection files. Matching provider source/notices and all publisher tooling belong in source packages.
