# Installing Harbor Browser 0.5.7

Close regular Harbor before running HarborBrowserSetup-0.5.7-release.exe. Approve Windows' administrator prompt yourself. Setup defaults to Program Files\Harbor Browser, recognizes the existing 0.5.3 installation, and keeps LocalAppData\HarborBrowser. Beta/trial installations and profiles remain separate. Use your usual Windows account when approving setup; upgrades with multiple simultaneously signed-in Windows users have not been validated.

The full installer contains Microsoft-signed .NET Framework 4.8 and WebView2 x64 offline packages. No developer tools are needed on another PC. Usable installed dependencies are skipped. If setup requests a restart, save your work and restart before opening Harbor. Harbor's installer is unsigned. Do not bypass a Windows security block; report it for diagnosis.

Package target: x64 Windows 10 build 19041 or later / Windows 11, .NET Framework 4.8+, WebView2 157+. Older Windows versions, 32-bit Windows and ARM64 are outside this package's support scope. Clean installation and genuine missing-prerequisite/restart behavior on another PC remain separate validation items.

The extracted portable build also uses the regular Harbor profile and single-instance identity. Close installed Harbor before opening it. Keep the complete extracted folder together. Never run older and newer releases against the same profile simultaneously.

For Proton, open VPN Off, enable browser protection, sign in yourself and connect. Wait for Protected. A restart may require Connect again without another sign-in. Reload paused pages explicitly. Disconnect pauses supported browsing until you reconnect and verify, or choose Turn off and remove. Forms and downloads are not resumed automatically. Private tabs cannot coexist with an enabled browser connection.

Windows keeps your current default browser. After installation, Harbor's default-browser command opens Windows Settings for you to choose. Setup advertises HTTP/HTTPS and local .htm/.html support without writing UserChoice.

Uninstall keeps data and Microsoft dependencies. Upgrades snapshot Harbor JSON/state files under LocalAppData\HarborBrowser\UpgradeBackups. Authentication databases remain in place and are not copied. Keep previous installers and backups. Before reverting to an older release, turn off and remove Proton in 0.5.7 and close Harbor. Do not delete a profile to fix setup or sign-in.
