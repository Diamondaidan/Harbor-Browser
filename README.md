# Harbor Browser 0.3.4

## What's new

- Regular new tabs append on the right. Pinned tabs remain together on the left.
- Drag tab headers to reorder them. A colored insertion edge shows the target position. Drag near either end of an overflowing strip to scroll it.
- Tab context menus include Move left and Move right. Ctrl+Shift+Page Up and Ctrl+Shift+Page Down provide the same commands. These moves respect the current group filter and the pinned-tab boundary.
- Reordering preserves the selected browser control and saves the new order for session restore.
- The selected Beacon icon is embedded in the executable and installer, set on the main window, and displayed beside HARBOR in the upper-left corner.
- Settings now has Appearance controls for Light and Dark mode, plus independent custom palettes for each mode.

## Customizing appearance

Open the cog menu, choose Settings, then Appearance.

1. Choose Light or Dark.
2. Select the color target: Accent, Window background, Panels, Input fields, or Text.
3. Choose hue and saturation on the color wheel. Use Brightness to set lighter/darker shades, or enter a hex value such as #A95627. The wheel also supports arrow-key adjustment.
4. Preview changes in the Settings window. Save settings applies them throughout Harbor. Cancel discards the draft.
5. Reset this color restores only the selected target. Reset this mode restores that mode's default palette.

Light and dark custom palettes are independent and persist in settings. Invalid hex values prevent saving. A contrast warning appears when chosen text and surfaces may be difficult to read. Accent foreground color is selected automatically for contrast.

The palette covers Harbor's header, tab strip, navigation, bookmarks/status/download bars, menus, dialogs and home page. WebView2 dialogs/menus and websites that support prefers-color-scheme follow the chosen mode. Website designs and Windows-owned message boxes/title bars retain their own styling.

Morning/Blue hour buttons on Harbor Home also change the app mode in normal tabs. In a private tab they change only that page and do not save an app-mode change.

## Install or build

The supplied installer is dist/HarborBrowserSetup-0.3.4.exe. It was built, not installed, during verification. Close Harbor and install over the existing version. The installer AppId, existing profile location and schema 5 are preserved.

To build from source, use Visual Studio 2022 desktop build tools with the .NET Framework 4.8 Developer Pack and Inno Setup. Run MAKE-SETUP.bat. The source archive omits build outputs and large Microsoft prerequisites; the build script obtains and verifies prerequisites when needed.

Windows can retain cached desktop shortcut icons after an update. The executable, installer and window resources in this release contain Beacon.

## Verification and previews

144 regression checks and 28 isolated WebView2 browser checks passed. The tab-order check covers 3,000 move configurations. Settings save/cancel tests use isolated dialogs, not your Harbor profile.

Run Tests/Run-Tests.ps1 and HomeSmokeTests/Run-Browser-Tests.ps1 after building the payload. See VERIFICATION.md for test limits. Previews contains offline WPF layouts and an actual WebView2 custom-color capture.
