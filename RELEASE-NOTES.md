# Harbor Browser 0.7.0

- Add optional inactive-tab unloading, off by default. Choose 5, 15 or 30 minutes, or one hour, in Settings > Performance and memory. Unloaded tabs release their view and reload their saved address when selected.
- Check detected unsaved input in documents, frames and open shadow roots before unloading. Active/private/pinned tabs, kept-awake tabs, provider sign-in, submitted pages, playing media and active or paused downloads stay loaded. Unavailable activity checks retain the page.
- Show separate sleeping, unloaded and not-yet-loaded states in Resource Monitor, with unload eligibility/reasons and an explicit Reload action. Process memory reporting explains shared renderers and private committed memory.
- Avoid an unused background controller startup while restoring the saved selected tab; other restored tabs remain deferred until selected.
- Size the VPN status button to its text so Disconnected and the other connection labels fit, including compact layouts and both color modes.

Unloading loses page state, scroll position and that tab's Back/Forward history. Some website state cannot be detected; keep important tabs awake. Turning the option off stops future unloading without reloading all discarded tabs at once. Private tabs remain protected from unloading in this version.

Proton's browser-only private-tab limit and explicit recovery behavior remain unchanged. Windows installers remain intentionally unsigned. This full 0.7.0 package supersedes RC1 for new installations; preparing it does not automatically upgrade an installed browser or activate the public update feed.

Installed RC1 checks confirmed upgrade retention, Proton restart/off/on and disconnected-page blocking, clean-tab unload/reload, detected draft/media retention and exact-content download pause/resume. The final application adds only the VPN label layout correction. Physical internet interruption, sleep/wake/reboot and clean/offline spare-PC installation remain untested. Live IPv6 protection was inconclusive. See LIVE-VERIFICATION.md for scope and remaining checks.
