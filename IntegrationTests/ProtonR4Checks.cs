using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
    private static async Task ProtonR4Checks(MainWindow main,string output,BetaGuardFixtureServer proxy,string benchmark=null)
    {
        main.AutomaticProtonMonitoringEnabled=false;
        var tab=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var core=tab.View.CoreWebView2;var environment=core.Environment;
        var fallback=(ProtonFallbackGuard)Field(main,"_protonFallback");
        var owner=new WindowInteropHelper(main).Handle;
        await fallback.Prepare(environment,core.Profile,owner,true);
        string fixture=Path.Combine(output,"OwnedR4Provider");Directory.CreateDirectory(fixture);
        var manifest=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","BrowserVpn","Proton","manifest.json")));
        File.WriteAllText(Path.Combine(fixture,"manifest.json"),Json.Serialize(new Dictionary<string,object>{{"manifest_version",3},{"name","Owned R4 proxy error fixture"},{"version","1.3.6"},{"key",manifest["key"]},{"permissions",new[]{"proxy","privacy"}}}));
        File.WriteAllText(Path.Combine(fixture,"popup.html"),"<!doctype html><title>Owned R4 proxy control</title>");
        File.WriteAllText(Path.Combine(fixture,"ordinary.html"),"<!doctype html><title>Owned live parent</title><script>window.ownedParentAlive=true;</script>");
        CoreWebView2BrowserExtension extension=null;
        try
        {
            extension=await core.Profile.AddBrowserExtensionAsync(fixture);
            var prepare=typeof(MainWindow).GetMethod("EnsureProtonObserverReady",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var control=await (Task<CoreWebView2>)prepare.Invoke(main,null);
            await BetaProxySet(control,new{mode="fixed_servers",rules=new{singleProxy=new{scheme="https",host="harbor-owned-proxy.invalid",port=new Uri(proxy.BaseUrl).Port},bypassList=new[]{"localhost","127.0.0.1"}}});
            Check("R4 owned WebRTC policy configures",await BetaProxyOperation(control,"chrome.privacy.network.webRTCIPHandlingPolicy.set({scope:'regular',value:'disable_non_proxied_udp'},()=>finish(!chrome.runtime.lastError));"));
            await Task.Delay(200);
            Check("R4 proxy fixture has genuine native ownership and supported shape",await ProtonRouteProbe.HasOwnedProxy(control));
            if(benchmark!=null){await R4RouteBenchmark(environment,owner,control,output,benchmark);return;}
            await control.ExecuteScriptAsync("window.ownedProxyErrors=[];chrome.proxy.onProxyError.addListener(d=>window.ownedProxyErrors.push({fatal:d.fatal===true,error:/^net::ERR_[A-Z_]+$/.test(d.error)?d.error:'other'}));");
            core.SetVirtualHostNameToFolderMapping("ordinary.owned.invalid",fixture,CoreWebView2HostResourceAccessKind.Allow);
            long epoch=main.BeginProtonProtection(true);main.MarkProtonRouteVerified(epoch);
            await AuthNavigate(core,"https://ordinary.owned.invalid/ordinary.html");
            var until=main.ProtonProtection.VerifiedUntilUtc;
            await core.ExecuteScriptAsync("fetch('https://unavailable-ipv6.owned.invalid/fail',{cache:'no-store',credentials:'omit'}).catch(()=>window.ownedRequestFailed=true)");
            await WaitJs(control,"window.ownedProxyErrors.length>0");
            await Task.Delay(250);
            string errors=await control.ExecuteScriptAsync("window.ownedProxyErrors");
            File.WriteAllText(Path.Combine(output,"native-request-error.json"),Json.Serialize(new{NativeErrors=errors,TabPaused=tab.ProtonPaused,State=main.ProtonProtection.State.ToString(),OwnedConnectAttempts=proxy.Count("CONNECT/unavailable-ipv6.owned.invalid:443"),AccountLogin=false,ExternalRequests=false}));
            Console.WriteLine("OWNED PROXY ERROR "+errors);
            Check("unavailable target reaches only the owned HTTPS proxy",proxy.Count("CONNECT/unavailable-ipv6.owned.invalid:443")>0);
            Check("native failed target reports a fatal transaction error",await control.ExecuteScriptAsync("window.ownedProxyErrors.every(e=>e.fatal)")=="true");
            Check("failed target leaves its protected parent usable",!tab.ProtonPaused&&await core.ExecuteScriptAsync("window.ownedParentAlive===true")=="true");
            Check("failed transaction does not renew or invent route evidence",main.ProtonProtection.VerifiedUntilUtc==until);
            Check("failed transaction requests a fresh route check",(bool)Field(main,"_protonRouteRefreshRequested"));
            var nextError=(DateTime)Field(main,"_protonNextErrorCheckUtc");
            for(int i=0;i<20;i++)main.HandleProtonProxyEvent(ProtonProxyEvent.FailedTransaction);
            Check("request-error burst cannot queue repeated immediate route checks",(DateTime)Field(main,"_protonNextErrorCheckUtc")==nextError);
            await WaitJs(core,"window.ownedRequestFailed===true");
            await core.ExecuteScriptAsync("fetch('https://api.ipify.org/?format=json',{cache:'no-store'}).then(r=>r.json()).then(v=>window.ownedFollowingRequest=v.ip==='198.51.100.2')");
            await WaitJs(core,"window.ownedFollowingRequest===true");
            Check("a following request still uses the owned native HTTPS proxy",proxy.Count("CONNECT/api.ipify.org:443")>0);

            await R4RouteSessionChecks(environment,owner,control,proxy,output);
            await R4StartupStatusCheck(main,tab,core);

            typeof(MainWindow).GetField("_protonLastCheck",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(main,"Protected browser route verified. Reload paused pages to continue.");
            await BetaProxyOperation(control,"chrome.proxy.settings.clear({scope:'regular'},()=>finish(!chrome.runtime.lastError));");
            await Wait(()=>tab.ProtonPaused);
            Check("actual native proxy clear still pauses the parent immediately",main.ProtonProtection.State!=ProtonProtectionPhase.Protected&&tab.ProtonPaused);
            Check("disconnected tooltip does not repeat an old Protected result",!Convert.ToString(main.TorIndicatorButton.ToolTip).Contains("Protected browser route verified"));
            Check("cleared proxy ownership cannot send an ordinary request",!await main.CanSendProtonRequestAsync("https://ordinary.owned.invalid/",false,CancellationToken.None));

            await BetaProxySet(control,new{mode="fixed_servers",rules=new{singleProxy=new{scheme="https",host="harbor-owned-proxy.invalid",port=new Uri(proxy.BaseUrl).Port},bypassList=new[]{"localhost","127.0.0.1"}}});
            main.MarkProtonRouteVerified(main.ProtonProtection.Epoch);
            await AuthNavigate(core,"https://ordinary.owned.invalid/ordinary.html");
            await BetaProxyOperation(control,"chrome.privacy.network.webRTCIPHandlingPolicy.set({scope:'regular',value:'default'},()=>finish(!chrome.runtime.lastError));");
            await Wait(()=>tab.ProtonPaused);
            Check("actual native privacy-policy change still pauses the parent",main.ProtonProtection.State!=ProtonProtectionPhase.Protected);
            await Wait(()=>core.Source=="about:blank");
            await BetaProxyOperation(control,"chrome.privacy.network.webRTCIPHandlingPolicy.set({scope:'regular',value:'disable_non_proxied_udp'},()=>finish(!chrome.runtime.lastError));");
            await Task.Delay(150);
            main.MarkProtonRouteVerified(main.ProtonProtection.Epoch);
            await AuthNavigate(core,"https://ordinary.owned.invalid/ordinary.html");
            main.HandleProtonProxyEvent(ProtonProxyEvent.UnsafeError);
            Check("unsafe or nonfatal proxy errors close the gate immediately",main.ProtonProtection.State!=ProtonProtectionPhase.Protected&&tab.ProtonPaused);
            ProtonProxyEvent parsed;
            Check("proxy event reader rejects an unrelated nonce",!ProtonProxyEvents.TryRead("{\"harborProxyChanged\":\"other\",\"kind\":\"proxy-error\",\"fatal\":true}","owned",out parsed));
            Check("proxy event reader treats a missing abort flag as unsafe",ProtonProxyEvents.TryRead("{\"harborProxyChanged\":\"owned\",\"kind\":\"proxy-error\"}","owned",out parsed)&&parsed==ProtonProxyEvent.UnsafeError);
            Check("proxy event reader accepts only the aborted-transaction category",ProtonProxyEvents.TryRead("{\"harborProxyChanged\":\"owned\",\"kind\":\"proxy-error\",\"fatal\":true}","owned",out parsed)&&parsed==ProtonProxyEvent.FailedTransaction);
            Check("proxy event reader rejects malformed and oversized messages",!ProtonProxyEvents.TryRead("{","owned",out parsed)&&!ProtonProxyEvents.TryRead(new string('x',1025),"owned",out parsed));
        }
        finally
        {
            main.BeginProtonProtection(false);
            if(extension!=null)await extension.RemoveAsync();
            await fallback.SetArmed(environment,core.Profile,owner,false);
            main.CloseProtonProtectionObserver();
        }
    }
    private static async Task R4RouteSessionChecks(CoreWebView2Environment environment,IntPtr owner,CoreWebView2 control,BetaGuardFixtureServer proxy,string output)
    {
        using(var direct=new BetaGuardFixtureServer())
        using(var session=new ProtonRouteSession(environment,owner))
        {
            direct.AddressReply="203.0.113.2";
            session.DirectAddressProvider=cancel=>ProtonRouteProbe.DirectAddress(new Uri(direct.Url("route-address")),cancel);
            object firstController=null;
            for(int i=0;i<3;i++)
            {
                var result=await session.Compare(control,CancellationToken.None);
                Check("owned native route comparison succeeds "+i,result.Verified);
                var controller=R4SessionController(session);
                if(i==0)
                {
                    controller.CoreWebView2.NavigationStarting+=(s,e)=>Console.WriteLine("INFO owned verifier starts "+e.NavigationId);
                    controller.CoreWebView2.NavigationCompleted+=(s,e)=>Console.WriteLine("INFO owned verifier completes "+e.NavigationId+" success="+e.IsSuccess+" error="+e.WebErrorStatus);
                    controller.CoreWebView2.WebResourceResponseReceived+=(s,e)=>Console.WriteLine("INFO owned verifier response "+e.Response.StatusCode);
                }
                if(firstController==null)firstController=controller;
                Check("route comparison stays in the same browser and profile "+i,controller.CoreWebView2.BrowserProcessId==control.BrowserProcessId&&controller.CoreWebView2.Profile.ProfilePath==control.Profile.ProfilePath);
            }
            Check("three checks reuse one actual native verifier controller",session.ControllerCreations==1&&ReferenceEquals(firstController,R4SessionController(session)));
            Check("three direct comparisons use the bounded HTTP control",direct.Count("route-address")==3);
            session.DirectAddressProvider=cancel=>Task.FromResult(IPAddress.Parse(proxy.AddressReply));
            Check("matching direct and browser replies cannot verify protection",!(await session.Compare(control,CancellationToken.None)).Verified);

            session.DirectAddressProvider=cancel=>ProtonRouteProbe.DirectAddress(new Uri(direct.Url("route-hang")),cancel);
            using(var cancel=new CancellationTokenSource())
            {
                var pending=session.Compare(control,cancel.Token);
                await Wait(()=>direct.Count("route-hang")>0);cancel.Cancel();
                bool rejected=false;try{await pending;}catch{rejected=true;}
                Check("cancelling a live HTTP control cannot return a successful proof",rejected);
                Check("cancelled comparison releases its native verifier",R4SessionController(session)==null);
            }
            session.DirectAddressProvider=cancel=>ProtonRouteProbe.DirectAddress(new Uri(direct.Url("route-address")),cancel);
            Check("a comparison recovers after cancellation",(await session.Compare(control,CancellationToken.None)).Verified);
            Check("recovery creates only one replacement native view",session.ControllerCreations==2);
            File.WriteAllText(Path.Combine(output,"route-session-resources.json"),Json.Serialize(new{RepeatedChecks=3,InitialNativeControllers=1,ControllerReused=true,AfterCancelledRecovery=session.ControllerCreations,DirectControl="HTTP without a separate browser environment",SameBrowserProcess=true,ExternalRequests=false,PublicAddressesRecorded=false}));
        }
    }
    private static async Task R4StartupStatusCheck(MainWindow main,BrowserTab parent,CoreWebView2 core)
    {
        var initialize=main.BrowserViewInitializer;var timeout=main.BrowserStartupTimeout;
        var blocked=new TaskCompletionSource<bool>();
        main.BrowserStartupTimeout=TimeSpan.FromMilliseconds(250);
        main.BrowserViewInitializer=(view,environment)=>blocked.Task;
        main.StatusText.Text="Owned visible page ready";int tabs=main.BrowserTabs.Items.Count;
        try
        {
            bool failed=false;try{await main.WarmProtonPopupAsync(core.Profile.ProfileName);}catch{failed=true;}
            Check("owned hidden sign-in preparation timeout is bounded",failed);
            Check("hidden startup failure cannot replace the active tab status",main.StatusText.Text=="Owned visible page ready");
            Check("hidden preparation failure leaves its visible parent alive",main.BrowserTabs.Items.Count==tabs&&await core.ExecuteScriptAsync("window.ownedParentAlive===true")=="true");
        }
        finally{main.BrowserViewInitializer=initialize;main.BrowserStartupTimeout=timeout;blocked.TrySetResult(true);}
    }
    private static CoreWebView2Controller R4SessionController(ProtonRouteSession session)
    {return (CoreWebView2Controller)typeof(ProtonRouteSession).GetField("_controller",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(session);}
}
