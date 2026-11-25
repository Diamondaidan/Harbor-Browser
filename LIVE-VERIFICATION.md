# Harbor 0.6.0 runtime validation scope

The final build passed the complete owned-profile regression suite, including actual WebView2 browsing/download/recovery/Proton tests and native updater routing-gate checks. Updater fixtures used ephemeral synthetic signing keys, local HTTP, synthetic profile data and harmless native parent/setup programs. Real installer bytes and the production release public key receive a separate non-executing signed-record roundtrip check after packaging.

The installed user browser remains the preserved 0.5.7 release. No 0.6.0 app was launched against that signed-in production profile. Earlier 0.5.7 live connection/disconnect/restart observations remain historical and do not establish an actual 0.6.0 installed sign-in or a live GitHub update.

Remaining: owner publication of GitHub assets; actual GitHub-hosted download/resume; real newer-version self-update and previous-version rollback; actual Windows setup/UAC from the updater; live 0.6.0 Proton account/routing checks; clean/offline spare-PC install and real missing dependencies/restart; Wi-Fi interruption, sleep/reboot and broader Windows 11/hardware checks. Windows code signing remains pending a certificate. Current IPv6 behavior on the installed 0.5.7 Proton connection was unavailable/inconclusive.

Native fixture success is not a complete privacy/security audit or certification of every Windows PC. This build preserves the documented limited browser connection scope.
