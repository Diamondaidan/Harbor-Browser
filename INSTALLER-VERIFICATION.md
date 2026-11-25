# Harbor 0.6.2 installer fixture verification

94 isolated native installer assertions passed for the final app/helper hashes in INSTALLER-VERIFICATION.json. The fixtures installed into owned temporary folders with unique AppIds and an isolated registration namespace; the production installer was not run over the regular browser.

Executed: install/reinstall/uninstall; app/helper hash checks; data preservation/snapshots; busy-profile update/uninstall refusal; unreadable-backup refusal; unrelated destination rejection; archived markerless 0.5.3 and known stable 0.5.7 upgrades; scoped browser registration; unchanged actual HTTP/HTTPS defaults; fake prerequisite skip, success, cancellation, failure, false-success and restart outcomes. Fake helpers were used, not actual Microsoft installers.

The production template retains the stable AppId and profile location, requests administrator rights, registers scoped HTTP/HTTPS/HTML capability under HKLM64 and includes full pinned Microsoft prerequisites. App/helper, template and registration hashes bind the fixture record to this build. The Windows installer remains intentionally unsigned.

The full-payload follow-up receipt and production installer hash are separate: see PACKAGED-PAYLOAD-VERIFICATION.json and LOCAL-PACKAGE-VERIFICATION.md once packaging completes. These tests do not establish real updater/UAC behavior, missing/offline Microsoft prerequisites, clean spare-PC setup, reboot or multi-user compatibility.
