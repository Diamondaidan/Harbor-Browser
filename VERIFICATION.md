# Harbor Browser 0.3.3 verification

Verified November 12, 2026 (America/Chicago).

## Results

- Windows .NET Framework 4.8 Release build: PASS, zero warnings and zero errors.
- Inno Setup installer compilation: PASS.
- Bundled .NET Framework and WebView2 x64 offline prerequisites passed the build script's size and Microsoft signature checks.
- The compiled app executable and staged installer payload executable have identical SHA-256 hashes.
- 84 regression checks: PASS. These include the previous filename/ZIP recovery and layout checks, trusted home-page URL/message handling, search/address normalization, bookmark filtering, default-home migration, and preservation of custom or later explicit homepage choices.
- 22 actual WebView2 browser checks against the final staged home-page assets: PASS. These cover local asset loading, search-engine state, address and query handling, bookmark rendering, malicious bookmark labels, private mode, theme persistence/reload, no external network requests on home-page load, and desktop/narrow layouts.
- Actual WebView2 captures of Morning and Blue hour were visually inspected.
- Generated Beacon PNG has transparent exterior; standard ICO encoding supplies seven Windows icon sizes.

An early isolated browser test caught a missing explicit local entry filename. The final canonical URL uses /index.html, and the browser checks passed after that correction. Background positioning was then adjusted to preserve the lighthouse at different window aspect ratios; the final assets were retested.

## Meaning and limits

The 84 regression checks call production services and perform offline WPF layout checks. The 22 browser checks host the packaged home-page HTML/CSS/JavaScript in a separate WebView2 test application, using production URL/state/message services and a dedicated test profile. They verify search destinations by intercepting messages; external search-result pages are not loaded.

The installed Harbor application was not upgraded, and the full running Harbor window was not operated. Installer execution/upgrade/uninstall, real desktop caption gestures, full-browser tab lifecycle, network-download events, and Settings/resource-monitor behavior still require live desktop verification. The Beacon icon and new assets are compiled/staged for installation; desktop shortcut icon caching has not been tested.

Original 0.3.1/0.3.2 projects, the installed application and synced sources were not modified. This update is a separate 0.3.3 folder.

## Implementation reference

The local-page integration uses Microsoft's documented [virtual host folder mapping](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content). The friendly address is harbor://home; only the local mapped home document receives app bookmark/search-engine state.
