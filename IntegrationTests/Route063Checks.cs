using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private static async Task Route063Checks(MainWindow main,string output,BetaGuardFixtureServer proxy)
    {
        main.AutomaticProtonMonitoringEnabled=false;
        var tab=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var core=tab.View.CoreWebView2;var environment=core.Environment;
        string folder=Path.Combine(output,"OwnedRouteProvider");Directory.CreateDirectory(folder);
        var manifest=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","BrowserVpn","Proton","manifest.json")));
        File.WriteAllText(Path.Combine(folder,"manifest.json"),Json.Serialize(new Dictionary<string,object>{{"manifest_version",3},{"name","Owned route reliability fixture"},{"version","1.3.6"},{"key",manifest["key"]},{"permissions",new[]{"proxy","privacy"}}}));
        File.WriteAllText(Path.Combine(folder,"popup.html"),"<!doctype html><title>Owned route reliability fixture</title>");
        var extension=await core.Profile.AddBrowserExtensionAsync(folder);
        var events=new List<object>();var watch=Stopwatch.StartNew();int attempt=0,passed=0;
        try
        {
            await AuthNavigate(core,ProtonPreviewService.PopupUrl);
            await BetaProxySet(core,new{mode="fixed_servers",rules=new{singleProxy=new{scheme="https",host="harbor-owned-proxy.invalid",port=new Uri(proxy.BaseUrl).Port},bypassList=new[]{"localhost","127.0.0.1"}}});
            await BetaProxyOperation(core,"chrome.privacy.network.webRTCIPHandlingPolicy.set({scope:'regular',value:'disable_non_proxied_udp'},()=>finish(!chrome.runtime.lastError));");
            using(var direct=new BetaGuardFixtureServer())
            using(var session=new ProtonRouteSession(environment,new WindowInteropHelper(main).Handle))
            {
                direct.AddressReply="203.0.113.2";
                session.DirectAddressProvider=cancel=>ProtonRouteProbe.DirectAddress(new Uri(direct.Url("route-address")),cancel);
                session.ProbeProgress=stage=>events.Add(new{Attempt=attempt,Event=stage.ToString(),Time=watch.ElapsedMilliseconds});
                try
                {
                    for(attempt=0;attempt<30;attempt++)
                    {
                        var result=await session.Compare(core,CancellationToken.None);
                        if(!result.Verified)throw new Exception("Owned sequential comparison was not verified.");
                        passed++;
                        if(attempt==0)
                        {
                            var verifier=R4SessionController(session).CoreWebView2;
                            verifier.NavigationStarting+=(s,e)=>events.Add(new{Attempt=attempt,Event="Starting",Time=watch.ElapsedMilliseconds,Id=e.NavigationId});
                            verifier.NavigationCompleted+=(s,e)=>events.Add(new{Attempt=attempt,Event="Completed",Time=watch.ElapsedMilliseconds,Id=e.NavigationId,Success=e.IsSuccess,Error=e.WebErrorStatus.ToString()});
                            verifier.WebResourceResponseReceived+=(s,e)=>events.Add(new{Attempt=attempt,Event="Response",Time=watch.ElapsedMilliseconds,Status=e.Response.StatusCode});
                        }
                    }
                    Check("thirty immediate native comparisons succeed without console pacing",passed==30);
                    Check("immediate comparisons reuse one native controller",session.ControllerCreations==1);
                    Check("each proof uses a fresh independent direct comparison",direct.Count("route-address")==30);
                    var checkedView=R4SessionController(session).CoreWebView2;
                    Check("verifier keeps website scripts disabled",!checkedView.Settings.IsScriptEnabled);
                    Task<string> altered=null;
                    session.DirectAddressProvider=cancel=>Task.FromResult(System.Net.IPAddress.Parse(proxy.AddressReply));
                    session.ProbeProgress=stage=>{if(stage==ProtonRouteStage.ContentRequested)altered=checkedView.ExecuteScriptAsync("document.querySelector('body > pre').textContent='{"+"\"ip\":\"203.0.113.2\"}';true");};
                    var tampered=await session.Compare(core,CancellationToken.None);await altered;
                    Check("controlled page-text tampering actually changed the rendered reply",await checkedView.ExecuteScriptAsync("document.querySelector('body > pre').textContent.includes('203.0.113.2')")=="true");
                    Check("page-text tampering cannot forge a protected route",!tampered.Verified);
                    session.ProbeProgress=stage=>events.Add(new{Attempt=attempt,Event=stage.ToString(),Time=watch.ElapsedMilliseconds});
                    session.DirectAddressProvider=cancel=>ProtonRouteProbe.DirectAddress(new Uri(direct.Url("route-address")),cancel);
                    async Task Reject(string name,Action setup)
                    {
                        setup();bool rejected=false;var elapsed=Stopwatch.StartNew();
                        try{rejected=!(await session.Compare(core,CancellationToken.None)).Verified;}
                        catch(Exception error){rejected=!(error is TimeoutException)&&!(error is OperationCanceledException);}
                        finally{proxy.AddressBody=null;proxy.AddressContentType="application/json";proxy.AddressStatus=200;proxy.AddressRedirect=null;}
                        Check(name+" cannot establish protection",rejected);
                        Check(name+" fails promptly rather than timing out",elapsed.ElapsedMilliseconds<5000);
                        Check(name+" releases the verifier controller",R4SessionController(session)==null);
                    }
                    await Reject("malformed JSON",()=>proxy.AddressBody="not JSON");
                    await Reject("empty JSON response",()=>proxy.AddressBody="");
                    await Reject("HTML instead of JSON",()=>proxy.AddressContentType="text/html");
                    await Reject("unsuccessful HTTP status",()=>proxy.AddressStatus=503);
                    await Reject("oversized response",()=>proxy.AddressBody="{\"ip\":\"198.51.100.2\",\"padding\":\""+new string('x',40000)+"\"}");
                    await Reject("redirect outside verifier",()=>proxy.AddressRedirect="https://ordinary.owned.invalid/not-a-verifier");
                    Check("rejected redirect never reaches its destination",proxy.Count("not-a-verifier")==0);
                    proxy.AddressDelayMilliseconds=10000;direct.AddressStatus=503;
                    var failedHalf=Stopwatch.StartNew();bool halfRejected=false;
                    try{await session.Compare(core,CancellationToken.None);}catch{halfRejected=true;}
                    finally{proxy.AddressDelayMilliseconds=0;direct.AddressStatus=200;}
                    Check("a failed direct half rejects an unfinished browser half immediately",halfRejected&&failedHalf.ElapsedMilliseconds<5000);
                    proxy.AddressDelayMilliseconds=3000;
                    using(var cancel=new CancellationTokenSource())
                    {
                        int before=proxy.Count("/");var pending=session.Compare(core,cancel.Token);
                        await Wait(()=>proxy.Count("/")>before);var stopping=Stopwatch.StartNew();cancel.Cancel();bool cancelled=false;
                        try{await pending;}catch(OperationCanceledException){cancelled=true;}
                        Check("cancellation stops an unfinished native verification",cancelled&&stopping.ElapsedMilliseconds<2000);
                        Check("cancellation discards the verifier view",R4SessionController(session)==null);
                    }
                    proxy.AddressDelayMilliseconds=0;
                    Check("a fresh check recovers after malformed replies and cancellation",(await session.Compare(core,CancellationToken.None)).Verified);
                    string unsafeMessage="https://private.invalid/account?token=secret 198.51.100.10";
                    Check("connection error messages cannot echo arbitrary exception data",!ProtonRouteProbe.FailureMessage(new Exception(unsafeMessage)).Contains("secret")&&!ProtonRouteProbe.FailureMessage(new Exception(unsafeMessage)).Contains("198.51"));
                    Check("timeouts explain an explicit retry without claiming protection",ProtonRouteProbe.FailureMessage(new TimeoutException()).Contains("Browsing is paused")&&ProtonRouteProbe.FailureMessage(new TimeoutException()).Contains("Check browser route"));

                    main.BeginProtonProtection(true);
                    typeof(MainWindow).GetMethod("QueueTorEnvironmentChange",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{false,"Owned offline event"});
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    Check("offline adapter state cannot accept route proof",!main.MarkProtonRouteVerified(main.ProtonProtection.Epoch));
                    int requests=proxy.Count("/");bool offline=false;
                    try{await (Task<ProtonRouteCheckResult>)typeof(MainWindow).GetMethod("VerifyProtonRouteCore",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);}catch(InvalidOperationException){offline=true;}
                    Check("manual route checks do not contact the server while offline",offline&&proxy.Count("/")==requests);
                    Check("local diagnostics show adapter availability without an address",main.CurrentPageLoadingReport().Contains("Network adapter: Unavailable")&&!main.CurrentPageLoadingReport().Contains("198.51"));
                    typeof(MainWindow).GetMethod("QueueTorEnvironmentChange",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{true,"Owned return event"});
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    Check("network return schedules fresh verification",(DateTime)Field(main,"_protonNextCheckUtc")==DateTime.MinValue&&(bool)Field(main,"_protonRouteRefreshRequested"));
                    Check("only fresh post-return proof restores protection",main.MarkProtonRouteVerified(main.ProtonProtection.Epoch));
                    main.BeginProtonProtection(false);
                }
                finally
                {
                    File.WriteAllText(Path.Combine(output,"route-stress.json"),Json.Serialize(new{Passed=passed,Attempt=attempt,ControllerCreations=session.ControllerCreations,DirectRequests=direct.Count("route-address"),ProxyRequests=proxy.Count("/"),Events=events,AddressesRecorded=false,AccountAccess=false}));
                }
            }
        }
        finally { await extension.RemoveAsync(); }
    }
}
