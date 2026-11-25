# Harbor Browser 0.3.8 verification

Verified November 12, 2026 (America/Chicago).

## Results

- .NET Framework 4.8 Release build: passed; zero warnings and zero errors.
- Inno Setup compilation: passed; application and installer versions are 0.3.8.0.
- Bundled .NET Framework 4.8 and WebView2 x64 prerequisites: size and Microsoft signature checks passed.
- Tested executable and staged executable SHA-256: identical.
- Compiled and staged Home assets: identical.
- 280 regression/service/settings/layout checks: passed.
- 191 isolated real-app/interface/media checks: passed.
- 38 isolated Home-page browser checks: passed.
- 509 automated checks in total.
- Five additional live provider checks: DuckDuckGo, Google, Bing, Brave and Startpage each returned six suggestions for the generic query weather.
- Total successful checks including provider connectivity: 514.

## New features exercised

Bookmarks:
- Nested Netscape-format HTML folders, HTML entities, empty folders, Harbor JSON and legacy JSON arrays.
- HTML/JSON export/import roundtrips and duplicate merging.
- Script/credential-bearing bookmark rejection and invalid folder paths.
- Real manager filtering, ordering within a filtered folder, parent-folder renaming, adding/editing, import and search.
- Draft isolation when closing and independent results when saving.
- Folder dropdown labels and manager layout were visually inspected.

Preferences and Home:
- Deep copying and storage of folders, custom shortcuts and appearance presets.
- Schema 5 to 6 migration, preservation of legacy bookmarks and a snapshot before migration.
- Background import into the profile, preservation of the original picture, invalid-image rejection and managed-directory traversal protection.
- Saved presets restoring colors; closing Settings discarding the draft; Save returning Home/online preferences.
- Fixed scenes independent of mode, custom background and ordered custom shortcuts reaching the actual Home page, and a 12-shortcut limit.
- Home customization action messages, normal/private action visibility and selected search completion behavior.
- Light/dark search text selection, preset names, manager labels and Home suggestion translucency were visually inspected.

Online suggestions:
- The Settings toggle prevents requests in both address-bar and Home flows.
- Private tabs prevent online requests in both flows, with the service also enforcing privacy policy.
- Common web/local-address and credential-shaped input guards prevent lookup.
- Provider responses are bounded, parsed as data, deduplicated and never rendered as HTML.
- Cancellation and malformed responses return safely; stale replies cannot replace current results.
- Provider completions cannot invoke internal browser pages; selection performs a search.
- Home arrows select results without scrolling the whole page, Escape dismisses the list, and stale IDs are ignored.
- The selected Home highlight is translucent in dark mode.
- The separate Home test host made no external page-resource requests.

The live probes used only a synthetic public query. App privacy/cancellation tests used an injected deterministic transport rather than real personal queries. Suggestion requests use a separate HTTP client with cookies and default credentials disabled, no automatic redirects, bounded responses and cancellation/time limits.

## Existing behavior retained

The suites also rechecked filename/extension handling, download collisions, real local HTTP pause/range-resume/cancellation, source-tab closure, ZIP byte integrity, tab restoration/reordering/groups, Downloads privacy separation, fullscreen video, picture-in-picture, compact right-side menu scrollbars, Settings actions and small-screen layouts.

## Limits and user-system checks

No installation, upgrade or uninstall was run. The installed browser and personal browsing profile were not modified. All app tests used isolated profiles, synthetic bookmarks/images and local media/download fixtures.

Physical mouse/trackpad actions and keyboard chords, file-picker interaction, real browser-export files, installation on the target laptop, live Windows display/DPI transitions and additional real-world websites remain user-system checks. The 1366×768 coverage uses logical layouts and injected monitor/work-area bounds; Windows display settings were not changed.

Provider connectivity is a point-in-time check. Provider availability, throttling and response formats can change; local suggestions and normal search remain available when online completion fails.

The profile schema is now 6. Installer identity and profile location are unchanged. Migration snapshots and newer-schema save protection remain in place.

Endpoint references were checked against [Chromium's current search-engine definitions](https://chromium.googlesource.com/external/search_engines_data/+/refs/heads/main/definitions/prepopulated_engines.json). Suggestions remain a provider feature; see [DuckDuckGo's autocomplete control](https://duckduckgo.com/duckduckgo-help-pages/privacy-pro/turn-off-autocomplete) and [Startpage's suggestion explanation](https://support.startpage.com/hc/en-us/articles/5141960270228-Search-Suggestions).

Source archives omit build outputs, test profiles and prerequisite executables. Installer/source hashes are in HarborBrowser-0.3.8-SHA256.txt.
