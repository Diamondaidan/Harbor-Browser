using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HarborBrowser.Services;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task R4RouteBenchmark(CoreWebView2Environment environment,IntPtr owner,CoreWebView2 provider,string output,string variant)
    {
        if(variant!="legacy"&&variant!="current")throw new ArgumentException("Unknown owned benchmark variant.");
        using(var directHttp=new BetaGuardFixtureServer())
        using(var directTls=new BetaGuardFixtureServer(true))
        using(var current=new ProtonRouteSession(environment,owner))
        {
            directHttp.AddressReply=directTls.AddressReply="203.0.113.2";directTls.AddressAtRoot=true;
            current.DirectAddressProvider=cancel=>ProtonRouteProbe.DirectAddress(new Uri(directHttp.Url("route-address")),cancel);
            var directBrowsers=new HashSet<uint>();var cycles=new List<object>();
            File.WriteAllText(Path.Combine(output,"benchmark-ready.json"),Json.Serialize(new{Variant=variant,RootProcessId=Process.GetCurrentProcess().Id,ExternalRequests=false}));
            var watch=Stopwatch.StartNew();
            for(int i=0;i<3;i++)
            {
                double due=2+i*10;
                if(watch.Elapsed.TotalSeconds<due)await Task.Delay(TimeSpan.FromSeconds(due-watch.Elapsed.TotalSeconds));
                double started=watch.Elapsed.TotalMilliseconds;
                bool verified;
                if(variant=="current")verified=(await current.Compare(provider,CancellationToken.None)).Verified;
                else
                {
                    var options=environment.CreateCoreWebView2ControllerOptions();options.ProfileName=provider.Profile.ProfileName;
                    var protectedView=await ProtonRouteProbe.CreateController(environment.CreateCoreWebView2ControllerAsync(owner,options),CancellationToken.None);
                    CoreWebView2Controller directView=null;
                    try
                    {
                        protectedView.IsVisible=false;protectedView.CoreWebView2.Settings.IsScriptEnabled=false;
                        string resolver="--host-resolver-rules=\"MAP api.ipify.org 127.0.0.1:"+new Uri(directTls.BaseUrl).Port+"\" --ignore-certificate-errors-spki-list="+directTls.CertificateSpkiHash;
                        var directEnvironment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(output,"LegacyDirect"+i),new CoreWebView2EnvironmentOptions{AdditionalBrowserArguments=resolver});
                        directView=await ProtonRouteProbe.CreateController(directEnvironment.CreateCoreWebView2ControllerAsync(owner),CancellationToken.None);
                        directView.IsVisible=false;directView.CoreWebView2.Settings.IsScriptEnabled=false;
                        directBrowsers.Add(directView.CoreWebView2.BrowserProcessId);
                        var pair=await Task.WhenAll(ProtonRouteProbe.Address(protectedView.CoreWebView2),ProtonRouteProbe.Address(directView.CoreWebView2));
                        verified=!pair[0].Equals(pair[1]);
                    }
                    finally{protectedView.Close();directView?.Close();}
                }
                Check("owned "+variant+" comparison "+i,verified);
                cycles.Add(new{Index=i,StartedMilliseconds=started,DurationMilliseconds=watch.Elapsed.TotalMilliseconds-started,Verified=verified});
            }
            if(watch.Elapsed.TotalSeconds<33)await Task.Delay(TimeSpan.FromSeconds(33-watch.Elapsed.TotalSeconds));
            File.WriteAllText(Path.Combine(output,"route-resource-benchmark.json"),Json.Serialize(new{Variant=variant,Cycles=cycles,AdditionalDirectBrowserProcesses=directBrowsers.Count,CachedControllersCreated=current.ControllerCreations,OwnMainBrowserProcessId=provider.BrowserProcessId,ExternalRequests=false,AccountLogin=false,Scope="Owned route-check component comparison. Legacy reproduces the prior controller/environment lifecycle using the current SDK/parser and owned endpoints; it is not a full R3-versus-R4 live VPN benchmark."}));
        }
    }
}
