# Offline prerequisites

The candidate embeds the complete Microsoft .NET Framework 4.8 offline installer and WebView2 Evergreen x64 standalone installer. Their pinned sizes, SHA-256 hashes and verified Microsoft publishers are recorded in redist-manifest.json. The WebView2 bundle contains 157.0.4341.46 version metadata. The real vendor installers were not executed on this development computer.

Build-Installer.ps1 checks those hashes, sizes and signatures before compilation. Installer/Get-Prerequisites.ps1 retrieves missing packages from Microsoft's official links and rejects an unreviewed package change. Source archives omit the large executable packages; use that script or supply the matching verified files.

Setup skips installed .NET 4.8+ and WebView2 157+. A missing .NET installation requests elevation through its separate Microsoft installer. The beta application itself installs per user. No forced reboot is requested; restart-required results are recorded and the optional application launch stays disabled until restart.

The fixture build uses small locally compiled console stubs. Its success/failure/cancellation/restart and false-success cases test setup control flow, not real Microsoft prerequisite installation. A clean spare-PC/offline test is still required.

References: [WebView2 offline distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution), [.NET deployment and return codes](https://learn.microsoft.com/en-us/dotnet/framework/deployment/deployment-guide-for-developers), [Inno Setup privilege behavior](https://jrsoftware.org/is6help/topic_setup_privilegesrequired.htm).
