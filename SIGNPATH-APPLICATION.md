# SignPath application record — archived

The owner reported on November 17, 2026 that the application was declined. Harbor is proceeding with unsigned Windows packages. These former application notes are kept for reference; they are not an active application task or a claim of approval or sponsorship. See CODE-SIGNING-POLICY.md for the current policy.

Project: Harbor Browser

Repository/home page: https://github.com/Diamondaidan/Harbor-Browser

Maintainer GitHub identity: Diamondaidan

License: GPL-3.0-only for Harbor-authored work. Third-party components retain their licenses.

Description: Windows desktop browser using WPF, .NET Framework 4.8 and Microsoft WebView2, with tab, download, appearance and privacy controls, optional Proton browser protection, and verified updates/recovery.

Signing targets: HarborBrowser.exe, HarborUpdateLauncher.exe and the Harbor installer. Do not sign upstream provider executables or Microsoft dependency installers under Harbor's signing policy.

Proposed build service: GitHub Actions using GitHub-hosted Windows runners, reviewed source, restricted signing artifacts and manual signing approval. Preparation workflows are not a production signing service.

Code signing policy: https://github.com/Diamondaidan/Harbor-Browser/blob/main/CODE-SIGNING-POLICY.md

Privacy information: https://github.com/Diamondaidan/Harbor-Browser/blob/main/PRIVACY.md

## Questions to resolve with the provider

1. Harbor has prior local releases but the public repository is new. Installers are currently draft-only at the owner's request. Does this satisfy the already-released and reputation requirements, or what evidence is needed?
2. Does the WebView2 SDK/runtime and .NET prerequisite distribution qualify under the system-library exception? Original Microsoft licenses and signatures are preserved.
3. Can the approved configuration sign Harbor EXEs before packaging and then sign an Inno Setup installer containing those signed EXEs, without signing upstream files?
4. What exact product/version metadata restrictions, trusted-build policies, signing roles and manual approvals should be configured?

## Any future reapplication

A future reapplication is the owner's decision, not a release requirement. If revisited, review https://signpath.org/terms and handle personal information and agreements directly with the provider. Do not put identity documents, passwords, API tokens or private keys in this file or GitHub issues.

Confirm GitHub/signing-service MFA. If accepted, configure the provider's GitHub integration and credentials with the minimum required access, update the public policy with the approved attribution, and validate signed builds on a clean Windows system before publication.
