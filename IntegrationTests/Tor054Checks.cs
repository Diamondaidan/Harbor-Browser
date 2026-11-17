using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Interop;
using HarborBrowser;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task Tor054Checks(MainWindow main,BrowserDataStore store,string output)
    {
        var service=(BrowserVpnService)Field(main,"_browserVpn");var tab=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;var core=tab.View.CoreWebView2;
        string before=ProxySnapshot();bool disposed=false;
        try{
            Check("indicator starts with Tor off",Convert.ToString(main.TorIndicatorButton.Content)=="Tor Off"&&!service.Enabled);
            Check("off mode makes no health request",!await main.CheckBrowserVpnHealth()&&service.LastVerifiedUtc==null);
            await main.ChangeBrowserVpn(true);await Wait(()=>Convert.ToString(main.TorIndicatorButton.Content)=="Tor On");
            Check("connected status requires actual route verification",service.RouteVerified&&service.Connected&&service.State==BrowserConnectionState.Connected&&service.LastVerifiedUtc.HasValue);
            int helper=service.HelperProcessId;var firstCheck=service.LastVerifiedUtc;
            Check("manual health check confirms the actual browser route",await main.CheckBrowserVpnHealth());
            Check("health check retains the existing helper",service.HelperProcessId==helper&&service.LastVerifiedUtc>=firstCheck);
            var lastGoodCheck=service.LastVerifiedUtc;
            await service.Routing((CoreWebView2Environment)Field(main,"_environment"),core.Profile.ProfileName,new WindowInteropHelper(main).Handle,service.SocksPort,true,CancellationToken.None);
            Check("routing inspector verifies SOCKS, WebRTC policy and prediction settings",true);
            service.VerificationOverride=(browser,token)=>Task.FromResult(false);
            Check("a non-Tor verification response fails the check",!await main.CheckBrowserVpnHealth());
            await Wait(()=>Convert.ToString(main.TorIndicatorButton.Content)=="Tor !");
            Check("failed verification keeps Tor enabled and blocks managed requests",service.Enabled&&!service.RouteVerified&&!service.Connected&&main.TorBlocksRequest("https://example.com/")&&main.TorBlocksRequest("http://[::1]/"));
            Check("a failure does not replace the last successful check time",service.LastVerifiedUtc==lastGoodCheck);
            Check("local Home remains available during a failed check",!main.TorBlocksRequest(HomePageService.Url));
            var routing=(await core.Profile.GetBrowserExtensionsAsync()).Single(e=>e.Id==BrowserVpnService.ExtensionId);await routing.EnableAsync(false);
            service.VerificationOverride=null;Check("missing routing is rejected before a verification request",!await main.CheckBrowserVpnHealth());
            // Home intentionally disallows fetch via CSP. Use a blank document
            // so this assertion reaches the native request guard.
            await TorNavigate(core,"about:blank");
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            try{
                string localTarget="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/tor-guard";
                const string target="https://harbor-guard-proof.invalid/tor-guard";bool guarded=false;
                EventHandler<CoreWebView2WebResourceRequestedEventArgs> observe=(s,e)=>{if(e.Request.Uri==target&&e.Response!=null&&e.Response.StatusCode==503)guarded=true;};
                core.WebResourceRequested+=observe;
                try{await core.ExecuteScriptAsync("fetch('"+target+"',{mode:'no-cors'}).catch(()=>null)");await Wait(()=>guarded);}
                finally{core.WebResourceRequested-=observe;}
                Check("guard returns a blocked response when the routing extension is disabled",guarded);
                bool cancelled=false;EventHandler<CoreWebView2NavigationStartingEventArgs> cancellation=(s,e)=>{if(e.Uri==localTarget)cancelled=e.Cancel;};core.NavigationStarting+=cancellation;
                bool result;try{result=await TorNavigate(core,localTarget);}finally{core.NavigationStarting-=cancellation;}
                await Task.Delay(200);bool preconnected=listener.Pending();int requestBytes=0;
                while(listener.Pending())using(var client=await listener.AcceptTcpClientAsync()){
                    var read=client.GetStream().ReadAsync(new byte[8192],0,8192);
                    if(await Task.WhenAny(read,Task.Delay(500))==read)requestBytes+=await read;
                }
                Console.WriteLine("INFO cancelled local navigation: completed="+result+", speculativeTcp="+preconnected+", observedHttpBytes="+requestBytes);
                Check("cancelled local navigation sends no HTTP bytes during the observation",cancelled&&requestBytes==0);
            }finally{listener.Stop();}
            const string blocked="https://example.com/?harbor-resume054=1";
            Check("top-level navigation is cancelled while Tor is unverified",!await TorNavigate(core,blocked));
            bool safeResume=false;EventHandler<CoreWebView2NavigationStartingEventArgs> resumed=(s,e)=>{if(e.Uri==blocked)safeResume=service.RouteVerified;};core.NavigationStarting+=resumed;
            await main.ChangeBrowserVpn(true);await Wait(()=>safeResume);core.NavigationStarting-=resumed;
            Check("reconnect restores routing before resuming a cancelled navigation",service.RouteVerified&&safeResume);
            await TorNavigate(core,HomePageService.Url);
            helper=service.HelperProcessId;
            await main.RecheckTorAfterChange(false,"Simulated network loss.");
            Check("network loss pauses browsing without disabling Tor",service.Enabled&&!service.RouteVerified&&main.TorBlocksRequest("https://example.com/"));
            await main.RecheckTorAfterChange(true,"Simulated network return.");
            Check("network return verifies the route without restarting a healthy helper",service.RouteVerified&&service.HelperProcessId==helper);
            await main.RecheckTorAfterChange(false,"Simulated suspend.");await main.RecheckTorAfterChange(true,"Simulated resume.");
            Check("wake handling rechecks the browser route",service.RouteVerified&&service.State==BrowserConnectionState.Connected);
            using(var process=System.Diagnostics.Process.GetProcessById(service.HelperProcessId)){process.Kill();await Task.Run(()=>process.WaitForExit(5000));}
            await Wait(()=>!service.Connected);
            Check("real helper termination produces a paused failure state",service.State==BrowserConnectionState.Failed&&!service.RouteVerified);
            await main.ChangeBrowserVpn(true);
            Check("manual reconnect replaces the dead helper and verifies its route",service.RouteVerified&&service.HelperProcessId!=0);
            var began=new TaskCompletionSource<bool>();var late=new TaskCompletionSource<bool>();
            service.VerificationOverride=(browser,token)=>{began.TrySetResult(true);return late.Task;};
            var pending=main.CheckBrowserVpnHealth();await began.Task;
            var disconnect=main.ChangeBrowserVpn(false);
            Check("Disconnect cancels a pending health check promptly",await Task.WhenAny(disconnect,Task.Delay(5000))==disconnect);await disconnect;
            late.TrySetResult(true);await pending;await Task.Delay(100);
            Check("late verification cannot re-enable a disconnected mode",!service.Enabled&&!service.RouteVerified&&service.HelperProcessId==0&&service.State==BrowserConnectionState.Off);
            Check("disconnected routing is removed",!(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==BrowserVpnService.ExtensionId));
            Check("regular browser routing is restored",!Convert.ToBoolean((await TorResult(core))["IsTor"]));
            Check("Windows proxy settings are unchanged",ProxySnapshot()==before);
            service.VerificationOverride=null;
            await main.ChangeBrowserVpn(true);
            var disposalBegan=new TaskCompletionSource<bool>();var disposalLate=new TaskCompletionSource<bool>();
            service.VerificationOverride=(browser,token)=>{disposalBegan.TrySetResult(true);return disposalLate.Task;};
            var duringDispose=main.CheckBrowserVpnHealth();await disposalBegan.Task;service.Dispose();disposed=true;
            Check("disposal cancels an active health check",await Task.WhenAny(duringDispose,Task.Delay(5000))==duringDispose);
            disposalLate.TrySetResult(true);await duringDispose;await Task.Delay(100);
            Check("disposal stops its owned helper",service.HelperProcessId==0);
            Check("a disposed service cannot accept a late positive verification",!service.RouteVerified&&!service.Connected);
        }finally{service.VerificationOverride=null;if(service.Enabled&&!disposed)await main.ChangeBrowserVpn(false);}
    }
}
