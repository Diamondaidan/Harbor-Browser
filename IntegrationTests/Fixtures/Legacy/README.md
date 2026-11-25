# Original application identity fixtures

These executables are original Harbor application payloads extracted from the preserved installers identified in Installer/LegacyReleases.json. They are used for exact-hash identification during isolated upgrade tests. The historical executables and original installers are never launched by those tests.

The fixture installer upgrades copies in disposable workspace folders using unique application IDs and test registry prefixes. These files are test inputs, not browser distribution payloads. Unknown or modified binaries must not be added based only on their filename or displayed version. See Installer/Verify-Legacy-Releases.ps1 and BUILDING.md for provenance regeneration.
