# Harbor Browser 0.3.3

Harbor is a Windows browser built with WPF, .NET Framework 4.8 and Microsoft Edge WebView2.

## What's new

Harbor Home includes a large search/address box, shortcuts from your saved bookmarks, a local date, and two original coastal scenes: Morning and Blue hour. Switch scenes with the buttons at the top right. The selected search engine is shared with the browser toolbar and Settings. A normal-tab theme choice survives reloads; private-tab changes are not saved.

Beacon is the selected identity: a white lighthouse, copper beam and wave on midnight navy. The app and installer use the new multi-resolution Windows icon. Artwork includes the selected mark, the three concept choices, backgrounds and a promotional banner.

The download fixes and compact window controls from 0.3.2 remain included.

## Opening the new home page

Enter `harbor://home` in Harbor's address bar. In Settings, use `harbor://home` as the Home page to use it for Home and new tabs.

The first 0.3.3 launch replaces the former default `https://duckduckgo.com/` home page with Harbor Home. A one-time preference marker prevents that upgrade from overriding a later explicit choice. Custom home-page URLs remain unchanged. Previously restored browsing tabs are still restored; click Home or open a new tab to see Harbor Home.

The page and artwork are bundled locally. The home page makes no external requests on load. Search submission and opening a bookmark navigate to external sites as normal.

## Install or build

The supplied installer is `dist/HarborBrowserSetup-0.3.3.exe`. It was built but not installed during verification. Close Harbor before running it and install over the existing version. The installer identity, profile location and profile schema 5 remain unchanged.

To build from source on Windows, use Visual Studio 2022 Build Tools or Community with .NET desktop tools and the .NET Framework 4.8 Developer Pack, and Inno Setup. Run `MAKE-SETUP.bat`. The build script retrieves and checks official Microsoft offline prerequisites when they are absent. The source ZIP excludes large prerequisites and build outputs.

## Artwork and verification

See `Artwork/README.md` for asset uses and `Artwork/PROMPTS.md` for the exact image-generation prompts. `Artwork/home-morning.png` and `Artwork/home-blue-hour.png` show actual WebView2 test captures.

Run `Tests/Run-Tests.ps1` for the 84 regression checks. Run `HomeSmokeTests/Run-Browser-Tests.ps1` after building the installer payload for 22 isolated WebView2 browser checks. The browser test runs in an offscreen test window and creates a separate test profile.

See `VERIFICATION.md` for executed checks and limits.
