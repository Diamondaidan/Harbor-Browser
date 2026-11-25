# Windows default-browser registration

The regular installer retains Harbor's established AppId, HarborBrowser.Desktop identity, machine browser registration and HarborBrowser.URL/HTML handlers. On Windows 11 Harbor opens the machine-registered Harbor Browser entry in Default apps; Windows 10 opens Default apps. Windows keeps the user's choice until the user changes it there.

Setup never writes HTTP/HTTPS UserChoice. Isolated native fixtures exercise equivalent registration under a nonactive test namespace and verify actual user choices stay unchanged. Selecting the actual default in Windows UI remains a manual installation check.

Beta registration and HarborTrials/057B are not migrated or removed.
