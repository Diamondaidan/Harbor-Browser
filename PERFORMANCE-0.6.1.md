# Harbor 0.6.1 local performance baseline

Measured November 17, 2026 on this Windows 10 computer with 8 logical processors and WebView2 157.0.4341.46. App SHA-256: 87155D68CFD41C49B927518A2635D11AA4BB06299763612CFF5E1285BE31E136.

All **12 cases and 120 samples** completed: 5/10/20 idle tabs twice, automatic suspension twice, video and paused-download cases, then two longer 20-tab runs. Video/download tabs stayed awake in every recorded sample; all woken samples had zero sleeping tabs.

Owned-window creation to first browser-tab readiness had a median of **1,478.5 ms**, with a 1,390–1,674 ms range. These runs use fresh fixture profiles on an already running Windows system. Windows/runtime caches were not controlled; this is not a cold-boot or production-profile startup figure.

## Longer 20-tab runs

Means over 10 samples per phase, after a five-second settling period:

| Phase | Sleeping tabs | CPU, percent of logical capacity | App + WebView2 private commit, MiB |
| --- | ---: | ---: | ---: |
| Awake | 0 | 4.28 | 1,084.35 |
| Sleeping | 19 | 2.67 | 1,056.62 |
| Woken | 0 | 4.16 | 1,073.00 |

Suspension reduced CPU in this workload but released relatively little committed memory. It keeps page state in memory; this baseline does not demonstrate unloading or large RAM savings. A future memory-focused milestone can evaluate optional inactive-tab unloading while protecting forms, media, pinned tabs and downloads.

These are synthetic pages that allocate/touch 8 MiB, create DOM rows and run timed script work. Private commit is not exclusive physical RAM, and working-set totals can double-count shared pages. Results depend on hardware, runtime, caching and other activity. No older application build was measured in this run, so no release-to-release performance improvement is claimed.

Raw samples, summaries, startup rows and aggregate results are in [PerformanceTests/Measurements-0.6.1](PerformanceTests/Measurements-0.6.1). They exclude test profiles and account data. See [PerformanceTests/README.md](PerformanceTests/README.md) for reproduction and measurement limits.
