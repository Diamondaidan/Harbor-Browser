# Harbor 0.7.0 performance and memory

Measured November 23, 2026 (America/Chicago), on the same Windows PC with eight logical processors and WebView2 157.0.4341.60. Frozen 0.6.3 application SHA-256: 103840485A9B49B361B632A22BDD8CF3747AFAB4A77D05DEABE7E2B528D097C1. Candidate 0.7.0 SHA-256: 4D68688B270E3A5CBE653C280CA66E62905F7231FDB994FC54ADF76338C65883.

## Memory results

These measurements describe the preserved RC1 application identified above. The final November 24 package changes only the VPN toolbar label layout; its application bytes differ and its release tests are recorded separately. No fresh performance benchmark of that final executable is claimed. Installed RC1 account, unloading and download checks are summarized in LIVE-VERIFICATION.md; physical recovery and spare-PC tests remain pending.

Each release ran ten owned-profile cases: 5/10/20 ordinary tabs twice, an automatic-sleep case twice, one muted cross-origin video case and one paused-download case. The same local workload allocates/touches 8 MiB per page, creates 300 rows and performs about 8 ms of timer work per tick; browser background throttling still applies. No personal profile or account was used.

Baseline: 90 one-second samples. Candidate: 126 samples, including explicit unload/reload phases for all six ordinary-tab cases. These values are WebView2 private committed memory in MiB, averaged across both trials and three samples per phase. They are not measurements of exclusive physical RAM.

| Tabs | 0.6.3 sleeping | 0.7.0 awake | 0.7.0 sleeping | 0.7.0 unloaded | Reduction from candidate awake |
| --- | ---: | ---: | ---: | ---: | ---: |
| 5 | 332.9 | 341.9 | 325.5 | 190.8 | 44.2% |
| 10 | 545.4 | 568.6 | 539.5 | 245.2 | 56.9% |
| 20 | 949.9 | 1,007.5 | 965.0 | 314.0 | 68.8% |

In the 20-tab candidate case, aggregate WebView2 working set fell from 1,821.9 to 499.1 MiB after 19 background tabs unloaded. The test-host-plus-engine process count fell from 25 to six. Working-set sums may count shared pages repeatedly. Reloading all discarded tabs returned private commit to about 987.5 MiB; the feature releases views and page state rather than compressing or preserving that state. Shared renderers need not exit on every website.

The unloaded phase settled for five seconds before sampling. These are short controlled scenarios, not cold-boot, real-website speed, long-term leak or all-hardware benchmarks. Other desktop activity and Windows/runtime caches were not controlled. CPU samples are in the CSVs, normalized to total machine capacity; no general CPU-speed claim is made.

## Restored-session startup

Two interleaved comparisons each used three fresh 20-tab restored profiles per release (12 process cases total). Both releases completed initialization of only the selected tab. Follow-up instrumentation showed 0.6.3 also requested one background controller startup that remained pending: two initialization requests/one pending startup in all three baseline trials. 0.7.0 made one request and left no background startup pending in all three trials.

The explicit restore guard prevents this unused startup attempt. Elapsed first-controller initialization varied: initial 0.6.3 trials were 1,582/1,615/1,736 ms; initial candidate trials 1,608/1,730/3,012 ms. Follow-up baseline trials were 2,048/2,204/1,714 ms; candidate 1,623/1,642/1,683 ms. No consistent startup-speed improvement is claimed. The timing starts at owned window construction, ends at first-tab initialization, and excludes cold Windows boot and the installed user's profile.

## Behavior and safety limits

Unloading is off by default, with a 15-minute default delay and selectable 5/15/30/60-minute delays. It reloads the saved address on selection and loses page state, scroll position and the tab's Back/Forward history. Turning the option off does not reload every discarded tab at once. Resource Monitor distinguishes sleeping, unloaded and deferred tabs; Reload selects the discarded tab and creates its view.

Detected input changes, edited fields in frames/open shadow roots, contenteditable pages, native POST documents, media, active/paused downloads, pinned/private/kept-awake tabs, provider sign-in, pending permissions, loading and protected-browsing pauses prevent unloading. Activity failures/timeouts and scans over 10,000 elements retain the page. Some JavaScript/custom controls and closed shadow state cannot be detected; keep important tabs awake. No field value is returned to Harbor or persisted by the activity probe.

The new document-creation script applies before future top-level and child documents; sandbox/CSP restrictions can affect execution, so a missing probe retains the view. See Microsoft's [document-created script reference](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2?view=webview2-winrt-1.0.4129.50). All browser-only protection guards remain installed before ordinary navigation on a restored view.

The matching native regression evidence is in RELEASE-VERIFICATION.json and RELEASE-VERIFICATION.md. Tests use generated input and local fixtures, including actual native disposal/reload, submitted pages, media, download ownership and navigation/selection/settings/close races. Additional request-counter cases exercise selection while Proton is unverified and explicit Retry after fresh synthetic proof; they send no real account traffic. The existing expiry test now controls both synthetic clocks so worker startup cannot consume its simulated one-second lease; the application lease/timeout behavior was not changed.

Raw sanitized measurements: PerformanceTests/Measurements-0.7.0/*.csv and identity.json. Generated profiles and runner folders remain under ignored T directories and are excluded from source archives. The normal portable app uses the regular profile when launched; isolated testing requires the owned harnesses. Installed 0.7.0 account/hardware/spare-PC validation remains pending.
