# Site modules in Harbor 0.4.2

Open **Cog > Tools > Site modules**. Choose **Add module**, select a trusted local `manifest.json`, review the listed sites, check the permission box, and install. Enable/disable and Remove take effect by reloading normal pages. Save unfinished forms before changing a module. The original source folder is preserved; Harbor installs a separate managed copy.

This is the first extension foundation: local Manifest V3 top-frame content scripts, CSS and optional `storage` permission. It is not a Chrome Web Store installer or support for all Chrome/Edge extensions. Background workers, toolbar actions, native messaging, arbitrary host permissions, frame injection and main-world injection are rejected. No .NET or operating-system host objects are exposed. Modules can read/change their allowed websites and may send data from those pages; site access is sensitive, so install only trusted code. Permission review is not a code audit.

The WebView2 extension engine enforces the generated manifest's HTTP/HTTPS match patterns and isolated script world. Harbor excludes `https://harbor.invalid/*` from every module. Modules are installed into the normal native profile, never a private profile. WebView2's built-in PDF and clipboard components are left untouched and excluded from this manager. Harbor manages the modules installed through this workflow; it does not administer extensions installed externally by other software or edits made directly to browser profile files.

An optional `harbor-module.json` declares the Harbor interface and minimum version:

```json
{"api_version":1,"minimum_harbor_version":"0.4.2"}
```

Unsupported API/minimum versions are rejected. A managed module is checked again when enabling and on browser-engine initialization. Native installed/enabled state persists in WebView2; Harbor never reinstalls or enables a module just because a metadata backup exists. `modules.json` records the native ID and managed folder. Preserve that file along with the profile if backing up modules manually; it is not a portable extension backup by itself.

Supported manifest keys: `manifest_version`, `name`, numeric `version`, `description`, `icons`, `permissions`, `content_scripts`. Content script keys: `matches`, `exclude_matches`, local `js`/`css`, `run_at`, `world` (ISOLATED only), `all_frames` (false only). Use patterns such as `https://example.com/*`; `<all_urls>`, file access and unknown capability fields are refused. Modules are limited to 500 files, 500 directories, 20 MB total and a 256 KB manifest. Links, traversal paths and executable binaries are refused.

Try `Examples/BeaconSiteModule/manifest.json` from the source package, then visit `https://example.com/`. The badge disappears after disabling/removing and the automatic page reload. The demonstration needs no account or remote module download. The sample is not installed automatically.

Script exceptions stay in WebView2's extension context and do not become Harbor/.NET exceptions. Heavy or hostile code can still slow a renderer. Broader extension APIs, per-site grant editing, store distribution, update/signing policies and independent security review remain future work. The feature needs a WebView2 Runtime with browser-extension APIs; older runtimes report an error in the manager without preventing ordinary browsing.

Engine API references: [AddBrowserExtensionAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile.addbrowserextensionasync), [CoreWebView2BrowserExtension lifecycle](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2browserextension), and [GetFaviconAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.getfaviconasync).
