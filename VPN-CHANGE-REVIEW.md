# Harbor 0.5.3 changes and reversal

All application changes are contained in HarborBrowser-0.5.3. The 0.5.2 release, preview packages and user-reported spare-PC results are preserved separately.

New private-session names retain a complete random GUID with a compact prefix. They reduce directory length without changing the normal profile location, sharing normal cookies with private tabs, or reusing the identity of a closed private session. Existing installed browser data does not migrate or move.

Startup steps have deadlines and cancellation guards. A failed controller is disposed and represented by an uninitialized replacement while the tab shows Reload recovery. Late completion from the abandoned view cannot initialize the new one. A private controller that never started cannot have navigated or held page data; cleanup skips recreating it. Sessions that did initialize still require their existing data cleanup. Hidden profile-controller creation and private data clearing have bounded waits; cleanup failure still prevents a clean shutdown journal.

Queued new-tab scroll requests only act while their tab remains selected. Suggestion tests wait for the private Home document before recording the user's navigation. Version labels and setup metadata use VERSION.txt and the compiled assembly. Setup records prerequisite detection, skips, helper results and restart requests.

Tor routing and its permission restrictions remain unchanged. This release adds no Windows networking configuration, drivers, services, VPN providers or extension privileges. Tor remains optional and experimental.

Use Disconnect and restore to stop Tor routing. Before an application downgrade, close Harbor and keep a separate profile backup; use the preserved earlier installer/source package. Setup makes its normal core-file UpgradeBackups snapshot, and uninstall retains the profile. Automatic downgrade compatibility is not guaranteed.
