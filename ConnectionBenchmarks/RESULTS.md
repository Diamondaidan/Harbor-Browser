# Connection comparison results - Harbor 0.5.5 Proton trial

Nine cases completed: three rotated trials each for Direct, verified Tor, and Proton extension enabled in a fresh signed-out profile. Ninety one-second resource samples were recorded (five Idle and five Active samples per case). Each case used five identical local documents in a standalone hidden WebView2 host. Heavy parent builds/tests were paused for the run. This is fixed connection/background overhead with local JavaScript work, excluding Harbor's full UI and sustained encrypted transfer cost.

The app binary used was SHA-256 `961750F138F0AC7845FAA6B61661EBDAAB8942DD3545A382686606F62DABFE47`. Runtime was WebView2 157.0.4341.46 on Windows x64, with eight logical processors. `benchmark-identity.json` records the fixture hash and evidence totals.

## Measured resource averages

Working set sums can double-count shared pages. Private commit is not exclusive physical RAM. CPU percentages are normalized by logical processor count.

| Mode | Phase | CPU | Total working set (MiB) | Total private commit (MiB) | Tor private commit (MiB) |
| --- | --- | ---: | ---: | ---: | ---: |
| direct | Active | 3.55% | 589.77 | 317.21 | 0.00 |
| direct | Idle | 0.73% | 570.81 | 306.67 | 0.00 |
| proton-signed-out | Active | 5.20% | 729.45 | 409.14 | 0.00 |
| proton-signed-out | Idle | 0.93% | 653.51 | 354.56 | 0.00 |
| tor | Active | 3.24% | 720.44 | 410.28 | 56.59 |
| tor | Idle | 0.98% | 701.91 | 399.71 | 56.61 |
Tor's idle private-commit increment over Direct was approximately 93 MiB in this test. Its owned helper itself averaged approximately 57 MiB private commit; other WebView2 differences account for the rest. This bounded measurement does not promise the same cost on other laptops or workloads.

The signed-out Proton registration used less private commit than Tor while idle, but approximately the same while active; its short-window active CPU average was higher. It is therefore not evidence that a usable or connected Proton session is lighter than Tor. The experiment must not be presented as a connected-provider performance win.

## Connection and navigation evidence

All three Tor preparations completed the service's actual same-profile raw HTTPS IsTor proof, and all thirty Tor resource samples contained exactly one owned `tor.exe`. Direct and signed-out Proton samples contained no Tor helper. The sampler also included the host and every process ID returned by that case's WebView2 environment. CPU deltas matched both PID and process start time.

Cold Tor preparation (bootstrap, routing and route proof) took 109.537, 13.143 and 15.077 seconds across the three fresh test directories. These are variable fresh-profile setup timings, not a warm reconnect guarantee. All nine Direct and nine Tor HTTPS Example Domain navigations succeeded. Their mean navigation-completed times were approximately 0.494 seconds Direct and 1.369 seconds Tor. This simple-site test is not a broad web-speed or throughput benchmark.

Proton was never signed in or connected in these cases. The raw hidden benchmark controller did not confirm the provider's specific sign-in-view marker during its ten-second observation. Its startup values around eleven seconds include that observation timeout and must not be interpreted as Proton connection times. The popup was closed before resource sampling. Separate interactive/WPF control tests are different evidence; these cases measure only extension-registration/background overhead.

## Cleanup and limits

All nine cases passed extension-removal/helper-cleanup and unchanged Windows proxy snapshot checks. A final independent process inventory found zero benchmark-owned hosts, WebView2 children or Tor helpers. No personal/installed browser profile was used. The comparison did not log provider credentials, tokens, public IP addresses, or Windows proxy values.

No authenticated Proton cost, battery consumption, packet-level leak protection, long-session behavior, Tor anonymity, or resource threshold was established. Tor's lower active CPU average than Direct in this small sample should be treated as measurement variation, not an optimization claim.

Raw per-case `measurements.json`, `samples.csv`, `summary.csv`, `startup-and-navigation.csv` and `comparison.json` are under `TestResults/comparison-20261114-232918`. Share those evidence files selectively; exclude browser profiles, caches and all other generated TestResults data from source/portable packages. See README.md for reproduction and scope.