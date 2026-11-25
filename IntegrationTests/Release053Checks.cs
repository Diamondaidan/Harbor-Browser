using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Services;
using HarborBrowser.Views;

internal static partial class Program
{
    private static async Task Release053Checks(MainWindow main,BrowserDataStore store,string output)
    {
        var first=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        await WaitJs(first.View.CoreWebView2,"document.readyState==='complete'&&document.getElementById('app-version').textContent==='Harbor "+AppVersion.Number+"'");
        Check("Home footer uses the running application version",true);
        Check("module compatibility uses the same application version",SiteModuleService.HarborVersion==AppVersion.Number);
        Check("window branding uses the same application version",Convert.ToString(main.HeaderBrandWordmark.ToolTip)==AppVersion.Name);
        var state=Json.DeserializeObject(HomePageService.CreateState(null,null,false)) as System.Collections.Generic.Dictionary<string,object>;
        Check("Home state delivers the authoritative version",Convert.ToString(state["version"])==AppVersion.Number);

        var blocked=new TaskCompletionSource<bool>();
        main.BrowserStartupTimeout=TimeSpan.FromMilliseconds(350);
        main.BrowserViewInitializer=(view,environment)=>blocked.Task;
        var failed=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=failed.Item;
        await Wait(()=>failed.FailureMessage!=null&&!failed.Initializing);
        Check("stalled private startup reaches a recovery state",failed.NeedsNewView&&!failed.Initialized&&failed.FailureMessage.Contains("too long"));
        Check("recovery screen offers Reload tab",Visuals<Button>(main).Any(b=>Convert.ToString(b.Content)=="Reload tab"));
        Check("failed controller never becomes a working private tab",failed.View.CoreWebView2==null);
        main.BrowserViewInitializer=null;main.BrowserStartupTimeout=TimeSpan.FromSeconds(45);
        await main.RecoverTab(failed);await Wait(()=>failed.Initialized);
        await WaitJs(failed.View.CoreWebView2,"document.readyState==='complete'&&location.href==='https://harbor.invalid/index.html'");
        Check("Reload creates a usable private controller",failed.FailureMessage==null&&!failed.NeedsNewView&&failed.View.CoreWebView2.Profile.IsInPrivateModeEnabled);
        Check("compact private identity retains a random GUID",failed.PrivateSessionId.Length==33&&Guid.TryParseExact(failed.PrivateSessionId.Substring(1),"N",out var sessionId));
        var restored=failed.View;blocked.SetResult(true);await Task.Delay(120);
        Check("late completion cannot replace the recovered controller",ReferenceEquals(restored,failed.View)&&failed.Initialized&&failed.FailureMessage==null);
        await failed.View.CoreWebView2.ExecuteScriptAsync("document.cookie='startupPrivate=yes';localStorage.setItem('startupPrivate','yes')");
        CloseTab(main,failed);await main.WaitForPrivateCleanup();

        var closingGate=new TaskCompletionSource<bool>();
        main.BrowserViewInitializer=(view,environment)=>closingGate.Task;
        var closing=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=closing.Item;
        await Wait(()=>closing.Initializing);CloseTab(main,closing);
        var cleanup=main.WaitForPrivateCleanup();
        Check("closing a never-started private tab does not recreate its controller",await Task.WhenAny(cleanup,Task.Delay(1500))==cleanup);
        await cleanup;closingGate.SetResult(true);await Task.Delay(100);
        Check("closed startup cannot initialize or reopen its tab",closing.IsClosing&&!closing.Initialized&&!main.BrowserTabs.Items.Contains(closing.Item));
        main.BrowserViewInitializer=null;
        var fresh=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=fresh.Item;await Wait(()=>fresh.Initialized);
        await WaitJs(fresh.View.CoreWebView2,"document.readyState==='complete'&&location.href==='https://harbor.invalid/index.html'");
        Check("new private session gets a different identity",fresh.PrivateSessionId!=failed.PrivateSessionId&&fresh.PrivateSessionId!=closing.PrivateSessionId);
        Check("new private session has no recovered-session storage",await fresh.View.CoreWebView2.ExecuteScriptAsync("!document.cookie.includes('startupPrivate')&&localStorage.getItem('startupPrivate')===null")=="true");
        Check("normal tab stays usable after startup failure and retry",await first.View.CoreWebView2.ExecuteScriptAsync("1+1")=="2");
        CloseTab(main,fresh);await main.WaitForPrivateCleanup();main.BrowserTabs.SelectedItem=first.Item;
    }
}
