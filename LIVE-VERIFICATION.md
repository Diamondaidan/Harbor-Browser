# Harbor 0.7.0 live validation scope

Automatic tests use injected owned profiles, generated files and local fixture servers. They do not open or copy the installed profile, sign in to a real Proton account, change real VPN/network/power settings or install over the user's browser.

The unloading tests check actual view disposal/reload; edited inputs including cross-origin frames and open shadow roots; contenteditable pages; native POST documents; media; private/pinned/kept-awake tabs; active/paused downloads; selection/navigation/settings/close races; the automatic timer; resource controls in both themes; and restoration of twenty tabs with only the saved selection initialized.

Unloading is off by default and loses in-page state, scroll position and the tab's Back/Forward history. Page-state detection is best effort, not a guarantee for all JavaScript/custom controls. A missing, failed, timed-out or oversized activity scan retains the page. Keep important tabs awake.

Still required: installed 0.7.0 upgrade and retained real-provider sign-in/off/on/restart checks, website challenges, conclusive live download pause/resume, physical network interruption, sleep/wake/reboot, real monitor/input and theme/layout checks, and actual clean/offline spare-PC installation with missing prerequisites/administrator prompts. Hosted update/rollback needs approved publication first. Earlier 0.6.3 live observations are historical and are not new 0.7.0 account results.

Windows installers are intentionally unsigned. Harbor's signed update record is separate. Source/build copies and automated fixtures do not replace the installed browser. The regular portable EXE uses the regular profile when launched normally; owned test hosts are required for isolated testing.