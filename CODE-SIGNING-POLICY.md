# Code signing policy

## Status

Windows release signing is not active. On November 17, 2026, the owner reported that SignPath declined the application and chose to keep the existing distribution unsigned. Windows Authenticode signing is no longer a release prerequisite. Existing binary releases remain drafts pending final validation and review; this policy change does not publish or install them.

Unsigned files may show an unknown publisher or SmartScreen warning, and some Windows policies may block them. Harbor does not claim a verified Windows publisher. No custom trusted-root certificate or Windows security-setting change is part of this distribution decision.

Harbor's signed update metadata, pinned public update key, expiry/sequence validation and package SHA-256 checks remain required. The private update key remains outside the repository. These integrity checks do not create a Windows publisher identity or remove operating-system warnings.

## Responsibilities

- Maintainer, author, reviewer and proposed release approver: [Diamondaidan](https://github.com/Diamondaidan).
- Contributions from others require maintainer review before merging.
- Maintainers should use multi-factor authentication for GitHub; any future signing service must also use it before activation.
- Every release and any future signing request require human review. Automated builds do not authorize publication.

## Optional future signing process

The following preparation is retained for a future approved provider. It is not an active requirement or service. The current no-cost decision is unsigned GitHub distribution; see [WINDOWS-SIGNING-OPTIONS.md](WINDOWS-SIGNING-OPTIONS.md).

Build Harbor's own application, update launcher and installer from the reviewed Git commit on GitHub-hosted Windows runners. Submit only Harbor-owned executable files for signing, with consistent product name and version restrictions. Preserve upstream signatures and do not sign Microsoft, Tor or Proton binaries as Harbor-owned files.

Keep signing-service credentials in the service's approved secret storage. Never commit certificates with private keys, API tokens, signed-in browser profiles or Harbor's private update-metadata key. The public update trust root may be committed.

Sign Harbor executables before packaging, and the resulting installer afterward. Verify signatures and timestamps, rerun the required release checks, then calculate final package hashes and issue fresh Harbor-signed update metadata. Existing metadata for unsigned packages must not be reused for modified packages.

The owner will review completed releases before changing a draft to a public release. Current releases may be unsigned; if signing is introduced later, its final signatures and package hashes must be validated first. No workflow in this repository may publish a release automatically as part of signing preparation.

## Privacy and dependencies

See [PRIVACY.md](PRIVACY.md) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Third-party licenses and redistribution obligations apply regardless of whether Harbor executables carry Windows signatures. No provider endorsement is claimed.

If approval is obtained, this policy will be updated with the approved service details, certificate attribution, enforced restrictions and operational roles. No provider attribution is claimed before approval.
