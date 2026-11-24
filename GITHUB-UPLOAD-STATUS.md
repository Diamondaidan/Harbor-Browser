# GitHub upload inventory — November 23, 2026

Checked live against Diamondaidan/Harbor-Browser. This is an inventory, not authorization to publish additional versions.

- Public default branch main: VERSION.txt is 0.6.0; commit ac94d300c3d242e66054b1a80a7437daec47a219.
- Public development/0.6.1 branch: VERSION.txt is 0.6.1; commit b9f81a873d2c94ccde7ea99d467b730db3243d1e. This predates the later local title-bar/Proton-off fixes and is not the final local 0.6.1 package.
- signing-preparation branch: commit 8bafbbb4ec445a3bddffe4fba4bfe7269a807ebc.
- Authenticated GitHub CLI release listing: one draft, Harbor Browser 0.6.0 — unsigned candidate, tag v0.6.0; draft=true and published_at=null. It has an installer, portable ZIP, source ZIP, SHA256SUMS.txt and harbor-update.json. No public installer release is published.
- GitHub returned no published tags. The draft tag is not a published release tag.
- The later local 0.6.1 fixes, verified 0.6.2 (dc02eb3317e640759463e6a309b76ed8ca0e25b9) and this 0.6.3 development workspace have not been uploaded by this task. Earlier individual version packages are not present in the release listing.

The connector's public release listing returned an empty list and could not expose the known draft. The already-authorized GitHub CLI connection confirmed the draft and its five asset names/digests. No token or credential was printed. No remote mutation was performed.

Links: https://github.com/Diamondaidan/Harbor-Browser ; https://github.com/Diamondaidan/Harbor-Browser/tree/development/0.6.1 ; owner-visible draft https://github.com/Diamondaidan/Harbor-Browser/releases/tag/untagged-ee57eee2f79d1aca884f .
