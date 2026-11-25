# Harbor Browser 0.3.5

## Video fullscreen and taskbar access

A site's fullscreen video button now expands the player to the current monitor and hides Harbor's toolbars, status bar and border. Escape exits video fullscreen and restores the previous window placement. Moving away from the fullscreen video tab restores the browser controls.

F11 toggles browser fullscreen, which hides Harbor's controls while keeping the taskbar's work area available. Normal maximization also respects the current monitor's work area. In video fullscreen, move the pointer to the taskbar's screen edge to temporarily reveal space for the taskbar; move back into the video to fill the monitor again.

The window uses per-monitor DPI awareness and saves/restores its window placement and resizing constraints. These behaviors do not modify Windows taskbar settings.

## Picture in picture

Start a video, then choose the square picture-in-picture button beside the cog, Cog > Tools > Picture in picture, Ctrl+Shift+P, or Picture in picture in the video context menu.

Harbor moves the original browser/player into a floating, resizable, always-on-top window. It retains the playing page and session rather than opening a second copy. Play/pause, mute/unmute and Return to tab are available. Closing the floating window returns the player to its tab and restores the page layout. Pop-out tabs are kept awake.

The video helper supports HTML video in the main page and nested/cross-origin frames. Player behavior varies by website; service-specific players and protected streams have not all been tested.

## Compact header and groups

Tabs now share the title row with the Beacon logo and window controls. Drag empty header space to move the window; drag tab headers to reorder them. Regular tabs still append on the right. The strip can scroll horizontally when it overflows.

The separate group bar is gone. Cog > Tab groups provides:
- Show all, ungrouped, or a selected group.
- Move the current tab into a group.
- Add, rename and remove groups.

Group assignments and reordered tab sessions remain preserved.

## Beacon icon refresh

The executable already contained Beacon in 0.3.4. This update also assigns a matching explicit Windows app identity and uses a separate versioned Beacon icon for installer-created shortcuts and the uninstall entry. The installer asks Explorer to refresh its icon information.

An older pinned shortcut can retain its own cached icon. If it still looks old after updating, unpin that old shortcut and pin the updated Harbor again.

## Install and verification

Close Harbor and run dist/HarborBrowserSetup-0.3.5.exe over the existing version. The installer identity, profile folder and schema 5 remain unchanged. The installer includes the offline .NET Framework 4.8 and WebView2 x64 installers.

The installer was built but not installed during verification. See VERIFICATION.md for results and remaining real-desktop checks.

Test runners:
- Tests/Run-Tests.ps1 — 176 service, settings and offline-layout checks.
- HomeSmokeTests/Run-Browser-Tests.ps1 — 28 isolated WebView2 home-page checks.
- IntegrationTests/Run-Integration-Tests.ps1 — 36 isolated app/video checks using a generated live video in a cross-origin iframe, a separate profile and offscreen monitor geometry.

Previews includes the compact header and captured video player viewports. Source archives omit build outputs, test profiles and large redistributables.
