# Harbor 0.5.2 performance checks

Measured November 14, 2026 on the Windows 10 x64 machine described in VERIFICATION.md, with WebView2 157.0.4341.46. The benchmark used the exact packaged 0.5.2.0 application, local pages/media/downloads and fresh profiles. No earlier-version comparison was performed.

All 10 scenarios passed, recording 90 samples: two trials each with 5/10/20 idle tabs, two automatic-sleep trials with 10 tabs, one video trial and one paused-download trial. Each phase recorded three one-second samples after short settling periods.

| Workload | Sleeping tabs | Outcome |
| --- | ---: | --- |
| 5 idle tabs, both trials | 4 | Background tabs slept; all woke and retained page state |
| 10 idle tabs, both trials | 9 | Passed |
| 20 idle tabs, both trials | 19 | Passed |
| 10 tabs with automatic timer, both trials | 9 | Slept despite unrelated activity updates every two seconds |
| 10 tabs with a video tab | 8 | Foreground and video tabs stayed awake |
| 10 tabs with a paused download | 8 | Foreground and download tabs stayed awake |

For 20 idle tabs, averages across both trials were:

| Phase | App + WebView2 CPU | WebView2 working set (MiB) | WebView2 private commit (MiB) |
| --- | ---: | ---: | ---: |
| Awake | 3.86% | 1716.04 | 998.08 |
| Sleeping | 4.50% | 1685.55 | 946.48 |
| Woken | 3.87% | 1694.50 | 955.63 |

These short windows include transition work. CPU results are mixed and do not establish a universal speed improvement. Memory and CPU vary with websites and the rest of the computer. The benchmark verifies suspension, wake and protected-tab behavior, rather than promising fixed resource savings.

Raw samples, grouped summaries and per-case measurements are in Evidence/Performance in the source package. Rebuild the performance project before rerunning PerformanceTests/Run-Measurements.ps1; -SkipBuild uses already built binaries. The older PERFORMANCE.md is retained as a historical 0.4.1 measurement report.
