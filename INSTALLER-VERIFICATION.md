# Harbor Browser 0.5.7 installer verification

84 isolated native installer/prerequisite assertions passed. See INSTALLER-VERIFICATION.json for exact application, template and registry hashes and evidence directory.

Executed: native install/reinstall/uninstall; data preservation and snapshots; busy-profile update/uninstall refusal; failed-backup refusal; unrelated destination rejection; exact markerless archived 0.5.3 upgrade; stable marker creation; supported HTTP/HTML registration in a nonactive fixture namespace; unchanged actual HTTP/HTTPS user choices; stubbed dependency skip/success/cancel/failure/false-success/restart outcomes. The fixture compiler overrides installation/profile/registry paths and uses its own AppId. No actual user profile was opened by these tests.

The production setup is compiled without fixture mode, uses the established Harbor AppId, requests administrator rights, registers under HKLM64 and defaults to Program Files. Its real bundled dependencies are pinned and Microsoft-signature checked before compilation.

Not established by fixtures: actual administrator/UAC setup flow, missing Microsoft dependency installation, clean offline setup, Windows reboot, multi-user upgrade behavior, or choosing the default browser in Windows UI. The real installer is unsigned.
