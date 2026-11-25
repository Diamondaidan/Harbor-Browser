# Harbor Browser Beta 0.5.7

This separate private beta carries forward the R4 connection-guard and resource fixes, adds an isolated per-user installer, and prepares recovery/spare-PC validation. It uses HarborTrials/057B instead of R4's 056G profile. No account or profile migration is performed.

All 1,089 assertions across thirteen application suites passed for this build. The native installer/prerequisite fixtures passed 75 additional checks. Actual Microsoft installers were not executed; live Proton, clean spare-PC installation, real outages, sleep/wake and reboot remain pending.

Build both test projects with desktop MSBuild, run Run-Full-Release-Tests.ps1, and run Record-Beta-Verification.ps1. Run IntegrationTests/Run-Beta-Installer-Fixtures.ps1 for isolated setup lifecycle and simulated prerequisite checks. Installer/Get-Prerequisites.ps1 supplies pinned Microsoft offline packages when needed. Build-Installer.ps1 -SkipBuild packages only matching passing application and setup records.

SETUP-README.md describes installation, prerequisites, profile isolation and reversal. The spare-PC kit provides file/environment checks, the local connection-check tool and manual steps. No production installation, account sign-in, default-browser change or networking setting was automated on the user's computer.
