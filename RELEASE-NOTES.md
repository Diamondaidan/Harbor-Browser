# Harbor Browser 0.6.3

- Correct a browser-verification reply read that could stall after a successful request. The existing verifier now reads a bounded original network reply, with page scripts disabled and request destinations restricted.
- Reject failed or malformed verification promptly, cancel unfinished comparisons and recover with a fresh verifier after a failure. Proxy ownership, TLS, address comparison and proof expiry remain required.
- Pause verification while Windows reports the network adapter unavailable; schedule a fresh check after it returns. Explain failed checks and explicit retry more clearly.
- Explain download recovery, expired/sign-in links and disk-space failures. Prevent duplicate Resume validation, preserve the original download row/file across replacement operations, and match private resumes to their own session.
- Include adapter availability in local diagnostics without exposing an IP address, network name or account data.

Private tabs remain unavailable while browser-only Proton/Tor is enabled. Turn off protection — keep sign-in permits private browsing on the regular connection. Disconnect keeps protection enabled and browsing paused. Pages, forms and downloads are not resumed automatically after recovery.

The installer remains intentionally unsigned for Windows. Microsoft prerequisites retain their verified signatures, and Harbor's signed update metadata is separate. This local package does not publish GitHub assets or replace the installed browser automatically. See RELIABILITY-0.6.3-VERIFICATION.md and RELEASE-VERIFICATION.md for automated results and the live-account, hardware and clean-install checks that remain manual.
