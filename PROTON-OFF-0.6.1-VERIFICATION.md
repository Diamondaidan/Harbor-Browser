# Harbor 0.6.1 Proton off/on correction

The previous off action uninstalled Proton's extension. Chromium removes extension-local storage on uninstall, which explains why its sign-in was lost while the account website stayed signed in. Disconnect remains distinct: it removes the protected connection while Harbor's request protection stays armed.

## New behavior

- **Turn off protection — keep sign-in** disables the installed extension, keeps its registration and saved data, and disarms the native fallback before declaring regular browsing available. It is visible in the compact Proton window and on paused pages.
- After off succeeds, Harbor shows **VPN Off** and paused pages show **Ready to resume**. Explicit Reload/Retry is still required; forms and downloads are not replayed automatically.
- Enabling again reuses the saved registration and requires fresh route verification. Saved sessions remain subject to Proton's own expiration or revocation.
- **Remove Proton and reset sign-in**, under Options, is a separate uninstall/reset action. Website cookies are retained.
- Failed off persistence, failed fallback persistence and unfinished native extension registration keep the request gate closed. A superseded off cannot override a later on. No provider files or profile format were changed.

## Executed local coverage

The new `--proton-off061` suite passed **59 checks** using owned profiles and artificial data, with the real WebView2 runtime and official signed-out Proton extension:

- Both visible off actions; actual local HTTP browsing after explicit Reload; no automatic replay; matching indicator and paused-page text; restored updater availability; unrelated extensions retained.
- A unique extension-local boolean survives repeated off/on cycles, failure recovery and a full browser/engine process restart while off. Its extension remains installed and disabled on startup, then is reused on enable.
- Real owned-file locks force off-intent and fallback-intent write failures. Requests remain blocked and explicit retry restores regular browsing.
- A queued off superseded by on cannot disable the later request. A controlled delay around a real native registration verifies that off waits for completion and disables it before opening the regular route.
- Only explicit Remove/reset clears the artificial extension-local marker. A separately seeded website cookie survives reset and reinstallation.

The full application and installer records identify the exact candidate hashes and additional results. The provider's pinned files were verified unchanged. Native test windows and generated browser profiles are separate from the installed Harbor profile.

## Limits

No real account credentials, tokens or authentication databases were read or copied. The retained marker proves extension-local data preservation, not live Proton session validity. An owner-run connected account check is still needed after installation: turn protection off, Reload, enable/connect, then repeat after restarting Harbor while off. Actual installer/UAC/prerequisite/reboot and physical network/DPI limits remain as documented in LOCAL-PACKAGE-VERIFICATION.md.
