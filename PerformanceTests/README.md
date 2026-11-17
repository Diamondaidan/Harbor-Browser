# Repeatable local performance measurements

The 0.6.1 harness also records `StartupReadyMilliseconds` and exports `startup.csv`. This measures owned-window creation to first-tab initialization on the current running Windows system with a fresh fixture profile. It does not control Windows/runtime caches and is not a cold-boot or installed-user-profile startup measurement.

Run Run-Measurements.ps1 on x64 Windows with the same build tools used for Harbor. Close other test builds first. The harness runs offscreen, with unique profiles and local pages/downloads. It does not install Harbor or open the personal profile.

The default run measures 5, 10 and 20 tabs twice each, an automatic-sleep scenario twice, one muted cross-origin video scenario, and one paused local download scenario. Every phase records three one-second CPU/memory samples: Awake, Sleeping and Woken. The fixture allocates/touches 8 MiB and creates 300 DOM rows per normal page, then performs about 8 ms of script work per timer tick. Browser background timer throttling still applies.

The automatic-sleep case uses a 15-second delay while calling the app's ordinary activity-update path every two seconds to simulate activity in other tabs. The case waits 17.5 seconds before measuring. This specifically exercises the timer-reset defect; it is not a generic website-speed benchmark.

Pass -Steady for two longer 20-tab follow-up runs: each phase settles for five seconds and then records five one-second samples. This separates transition work from a later measurement window. Keep both short and longer results when assessing the effect.

To compare with 0.4.0, pass -BaselineExe followed by the full path to the verified 0.4.0 HarborBrowser.exe. The harness copies that build into its own runner folder; it does not rebuild or change the original release. It checks the assembly version at runtime. Without that argument, only candidate cases run.

Raw JSON per case includes app/runtime version, process bitness, logical CPU count, tab count and trial. samples.csv and summary.csv combine the measurements. Working-set sums can count shared pages more than once. Private memory is committed process memory, not physical RAM used exclusively by a tab. CPU is normalized against available logical processors. Process lifetime is matched before computing deltas.

Keep the generated test profiles out of source archives and shared reports. Share only the JSON/CSV measurements and summary. TestResults is ignored. Measurements vary with hardware, background work, runtime, cache and OS memory pressure; repeat them before drawing broad conclusions. Media and paused-download scenarios intentionally keep one extra background tab running.
