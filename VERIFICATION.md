# Harbor Browser 0.3.6 verification

Verified November 12, 2026 (America/Chicago).

This rebuilt 0.3.6 package includes the History scrollbar fix.

## Executed results

- All 125 isolated real-app/interface/media checks passed on the rebuilt 0.3.6 app.
- History scrollbar position passed in both light and dark modes.
- Windows .NET Framework 4.8 Release build passed with zero warnings and zero errors.
- Inno Setup installer compilation passed.
- Application, staged application and installer file versions verified as 0.3.6.0.
- Application, installer defaults, About text, status bar and home-page version references checked for 0.3.6.
- Bundled .NET Framework 4.8 and WebView2 x64 prerequisite size and Microsoft signature checks passed.
- Compiled executable and staged installer payload hashes matched.

## History scrollbar

The real cog > History menu was opened with 30 sample history records. In both modes the vertical scrollbar was visible at the physical right edge, the flyout retained its left-opening placement, and scrolling reached the bottom.

The same right-side scrollbar convention applies to other overflowing cog menus. Menu contents retain their original direction.

The 125-check app suite also rechecked small-screen layouts, address selection, themed menus, Settings scrolling/actions, native fitting, video fullscreen, picture-in-picture controls/restoration, tab groups and tab reordering.

## Limits

Tests used an isolated profile and offscreen monitor geometry. The installed browser and personal browsing data were not changed. No installation, upgrade or uninstall was performed.

Physical mouse/trackpad scrolling, installation on the target laptop and real Windows display/scale changes remain user-system checks.

The separate 241-check regression suite and 28-check home-page suite passed for the earlier 0.3.6 build before the History scrollbar patch. They were not rerun for this packaging update and are not counted among the 125 checks above.

The updated installer and source archive hashes are in HarborBrowser-0.3.6-SHA256.txt. The source archive excludes build outputs, test profiles and prerequisite executables.
