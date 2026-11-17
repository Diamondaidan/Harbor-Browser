# Windows VPN profiles in Harbor 0.5.0

Open **Cog > VPN > Manage Windows VPN connections**. Select a current-user or all-user Windows VPN profile, then Connect or Disconnect selected. The same profile name can appear in both scopes; Harbor keeps them separate and targets the matching phonebook. There is no disconnect-all command.

Use **Add / edit profiles in Windows** to configure the server, connection type and saved sign-in details. Harbor does not collect, store or put usernames/passwords into a command line. Credentials, certificate/EAP prompts, unsupported connections and administrative changes stay with Windows. You need an existing VPN service/server; Harbor does not provide one. Profiles managed exclusively by a separate provider application may not be listed.

The window displays status returned by Windows and refreshes every five seconds while open. Successful command output is followed by a status query; it is not treated as proof of a working tunnel. Unknown status disables actions, stale/deleted profiles are rechecked before commands, and errors point to Windows settings. A timeout does not prove that Windows cancelled the connection; refresh its status there. Closing Harbor does not disconnect a Windows VPN.

These are Windows network connections and can affect other applications, including private tabs. Split-tunneling profiles are identified because some traffic can bypass the tunnel. A Connected label is Windows-reported connection state, not a leak test or proof that all traffic uses the VPN.

Discovery uses Windows PowerShell's Get-VpnConnection for each scope, selecting only display/status fields. Connection commands use the Windows rasdial executable directly with a quoted profile name and explicit phonebook path; no shell interpolation or credentials are used. Output is drained asynchronously and commands have time limits. Sign-in rejection, missing profiles, server resolution/reachability and protocol errors receive separate guidance.

This Windows environment returned zero configured profiles without a query error. Automated controls/actions were tested with simulated states/results, so no real VPN was connected or disconnected. Actual server authentication, routing, MFA, provider-specific profiles and policy behavior require a configured Windows VPN and user testing. VPN performance and privacy were not benchmarked.

References: [Get-VpnConnection](https://learn.microsoft.com/en-us/powershell/module/vpnclient/get-vpnconnection) and [Rasdial](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2012-r2-and-2012/ff859533(v=ws.11)).
