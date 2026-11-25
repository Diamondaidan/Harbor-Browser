# Harbor Browser 0.5.7

Regular Harbor release, promoted from the separately tested Proton candidate.

- Removes beta wording from Harbor's interface and optional Proton browser controls.
- Uses the established Harbor installation, Windows browser registration, link forwarding and per-user profile. Earlier beta profiles remain separate; account databases are never copied.
- Retains Proton sign-in, popup handoff, request-failure, route-monitoring and browser recovery corrections. Proton is optional and off by default in profiles that have never enabled it.
- Retains explicit recovery: reconnect, wait for Protected, then reload paused pages. Forms and downloads are not replayed automatically.
- Recognizes upgrades from the exact archived 0.5.3 executable and stable-channel installations. Setup refuses a busy profile or unrelated destination, snapshots Harbor data files, and retains profiles on uninstall.
- Includes Microsoft-signed offline .NET Framework 4.8 and WebView2 x64 packages. Setup skips usable installed prerequisites and never forces a restart.

Package target: x64 Windows 10 build 19041 or later / Windows 11, .NET Framework 4.8 or later, WebView2 157 or later. This PC runs Windows 10 build 19045. Harbor's installer is unsigned.

Proton applies to supported Harbor browser requests. Other applications keep their existing connection. Private tabs cannot be used while this connection is enabled. Local/account traffic may bypass the provider as required for operation. A verified route is not a guarantee of anonymity, every protocol or DNS leak prevention. Harbor is independent and does not claim a Proton partnership.

Executed checks and remaining platform limits are recorded in RELEASE-VERIFICATION.md, INSTALLER-VERIFICATION.md and LIVE-VERIFICATION.md. Removing the beta label does not change those limits.
