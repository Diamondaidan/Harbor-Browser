# Site modules in Harbor 0.4.3

Open **Cog > Tools > Site modules**. Choose **Add module**, select a trusted local `manifest.json`, review the listed sites, check the permission box, and install. Enable/disable and Remove take effect by reloading normal pages. Save unfinished forms before changing a module. The original source folder is preserved; Harbor installs a separate managed copy.

This is the first extension foundation: local Manifest V3 top-frame content scripts, CSS and optional `storage` permission. It is not a Chrome Web Store installer or support for all Chrome/Edge extensions. Background workers, toolbar actions, native messaging, arbitrary host permissions, frame injection and main-world injection are rejected. No .NET or operating-system host objects are exposed. Modules can read/change their allowed websites and may send data from those pages; site access is sensitive, so install only trusted code. Permission review is not a code audit.

The WebView2 extension engine enforces the generated manifest's HTTP/HTTPS match patterns and isolated script world. Harbor excludes `https://harbor.invalid/*` from every module. Modules are installed into the normal native profile, never a private profile. WebView2's built-in PDF and clipboard components are left untouched and excluded from this manager. Harbor manages the modules installed through this workflow; it does not administer extensions installed externally by other software or edits made directly to browser profile files.

## Choose site access

Select a module and choose **Site access**. Check the requested HTTP/HTTPS patterns it may use, then save. The browser installs only the checked patterns into its native content-script rules. You cannot grant an undeclared pattern. A wildcard can include many websites; overlapping checked patterns can still allow a site whose other pattern was unchecked. This release selects declared patterns, rather than inventing narrower rules for arbitrary hosts.

Uncheck everything to stop all site access. The saved module remains visible as **No sites allowed**, and Enable cannot bypass it. Choose a site first, then enable. Editing access on a disabled module keeps it disabled until explicit Enable. Changes reload normal pages, including reloading a sleeping page before it resumes with revoked access. Private tabs and Harbor Home are always excluded.

**Changing site access may reset the module's own saved preferences.** Save unfinished forms first. Harbor must unregister the old native module and apply a filtered manifest; it cannot promise to preserve that extension's storage across this step. Ordinary Disable/Enable does not use that manifest replacement path. See [Chrome's storage documentation](https://developer.chrome.com/docs/extensions/reference/api/storage) for removal/storage behavior and [WebView2's installation contract](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile.addbrowserextensionasync) for native installation behavior.

`module-permissions.json` preserves the original request separately from the active manifest, alongside the chosen patterns. Schema 9 protects this state from older Harbor builds and adds it to migration/installer snapshots. Unreadable saved permission data disables the managed modules instead of restoring broader access from a backup. The manager reports the problem; remove the affected modules and reinstall trusted copies to recover. When the final affected module is removed, the damaged file is retained as an atomic-write backup and a fresh empty policy is saved. Keep this metadata with your profile backups; it is not a portable extension backup by itself.

The list shows Enabled, Disabled, No sites allowed or Blocked status, and the details explain the granted sites. Disabled copies awaiting explicit Enable are retained in the list even when unregistered from WebView2. The manager and site editor scroll and wrap to fit short windows.

An optional `harbor-module.json` declares the Harbor interface and minimum version:

```json
{"api_version":1,"minimum_harbor_version":"0.4.2"}
```

Unsupported API/minimum versions are rejected. A managed module is checked again when enabling and on browser-engine initialization. Native installed/enabled state persists in WebView2; Harbor never reinstalls or enables a module just because a metadata backup exists. `modules.json` records the native ID and managed folder. Preserve that file along with the profile if backing up modules manually; it is not a portable extension backup by itself.

Supported manifest keys: `manifest_version`, `name`, numeric `version`, `description`, `icons`, `permissions`, `content_scripts`. Content script keys: `matches`, `exclude_matches`, local `js`/`css`, `run_at`, `world` (ISOLATED only), `all_frames` (false only). Use patterns such as `https://example.com/*`; `<all_urls>`, file access and unknown capability fields are refused. Modules are limited to 500 files, 500 directories, 20 MB total and a 256 KB manifest. Links, traversal paths and executable binaries are refused.

Try `Examples/BeaconSiteModule/manifest.json` from the source package, then visit `https://example.com/`. The badge disappears after disabling/removing and the automatic page reload. The demonstration needs no account or remote module download. The sample is not installed automatically.

Script exceptions stay in WebView2's extension context and do not become Harbor/.NET exceptions. Heavy or hostile code can still slow a renderer. Broader extension APIs, store distribution, update/signing policies and independent security review remain future work. The feature needs a WebView2 Runtime with browser-extension APIs; older runtimes report an error in the manager without preventing ordinary browsing.

Engine API references: [AddBrowserExtensionAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile.addbrowserextensionasync), [CoreWebView2BrowserExtension lifecycle](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2browserextension), and [GetFaviconAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.getfaviconasync).
