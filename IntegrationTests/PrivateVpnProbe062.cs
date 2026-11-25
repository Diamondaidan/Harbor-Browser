using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Interop;
using HarborBrowser;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task PrivateVpnProbe062(MainWindow main,string output,BetaGuardFixtureServer proxy)
    {
        output=Path.GetFullPath(output);
        var tab=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var core=tab.View.CoreWebView2;var environment=core.Environment;var owner=new WindowInteropHelper(main).Handle;
        string folder=Path.Combine(output,"ProxyFixture");Directory.CreateDirectory(folder);
        var manifest=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","BrowserVpn","Proton","manifest.json")));
        File.WriteAllText(Path.Combine(folder,"manifest.json"),Json.Serialize(new Dictionary<string,object>{{"manifest_version",3},{"name","Owned private proxy probe"},{"version","0.1.0"},{"key",manifest["key"]},{"permissions",new[]{"proxy","privacy","webRequest","webRequestAuthProvider","storage"}},{"host_permissions",new[]{"<all_urls>"}},{"background",new Dictionary<string,object>{{"service_worker","worker.js"}}}}));
        File.WriteAllText(Path.Combine(folder,"popup.html"),"<!doctype html><title>Owned private proxy probe</title>");
        File.WriteAllText(Path.Combine(folder,"worker.js"),"chrome.webRequest.onAuthRequired.addListener((d,cb)=>chrome.storage.local.get('pw',v=>cb({authCredentials:{username:'owned',password:v.pw||'canary'}})),{urls:['<all_urls>']},['asyncBlocking']);");
        var extension=await core.Profile.AddBrowserExtensionAsync(folder);
        var results=new Dictionary<string,object>();
        var controllers=new List<CoreWebView2Controller>();
        try
        {
            await AuthNavigate(core,ProtonPreviewService.PopupUrl);
            await BetaProxySet(core,new{mode="fixed_servers",rules=new{singleProxy=new{scheme="https",host="harbor-owned-proxy.invalid",port=new Uri(proxy.BaseUrl).Port}}});
            var normalOptions=environment.CreateCoreWebView2ControllerOptions();normalOptions.ProfileName=core.Profile.ProfileName;
            var normal=await environment.CreateCoreWebView2ControllerAsync(owner,normalOptions);normal.IsVisible=false;controllers.Add(normal);
            var sameOptions=environment.CreateCoreWebView2ControllerOptions();sameOptions.ProfileName=core.Profile.ProfileName;sameOptions.IsInPrivateModeEnabled=true;
            var same=await environment.CreateCoreWebView2ControllerAsync(owner,sameOptions);same.IsVisible=false;controllers.Add(same);
            var separateOptions=environment.CreateCoreWebView2ControllerOptions();separateOptions.ProfileName="p"+Guid.NewGuid().ToString("N");separateOptions.IsInPrivateModeEnabled=true;
            var separate=await environment.CreateCoreWebView2ControllerAsync(owner,separateOptions);separate.IsVisible=false;controllers.Add(separate);
            foreach(var controller in controllers)controller.CoreWebView2.BasicAuthenticationRequested+=(s,e)=>{e.Response.UserName="owned";e.Response.Password=proxy.ProxyPassword;};
            string domain="ordinary.owned.invalid";
            core.CookieManager.AddOrUpdateCookie(core.CookieManager.CreateCookie("normal-canary","normal",domain,"/"));
            results["same_profile_private_is_native_inprivate"]=same.CoreWebView2.Profile.IsInPrivateModeEnabled;
            results["same_profile_private_has_no_normal_cookie"]=!(await same.CoreWebView2.CookieManager.GetCookiesAsync("https://"+domain+"/")).Any(c=>c.Name=="normal-canary");
            results["normal_without_proxy_auth"]=await PrivateProbeNavigate(normal.CoreWebView2);
            results["same_profile_private_without_proxy_auth"]=await PrivateProbeNavigate(same.CoreWebView2);
            results["separate_profile_private_without_proxy_auth"]=await PrivateProbeNavigate(separate.CoreWebView2);
            proxy.RequireProxyAuthentication=true;
            results["normal_with_proxy_auth"]=await PrivateProbeNavigate(normal.CoreWebView2);
            results["same_profile_private_with_proxy_auth"]=await PrivateProbeNavigate(same.CoreWebView2);
            results["separate_profile_private_with_proxy_auth"]=await PrivateProbeNavigate(separate.CoreWebView2);
            proxy.ProxyPassword="rotated";
            await BetaProxyOperation(core,"chrome.storage.local.set({pw:'rotated'},()=>finish(!chrome.runtime.lastError));");
            results["normal_after_auth_rotation"]=await PrivateProbeNavigate(normal.CoreWebView2);
            results["same_profile_private_after_auth_rotation"]=await PrivateProbeNavigate(same.CoreWebView2);
            try
            {
                var privateExtension=await same.CoreWebView2.Profile.AddBrowserExtensionAsync(folder);
                results["private_add_extension"]=privateExtension.Id;
                await AuthNavigate(same.CoreWebView2,ProtonPreviewService.PopupUrl);
                results["private_proxy_api"]=await same.CoreWebView2.ExecuteScriptAsync("typeof chrome.proxy?.settings?.set");
                await BetaProxySet(same.CoreWebView2,new{mode="fixed_servers",rules=new{singleProxy=new{scheme="https",host="harbor-owned-proxy.invalid",port=new Uri(proxy.BaseUrl).Port}}});
                results["private_after_explicit_proxy_install"]=await PrivateProbeNavigate(same.CoreWebView2);
            }
            catch(Exception ex){results["private_extension_error"]=ex.GetType().Name+": "+ex.Message;}
            results["proxy_connects"]=proxy.Count("CONNECT/ordinary.owned.invalid:443");
            results["auth_challenges"]=proxy.Count("proxy-auth-challenge");
            File.WriteAllText(Path.Combine(output,"private-proxy-probe.json"),Json.Serialize(results));
            Console.WriteLine(Json.Serialize(results));
        }
        finally{foreach(var controller in controllers)controller.Close();await extension.RemoveAsync();}
    }
    private static async Task<string> PrivateProbeNavigate(CoreWebView2 core)
    {
        var done=new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>();
        EventHandler<CoreWebView2NavigationCompletedEventArgs> completed=(s,e)=>done.TrySetResult(e);core.NavigationCompleted+=completed;
        try
        {
            core.Navigate("https://ordinary.owned.invalid/?probe="+Guid.NewGuid().ToString("N"));
            if(await Task.WhenAny(done.Task,Task.Delay(10000))!=done.Task)return "timeout";
            if(!done.Task.Result.IsSuccess)return done.Task.Result.WebErrorStatus.ToString();
            return (await core.ExecuteScriptAsync("document.body.innerText.includes('198.51.100.2')"))=="true"?"owned-proxy":"unexpected-body";
        }
        finally{core.NavigationCompleted-=completed;}
    }
}
