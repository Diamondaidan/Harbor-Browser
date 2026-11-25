# Harbor Browser 0.7.1 RC1

- Recognize 25 exact original application builds during markerless upgrades, from 0.3.2 through 0.7.0, including the preserved 0.7.0 RC1. Unknown or altered applications are refused. Already marked regular installations retain their existing upgrade path.
- Display RC1 in the browser and updater. Keep the build channel in executable metadata and package manifests, separate from the regular installation/profile identity.
- Offer an authenticated full release of the same base version to an RC. A full release still cannot offer itself or an older version as a normal update. The launch helper checks the actual running browser's executable identity.
- Require matching source, app, launcher and package channel identities. Refuse candidate installers when preparing the stable signed update feed.
- Exercise public GitHub update verification, pause and ranged resume with a disposable updater profile. No downloaded public installer is executed by that test.

This is a local release candidate. Windows installers remain intentionally unsigned. RC1 uses the established regular installation and browser profile; the portable executable also uses that profile. Close Harbor and save work before installing. It is not a separate signed-in test channel.

The published full 0.7.0 and earlier packages remain preserved. This candidate does not publish a release, replace the public update feed, or upgrade the installed browser automatically. Physical network loss/recovery, sleep/wake/reboot, actual installed upgrade, and clean/offline spare-PC installation remain manual checks. Previous 0.7.0 RC1 observations are historical and are not claimed as 0.7.1 results.