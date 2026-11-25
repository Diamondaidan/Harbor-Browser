# Harbor Browser 0.3.2 verification

Date: November 12, 2026 (America/Chicago).

## Delivered changes

- Window controls replace the cog in Harbor's existing top header.
- The separate Windows title bar is hidden. WindowChrome provides native caption gestures and resize borders; custom buttons call Windows minimize/maximize/restore/close commands.
- The cog is at the right end of the address bar.
- Downloads use Content-Disposition and WebView2 suggestions before falling back to the URL.
- Known MIME types add missing extensions; completed extensionless/generic downloads can recover their type from their bytes.
- ZIP container inspection distinguishes common Office, EPUB, JAR and APK files.
- Windows reserved device names, trailing dots/spaces, invalid characters, long names, and file/directory/reserved-path collisions are handled.
- Completed-file renaming never overwrites another file. Unknown formats retain their names. If recovery fails, the completed original file remains available.
- Installer AppId, profile folder and data schema 5 are preserved.

## Executed checks

- Actual Windows .NET Framework 4.8 Release build: PASS, zero warnings and zero errors.
- Inno Setup installer compilation: PASS.
- Embedded prerequisites: .NET Framework 4.8 and WebView2 x64 offline installers passed the build script's size and Microsoft Authenticode signature checks. Cached prerequisites were used; downloads were not repeated.
- Installer payload executable SHA-256 matches the compiled executable.
- 56 automated regression checks: PASS. See the accompanying test log and Tests/Run-Tests.ps1.
- The original 93,245-byte 0.3.1 ZIP was copied to an extensionless test file, renamed automatically by the new service, and verified by SHA-256 and a 37-entry archive read.
- File/header/URL/MIME naming, UTF-8 and language-tagged filename headers, literal percent signs, reserved names, invalid names, path stripping, bounded names, collisions, non-overwrite behavior and idempotent recovery: PASS.
- File-type checks include ZIP, PDF, PNG, JPEG, common Office ZIP containers, EPUB, PE executable, non-executable DLL and unknown bytes.
- Offline WPF renders at 1320x850 and 800x560: PASS. Controls fit in the top row and the cog sits in the address row. Caption hit-testing is enabled in the XAML.
- Layout preview was visually inspected.

## Limits and remaining desktop checks

The layout renders parse the production XAML with event handlers removed for offline rendering; they do not launch Harbor or instantiate a real WebView2 browser. Filename regression checks call the production naming service directly. These are not end-to-end download or desktop interaction tests.

Live desktop capture previously timed out twice. Not verified in a running 0.3.2 browser:
- Minimize/maximize/restore/close, caption double-click/drag, snap, Alt+Space, taskbar bounds or multiple monitors/DPI settings.
- Actual network download progress, cancel/resume, simultaneous transfers and completion event timing.
- Settings save/reopen/scroll behavior or resource-monitor accuracy/refresh.
- Installer execution, upgrade, prerequisites on a clean machine, or uninstall.

No original 0.3.1 source, installed application or synced project reference files were modified. The 0.3.2 installer was built but not installed.
