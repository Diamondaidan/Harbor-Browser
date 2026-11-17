#ifndef BrowserRegistryRoot
#define BrowserRegistryRoot "HKLM64"
#endif
[Registry]
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser"; ValueType: string; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\DefaultIcon"; ValueType: string; ValueData: """{app}\Branding\HarborBeacon-{#AppVersion}.ico"",0"
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"""
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Harbor web browser for HTTP and HTTPS links and local HTML pages."
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: """{app}\Branding\HarborBeacon-{#AppVersion}.ico"",0"
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\Capabilities\URLAssociations"; ValueType: string; ValueName: "http"; ValueData: "HarborBrowser.URL"
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\Capabilities\URLAssociations"; ValueType: string; ValueName: "https"; ValueData: "HarborBrowser.URL"
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\Capabilities\FileAssociations"; ValueType: string; ValueName: ".htm"; ValueData: "HarborBrowser.HTML"
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\Capabilities\FileAssociations"; ValueType: string; ValueName: ".html"; ValueData: "HarborBrowser.HTML"
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\Capabilities\Startmenu"; ValueType: string; ValueName: "StartMenuInternet"; ValueData: "HarborBrowser"
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\RegisteredApplications"; ValueType: string; ValueName: "{#AppName}"; ValueData: "{#RegistryPrefix}\Clients\StartMenuInternet\HarborBrowser\Capabilities"; Flags: uninsdeletevalue
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Classes\HarborBrowser.URL"; ValueType: string; ValueData: "Harbor web link"; Flags: uninsdeletekey
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Classes\HarborBrowser.URL"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Classes\HarborBrowser.URL\DefaultIcon"; ValueType: string; ValueData: """{app}\Branding\HarborBeacon-{#AppVersion}.ico"",0"
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Classes\HarborBrowser.URL\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" -- ""%1"""
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Classes\HarborBrowser.HTML"; ValueType: string; ValueData: "Harbor HTML document"; Flags: uninsdeletekey
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Classes\HarborBrowser.HTML\DefaultIcon"; ValueType: string; ValueData: """{app}\Branding\HarborBeacon-{#AppVersion}.ico"",0"
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Classes\HarborBrowser.HTML\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" -- ""%1"""
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Classes\.htm\OpenWithProgids"; ValueType: string; ValueName: "HarborBrowser.HTML"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty
Root: {#BrowserRegistryRoot}; Subkey: "{#RegistryPrefix}\Classes\.html\OpenWithProgids"; ValueType: string; ValueName: "HarborBrowser.HTML"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty

