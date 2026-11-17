# Harbor 0.6.0 installer verification

94 isolated native installer assertions passed for the final app and helper hashes recorded in INSTALLER-VERIFICATION.json.

Executed: install/reinstall/uninstall; copied app/helper hash checks; data preservation/snapshots; busy profile update/uninstall refusal; unreadable backup refusal; unrelated destination rejection; exact archived markerless 0.5.3 upgrade; known stable 0.5.7 upgrade to 0.6.0; nonactive scoped browser registration; unchanged actual HTTP/HTTPS user choices; fake dependency skip/success/cancel/failure/false-success and restart outcomes. AppId, install/profile paths and registry namespace were isolated. The user installation/profile was not used.

The production template requests administrator rights, retains the established AppId and stable marker, registers under HKLM64 and includes the full pinned Microsoft prerequisites. The app/helper, template and registry hashes bind the fixture record to this build. The full production setup is built without fixture mode.

Not established here: real updater-driven UAC/setup, missing Microsoft prerequisites, clean/offline installation, Windows reboot or multi-user compatibility. Installer signing is prepared but not performed because no code-signing certificate is available.
