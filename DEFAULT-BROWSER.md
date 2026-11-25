# Set Harbor as the Windows browser

1. Close Harbor and install 0.5.0 using HarborBrowserSetup-0.5.0.exe.
2. Start Harbor and choose **Cog > Tools > Default browser**.
3. On Windows 10, choose **Web browser > Harbor Browser** in Default apps. On supported Windows 11 versions, the shortcut opens Harbor's page; choose **Set default**, or choose Harbor for HTTP and HTTPS individually.
4. Optionally choose Harbor for .htm and .html files. Other file types, including PDFs, are not claimed by this installer.

If Harbor is missing, finish installation and reopen Windows Settings. Running the portable executable alone does not install its browser registration. A company policy can restrict default-app changes. The supported behavior requires you to make the final choice in Windows; Harbor never writes UserChoice or forces itself to be the default.

External HTTP/HTTPS links open a new normal tab in the existing window. This also applies when a private tab is currently selected. With no existing window, Harbor starts and opens the target, while retaining normal session restore/recovery rules. Local .htm/.html paths with spaces are supported; remote/UNC files and unrelated command schemes are not. A second launch with no target activates the existing window. Close older Harbor builds before upgrading: they do not have the new forwarding listener.

The production installer registers machine-wide capabilities under its own StartMenuInternet client, RegisteredApplications and owned ProgIDs. It adds OpenWith entries for HTML files, but does not replace the selected browser or extension defaults. Uninstall removes only Harbor's owned entries. If you uninstall while Harbor is selected as default, choose another browser in Windows settings.

Automated installer tests used a separate per-user fixture identity and verified registry contents, quoted launch commands, upgrade preservation, removal and unchanged HTTP/HTTPS UserChoice values. Real App tests exercised cold and warm link/file activations with isolated profiles. Selecting the production browser in Windows Settings remains a manual check; the tests did not alter the user's default browser.

References: [Microsoft Default Programs registration](https://learn.microsoft.com/en-us/windows/win32/shell/default-programs) and [launching Default apps settings](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-default-apps-settings).
