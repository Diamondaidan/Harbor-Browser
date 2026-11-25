# Harbor Browser 0.3.4 verification

Date: November 12, 2026 (America/Chicago).

## Executed and passed

- Windows Release build: zero warnings, zero errors.
- Inno Setup installer compilation: passed.
- Microsoft .NET Framework 4.8 and WebView2 x64 bundled prerequisite size/signature checks: passed using previously cached packages.
- Compiled app and staged installer payload executable SHA-256 match.
- 144 regression checks: passed.
- 28 isolated WebView2 browser checks: passed.

Regression coverage includes the previous filename recovery, home-page and layout tests; new-tab insertion; forward/backward movement; pinned-tab boundaries; self-drop; exhaustive permutation, stable-neighbor and pin-boundary invariants across 3,000 moves; HSV/hex conversion; separate palettes; JSON round trips; legacy-profile defaults; invalid values; automatic accent contrast; trusted theme-message parsing; embedded header-logo loading; and minimum/normal layouts.

The Settings tests instantiate real SettingsWindow dialogs with a temporary test application. They exercise mode selection, wheel-to-hex synchronization, invalid-color save prevention, successful Save, Cancel isolation and reset behavior. They do not load the user's Harbor profile.

The WebView2 checks load the staged home-page assets in a separate test host/profile. They verify custom accent/text colors and the actual search-button color, app mode reaching Home, Home requesting mode changes, private-mode isolation, and the previous search/bookmark/responsive-layout cases. External search destinations are intercepted rather than fetched. No external request was made during home-page loading.

An initial browser run under restricted access timed out starting the test browser. The same suite passed after granting the WebView2 runtime the required access.

Dark Settings and shell previews were visually inspected. WPF previews are offline renders. The custom-color Home preview is captured from the isolated WebView2 test host. They are not screenshots of the user's installed Harbor window.

## Still requires live desktop verification

- Native mouse drag-and-drop, overflow edge scrolling, keyboard shortcuts while focus is inside a real webpage, and full session restore in the installed browser.
- Desktop/taskbar icon-cache refresh after installation.
- Installer execution/upgrade/uninstall.
- Full browser network-download events, sleep/wake behavior and resource-monitor operation.

The implementation uses tested ordering/palette services, but these checks do not constitute an end-to-end test of the installed Harbor application. The installer was built and not run. Earlier version folders, installed app/profile and synced sources were not modified.
