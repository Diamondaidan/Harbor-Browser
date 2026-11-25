# Harbor 0.5.4 performance checks

Measured November 14, 2026 on the Windows 10 x64 environment in VERIFICATION.md, using the final packaged application (SHA-256 83EA457909C821074D301F1B70854843464ED1378D43D84671B37C8F9A808E40) and local fixture pages with fresh profiles. All 10 scenarios passed, with 90 recorded samples. Heavy compilation and other release suites completed before these measurements.

Two trials each exercised 5, 10 and 20 idle tabs. Two trials exercised 10-tab automatic suspension despite unrelated activity updates. One video and one paused-download scenario verified that the protected background tab stayed awake. All tabs woke successfully.

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
| Awake | 4.05% | 1731.14 | 977.48 |
| Sleeping | 5.47% | 1702.08 | 931.70 |
| Woken | 4.21% | 1715.15 | 945.57 |

These short windows include transition work; sleeping CPU was higher than awake CPU in this 20-tab sampling window. This is not evidence of a universal CPU or memory improvement. Working-set sums may count shared pages more than once; private memory is committed memory rather than exclusive physical RAM. Hardware, background applications, runtime and website workload affect results. No prior-version comparison or long-session benchmark was performed.

Evidence/Performance in the source ZIP contains raw samples, grouped summaries and per-case JSON. Earlier PERFORMANCE files and Measurements are historical evidence.
