# Harbor Browser 0.3.5 verification

Verified November 12, 2026 (America/Chicago).

## Executed results

- Windows .NET Framework 4.8 Release build: passed, zero warnings and zero errors.
- Inno Setup installer compilation: passed.
- Cached .NET Framework 4.8 and WebView2 x64 offline prerequisite signature/size validation: passed.
- Compiled executable and installer payload executable SHA-256: identical.
- 176 regression checks: passed.
- 28 isolated WebView2 home-page checks: passed.
- 36 isolated full-app/media integration checks: passed.
- Total: 240 passed checks.

The regression suite includes previous filename, settings, appearance, home-page and tab-order checks, plus monitor/work-area geometry, taskbar edges on all sides, scaled auto-hide reserve space, media-script parsing, isolated profile storage and the compact title-row layout.

The full-app test constructs the real MainWindow with a dedicated test profile and injected offscreen monitor bounds. It loads a generated live canvas video in a cross-origin iframe. No personal browsing profile or internet video service is used. The test activates its own offscreen window and uses a simulated user gesture to request fullscreen on the actual video element; an initial inactive-window attempt was rejected by the browser, as expected.

Integration checks confirm:
- Cross-origin video discovery.
- Removal of the group bar; tabs and caption controls in the same row; Beacon window/header icon resources.
- Browser fullscreen uses the work area; video fullscreen uses the whole monitor rectangle.
- Toolbars/status/borders disappear for video fullscreen.
- Switching away restores chrome; nested browser/video fullscreen exits preserve the prior mode.
- Normal minimum sizes return after exiting fullscreen.
- Picture-in-picture opens, stays on top, preserves the original WebView/player and playback position, and exits video fullscreen.
- Parent frame/video layouts and original controls are restored on return.
- No scrollbars in the final PiP page/frame; play/pause and mute buttons work.
- Cog-based group assignment/filtering, right-side new tabs, tab movement and saved order.

Fullscreen and picture-in-picture viewports were captured from the live test player and inspected. A visual check found an unwanted PiP scrollbar; ancestor overflow handling was corrected and the no-scrollbar checks passed afterward. Compact-header previews were also inspected.

The installed 0.3.4 executable was inspected read-only and its extracted icon was already Beacon. The 0.3.5 installer uses a versioned icon file and an explicit app identity to address stale shortcut/taskbar references.

## Limits

No installation/upgrade/uninstall was run on the user's system. The installed browser/profile and earlier project versions remain unchanged.

Offscreen monitor geometry lets the app tests verify actual native window rectangles without covering the user's desktop. It does not verify physical mouse movement against the real Windows taskbar, multi-monitor hot-plug/DPI changes, existing pinned shortcut cache refresh, every website/player, protected video services, or actual desktop drag gestures. Those remain live-desktop checks.

Video fullscreen uses the documented WebView2 [fullscreen-element event](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.containsfullscreenelementchanged). Window sizing uses monitor and work-area rectangles as described in Microsoft's [multiple-monitor guidance](https://learn.microsoft.com/en-us/windows/win32/gdi/positioning-objects-on-multiple-display-monitors). Shortcut icon and app identity settings follow the [Inno Setup Icons documentation](https://jrsoftware.org/ishelp/topic_iconssection.htm).
