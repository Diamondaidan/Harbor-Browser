# Code signing policy

## Status

Windows release signing is not active. Harbor is preparing an application for free open-source signing through SignPath Foundation. Approval is discretionary and has not been obtained. Current local installers are unsigned and GitHub installer releases must remain drafts until signing and validation are complete.

## Responsibilities

- Maintainer, author, reviewer and proposed release approver: [Diamondaidan](https://github.com/Diamondaidan).
- Contributions from others require maintainer review before merging.
- The maintainer must use multi-factor authentication for GitHub and any signing service before production signing is enabled.
- Every release signing request requires explicit human approval. Automated builds do not authorize publication.

## Intended signing process

Build Harbor's own application, update launcher and installer from the reviewed Git commit on GitHub-hosted Windows runners. Submit only Harbor-owned executable files for signing, with consistent product name and version restrictions. Preserve upstream signatures and do not sign Microsoft, Tor or Proton binaries as Harbor-owned files.

Keep signing-service credentials in the service's approved secret storage. Never commit certificates with private keys, API tokens, signed-in browser profiles or Harbor's private update-metadata key. The public update trust root may be committed.

Sign Harbor executables before packaging, and the resulting installer afterward. Verify signatures and timestamps, rerun the required release checks, then calculate final package hashes and issue fresh Harbor-signed update metadata. Existing metadata for unsigned packages must not be reused for modified packages.

The owner will review the completed signed release before changing a draft to a public release. No workflow in this repository may publish a release automatically as part of signing preparation.

## Privacy and dependencies

See [PRIVACY.md](PRIVACY.md) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). SignPath must review whether the Microsoft WebView2 components and runtime meet its system-library exception and whether this newly public project meets its release and reputation requirements. GPL licensing alone does not guarantee eligibility.

If approval is obtained, this policy will be updated with the approved service details, certificate attribution, enforced restrictions and operational roles. No provider attribution is claimed before approval.
