using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser
{
    public sealed class SiteModuleRow
    {
        public string Id {get;set;}
        public string Name {get;set;}
        public string Version {get;set;}
        public string Status {get;set;}
        public string Sites {get;set;}
        public bool Enabled {get;set;}
        public List<string> RequestedSites {get;set;}=new List<string>();
        public List<string> AllowedSites {get;set;}=new List<string>();
        public string Display {get{return Name+"  ·  "+Status;}}
        public string AccessDetails {get{return Name+" "+Version+"\n"+Status+"\n\nAllowed site patterns:\n"+(AllowedSites.Count==0?"None":string.Join("\n",AllowedSites))+"\n\nCan read and change matching pages; local extension storage is available when declared. Scripts run in an isolated world in normal tabs only. Harbor Home and private tabs are excluded.";}}
    }
    public sealed class ModuleSitePolicy
    {
        public string OriginalManifest {get;set;}
        public List<string> AllowedSites {get;set;}
        public bool AwaitingEnable {get;set;}
    }
    public partial class MainWindow
    {
        private readonly Dictionary<string,string> _moduleFolders=new Dictionary<string,string>();
        private Task _moduleValidation;
        private int _moduleValidationGeneration=-1;
        private Dictionary<string,ModuleSitePolicy> _modulePolicies=new Dictionary<string,ModuleSitePolicy>();
        private string _modulePolicyError;
        private string ModuleRoot {get{return Path.Combine(_store.RootFolder,"Modules");}}
        private Task InitializeModules(CoreWebView2Profile profile)
        {
            if(profile.IsInPrivateModeEnabled)return Task.CompletedTask;
            if(_moduleValidationGeneration!=_browserGeneration){_moduleValidationGeneration=_browserGeneration;_moduleValidation=ValidateModules(profile);}
            return _moduleValidation;
        }
        private async Task ValidateModules(CoreWebView2Profile profile)
        {
            _moduleFolders.Clear();
            try
            {
                string metadata=Path.Combine(_store.RootFolder,"modules.json");
                if(File.Exists(metadata)&&new FileInfo(metadata).Length<262144)
                {
                    var saved=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(metadata));
                    if(saved!=null)foreach(var entry in saved){Guid id;if(Guid.TryParseExact(entry.Value,"N",out id))_moduleFolders[entry.Key]=entry.Value;}
                }
            }
            catch(Exception ex){System.Diagnostics.Debug.WriteLine("Module metadata unavailable: "+ex.Message);}
            _modulePolicies=new Dictionary<string,ModuleSitePolicy>();_modulePolicyError=null;
            try
            {
                string file=Path.Combine(_store.RootFolder,"module-permissions.json");
                if(File.Exists(file))
                {
                    if(new FileInfo(file).Length>4*1024*1024)throw new InvalidDataException("Site-access file is too large.");
                    _modulePolicies=ModuleJson().Deserialize<Dictionary<string,ModuleSitePolicy>>(File.ReadAllText(file));
                    if(_modulePolicies==null)throw new InvalidDataException("Site-access file is empty.");
                }
            }
            catch(Exception ex){_modulePolicies=new Dictionary<string,ModuleSitePolicy>();_modulePolicyError="Saved site access could not be read. Remove and reinstall the affected module after backing up your profile. "+ex.Message;}
            try
            {
                // WebView2 also returns its PDF/clipboard engine components here.
                // Only manage modules installed through Harbor's reviewed path.
                foreach(var extension in (await profile.GetBrowserExtensionsAsync()).Where(e=>_moduleFolders.ContainsKey(e.Id)))
                {
                    try{ModulePackage(extension.Id);CheckActiveManifest(extension.Id);}catch{await extension.EnableAsync(false);}
                }
            }
            catch(Exception ex){System.Diagnostics.Debug.WriteLine("Site modules unavailable: "+ex.Message);}
        }
        private SiteModulePackage ModulePackage(string id)
        {
            string folder;if(!_moduleFolders.TryGetValue(id,out folder))throw new InvalidOperationException("Unrecognized module. Remove it and add a reviewed copy.");
            if(_modulePolicyError!=null)throw new InvalidOperationException(_modulePolicyError);
            ModuleSitePolicy policy;_modulePolicies.TryGetValue(folder,out policy);
            if(policy!=null&&(policy.OriginalManifest==null||policy.AllowedSites==null))throw new InvalidDataException("Saved site access is invalid. Remove and reinstall this module.");
            var package=SiteModuleService.Inspect(Path.Combine(ModuleRoot,folder),policy?.OriginalManifest);
            if(policy!=null)SiteModuleService.Manifest(package,policy.AllowedSites);
            return package;
        }
        private static JavaScriptSerializer ModuleJson(){return new JavaScriptSerializer{MaxJsonLength=4*1024*1024,RecursionLimit=40};}
        private List<string> ModuleSites(string id,SiteModulePackage package)
        {ModuleSitePolicy policy;return _modulePolicies.TryGetValue(_moduleFolders[id],out policy)?policy.AllowedSites.ToList():package.Sites.ToList();}
        private void CheckActiveManifest(string id)
        {
            ModuleSitePolicy policy;if(!_modulePolicies.TryGetValue(_moduleFolders[id],out policy))return;
            var package=ModulePackage(id);string active=File.ReadAllText(Path.Combine(package.Folder,"manifest.json"));
            // Compare canonical JSON values; formatting alone does not change access.
            var json=ModuleJson();if(json.Serialize(json.DeserializeObject(active))!=SiteModuleService.Manifest(package,policy.AllowedSites))throw new InvalidDataException("Site access needs to be reapplied. Open Site access and save it again.");
            if(policy.AwaitingEnable||policy.AllowedSites.Count==0)throw new InvalidOperationException("This module is disabled until you allow a site and enable it.");
        }
        internal async Task<List<SiteModuleRow>> GetModules()
        {
            var rows=new List<SiteModuleRow>();
            await WithProfile(null,async profile=>
            {
                await InitializeModules(profile);
                var extensions=(await profile.GetBrowserExtensionsAsync()).ToDictionary(e=>e.Id);
                foreach(var entry in _moduleFolders.ToList())
                {
                    CoreWebView2BrowserExtension extension;extensions.TryGetValue(entry.Key,out extension);
                    var row=new SiteModuleRow{Id=entry.Key,Name=extension?.Name??"Saved module",Enabled=extension!=null&&extension.IsEnabled,Status=extension==null?"Not installed — enable to retry":extension.IsEnabled?"Enabled":"Disabled"};
                    try
                    {
                        var package=ModulePackage(entry.Key);row.Name=package.Name;row.Version=package.Version;row.RequestedSites=package.Sites.ToList();row.AllowedSites=ModuleSites(entry.Key,package);row.Sites=string.Join(", ",row.AllowedSites);
                        ModuleSitePolicy policy;_modulePolicies.TryGetValue(entry.Value,out policy);
                        if(row.AllowedSites.Count==0)row.Status="No sites allowed";
                        else if(policy!=null&&policy.AwaitingEnable)row.Status="Disabled — site access saved";
                        else if(extension!=null)CheckActiveManifest(entry.Key);
                    }
                    catch(Exception ex){row.Status="Blocked: "+ex.Message;row.Enabled=false;if(extension!=null)await extension.EnableAsync(false);}
                    rows.Add(row);
                }
            });return rows;
        }
        private void SaveModuleFolders()
        {
            if(_store.IsReadOnlyForNewerDataVersion||!_sessionWritesAllowed)throw new InvalidOperationException("Module changes are unavailable in a read-only profile.");
            BrowserDataStore.WriteTextAtomically(Path.Combine(_store.RootFolder,"modules.json"),new JavaScriptSerializer().Serialize(_moduleFolders));
        }
        private void SaveModulePolicies()
        {
            if(_modulePolicyError!=null)throw new InvalidOperationException(_modulePolicyError);
            BrowserDataStore.WriteTextAtomically(Path.Combine(_store.RootFolder,"module-permissions.json"),ModuleJson().Serialize(_modulePolicies));
        }
        private void RequireModuleWrites(){if(_store.IsReadOnlyForNewerDataVersion||!_sessionWritesAllowed)throw new InvalidOperationException("Module changes are unavailable in a read-only profile.");}
        internal async Task<string> InstallModule(SiteModulePackage reviewed)
        {
            if(_store.IsReadOnlyForNewerDataVersion||!_sessionWritesAllowed)throw new InvalidOperationException("This profile is read-only.");
            string folder=SiteModuleService.Stage(reviewed,ModuleRoot),id=null;
            try
            {
                await WithProfile(null,async profile=>
                {
                    await InitializeModules(profile);if(_modulePolicyError!=null)throw new InvalidOperationException(_modulePolicyError);var extension=await profile.AddBrowserExtensionAsync(folder);id=extension.Id;
                    _moduleFolders[id]=Path.GetFileName(folder);
                    try{SaveModuleFolders();}catch{await extension.RemoveAsync();_moduleFolders.Remove(id);throw;}
                });
            }
            catch{SiteModuleService.RemoveManaged(ModuleRoot,folder);throw;}
            ReloadModuleTabs();return id;
        }
        internal async Task ChangeModule(string id,bool enabled,bool remove=false)
        {
            if(_store.IsReadOnlyForNewerDataVersion||!_sessionWritesAllowed)throw new InvalidOperationException("This profile is read-only.");
            await WithProfile(null,async profile=>
            {
                await InitializeModules(profile);var extension=(await profile.GetBrowserExtensionsAsync()).FirstOrDefault(e=>e.Id==id);
                if(!_moduleFolders.ContainsKey(id))throw new InvalidOperationException("Module is no longer managed by Harbor.");
                if(remove){if(extension!=null)await extension.RemoveAsync();}
                else if(enabled)
                {
                    var package=ModulePackage(id);var sites=ModuleSites(id,package);if(sites.Count==0)throw new InvalidOperationException("Choose at least one allowed site in Site access first.");
                    ModuleSitePolicy policy;_modulePolicies.TryGetValue(_moduleFolders[id],out policy);
                    if(extension==null)
                    {
                        File.WriteAllText(Path.Combine(package.Folder,"manifest.json"),SiteModuleService.Manifest(package,sites));
                        extension=await profile.AddBrowserExtensionAsync(package.Folder);
                        if(extension.Id!=id){await extension.RemoveAsync();throw new InvalidOperationException("The module identity changed. Remove this saved copy and install it again.");}
                    }
                    try
                    {
                        if(policy!=null){policy.AwaitingEnable=false;SaveModulePolicies();}
                        CheckActiveManifest(id);await extension.EnableAsync(true);
                    }
                    catch{await extension.EnableAsync(false);ReloadModuleTabs();throw;}
                }
                else if(extension!=null)await extension.EnableAsync(false);
            });
            ReloadModuleTabs();
            if(remove)
            {
                string folder;if(_moduleFolders.TryGetValue(id,out folder)){_moduleFolders.Remove(id);SaveModuleFolders();_modulePolicies.Remove(folder);if(_moduleFolders.Count==0){_modulePolicies.Clear();_modulePolicyError=null;}if(_modulePolicyError==null)SaveModulePolicies();SiteModuleService.RemoveManaged(ModuleRoot,Path.Combine(ModuleRoot,folder));}
            }
        }
        internal async Task SetModuleSites(string id,IEnumerable<string> selected)
        {
            RequireModuleWrites();
            try
            {
                await WithProfile(null,async profile=>
                {
                    await InitializeModules(profile);var package=ModulePackage(id);
                    var sites=(selected??Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToList();
                    string manifest=SiteModuleService.Manifest(package,sites); // Validate before changing native state.
                    var extension=(await profile.GetBrowserExtensionsAsync()).FirstOrDefault(e=>e.Id==id);bool enabled=extension!=null&&extension.IsEnabled;
                    if(extension!=null)await extension.EnableAsync(false);
                    var policy=new ModuleSitePolicy{OriginalManifest=SiteModuleService.Manifest(package),AllowedSites=sites,AwaitingEnable=true};
                    _modulePolicies[_moduleFolders[id]]=policy;SaveModulePolicies();
                    if(extension!=null)await extension.RemoveAsync();
                    File.WriteAllText(Path.Combine(package.Folder,"manifest.json"),manifest);
                    // AddBrowserExtensionAsync starts enabled. Keep disabled modules
                    // unregistered until explicit Enable, avoiding a transient grant.
                    if(enabled&&sites.Count>0)
                    {
                        extension=await profile.AddBrowserExtensionAsync(package.Folder);
                        if(extension.Id!=id){await extension.RemoveAsync();throw new InvalidOperationException("The module identity changed. Remove this saved copy and install it again.");}
                        policy.AwaitingEnable=false;
                        try{SaveModulePolicies();}catch{await extension.EnableAsync(false);throw;}
                    }
                });
            }
            finally{ReloadModuleTabs();}
        }
        private void ReloadModuleTabs()
        {
            foreach(var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Where(t=>t!=null&&!t.IsPrivate&&t.Initialized&&!t.NeedsNewView&&!t.IsClosing))
                try{tab.View.CoreWebView2.Reload();}catch{}
        }
        private void Modules_Click(object sender,RoutedEventArgs e){new SiteModulesWindow(GetModules,InstallModule,ChangeModule,SetModuleSites){Owner=this}.ShowDialog();}
    }
}
