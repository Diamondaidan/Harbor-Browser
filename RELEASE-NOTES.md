# Harbor Browser 0.7.0

- Add optional inactive-tab unloading, off by default. Choose 5, 15 or 30 minutes, or one hour, in Settings > Performance and memory. Unloaded tabs release their view and reload their saved address when selected.
- Check detected unsaved input in documents, frames and open shadow roots before unloading. Active/private/pinned tabs, kept-awake tabs, provider sign-in, submitted pages, playing media and active or paused downloads stay loaded. Unavailable activity checks retain the page.
- Show separate sleeping, unloaded and not-yet-loaded states in Resource Monitor, with unload eligibility/reasons and an explicit Reload action. Process memory reporting explains shared renderers and private committed memory.
- Avoid an unused background controller startup while restoring the saved selected tab; other restored tabs remain deferred until selected.

Unloading loses page state, scroll position and that tab's Back/Forward history. Some website state cannot be detected; keep important tabs awake. Turning the option off stops future unloading without reloading all discarded tabs at once. Private tabs remain protected from unloading in this version.

Proton's browser-only private-tab limit and explicit recovery behavior remain unchanged. Windows installers remain intentionally unsigned. This local development build does not publish a release, activate an update feed or upgrade the installed browser automatically.
