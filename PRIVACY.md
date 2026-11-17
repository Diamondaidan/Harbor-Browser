# Harbor privacy information

Harbor stores browser data locally in the Windows user's Harbor profile. Depending on the feature, this includes settings, bookmarks, history, downloads, restored tabs, cookies, website storage and optional provider account state. Uninstalling the application does not automatically erase all profile data; use Harbor's data controls or explicitly remove the profile when appropriate.

Private tabs use a separate temporary browser profile. They do not hide traffic from websites, the network operator or a protection provider, and downloaded files remain on disk.

## Network connections

- Visiting pages, searching, downloading files and requesting site icons contact the relevant sites and selected search provider. Sites can make their own requests and collect data under their own policies.
- Online address-bar suggestions are enabled by default for normal tabs. Eligible typed search text is sent to the selected search provider. Turn off online suggestions in Settings to stop those requests. Harbor suppresses these suggestions in private tabs and while its protection policy disallows the separate suggestion transport.
- Update checks are explicit user actions. They contact GitHub for release metadata and packages without sending Harbor browsing history or browser account cookies. The updater uses the Windows network connection and is disabled while Proton or Tor protection is enabled. GitHub and other servers still receive ordinary connection information, such as the requesting IP address.
- Optional Proton browser protection uses Proton's official extension, account services and connection behavior. It has its own privacy, telemetry and error-reporting behavior. The connection check contacts ipify through the browser to assess the route. This is browser protection and does not route other applications or provide a system-wide kill switch.
- The underlying Microsoft WebView2 Runtime and installed Microsoft prerequisites have their own networking, updating and diagnostic behavior and terms. Harbor does not promise that third-party components make no background connections.

Harbor does not provide its own analytics collection server. This statement is not a claim that visited websites, Microsoft, Proton or other services collect no information.

## Third-party policies

- [Microsoft privacy statement](https://privacy.microsoft.com/privacystatement)
- [GitHub privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement)
- [Proton VPN privacy policy](https://protonvpn.com/privacy-policy)
- [ipify](https://www.ipify.org/)
- [DuckDuckGo privacy policy](https://duckduckgo.com/privacy)

If you select another search provider or optional service, its privacy policy also applies. Harbor's local data snapshots are for recovery; they do not copy provider account databases and are not a full browser-profile backup.
