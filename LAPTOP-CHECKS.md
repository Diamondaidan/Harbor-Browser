# Windows laptop checks for Harbor 0.5.0

Close Harbor before running the normal 0.5.0 installer. Use a Windows 10/11 x64 computer; the exact target hardware and administrator/prerequisite flow still need testing.

1. Try a fresh installation on the other laptop. Confirm it starts and handles missing .NET Framework/WebView2 prerequisites. Confirm that a reboot request, if any, is handled before retrying startup.
2. Upgrade the existing Harbor installation. Check bookmarks, bookmark order, history, appearance settings, saved normal tabs and modules before/after. Keep the installer-created profile backup.
3. At 1366 x 768, test the actual Windows scale setting. Check the caption buttons, address field, cog, settings and module dialogs. Test moving between monitors if available.
4. Drag a bookmark before/after another, then onto empty space at the right. Repeat with a folder; hold near the edge of a long bar to scroll. Restart and confirm order. Right-click Move left/right remains available.
5. Open enough tabs to reach icon-only scrolling. Select an offscreen tab with Ctrl+Tab and confirm it becomes visible. Drag tabs, including pinned tabs, and confirm session order after restart.
6. Click address suggestions by title, URL and source label with the mouse/trackpad. Check fresh clicks, repeated clicks and keyboard arrows/Enter. Native popup capture remains a physical-input check.
7. Download a ZIP/PDF and check the name, extension, pause/resume/cancel and opening the completed file. Try fullscreen video, picture in picture, taskbar access and audio on the actual GPU/display.
8. For a trusted local module, remove one requested site pattern and confirm it stops running there after reload. Check overlapping wildcards carefully. Deny all, restart, then verify Enable cannot override the empty grant list. Changing site access can reset that module's own preferences.
9. Before uninstalling on a disposable test installation, confirm which app is being removed. Check that its separately stored profile survives as documented. Do not remove your working installation just to complete this checklist.

The automated fixture tests use a separate app identity/profile and preinstalled prerequisites. They do not validate every Windows laptop, UAC, missing-prerequisite installation, native mouse/trackpad interaction or real hardware media behavior.

Additional 0.5.0 checks:
- Choose Harbor in Windows Default apps, then open links from another app with Harbor closed and open. Confirm links enter normal tabs even when a private tab is selected. Open an HTML file with spaces in its name.
- Check Private text and the P badge on wide, crowded and pinned tabs in both themes.
- With an actual Windows VPN profile, verify the reported status, correct user/all-user scope, saved-credential connection, targeted disconnect, server failures and split-tunnel behavior. No real VPN was connected during automated validation.
