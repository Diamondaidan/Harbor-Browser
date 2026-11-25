# Harbor 0.6.3 installer fixture verification

94 isolated native installer assertions passed against the app/helper hashes in INSTALLER-VERIFICATION.json. Temporary install/profile folders, AppIds and registration namespaces were used; the production installer was not run over regular Harbor.

Executed: install/reinstall/uninstall, copied app/helper hashes, data snapshots/preservation, busy-profile and unreadable-backup refusal, unrelated-destination rejection, archived markerless 0.5.3 and stable 0.5.7 upgrades, scoped browser registration, unchanged actual HTTP/HTTPS defaults, and fake prerequisite skip/success/cancel/failure/false-success/restart outcomes. The fixture used harmless helpers, not actual Microsoft installers.

The production template retains the stable AppId and established profile location, requests administrator approval and embeds the pinned Microsoft prerequisites. App/helper/template/registry hashes bind the fixture receipt to this candidate. The Windows installer is intentionally unsigned. Full-payload and packaged-app results are in PACKAGED-PAYLOAD-VERIFICATION.json and LOCAL-PACKAGE-VERIFICATION.md.

Actual updater/UAC behavior, missing/offline vendor prerequisites, clean spare-PC setup, reboot and multi-user compatibility remain manual checks.
