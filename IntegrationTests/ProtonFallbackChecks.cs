using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Interop;
using HarborBrowser;
using HarborBrowser.Services;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task ProtonFallbackChecks(MainWindow main,string output)
    {
        await ProtonAuthenticationFallbackChecks(main,output);
        string workspace=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","..","..",".."));
        string root=Path.Combine(workspace,"B6F",Guid.NewGuid().ToString("N").Substring(0,8));
        Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,".owned-fallback-fixture"),"Synthetic native proxy precedence; no account.");
        string fixture=Path.Combine(root,"Fixture");Directory.CreateDirectory(fixture);
        var manifest=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","BrowserVpn","Proton","manifest.json")));
        File.WriteAllText(Path.Combine(fixture,"manifest.json"),Json.Serialize(new Dictionary<string,object>{{"manifest_version",3},{"name","Harbor owned fallback fixture"},{"version","0.1.0"},{"key",manifest["key"]},{"permissions",new[]{"proxy"}}}));
        File.WriteAllText(Path.Combine(fixture,"proof.html"),"<!doctype html><title>Owned proxy fallback fixture</title>");
        string guardFolder=Path.Combine(root,"Guard");Directory.CreateDirectory(guardFolder);
        File.WriteAllText(Path.Combine(guardFolder,"manifest.json"),Json.Serialize(new{manifest_version=3,name="Owned fallback priority fixture",version="0.1.0",permissions=new[]{"proxy"}}));
        File.WriteAllText(Path.Combine(guardFolder,"proof.html"),"<!doctype html><title>Owned fallback priority fixture</title>");
        using(var server=new BetaGuardFixtureServer())
        using(var secureServer=new BetaGuardFixtureServer(true))
        {
            var sink=new TcpListener(IPAddress.Loopback,0);sink.Start();
            int port=((IPEndPoint)sink.LocalEndpoint).Port;
            var options=new CoreWebView2EnvironmentOptions{AreBrowserExtensionsEnabled=true,AdditionalBrowserArguments="--proxy-server=http://127.0.0.1:"+port+" --proxy-bypass-list=<-loopback>"};
            var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"WebView2"),options);
            var controller=await environment.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(main).Handle);controller.IsVisible=false;
            var core=controller.CoreWebView2;CoreWebView2BrowserExtension extension=null,guard=null;
            core.ServerCertificateErrorDetected+=(s,e)=>{if(e.RequestUri.StartsWith(secureServer.BaseUrl,StringComparison.Ordinal))e.Action=CoreWebView2ServerCertificateErrorAction.AlwaysAllow;};
            try
            {
                guard=await core.Profile.AddBrowserExtensionAsync(guardFolder);
                await AuthNavigate(core,"chrome-extension://"+guard.Id+"/proof.html");
                var blocked=new{mode="fixed_servers",rules=new{singleProxy=new{scheme="http",host="127.0.0.1",port=port},bypassList=new[]{"<-loopback>"}}};
                Check("fallback layer configures before the provider",await OwnedProxyOperation(core,guard.Id,"chrome.proxy.settings.set({scope:'regular',value:"+Json.Serialize(blocked)+"},()=>finish(!chrome.runtime.lastError));"));
                extension=await core.Profile.AddBrowserExtensionAsync(fixture);
                await AuthNavigate(core,"chrome-extension://"+extension.Id+"/proof.html");
                await BetaProxySet(core,new{mode="direct"});
                Check("direct WebSocket positive control completes an echo",await FallbackSocket(core,server,"direct-control"));
                // WebSocket certificate failures do not raise the page event
                // on every runtime. Approve this owned certificate via HTTPS
                // first; no system trust store or general bypass is changed.
                await AuthNavigate(core,secureServer.Url("certificate-control"));
                Check("direct secure WebSocket positive control completes an echo",await FallbackSocket(core,secureServer,"direct-control"));
                await AuthNavigate(core,"chrome-extension://"+extension.Id+"/proof.html");
                Check("owned extension can override the native fallback",server.Count("direct-control/websocket")==1);
                Check("native fallback does not take proxy ownership from the extension",await BetaProxyOperation(core,"chrome.proxy.settings.get({},s=>finish(s.levelOfControl==='controlled_by_this_extension'));"));
                await AuthNavigate(core,"chrome-extension://"+guard.Id+"/proof.html");
                Check("lower priority fallback can prepare off without overriding provider",await OwnedProxyOperation(core,guard.Id,"chrome.proxy.settings.set({scope:'regular',value:{mode:'system'}},()=>finish(!chrome.runtime.lastError));"));
                await FallbackSocket(core,server,"provider-stays-active");
                Check("changing lower priority fallback leaves provider route active",server.Count("provider-stays-active/websocket")==1);
                await AuthNavigate(core,"chrome-extension://"+extension.Id+"/proof.html");
                await BetaProxyOperation(core,"chrome.proxy.settings.clear({scope:'regular'},()=>finish(!chrome.runtime.lastError));");
                await FallbackSocket(core,server,"explicit-off");
                Check("explicit system mode restores ordinary route over native baseline",server.Count("explicit-off/websocket")==1);
                await AuthNavigate(core,"chrome-extension://"+guard.Id+"/proof.html");
                Check("fallback layer can rearm without reinstalling",await OwnedProxyOperation(core,guard.Id,"chrome.proxy.settings.set({scope:'regular',value:"+Json.Serialize(blocked)+"},()=>finish(!chrome.runtime.lastError));"));
                await FallbackSocket(core,server,"cleared");
                Check("native fallback blocks WebSocket after extension proxy clear",server.Count("cleared/websocket")==0);
                await FallbackSocket(core,secureServer,"cleared");
                Check("native fallback blocks secure WebSocket after proxy clear",secureServer.Count("cleared/websocket")==0);
                await AuthNavigate(core,"chrome-extension://"+extension.Id+"/proof.html");
                await BetaProxySet(core,new{mode="fixed_servers",rules=new{singleProxy=new{scheme="http",host="127.0.0.1",port=port},bypassList=new[]{"<-loopback>"}}});
                await FallbackSocket(core,server,"proxy-unavailable");
                Check("unavailable fixed proxy does not fall back to a direct WebSocket",server.Count("proxy-unavailable/websocket")==0);
            }
            finally{try{if(extension!=null)await extension.RemoveAsync();if(guard!=null)await guard.RemoveAsync();}finally{controller.Close();sink.Stop();}}
        }
    }
    private static async Task ProtonAuthenticationFallbackChecks(MainWindow main,string output)
    {
        string root=Path.Combine(output,"OwnedAuthenticationFallback");Directory.CreateDirectory(root);
        using(var server=new BetaGuardFixtureServer(true))
        using(var fallback=new ProtonFallbackGuard(root))
        {
            int fixturePort=new Uri(server.BaseUrl).Port;
            // Native DNS remapping is confined to this owned environment.
            // These hostnames cannot contact Proton or a public lookalike.
            string resolver=" --host-resolver-rules=\"MAP account-api.proton.me 127.0.0.1:"+fixturePort+", MAP account-api.proton.me.evil.invalid 127.0.0.1:"+fixturePort+"\"";
            var options=new CoreWebView2EnvironmentOptions{AreBrowserExtensionsEnabled=true,AdditionalBrowserArguments=fallback.BrowserArguments+resolver+" --ignore-certificate-errors-spki-list="+server.CertificateSpkiHash};
            var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"WebView2"),options);
            var owner=new WindowInteropHelper(main).Handle;
            var controller=await environment.CreateCoreWebView2ControllerAsync(owner);controller.IsVisible=false;
            var core=controller.CoreWebView2;
            try
            {
                Check("cold native fallback permits the exact HTTPS verification origin",await FallbackNavigate(core,"https://account-api.proton.me/cold-verification")&&server.Count("cold-verification")==1);
                Check("cold native fallback rejects a verification host lookalike",!await FallbackNavigate(core,"https://account-api.proton.me.evil.invalid/cold-lookalike")&&server.Count("cold-lookalike")==0);
                await fallback.Prepare(environment,core.Profile,owner,true);
                Check("armed extension fallback permits the exact HTTPS verification origin",await FallbackNavigate(core,"https://account-api.proton.me/warm-verification")&&server.Count("warm-verification")==1);
                Check("armed extension fallback rejects a verification host lookalike",!await FallbackNavigate(core,"https://account-api.proton.me.evil.invalid/warm-lookalike")&&server.Count("warm-lookalike")==0);
                Check("native HSTS upgrade reaches verification only over HTTPS",await FallbackNavigate(core,"http://account-api.proton.me/insecure-verification")&&core.Source=="https://account-api.proton.me/insecure-verification"&&server.Count("insecure-verification")==1);
                Check("armed native fallback rejects custom verification ports",!await FallbackNavigate(core,"https://account-api.proton.me:8443/custom-port")&&server.Count("custom-port")==0);
                await fallback.SetArmed(environment,core.Profile,owner,false);
                Check("lookalike positive control is reachable only after explicit off",await FallbackNavigate(core,"https://account-api.proton.me.evil.invalid/off-lookalike")&&server.Count("off-lookalike")==1);
            }
            finally{controller.Close();}
        }
    }
    private static async Task<bool> FallbackNavigate(CoreWebView2 core,string target)
    {
        var done=new TaskCompletionSource<bool>();
        EventHandler<CoreWebView2NavigationCompletedEventArgs> complete=(s,e)=>{Console.WriteLine("OWNED AUTH FALLBACK "+target+" "+e.IsSuccess+" "+e.WebErrorStatus);done.TrySetResult(e.IsSuccess);};
        core.NavigationCompleted+=complete;
        try{core.Navigate(target);if(await Task.WhenAny(done.Task,Task.Delay(15000))!=done.Task)throw new TimeoutException("Owned native authentication navigation timed out.");return await done.Task;}
        finally{core.NavigationCompleted-=complete;}
    }
    private static async Task<bool> FallbackSocket(CoreWebView2 core,BetaGuardFixtureServer server,string prefix)
    {
        await core.ExecuteScriptAsync("(()=>{window.fallbackEcho=false;const s=new WebSocket("+Json.Serialize(server.BaseUrl.Replace("https:","wss:").Replace("http:","ws:")+prefix+"/websocket")+");s.onopen=()=>s.send('harbor-owned-echo');s.onmessage=e=>{window.fallbackEcho=e.data==='harbor-owned-echo';s.close();};s.onerror=()=>s.close();setTimeout(()=>s.close(),1000);})()");
        await Task.Delay(1250);
        return await core.ExecuteScriptAsync("window.fallbackEcho===true")=="true";
    }
}
