# 0.5.2 changes and reversal

The regular release is in HarborBrowser-0.5.2. Stable 0.5.0 and earlier Tor preview source/packages are preserved in their original folders.

Site modules save explicit enable choices, reviewed package hashes and site grants. A missing native registration is restored only after integrity and permission checks. Disabled, changed or corrupt-policy packages remain blocked. Ordinary modules cannot inject Harbor Home or internal Tor controls. Legacy modules lacking the new metadata may need Site access saved again and an explicit Enable; files and grants are not silently trusted.

The optional Tor helper is cancelled on disposal and remains owned by Harbor. It has no Windows driver or service and changes no Windows proxy settings. Its requested state is persisted in the regular profile. Disconnect and restore reverses routing; complete/cancel downloads first.

The regular installer keeps Harbor's production upgrade identity and profile, advertises browser capabilities without changing UserChoice, and preserves profile data on uninstall. Before upgrade it snapshots existing core metadata in UpgradeBackups, including the connection flag and startup state. It bundles official signed offline Microsoft prerequisites and the required Tor notices and matching source. Restart-required results are carried through setup completion.

For application rollback, close Harbor and uninstall/reinstall the earlier version from its preserved package. Keep a full profile backup: an older build may not accept newer data. The earlier portable Tor previews use separate profiles and can be opened separately after this release is closed. Profile snapshots are safety copies, not a tested automatic downgrade tool.

VERIFICATION.md distinguishes live checks, simulated prerequisite checks and untested clean-computer/hardware/privacy cases.
