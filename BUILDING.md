# Building Harbor 0.7.1 RC1

Use Windows desktop MSBuild, .NET Framework 4.8 targeting tools, the pinned WebView2 NuGet package and Inno Setup 7. Preserve previous installers, signed records and test receipts. Windows signing is optional; metadata signing remains required for public updates.

1. Set VERSION.txt to the three-part base version and RELEASE-CHANNEL.txt to release or rcN. This checkout is rc1. Build UpdateTests/HarborUpdateTests.csproj, IntegrationTests/HarborIntegrationTests.csproj and Tests/HarborRegressionTests.csproj in Release. App/helper ProductVersion must match the source channel; FileVersion remains numeric.
2. Run Run-Full-Release-Tests.ps1, then Record-Release-Verification.ps1. Tests inject owned profiles and synthetic signing keys.
3. Run IntegrationTests/Run-Release-Installer-Fixtures.ps1. It uses unique fixture IDs, test registry prefixes, owned folders and fake prerequisite programs. Original historical executables are copied for identification only; original applications and installers are never executed.
4. Acquire pinned Microsoft prerequisites with Installer/Get-Prerequisites.ps1. Build-Installer.ps1 verifies their exact hashes, sizes and Microsoft signatures.
5. Run Build-Installer.ps1 -SkipBuild. The channel derives from source and compiled metadata; a contradictory Candidate override is refused. Run installer fixtures again with -PayloadDirectory pointing to the new staged payload for installed-file and app smoke checks. Existing stages/artifacts are never overwritten.
6. Optional public updater check: UpdateTests/bin/Release/net48/HarborUpdateTests.exe --public-updater NEW-OWNED-DIRECTORY. It verifies the production feed and downloads its current installer, with pause/recreation/ranged resume and hash verification. It does not execute that installer or touch the actual profile. Requires public internet and a current signed record.

After building a candidate installer, run ReleaseTools/Test-Package-Identity.ps1 -Installer PATH. It checks the source/app/helper/installer identities and refuses misleading release labels and stable-feed signing, including an RC renamed to a release filename. It uses an intentionally nonexistent test key path; the real private release key is never read by this test.

Promoting to full release requires setting RELEASE-CHANNEL.txt to release, rebuilding both app/helper, rerunning current verification, and creating new release artifacts. Renaming an RC file does not promote it. ReleaseTools/New-Update-Record.ps1 refuses RC ProductVersion metadata and noncanonical filenames before reading a private key. Stable feed manifests remain schema 1 with plain numeric versions so previous clients can read them. A newer installed candidate can accept a same-version signed stable package; old candidates without this change need a manual full installer.

Only finalize/sign a public update record after a full-release installer is ready. Use the existing owner key outside the source tree; never regenerate the root for a routine release. Keep Windows Authenticode signing separate. --verify-record checks prepared stable records without executing installers.

Installer/LegacyReleases.json records original installer hashes and extracted application hashes. LegacyReleaseHashes.iss is generated from those records. Verify-Legacy-Releases.ps1 regenerates both from the preserved original catalog using an explicitly supplied InnoUnp console tool. The tool's provenance is recorded; it is not part of the browser payload. Do not approve additional binaries from their names/version strings alone.

Normal app/portable launches use the regular profile. Source ZIPs exclude private keys, profiles, build/test output, caches, extracted inspection tools and Microsoft prerequisite executables. Matching provider source/notices and publisher tools remain included.
