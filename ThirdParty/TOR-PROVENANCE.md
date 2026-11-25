# Tor provenance and source

Official binary bundle URL:
https://dist.torproject.org/torbrowser/16.0.1/tor-expert-bundle-windows-x86_64-16.0.1.tar.gz

Verified against the official published SHA-256:
9eed3c3ff1fe43df034ce360196c9ec95472560d77ff55614d0cd5120edea27b
https://dist.torproject.org/torbrowser/16.0.1/sha256sums-signed-build.txt

Included runtime: Tor 0.4.9.16, tor/tor.exe; data/geoip and data/geoip6. Original component notices from the bundle are preserved under Assets/BrowserVpn/TorRuntime/docs. Unused bridge/transport and tor-gencert executables were omitted. Harbor runs Tor as a client bound to loopback, with no Windows service installation.

Matching source URL:
https://dist.torproject.org/tor-0.4.9.16.tar.gz
Included as tor-0.4.9.16-source.tar.gz.
Observed source archive SHA-256:
6180e193152f4525bdca5475168419b2a2035b963c3c60162a187f86e6160e48

The official binary's published SHA-256 was checked; detached-signature verification was not performed. No Tor source modifications were made. Official build tooling: https://gitlab.torproject.org/tpo/applications/tor-browser-build

Harbor's routing extension, control page and WPF adapter are included as source in the Harbor source archive. The manifest public key fixes its identity across extraction paths; it is not a signing or publisher-authentication claim. No proprietary provider SDK or paid redistribution license is used for this trial.
