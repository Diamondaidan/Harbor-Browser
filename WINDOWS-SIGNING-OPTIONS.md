# Windows signing decision — November 17, 2026

The owner reported that SignPath declined Harbor because the project was not established enough, and chose to keep the existing Windows distribution unsigned. No other currently suitable, ongoing free trusted-signing service for Harbor's direct-download EXE installer was verified in this review. This is a research result, not a claim that no sponsorship could ever become available.

## Available alternatives

- **Microsoft Store MSIX:** Microsoft provides signing for certified Store MSIX packages and offers a free developer account. This would require separate packaging, account/identity setup, compatibility work and Store certification. It does not sign Harbor's current EXE installer or provide a general certificate for GitHub downloads. Harbor's MSIX/Store compatibility has not been tested. See [Microsoft's signing options](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options).
- **Microsoft Artifact Signing:** an ongoing paid service; its Basic tier has a monthly charge. It does not meet the owner's free-only preference. See [Microsoft pricing tiers](https://learn.microsoft.com/azure/artifact-signing/how-to-change-sku).
- **Certum open-source signing:** discounted, with a purchase price rather than a free service. See [Certum's official shop](https://shop.certum.eu/code-signing.html).
- **Self-signed Windows certificates:** useful for controlled development, but do not supply public Windows trust. They do not solve public installer warnings. See [Microsoft's comparison](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options).

## Current implementation decision

Keep Windows packages unsigned and label them accurately. Preserve Harbor-signed update metadata, the existing public trust root, package hashes, bounds, expiry/sequence checks and explicit setup approval. Do not change Windows trust/security settings. Source licensing and dependency notices stay in place.

The signing workflow example remains inactive and optional. No paid resource, Store account or Store submission was created. Existing binary drafts remain unpublished pending final validation/review; choosing unsigned distribution does not complete physical spare-PC or end-to-end update tests.
