# Harbor 0.5.4 changes and reversal

All changes are in HarborBrowser-0.5.4. Earlier releases, installers and user-reported spare-PC results are preserved separately. Synced sources are reference material and were not changed.

A shared native title-bar service follows Harbor's global appearance and per-window preview resources. It applies dark/light caption attributes to Harbor WPF dialogs, including History, Bookmarks, Settings, resource monitor, permission/module dialogs and Tor controls. Windows 11 caption/text color attributes are attempted where supported; Windows 10 retains its native dark caption styling. Main window custom chrome and Windows-wide appearance settings are unchanged.

Tor state now separates enabled intent, helper bootstrap, current verified route and last successful check. A fixed-key profile-local routing extension reads back actual proxy/WebRTC/prediction settings. A hidden browser controller in the same profile verifies the raw HTTPS Tor Project response rather than trusting rendered page text. Checks have deadlines, cancellation and stale-result guards. The toolbar and connection panel expose status, recheck, reconnect and disconnect.

Periodic checks run every five minutes while enabled. Network/wake notifications invalidate verification and queue recovery. The normal page request and navigation guards pause new managed requests while unverified. Only a recorded cancelled top-level navigation can automatically resume after verification. Existing sockets, all browser background traffic and full packet-level privacy are not certified by this change. See VPN-PREVIEW-README.md.

No extra extension permission, system proxy, Windows networking configuration, driver, VPN provider or service was added. Private tabs and native online suggestions remain paused while Tor is enabled. The full random private-session identities and startup recovery introduced in 0.5.3 are retained.

Use Disconnect and restore to reverse Tor routing. For an application downgrade, close Harbor and retain a separate profile backup before using a preserved earlier installer. Setup makes its normal core-file UpgradeBackups snapshot; uninstall retains the profile. Automatic downgrade data-format compatibility is not guaranteed.
