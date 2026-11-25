using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task ModuleRestore052(string output)
    {
        var store=new BrowserDataStore(Path.Combine(output,"Restore052"),Path.Combine(output,"RestoreDownloads"));
        store.SaveSettings(new BrowserSettings{RestorePreviousSession=false,HarborHomeIntroduced=true,OnlineSuggestionsEnabled=false});
        string source=Path.Combine(output,"Reviewed052"),pages=Path.Combine(output,"RestorePages");Directory.CreateDirectory(source);Directory.CreateDirectory(pages);
        File.WriteAllText(Path.Combine(source,"manifest.json"),"{\"manifest_version\":3,\"name\":\"Restore integrity fixture\",\"version\":\"1.0.0\",\"content_scripts\":[{\"matches\":[\"https://a.restore.test/*\",\"https://b.restore.test/*\",\"https://harbor-tor-control.invalid/*\"],\"js\":[\"test.js\"]}]}");
        const string script="document.body.dataset.restore052='reviewed';";File.WriteAllText(Path.Combine(source,"test.js"),script);File.WriteAllText(Path.Combine(pages,"index.html"),"<body>Restore fixture</body>");
        string id=null,folder=null;
        for(int run=0;run<5;run++)
        {
            if(run==4){File.WriteAllText(Path.Combine(folder,"test.js"),script);File.WriteAllText(Path.Combine(store.RootFolder,"module-permissions.json"),"broken json");}
            var window=Offscreen(new MainWindow(store));window.Show();
            try
            {
                await Wait(()=>window.BrowserTabs.Items.Count>0&&((BrowserTab)((TabItem)window.BrowserTabs.Items[0]).Tag).Initialized);
                var tab=(BrowserTab)((TabItem)window.BrowserTabs.Items[0]).Tag;var core=tab.View.CoreWebView2;
                foreach(string host in new[]{"a.restore.test","b.restore.test",BrowserVpnService.ControlHost})core.SetVirtualHostNameToFolderMapping(host,pages,CoreWebView2HostResourceAccessKind.DenyCors);
                if(run==0)
                {
                    id=await window.InstallModule(SiteModuleService.Inspect(source));
                    await NavigateFixture(core,"https://"+BrowserVpnService.ControlHost+"/index.html");
                    Check("ordinary site modules cannot inject into internal Tor controls",await core.ExecuteScriptAsync("document.body.dataset.restore052===undefined")=="true");
                    await window.SetModuleSites(id,new[]{"https://b.restore.test/*"});
                    folder=Directory.GetDirectories(Path.Combine(store.RootFolder,"Modules")).Single();
                    await (await core.Profile.GetBrowserExtensionsAsync()).Single(e=>e.Id==id).RemoveAsync();
                    Check("fixture simulates a missing native extension with saved permission intact",(await window.GetModules()).Single().AllowedSites.SequenceEqual(new[]{"https://b.restore.test/*"}));
                }
                else if(run==1)
                {
                    var row=(await window.GetModules()).Single();Check("saved enabled module is reinstalled after native registration loss",row.Id==id&&row.Enabled);
                    await NavigateFixture(core,"https://b.restore.test/index.html");await WaitJs(core,"document.body.dataset.restore052==='reviewed'");Check("restored module runs on its saved allowed site",true);
                    await NavigateFixture(core,"https://a.restore.test/index.html");Check("restoration does not regain a revoked site",await core.ExecuteScriptAsync("document.body.dataset.restore052===undefined")=="true");
                    await window.ChangeModule(id,false);await (await core.Profile.GetBrowserExtensionsAsync()).Single(e=>e.Id==id).RemoveAsync();
                }
                else if(run==2)
                {
                    Check("disabled module remains unregistered after restart",!(await window.GetModules()).Single().Enabled&&!(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==id));
                    await window.ChangeModule(id,true);await (await core.Profile.GetBrowserExtensionsAsync()).Single(e=>e.Id==id).RemoveAsync();
                    File.AppendAllText(Path.Combine(folder,"test.js"),"document.body.dataset.changed='yes';");
                }
                else if(run==3)
                {
                    var row=(await window.GetModules()).Single();Check("changed module content is blocked before automatic restoration",!row.Enabled&&row.Status.StartsWith("Blocked:")&&!(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==id));
                    bool refused=false;try{await window.ChangeModule(id,true);}catch(InvalidDataException){refused=true;}
                    Check("Enable cannot transiently register a changed package",refused&&!(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==id));
                    await NavigateFixture(core,"https://b.restore.test/index.html");Check("changed script never executes on its formerly allowed site",await core.ExecuteScriptAsync("document.body.dataset.changed===undefined")=="true");
                }
                else
                {
                    Check("corrupt saved permissions cannot trigger restoration",!(await window.GetModules()).Single().Enabled&&!(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==id));
                    await window.ChangeModule(id,false,true);
                }
            }
            finally{await CloseTestWindow(window);}
        }
        var disposedStore=new BrowserDataStore(Path.Combine(output,"DisposedStartup"),Path.Combine(output,"DisposedDownloads"));
        disposedStore.SaveSettings(new BrowserSettings{RestorePreviousSession=false,HarborHomeIntroduced=true,OnlineSuggestionsEnabled=false});
        var closing=Offscreen(new MainWindow(disposedStore));closing.Show();
        try
        {
            await Wait(()=>closing.BrowserTabs.Items.Count>0&&((BrowserTab)((TabItem)closing.BrowserTabs.Items[0]).Tag).Initialized);
            var service=(BrowserVpnService)Field(closing,"_browserVpn");Task pending=closing.ChangeBrowserVpn(true);await Wait(()=>service.Enabled);service.Dispose();
            bool cancelled=false;try{await pending;}catch(OperationCanceledException){cancelled=true;}catch(ObjectDisposedException){cancelled=true;}
            Check("closing the connection service cancels in-flight setup",cancelled&&!service.Connected&&service.HelperProcessId==0);
            await closing.ChangeBrowserVpn(false);Check("interrupted setup can still restore the regular route",!service.Enabled);
        }
        finally{await CloseTestWindow(closing);}
    }
    private static async Task NavigateFixture(CoreWebView2 core,string target)
    {
        if(!await TorNavigate(core,target))throw new Exception("Fixture navigation failed.");await Task.Delay(150);
    }
    private static async Task CloseTestWindow(MainWindow window)
    {
        await (Task)typeof(MainWindow).GetMethod("FinishPrivacyBeforeClose",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
        typeof(MainWindow).GetField("_closingAfterPrivacyCleanup",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);window.Close();
    }
}
