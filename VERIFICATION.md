# Harbor Browser 0.4.1 Hotfix 1 verification

Verified November 13, 2026. File build: 0.4.1.1; application version remains 0.4.1 and assembly version remains 0.4.1.0. Installer identity and profile schema 7 are unchanged. No installation or upgrade was performed. Original 0.4.1 artifacts, the installed browser, personal profile and synced sources were not modified.

## Problem and fix

Suggestion rows intentionally keep keyboard focus in the address box. The old mouse-up handler used SelectedItem, but a non-focusable row does not receive the normal WPF focus-driven mouse selection. A fresh mouse click therefore had no selected item, or could use a different row previously selected with the keyboard.

The hotfix resolves the actual row from the input's original source, selects it explicitly on press while preserving address focus, and opens it only if release is over that same row. Pressing a row cancels pending online replies so it cannot be replaced mid-click. Blank-list and outside-release input do not activate a prior selection.

The earlier test manually preselected an item and raised an event on the list itself, missing the user's actual case. It now raises routed mouse input on the row's displayed contents without preselection.

## Executed checks

| Check | Result |
| --- | --- |
| Original 0.4.1 executable with corrected row-routing regression | Reproduced: fresh suggestion click never navigated (expected failing baseline) |
| Focused suggestion-click regression | 17 passed |
| Regression/service/settings/layout suite | 322 passed |
| Full actual-app/media/download/privacy/recovery suite | 281 passed |
| Staged Home-page browser suite | 38 passed |
| Total successful automated checks | 658 |
| Release build | Passed, zero warnings and zero errors |
| Inno Setup compile | Passed; setup/app file versions 0.4.1.1 |
| Bundled .NET Framework 4.8 / WebView2 x64 prerequisites | Sizes and Microsoft signatures verified |
| Tested, compiled and staged app executable | SHA-256 identical |
| Compiled and staged Home assets | SHA-256 identical |

Focused coverage includes a fresh unselected row; title, URL, source label and padding; a different keyboard-selected row; blank-list/outside release; arrows/Enter; provider-search behavior; internal-page-looking online text remaining a search; delayed replies during press/release; private bookmark clicks without normal history; and Home suggestions in both themes using actual DevTools browser pointer input.

Address-bar regression input is routed through WPF elements, with native popup auto-capture disabled only in that offscreen test to avoid the physical cursor's unrelated desktop position. Production popup behavior is unchanged. A separate native Windows mouse check was attempted in a temporary isolated window; screenshot capture timed out and the desktop input guard interrupted text entry before any mouse click. The test process was closed. Physical mouse/trackpad behavior with normal popup capture remains a user-system check; it is not counted as verified here.

No live personal queries were sent. Search tests used injected replies; navigation to external providers was captured and cancelled. Home pointer tests used the local mapped Home page. Performance measurements were not repeated for this input-only patch: PERFORMANCE.md, Measurements and Base-0.4.1 retain the earlier release's evidence.

## Installation and remaining limits

Close Harbor and run dist/HarborBrowserSetup-0.4.1-hotfix1.exe over the existing installation. Prerequisites are bundled. The original installer/source remain separate. Source archives exclude generated binaries, test profiles, runner binaries and prerequisite executables. Hashes are distributed in HarborBrowser-0.4.1-hotfix1-SHA256.txt.

Fresh installation, installed upgrade/uninstall, physical input/display/hardware checks and independent security auditing remain outside this verification. The app and installer are not code-signed.

The [WPF ListBoxItem implementation](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ListBoxItem.cs) supports the focus/selection diagnosis. See Base-0.4.1/VERIFICATION.md for earlier feature coverage and performance limits.