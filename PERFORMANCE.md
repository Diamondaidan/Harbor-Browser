Retained measurements from the original 0.4.1 performance release; these were not rerun for Hotfix 1.

# Harbor 0.4.1 performance measurements

Measured November 13, 2026 on Windows 10 build 19045, Intel Core i7-7700HQ, 8 logical processors, 12 GiB installed memory. Test hosts were x64; WebView2 runtime was 157.0.4341.46. All pages, media and downloads were local fixtures with separate profiles.

The timer fix is verified: with 10 tabs, a 15-second sleep delay and unrelated activity updates every two seconds, 0.4.0 kept all 10 tabs awake; 0.4.1 put 9 background tabs to sleep. Both versions were tested twice. This isolates the timer-reset defect; it does not imply that 0.4.0 cannot sleep tabs in other circumstances.

## Longer 20-tab follow-up

The initial short runs included a CPU spike during the transition into sleep. Two longer follow-up runs allowed five seconds to settle, then recorded five one-second samples per phase. The averages below include all 10 samples per phase across the two runs.

| Phase | Sleeping tabs | App + WebView2 CPU | WebView2 working set | WebView2 private commit |
| --- | ---: | ---: | ---: | ---: |
| Awake | 0 | 4.74% | 1,710.3 MiB | 976.3 MiB |
| Sleeping | 19 | 2.81% | 1,689.8 MiB | 941.2 MiB |
| Woken | 0 | 4.50% | 1,704.6 MiB | 956.4 MiB |

In this longer local workload, sleeping 19 background tabs reduced sampled CPU and private committed memory; waking them restored script execution and preserved page state. This is a small, repeatable synthetic measurement on one machine, not a promise of a fixed RAM/CPU reduction for websites.

## Initial short runs

Two runs per tab count; three one-second samples per phase. The Sleeping window starts 0.6 seconds after the suspend requests, so it includes transition/cleanup work. CPU results are mixed at these short intervals and must not be presented as a universal speed improvement.

| Tabs | Phase | Sleeping | App + WebView2 CPU | WebView2 working set | WebView2 private commit |
| ---: | --- | ---: | ---: | ---: | ---: |
| 5 | Awake | 0 | 1.89% | 612.1 MiB | 332.8 MiB |
| 5 | Sleeping | 4 | 1.59% | 600.9 MiB | 314.7 MiB |
| 5 | Woken | 0 | 1.57% | 598.4 MiB | 312.2 MiB |
| 10 | Awake | 0 | 2.55% | 984.1 MiB | 559.0 MiB |
| 10 | Sleeping | 9 | 3.19% | 964.1 MiB | 527.5 MiB |
| 10 | Woken | 0 | 2.74% | 963.9 MiB | 526.1 MiB |
| 20 | Awake | 0 | 4.15% | 1,710.8 MiB | 978.0 MiB |
| 20 | Sleeping | 19 | 5.86% | 1,681.2 MiB | 927.2 MiB |
| 20 | Woken | 0 | 4.89% | 1,693.7 MiB | 939.9 MiB |

The 10-tab video and paused-download cases each kept the foreground tab plus one protected background tab running and suspended the other 8. Both woke all tabs successfully. The download case measures a paused, resumable real local download, not transfer throughput or a live internet connection.

## Method and limits

- 14 measured runs total: 6 short idle runs, 4 automatic-sleep comparison runs, 1 video run, 1 paused-download run, and 2 longer idle follow-ups. Raw data contains 138 samples.
- Each workload tab allocates and touches 8 MiB, creates 300 DOM rows and performs roughly 8 ms of script work per interval. Native background timer throttling still applies. Media uses a muted canvas-fed video in a separate-origin frame.
- CPU is the sum of the test host and its WebView2 processes, normalized to 8 logical processors. Web memory columns exclude the host; CSV files include host memory separately. Working-set sums can count shared pages twice. Private commit is not exclusive physical RAM attributed to a tab.
- The short timer-comparison CPU averages were approximately 2.06% for 0.4.0 and 2.11% for 0.4.1 after the wait; those short samples do not establish a version-to-version CPU speedup. The proven change is reliable automatic sleep under unrelated activity.
- No tab discarding or forced process working-set trimming was used. Native suspension and resume retain in-page state. Memory reuse is best effort and depends on runtime/OS pressure.
- Benchmarks preceded a final correction to failed-tab status in the monitor; scheduling and sleep paths were unchanged. Final functional suites run against the packaged app. Runner hashes are retained below for reproducibility.
- The machine was not a dedicated performance lab; repeat with real sites, longer sessions and other hardware before choosing further optimization targets.

Raw JSON/CSV is under Measurements/Short and Measurements/Steady. Test profiles, runner binaries and downloaded fixture files are excluded from the source archive. Run PerformanceTests/Run-Measurements.ps1 to repeat; use -Steady for the longer 20-tab follow-up, and -BaselineExe for the 0.4.0 timer comparison.

70028120059F637605B914F63D3C752ECD355F057631034A39E6D0AD18C32970 — PerformanceTests\TestResults\20261113-093316\Runners\Candidate\HarborBrowser.exe
998E197F89873849FFBA47C0DB318BB65017622B5A00D5489F9166D51559ED0D — PerformanceTests\TestResults\20261113-093316\Runners\Baseline\HarborBrowser.exe
70028120059F637605B914F63D3C752ECD355F057631034A39E6D0AD18C32970 — PerformanceTests\TestResults\20261113-094119\Runners\Candidate\HarborBrowser.exe
