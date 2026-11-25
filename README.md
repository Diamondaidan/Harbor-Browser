# Harbor Browser 0.3.7

## Reopen closed tabs

Press Ctrl+Shift+T, choose Reopen closed tab from a tab's context menu, or use Cog > History > Reopen closed tab.

Harbor remembers up to 20 closed normal tabs for the current browser session and restores the most recently closed first. The URL, title, pinned state, original position and existing group are restored. A removed group falls back to ungrouped. Restoring makes the tab visible even if a different group was selected.

Private tabs are excluded. The closed-tab list is kept in memory and clears when Harbor exits.

## Address suggestions

Type in the address bar to see matching bookmarks and history. Use Up/Down and Enter, or click a suggestion. Escape dismisses the list; Enter without a selection uses the normal address/search behavior.

Suggestions are generated locally. Private tabs suggest bookmarks only and exclude browsing history. Private navigation is not added to history. Page navigation updates preserve text being edited in the address bar.

## Downloads page

Press Ctrl+J, choose Cog > Downloads > Show all downloads, or enter harbor://downloads in the address bar. Harbor opens a Downloads tab and reuses an existing page for the same normal/private context.

The page includes filename search, live progress and sizes, Pause, Resume, Cancel, Open file, Show in folder, Remove and Clear completed. Removing list items keeps completed files on disk. Active/resumable downloads must be cancelled before removal. The page scrolls at small window sizes.

The list covers the current browser session and is not saved across restarts. Normal and private download lists are separated. Files downloaded in private tabs remain on the computer.

Resume is enabled when supported by the download. Resumed transfers retain their original list item and filename, including when the browser raises another download-start event. Source tabs can close while their transfers continue; their retained browser controls are released when no transfer needs them.

## Existing features

This release includes all 0.3.6 fixes: 1366 × 768 compatibility, compact light/dark cog menus with right-side scrollbars, full-URL selection, Beacon branding, home-page artwork, custom appearance palettes, right-side new tabs, tab reordering/groups, download filename/extension repair, video fullscreen and picture in picture.

## Install and build

Close Harbor, then run dist/HarborBrowserSetup-0.3.7.exe over the existing installation. Installer identity, profile location and schema 5 remain unchanged. The installer bundles the offline .NET Framework 4.8 and WebView2 x64 prerequisite packages.

The installer was built but not installed during verification. The installed browser and personal profile were not modified.

For building from source, follow SETUP-README.md. Source archives omit build outputs, test profiles and prerequisite executables.

See VERIFICATION.md for executed checks and limits, and ROADMAP.md for later milestones. Previews contains rendered interface examples.
