# Proton extension source and build provenance

Official project: https://github.com/ProtonVPN/proton-vpn-browser-extension

The GitHub main revision was checked on November 14, 2026. It remains `71d7d86f164a2ee8e50ce5a389c9e5cbe0fd6883`, committed October 9, 2026; the repository listed no GitHub releases. The production manifest still declares version **1.3.6**, official extension identity **jplgfhpmjnbigmhklmmbgecoobifkmpa**, and the same key, permissions, popup and authentication contract as the prior Harbor 0.5.1 experiment. There is no newer upstream provider release in this trial.

The fresh official source archive is `ProtonVPN-source.zip`, downloaded from:

https://codeload.github.com/ProtonVPN/proton-vpn-browser-extension/zip/71d7d86f164a2ee8e50ce5a389c9e5cbe0fd6883

Archive SHA-256: `975BF0C429BA3EA030871E5719913ABD13D6DE1E2BBEE2D3D936B465BB49F0D0`.

All 634 source-file contents match the preserved earlier source archive after ignoring its top-level directory name; the ZIP container hash differs because of archive packaging/root naming. Upstream package-lock SHA-256: `95BB9CDB922F8BF6F0FA753ECBB728F03CAA8BE95540AAE4CA8C394736AC6CAC`.

## Build

Rebuilt with Node **24.22.1** and pnpm **11.25.0**, using the exact previously converted pnpm lock at `ProtonBuild/pnpm-lock.yaml`. The supplemental pnpm lock was generated in the original trial from the unchanged upstream package-lock.json. No provider TypeScript, JavaScript, configuration, manifest, key or permission was patched.

1. Extract the matching source archive.
2. Copy `ProtonBuild/pnpm-lock.yaml` into the extracted source directory.
3. Run `pnpm install --ignore-scripts --frozen-lockfile --store-dir <local-build-cache>`.
4. Run `pnpm exec webpack --mode=production`.
5. Copy `distribution/*` and the upstream GPLv3 `LICENSE` into `HarborBrowser/Assets/BrowserVpn/Proton`.

Both commands exited successfully. The production webpack configuration uses `stats: 'errors-only'`, so an empty successful webpack log is expected. Required nonempty runtime files and official manifest identity were checked after compilation.

`ProtonBuild/Build-Proton.ps1` repeats this process from the pinned source archive. It writes into this isolated trial only and does not install an extension into a browser. Use `-Offline` only after populating its local dependency cache. Build products may differ byte-for-byte with build path/tooling context; this trial records every shipped provider file in `PROTON-PACKAGE-HASHES.json`.

The final provider directory has **336 files, 6,207,650 bytes**, including its GPLv3 license. Manifest SHA-256: `3EBB92A60F4F9498F24D1E74332B3F4BDD0C95E97CB7C7299FA7D7C2DB4B778B`.

Exclude `node_modules`, `ProtonBuild/store`, extracted build work directories and test profiles from portable/source deliverables. They are rebuild caches, not runtime dependencies. Retain the matching source ZIP, lock, revision, notices and build instructions.

## Compatibility and safe diagnostics

The provider uses `action.default_popup = popup.html`, the account.proton.me external login handoff, asynchronous webRequest proxy authentication, tabs and privacy APIs. Their behavior in Harbor/WebView2 must be tested; ordinary Chrome/Edge support is not proof of WebView2 compatibility.

Do not serialize provider storage or the complete runtime `{type:'state'}` response. State can contain `credsData.Username` and `credsData.Password`. For diagnostics, reduce UI status to visible `#logged-view`, `#protected-label` and `#connecting-label` booleans, and proxy inspection to ownership, mode and presence of PAC data. These remain provider-reported status, not independent route proof.

The provider's `https://account.proton.me/api/vpn/v1/location` endpoint returns IP/location data for a request intentionally excluded from proxy routing (`excludeApiFromProxy = true`). It is not a valid protected-route proof. Use independent same-profile HTTPS routing evidence and document exactly what it demonstrates, without logging account tokens, credential objects or IP addresses.

The official extension includes its own Proton telemetry/error-reporting behavior. No account was created, login performed, live VPN connection established, desktop VPN installed or Windows networking configuration changed by this source-build task.
