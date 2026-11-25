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
    }
    public partial class MainWindow
    {
        private readonly Dictionary<string,string> _moduleFolders=new Dictionary<string,string>();
        private Task _moduleValidation;
        private int _moduleValidationGeneration=-1;
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
            try
            {
                // WebView2 also returns its PDF/clipboard engine components here.
                // Only manage modules installed through Harbor's reviewed path.
                foreach(var extension in (await profile.GetBrowserExtensionsAsync()).Where(e=>_moduleFolders.ContainsKey(e.Id)))
                {
                    try{ModulePackage(extension.Id);}catch{await extension.EnableAsync(false);}
                }
            }
            catch(Exception ex){System.Diagnostics.Debug.WriteLine("Site modules unavailable: "+ex.Message);}
        }
        private SiteModulePackage ModulePackage(string id)
        {
            string folder;if(!_moduleFolders.TryGetValue(id,out folder))throw new InvalidOperationException("Unrecognized module. Remove it and add a reviewed copy.");
            return SiteModuleService.Inspect(Path.Combine(ModuleRoot,folder));
        }
        internal async Task<List<SiteModuleRow>> GetModules()
        {
            var rows=new List<SiteModuleRow>();
            await WithProfile(null,async profile=>
            {
                await InitializeModules(profile);
                foreach(var extension in (await profile.GetBrowserExtensionsAsync()).Where(e=>_moduleFolders.ContainsKey(e.Id)))
                {
                    var row=new SiteModuleRow{Id=extension.Id,Name=extension.Name,Enabled=extension.IsEnabled,Status=extension.IsEnabled?"Enabled":"Disabled"};
                    try{var package=ModulePackage(extension.Id);row.Version=package.Version;row.Sites=string.Join(", ",package.Sites);}
                    catch(Exception ex){row.Status="Blocked: "+ex.Message;row.Enabled=false;await extension.EnableAsync(false);}
                    rows.Add(row);
                }
            });return rows;
        }
        private void SaveModuleFolders()
        {
            if(_store.IsReadOnlyForNewerDataVersion||!_sessionWritesAllowed)throw new InvalidOperationException("Module changes are unavailable in a read-only profile.");
            BrowserDataStore.WriteTextAtomically(Path.Combine(_store.RootFolder,"modules.json"),new JavaScriptSerializer().Serialize(_moduleFolders));
        }
        internal async Task<string> InstallModule(SiteModulePackage reviewed)
        {
            if(_store.IsReadOnlyForNewerDataVersion||!_sessionWritesAllowed)throw new InvalidOperationException("This profile is read-only.");
            string folder=SiteModuleService.Stage(reviewed,ModuleRoot),id=null;
            try
            {
                await WithProfile(null,async profile=>
                {
                    await InitializeModules(profile);var extension=await profile.AddBrowserExtensionAsync(folder);id=extension.Id;
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
                if(extension==null)throw new InvalidOperationException("Module is no longer installed.");
                if(remove)await extension.RemoveAsync();else{if(enabled)ModulePackage(id);await extension.EnableAsync(enabled);}
            });
            ReloadModuleTabs();
            if(remove)
            {
                string folder;if(_moduleFolders.TryGetValue(id,out folder)){_moduleFolders.Remove(id);SaveModuleFolders();SiteModuleService.RemoveManaged(ModuleRoot,Path.Combine(ModuleRoot,folder));}
            }
        }
        private void ReloadModuleTabs()
        {
            foreach(var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Where(t=>t!=null&&!t.IsPrivate&&t.Initialized&&!t.NeedsNewView&&!t.IsClosing))
                try{tab.View.CoreWebView2.Reload();}catch{}
        }
        private void Modules_Click(object sender,RoutedEventArgs e){new SiteModulesWindow(GetModules,InstallModule,ChangeModule){Owner=this}.ShowDialog();}
    }
}
