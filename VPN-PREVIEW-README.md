# Harbor 0.5.4 optional free Tor connection

The regular installer includes an experimental Tor connection, off by default. No account is needed. Open the toolbar Tor button or cog > VPN > Free browser connection - Tor preview. Connect before browsing; existing connections are not retrofitted. Private tabs and native online typing suggestions pause while enabled. Other apps and external programs opened from downloads keep their own routes.

Tor On means Harbor's routing extension settings were read back, the owned helper bootstrapped, and an HTTPS request made through that browser profile received a successful Tor Project IsTor response. A running helper alone is not treated as a verified route. The last successful check appears in the panel and toolbar tooltip.

Harbor checks at connection time, every five minutes while enabled, and after network/wake notifications. These requests contact check.torproject.org through the browser route without including typed searches. An ordinary periodic check keeps an already verified route active while pending. Network loss/address changes, suspend/resume, helper exit or failed verification invalidate it and pause new managed HTTP/HTTPS page requests. The local Home remains available. Selected top-level navigation cancelled by the guard resumes only after verification; failed subresources may need a page reload.

Use Check connection now to inspect and verify the existing route. Reconnect with Tor rebuilds routing and restarts the owned helper. Disconnect and restore cancels outstanding connection work, stops the helper and removes Harbor's routing extension. Finish or cancel downloads before changing routing. Late check results cannot re-enable a disconnected mode. An enabled choice is restored before normal browsing on the next start.

Routing uses the normal Harbor browser profile and covers normal page traffic and browser-managed downloads. Tor has no country picker and can be slow, blocked or challenged by websites. There is no silent direct fallback for managed page requests after a failed check. Already established connections are not retroactively removed or certified. In a deliberately disabled-extension test, Chromium made a speculative TCP connection before the navigation guard cancelled the page; no HTTP bytes were observed in that short local test. The application guard is not a packet-level firewall. Native background services, external handlers and packet-level coverage require further auditing.

The extension reads back SOCKS proxy ownership, endpoint and bypass settings, non-proxied WebRTC UDP restriction and disabled network prediction. Release tests also forward a unique .invalid hostname through a local SOCKS test server and compare protected WebRTC UDP against a functioning local STUN positive control. These bounded tests do not establish complete DNS-leak prevention, background traffic coverage or anonymity. Harbor does not provide Tor Browser's fingerprinting defenses; use official Tor Browser for its protections.

The bundled official expert bundle 16.0.1 contains Tor 0.4.9.16. Its archive matched the Tor Project's published SHA-256. Required runtime, geoip files, original component notices and matching Tor source are included; unused transport executables are omitted. See ThirdParty/TOR-PROVENANCE.md or installed Licensing/TOR-PROVENANCE.md.

Disconnecting is reversible and does not require Windows networking repairs. This feature adds no VPN driver, service or Windows proxy change. See VERIFICATION.md for executed checks and unverified cases.

Official references:
- https://support.torproject.org/tor-browser/security/using-tor-with-other-browsers/
- https://support.torproject.org/little-t-tor/troubleshooting/check-for-leaks/
- https://developer.chrome.com/docs/extensions/reference/api/privacy
