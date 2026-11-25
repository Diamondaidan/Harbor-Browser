# Harbor Browser 0.7.1

- Recognize 25 exact original application builds during markerless upgrades, from 0.3.2 through 0.7.0, including the preserved 0.7.0 RC1. Unknown or altered applications are refused. Already marked regular installations retain their existing upgrade path.
- Distinguish candidates from full releases in the browser, updater, executable resources and package manifests.
- Offer an authenticated full release of the same base version to an RC. A full release still cannot offer itself or an older version as a normal update. The launch helper checks the actual running browser's executable identity.
- Require matching source, app, launcher and package channel identities. Refuse candidate installers when preparing the stable signed update feed.
- Exercise public GitHub update verification, pause and ranged resume with a disposable updater profile. The public installer downloaded by that test is never executed.

This full build follows user-confirmed RC1 bookmarks/settings retention, Proton reconnection after restart without another sign-in, physical network recovery and Windows sleep/wake. Those are user-reported RC1 results, not independently measured route/leak checks. From RC1, the full app/helper change only compiled channel identity and visible version labels; the full files receive separate automated verification.

Windows installers remain intentionally unsigned. Installed and portable launches use the established regular profile. Save work and close Harbor before installing. Preparing this package does not replace the installed browser or publish a GitHub release/update record.

Clean/offline spare-PC installation, missing-prerequisite/admin/reboot behavior and actual installation of this new full executable remain separate checks. Protected IPv6 is unconfirmed; no complete leak/anonymity audit is claimed. Published 0.7.0 and RC1 packages remain preserved.