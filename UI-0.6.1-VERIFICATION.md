# Harbor 0.6.1 UI corrections

Prepared locally on November 17, 2026. This candidate retains version 0.6.1 and the existing stable profile format. Earlier local installers remain preserved in their separate checkout. Nothing was published or installed over the regular browser.

## Changes

- Harbor-owned auxiliary windows use a shared caption drawn with the browser palette. Caption background, title and buttons follow light/dark changes independently of the Windows theme. WindowChrome retains Windows moving, resizing and system-menu behavior. Native Windows file pickers and system prompts still follow Windows.
- Proton's window opens at 404 by 696 logical pixels. Its main area belongs to the official extension; secondary actions and scope details are under a collapsed Options panel. A short route-check privacy notice stays visible below it. Provider zoom follows the available width and height, with room reserved for the sign-in artwork/footer. Account pages retain normal zoom and scrolling. Provider files, sign-in handoff and request protection are unchanged.
- The plus button follows the final tab while the strip has spare room. With overflowing tabs it remains accessible at the right edge of the scrolling tab viewport. Existing equal-width shrinking, pinning and drag order are retained.

## Focused tests executed

The new `--ui061` suite exercises 46 assertions on Windows 10 with WebView2 157.0.4341.46, using isolated profiles:

- Bookmarks, Settings, Resource Monitor, Clear Browsing Data and Site Permissions: shared caption, Dark/Light/Dark palette changes and close-command cancellation.
- Actual History dialog: shared caption and live application theme changes.
- Genuine signed-out Proton controls load; secondary options start collapsed; provider dimensions and sign-in footer fit at 404x696, 360x540 and 404x440.
- Plus-button adjacency with two, three and four tabs; adjacency and availability after selecting the newest tab in an overflowing narrow strip.

Dark and light dialog renders, the genuine extension renderer capture and the overflowing tab strip were visually inspected. WPF window captures are offscreen renders; the extension image is a separate WebView2 renderer capture. They do not establish physical mouse hit testing.

See RELEASE-VERIFICATION.md for the complete current application run and LOCAL-PACKAGE-VERIFICATION.md for packaging checks.

## Remaining manual checks

Physical monitor/DPI transitions, caption dragging/double-click/snap behavior and mouse/trackpad tab interactions still need target-hardware checks. No Proton account was signed in during this pass: the connected account's controls and reconnection should be checked after installation. The existing clean/offline-install, actual prerequisite/reboot and live hosted-update limitations remain.

The application and installer remain intentionally unsigned for Windows. The bundled Microsoft prerequisites retain their verified Microsoft signatures. Harbor's signed update metadata is a separate integrity mechanism.
