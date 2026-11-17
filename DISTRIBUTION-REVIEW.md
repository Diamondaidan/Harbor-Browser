# Harbor distribution record

## Current publication decision

On November 17, 2026, after reporting SignPath's rejection, the owner chose unsigned Windows distribution. This replaces the earlier condition that installers stay draft until Windows signing is available. Final release validation and review still apply; no release is published by this documentation change. Harbor-authored work remains GPL-3.0-only, and third-party licenses/notices remain in place. See `CODE-SIGNING-POLICY.md` and `WINDOWS-SIGNING-OPTIONS.md`.

The November 16 source-publication decision and the original unsigned packages remain part of the release history. No provider approval or endorsement is claimed.

## Historical 0.5.7 local release record

Prepared locally at the owner's request to remove beta labeling. No public upload or provider contact was performed.

The 336 Proton runtime files remain unchanged from the pinned official source build. GPL text, matching source, revision, build recipe, supplemental lock, dependency notices and exact cached dependency inputs accompany the app. WebView2 SDK LICENSE/NOTICE also accompany it. The full installer embeds Microsoft-signed offline dependencies; their manifest pins hashes and sizes.

Harbor is independent and does not claim an official Proton partnership or a trademark license. Removing beta wording is not a legal clearance of a future public distribution. Decisions about Harbor's own code license, public hosting, combined redistribution obligations and service/branding terms remain with the owner. This record does not block the locally requested build or testing.

The installer is unsigned. Current test coverage and remaining clean-install/platform limits are listed in the verification reports. A code-signing certificate, public update infrastructure and broad hardware certification are separate work.
