# Harbor Browser — third-party notices

Harbor is an independent application. Inclusion of third-party code or names does not imply provider endorsement or grant trademark rights.

## Proton browser extension 1.3.6

Copyright and license notices remain in the unmodified provider package at `Assets/BrowserVpn/Proton/LICENSE` (GNU GPL version 3). Matching upstream source for revision `71d7d86f164a2ee8e50ce5a389c9e5cbe0fd6883`, the supplemental frozen dependency lock and repeatable build instructions accompany the portable archive in `Licensing`. See `PROTON-PROVENANCE.json`, `PROTON-PACKAGE-HASHES.json` and `PROTON-BUILD.md` for file identities and the source archive SHA-256.

Project: https://github.com/ProtonVPN/proton-vpn-browser-extension

The provider has not been patched. It retains its own account, privacy, telemetry, service and routing behavior. No credentials or signed-in browser profile are included.

## Provider dependency notices and package inputs

`ProtonDependencyNotices/inventory.json` identifies 16 pinned packages from the provider's runtime dependency closure. Each package's original license/copyright text is preserved in its own directory. The collected closure is conservative: webpack can remove unused code; this inventory does not claim every package file appears in the resulting JavaScript.

The six Sentry packages (browser-utils, browser, core, feedback, replay-canvas and replay) are version 10.58.0. Additional packages: dom-form-serializer 2.0.0, lz-string 1.5.0, plural-forms 0.5.5, throttle-debounce 5.0.2, uint8array-extras 1.4.0, webext-detect 5.3.2, webext-events 3.1.1, webext-options-sync 4.3.0, webext-polyfill-kinda 1.0.2 (MIT), and webextension-polyfill 0.12.0 (MPL 2.0).

`Proton-dependency-inputs.zip` includes the exact installed upstream package inputs used by the pinned build, including available source files/maps and package metadata. These are unmodified npm package inputs, not newly authored Harbor code or a full development cache. The provider source archive and frozen lock remain the authoritative build inputs. License hashes and the package-input archive hash are recorded alongside the notices.

## Microsoft WebView2 SDK 1.0.4314.35

The exact NuGet package `LICENSE.txt` and `NOTICE.txt` are preserved under `WebView2`. These notices apply to the redistributed SDK assemblies and loader. The separately installed WebView2 Runtime has its own terms and installation requirements.

## Tor reference/runtime assets

The source tree retains the unmodified Tor 0.4.9.16 runtime, geographic data and original component notices from an earlier optional integration, plus its matching source archive. See `ThirdParty/TOR-PROVENANCE.md` and `HarborBrowser/Assets/BrowserVpn/TorRuntime/docs`. These are upstream components, not Harbor-authored files. They must not be signed as Harbor-owned executables.

## Distribution scope

Harbor-authored work is GPL-3.0-only; see `LICENSE` and `LICENSE-NOTICE.md`. This does not replace the licenses above. The original 0.6.0 installer packages contain Microsoft-signed offline prerequisites and the Proton provider. They remain unsigned draft releases until Windows release signing is ready. See `CODE-SIGNING-POLICY.md` and `PRIVACY.md`. Older trial and distribution reports describe historical packages, not the present publication status.
