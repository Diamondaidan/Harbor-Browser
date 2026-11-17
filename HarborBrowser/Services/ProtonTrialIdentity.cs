using System;
using System.IO;

namespace HarborBrowser.Services
{
    // Stable releases share the established Harbor profile and link-activation identity.
    internal static class ProtonTrialIdentity
    {
        internal const string InstanceSuffix="";
        internal const string AppUserModelId="HarborBrowser.Desktop";
        internal const string RegisteredName="Harbor Browser";
        internal static string DisplayName {get{return "Harbor Browser";}}
        internal static string DataFolder(string localApplicationData)
        {
            if(string.IsNullOrWhiteSpace(localApplicationData)||!Path.IsPathRooted(localApplicationData))
                throw new ArgumentException("A Windows application-data folder is required.",nameof(localApplicationData));
            return Path.Combine(localApplicationData,"HarborBrowser");
        }
        internal static BrowserDataStore CreateStore()
        {
            return new BrowserDataStore();
        }
    }
}
