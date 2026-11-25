# Harbor 0.5.6 Proton Beta — third-party notices

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

## Distribution scope

This private beta is unsigned, portable and off by default. It contains neither Tor binaries nor dependency installers. No license or partnership for Harbor itself is invented by these notices. Public distribution requires the owner's remaining review in `DISTRIBUTION-REVIEW.md` in the source package.
