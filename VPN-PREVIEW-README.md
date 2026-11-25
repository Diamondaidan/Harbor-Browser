# Harbor 0.5.2 optional free Tor connection

The regular Harbor installer includes an experimental Tor connection. No account or subscription is required. This is a Tor connection, not a conventional VPN or Tor Browser.

Open cog > VPN > Free browser connection - Tor preview, then Connect with Tor. It is OFF by default. Connect before browsing; already-open connections are not retrofitted. Wait for Connected to Tor. The first connection can take up to two minutes. Check Tor connection opens Tor's verification page. Websites may be slower, reject Tor exits or show CAPTCHAs. There is no country picker.

Disconnect and restore stops Harbor's helper, removes its routing extension and restores direct browsing. It can also cancel startup. Finish or cancel downloads before changing routing. An enabled choice is restored before normal page navigation at the next startup. The regular release uses %LOCALAPPDATA%\HarborBrowser and Downloads\Harbor Browser. Close earlier previews before using this release; their separate profiles are not imported automatically.

Only normal Harbor pages and browser-managed downloads use the connection. Other apps retain their routing. Private tabs and native online typing suggestions pause while enabled; saved preferences are preserved. History/bookmark suggestions remain available. External apps opened from downloads are outside this connection.

When the helper was killed in tests, page requests failed instead of falling back directly. Disconnect and restore returns to direct browsing. The routing extension restricts non-proxied WebRTC UDP and network prediction. SafeSocks rejects unsafe IP-only requests. A complete packet-level DNS/WebRTC leak, anonymity and background-service audit has not been performed. Harbor lacks Tor Browser's fingerprinting defenses. For strong anonymity use the official Tor Browser.

The bundled official Tor expert bundle 16.0.1 contains Tor 0.4.9.16. Its archive matched the Tor Project's published SHA-256. The required client, geoip files, original component notices and matching Tor source are included. Unused transport executables are omitted. Read Licensing/TOR-PROVENANCE.md in the installed application or ThirdParty/TOR-PROVENANCE.md in source.

See VERIFICATION.md for release test results. Disconnecting is reversible and does not require Windows networking repairs. The feature does not install a VPN driver/service or change Windows proxy settings.

Official references:
- https://download.torproject.org/tor/
- https://support.torproject.org/tor-browser/security/using-tor-with-other-browsers/
- https://support.torproject.org/little-t-tor/troubleshooting/check-for-leaks/
