# Harbor Browser 0.5.2

- Complete Windows x64 installer with bundled, signature-checked Microsoft .NET Framework 4.8 and WebView2 offline packages.
- Include the optional Tor client, matching source and component notices in installed files. Tor remains experimental and off by default.
- Use the regular Harbor profile and upgrade identity. Back up the saved Tor choice and startup state with core profile files before upgrade.
- Preserve the prerequisite restart-required result through the setup completion prompt and exit status.
- Restore enabled site modules after native registration is lost only with saved site access, explicit enable state and unchanged reviewed files.
- Keep disabled modules unregistered and block changed packages until review/reinstallation.
- Exclude internal Tor controls from ordinary modules and prevent helper startup after service disposal.
- Test real Tor restoration after normal restart and forced browser termination, helper cleanup, failures and direct-route restoration.

See VERIFICATION.md for executed tests and limits. This release uses %LOCALAPPDATA%\HarborBrowser; earlier portable previews keep their separate profiles. It does not change Windows proxy settings or the default-browser choice.
