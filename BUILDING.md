# Build Harbor Browser 0.5.7

Use Windows desktop MSBuild, the .NET Framework 4.8 targeting pack and pinned NuGet dependencies. Inno Setup 7 is required for setup.

1. Build IntegrationTests/HarborIntegrationTests.csproj and Tests/HarborRegressionTests.csproj in Release.
2. Run Run-Full-Release-Tests.ps1, then Record-Release-Verification.ps1.
3. Run IntegrationTests/Run-Release-Installer-Fixtures.ps1. It uses owned test folders, a nonactive registry namespace and fake dependency helpers; it never launches the real installed app.
4. Obtain the pinned Microsoft packages using Installer/Get-Prerequisites.ps1 or place them in Installer/redist. Build-Installer.ps1 checks sizes, hashes and Microsoft signatures.
5. Run Build-Installer.ps1 -SkipBuild -Candidate release. Existing artifacts are never overwritten. Use a new rc number to investigate a later build.
6. Build-Spare-PC-Kit.ps1 prepares a manual validation kit from the installer record. The local connection tool and matching source are included under TestTools/ConnectionCheck.

The regular app uses the established Harbor profile and instance identity. Only tests that explicitly inject owned stores should run during development. Do not launch the normal app to probe a synthetic profile.

Source packaging omits used profiles, build/test output, prerequisite inspection files and large Microsoft dependency executables. The dependency manifest and acquisition script are included. Creating setup does not establish clean installation on every PC.
