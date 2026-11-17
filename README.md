# Harbor Browser

This branch develops **0.6.1**. It closes the Proton request gate immediately on network/wake events and prevents clock changes from extending old route evidence. It is a development candidate, separate from the frozen 0.6.0 draft. See [DEVELOPMENT-0.6.1.md](DEVELOPMENT-0.6.1.md) for verification scope and remaining work.

Harbor is a Windows browser built with WPF, .NET Framework 4.8 and Microsoft WebView2. It is developed with ChatGPT under the maintainer's guidance.

Features include movable tabs, bookmarks, history, downloads, appearance controls, private tabs, video tools and optional Proton browser protection. Version 0.6.0 adds verified update downloads and backup recovery.

## Release status

The source is public. Windows installer and portable packages will remain **unsigned by the owner's choice**. There is currently no published installer release or public update feed; existing releases stay draft until final release validation and review. Windows may display an unknown-publisher/SmartScreen warning or block installation. Harbor's cryptographically signed update records and package checksums remain required; they are separate from Windows publisher signing.

The installer targets Windows 10 build 19041 or later on x64, including compatible Windows 11 systems. It includes verified Microsoft .NET 4.8 and WebView2 prerequisites. This does not constitute testing on every supported computer.

## Build and verification

See [BUILDING.md](BUILDING.md), [SETUP-README.md](SETUP-README.md), [UPDATING.md](UPDATING.md) and [RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md). Historical verification reports describe the original local 0.6.0 executable; a new build needs fresh verification before packaging.

The source retains upstream provider assets, matching source archives, notices and a small number of historical test executables needed by regression tests. It excludes signed-in profiles, private release keys, dependency installers and development caches. Do not run historical fixture executables as a browser.

## License

Harbor-authored code and artwork in this repository are available under **GNU GPL version 3 only (GPL-3.0-only)**; see [LICENSE](LICENSE). Third-party components retain their original copyright notices and licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Harbor is independent and is not endorsed by Microsoft, Proton or SignPath.

## Code signing policy

See [CODE-SIGNING-POLICY.md](CODE-SIGNING-POLICY.md). SignPath signing is unavailable for Harbor at present, and no signing service approval, certificate or sponsorship is claimed. [WINDOWS-SIGNING-OPTIONS.md](WINDOWS-SIGNING-OPTIONS.md) records the researched alternatives, including a possible future Microsoft Store package.

## Privacy

See [PRIVACY.md](PRIVACY.md) for local data, online suggestions, optional protection and third-party network behavior.
