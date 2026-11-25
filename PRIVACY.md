# Harbor privacy information

Harbor stores browser data locally in the Windows user's Harbor profile. Depending on the feature, this includes settings, bookmarks, history, downloads, restored tabs, cookies, website storage and optional provider account state. Uninstalling the application does not automatically erase all profile data; use Harbor's data controls or explicitly remove the profile when appropriate.

Private tabs use a separate temporary browser profile. They do not hide traffic from websites, the network operator or a protection provider, and downloaded files remain on disk.

Private tabs are unavailable while browser-only Proton or Tor is enabled. The tested WebView2 private views do not inherit Proton's normal-profile extension route. Harbor keeps this restriction to avoid silently sending private traffic directly. Proton's "Turn off protection — keep sign-in" allows private browsing on the regular connection without removing its saved registration. Disconnect alone keeps protection enabled and browsing paused.

## Page loading details

Harbor observes a bounded number of first-party GET page-file responses in regular, non-Proton-account tabs to distinguish verification challenges, website refusals and missing/server-error files. Observations are local and temporary. Harbor reads status and the Content-Type/cf-mitigated response headers; it does not read response bodies, cookies or authorization headers for this feature. Pending request addresses stay in bounded tab memory, not diagnostic files. Resource inspection is omitted in private and Proton account tabs, and third-party/native-tracking failures are not fully counted.

Help > Page loading details produces version numbers, error categories, protection state and counts. It excludes URLs, hosts, public addresses, page titles, account data and profile paths. Copy report writes this text to the Windows clipboard only when you choose it. Nothing is uploaded. A verification helper opens only after your click, as a normal tab on the same HTTPS origin and profile; its address omits query/fragment tokens. Complete checks yourself and explicitly reload the original page. Protections are kept enabled.

## Network connections

- Visiting pages, searching, downloading files and requesting site icons contact the relevant sites and selected search provider. Sites can make their own requests and collect data under their own policies.
- Online address-bar suggestions are enabled by default for normal tabs. Eligible typed search text is sent to the selected search provider. Turn off online suggestions in Settings to stop those requests. Harbor suppresses these suggestions in private tabs and while its protection policy disallows the separate suggestion transport.
- Update checks are explicit user actions. They contact GitHub for release metadata and packages without sending Harbor browsing history or browser account cookies. The updater uses the Windows network connection and is disabled while Proton or Tor protection is enabled. GitHub and other servers still receive ordinary connection information, such as the requesting IP address.
- Optional Proton browser protection uses Proton's official extension, account services and connection behavior. It has its own privacy, telemetry and error-reporting behavior. The connection check contacts ipify through the browser to assess the route. This is browser protection and does not route other applications or provide a system-wide kill switch.

  The dedicated verifier reads the original network reply through the engine's protocol interface. Network capture is limited to that verifier, with a 64 KiB buffer and a 16 KiB reply limit. The renderer permits only the fixed verification endpoint, keeps page scripts disabled and bypasses its cache/service workers for a fresh comparison. Original bytes are used even if page text changes. Addresses are compared in memory and are not logged, persisted or included in page-loading reports. Adapter availability in those reports is a boolean, not an address or proof of Internet/VPN access.
- The underlying Microsoft WebView2 Runtime and installed Microsoft prerequisites have their own networking, updating and diagnostic behavior and terms. Harbor does not promise that third-party components make no background connections.

Harbor does not provide its own analytics collection server. This statement is not a claim that visited websites, Microsoft, Proton or other services collect no information.

## Third-party policies

- [Microsoft privacy statement](https://privacy.microsoft.com/privacystatement)
- [GitHub privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement)
- [Proton VPN privacy policy](https://protonvpn.com/privacy-policy)
- [ipify](https://www.ipify.org/)
- [DuckDuckGo privacy policy](https://duckduckgo.com/privacy)

If you select another search provider or optional service, its privacy policy also applies. Harbor's local data snapshots are for recovery; they do not copy provider account databases and are not a full browser-profile backup.
