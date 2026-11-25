# Harbor 0.5.3 performance checks

Measured November 14, 2026 on the Windows 10 x64 environment in VERIFICATION.md, using the exact packaged application and local fixture pages with fresh profiles. All 10 scenarios passed, with 90 recorded samples.

Two trials each exercised 5, 10 and 20 idle tabs. Two trials exercised 10-tab automatic suspension despite unrelated activity updates. One video and one paused-download scenario verified that the protected tab stayed awake. All tabs woke and retained page state.

| Workload | Background tabs sleeping |
| --- | ---: |
| 5 idle tabs | 4 |
| 10 idle tabs | 9 |
| 20 idle tabs | 19 |
| Automatic, 10 tabs | 9 |
| Video, 10 tabs | 8; foreground and video stay awake |
| Paused download, 10 tabs | 8; foreground and download stay awake |

20-tab idle averages across both trials:

| Phase | App + WebView2 CPU | WebView2 working set (MiB) | WebView2 private commit (MiB) |
| --- | ---: | ---: | ---: |
| Awake | 3.46% | 1713.89 | 993.18 |
| Sleeping | 4.50% | 1690.60 | 948.44 |
| Woken | 3.23% | 1700.89 | 959.46 |

These short sampling windows include transition work. They do not establish a universal performance gain. Website workload and other applications affect the results. No old-version comparison was performed for this release.

Evidence/Performance in the source ZIP includes raw samples, grouped summaries and per-case JSON. Earlier PERFORMANCE.md and PERFORMANCE-0.5.2.md remain historical reports.
