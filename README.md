# Harbor Browser 0.4.0

## New in 0.4.0: privacy and recovery

Open Cog > Tools > Site permissions to review, allow, block or reset camera, microphone, location and notification access. Each remembered choice belongs to both the requesting site and the page embedding it. Applying changes reloads matching open pages so previously granted access stops.

New requests appear in a Harbor banner with Allow and Block buttons. Leave Remember unchecked for a one-time decision. Normal remembered choices are saved on this computer. Background tabs and unsupported permission types are blocked. Pending requests are denied when you navigate, switch tabs, close the tab or leave the prompt unanswered for a minute.

Open Cog > Tools > Clear browsing data, or press Ctrl+Shift+Delete. Choose the last hour, 24 hours, 7 days, 4 weeks or all time, then select history, cookies/site storage, cache and/or remembered permissions. The panel names its scope: normal browsing or the currently selected private session. It keeps open tabs, bookmarks and downloaded files. History cleanup also resets closed-tab/recovery lists and sanitizes matching records in Harbor-managed backups. Separate copies you exported yourself are outside this cleanup.

WebView2 applies its own time matching to cookies, cache and site storage; storage for an entire matching site can be removed. Open pages can write new data after cleanup. This is browsing-data removal, not secure disk erasure.

Private tabs share an in-memory permission policy and an InPrivate profile for that session. Closing the last private tab clears its browsing data and download list. Unfinished private downloads require confirmation before cancellation; completed files remain on disk. The next private session uses a fresh profile identity. Private pages stay out of Harbor history, session restore and online search completion. Private browsing does not hide network activity from websites, network administrators or an internet provider.

After an unclean shutdown, Harbor offers Restore tabs before reopening saved pages. The recovery checkpoint contains normal tabs, pin states and groups. Dismiss hides the startup offer; Cog > Tools > Restore recovery snapshot can reopen the last checkpoint. Clearing history removes that checkpoint. Only one Harbor process can write a profile at a time.

A page or browser-engine failure now shows Reload tab. Engine recovery recreates the broken browser control; active downloads are marked interrupted. Unsaved forms, in-page state and interrupted downloads are not restored automatically.

## Bookmarks and folders

Open Cog > Bookmarks > Manage bookmarks. Add/edit names, addresses and folders, filter/search the library, reorder entries, and create or rename folders. Use / for subfolders, for example Work/Projects. Removing a folder moves its bookmarks to Unfiled.

Folders appear on the bookmarks bar and in the cog menu. Existing bookmarks stay available without needing a folder.

Import browser bookmark HTML in Netscape format or Harbor JSON. Export HTML or Harbor JSON, including folder structure and empty folders. Imports merge into the current draft and skip duplicate folder/address pairs. Unsafe or credential-bearing addresses are excluded. Import files are parsed as data and are not opened as web pages.

Choose Save to apply manager changes. Closing or cancelling leaves the original library unchanged. Export includes the entire current draft library.

## Home page customization

Choose Customize on Harbor Home, or open Settings > Home page customization.

- Choose the automatic light/dark scene, a fixed Morning or Blue hour scene, or a custom image.
- Custom pictures are copied into the Harbor profile as PNG; the original image is unchanged. Images are resized to a maximum long edge of 1600 pixels.
- Show the first 12 bookmarks automatically, or edit an independent list of up to 12 home shortcuts and arrange their order.

Image input limits are 12 MB and 24 million pixels. Customize is hidden on private Home tabs; private tabs use the saved appearance without making Home-page changes automatically.

## Saved appearance presets

Settings > Appearance lets you save, apply and remove up to 20 named presets. Presets store both light/dark color palettes and the selected mode. Changes stay in the Settings draft until Save settings. Background images and shortcuts are configured separately.

## Search highlight and online completion

Search/address-bar selection is lighter, with text remaining above the color. Home-page suggestions use a translucent highlight in dark and light mode.

Online suggestions use the selected search engine: DuckDuckGo, Google, Bing, Brave or Startpage. They work in the address bar and Harbor Home. They are enabled for normal tabs and can be turned off in Settings > Search suggestions.

Online lookup sends search text to the selected provider. It uses separate HTTP requests without browser cookies or default credentials. It is always disabled in private tabs. Full web addresses, IP addresses, localhost, paths and credential-shaped inputs are excluded from online lookup.

Requests are delayed briefly while typing, cancelled when superseded, and stale replies are ignored. Provider errors fall back to the existing local bookmark/history suggestions and search action. A selected provider completion always performs a search.

## Install and data

Close Harbor and run dist/HarborBrowserSetup-0.4.0.exe over the existing installation. The installer identity and profile location remain the same. It bundles the offline .NET Framework 4.8 and WebView2 x64 prerequisite packages.

Profile schema advances from 6 to 7 for permission rules and recovery metadata. Harbor snapshots the existing profile before migration. Earlier builds protect newer-schema data by disabling saves.

No installation or upgrade was performed during verification. The installed browser and personal profile were not modified.

All 0.3.7 tab restoration, Downloads, fullscreen, picture-in-picture and compatibility fixes are retained.

For building from source, follow SETUP-README.md. See VERIFICATION.md for executed tests and limits, ROADMAP.md for later milestones, and Previews for interface examples.
