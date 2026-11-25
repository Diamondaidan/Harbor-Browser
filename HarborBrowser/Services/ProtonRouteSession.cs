using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Services
{
    // One verifier renderer in the existing browser environment, reused across
    // checks. The direct control is a cookie/credential-free HTTP request.
    internal sealed class ProtonRouteSession : IDisposable
    {
        private readonly CoreWebView2Environment _environment;
        private readonly IntPtr _owner;
        private readonly SemaphoreSlim _gate=new SemaphoreSlim(1);
        private readonly CancellationTokenSource _lifetime=new CancellationTokenSource();
        private CoreWebView2Controller _controller;
        private string _profileName;
        private bool _disposed,_controllerFaulted;
        internal int ControllerCreations {get;private set;}
        internal Func<CancellationToken,Task<IPAddress>> DirectAddressProvider;
        internal Action<ProtonRouteStage> ProbeProgress;

        internal ProtonRouteSession(CoreWebView2Environment environment,IntPtr owner)
        {
            _environment=environment;_owner=owner;
            DirectAddressProvider=cancel=>ProtonRouteProbe.DirectAddress(new Uri(ProtonRouteProbe.AddressTarget()),cancel);
        }
        internal async Task<ProtonRouteCheckResult> Compare(CoreWebView2 provider,CancellationToken cancellation)
        {
            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,_lifetime.Token))
            {
                var cancel=linked.Token;
                await _gate.WaitAsync(cancel);
                try
                {
                    if(_disposed)throw new ObjectDisposedException(nameof(ProtonRouteSession));
                    var first=await ProtonRouteProbe.ReadProxyProof(provider,cancel);
                    if(!first.Owned)return ProtonRouteProbe.Result(false,"Disconnected. Connect in Proton's controls; ordinary browsing remains paused.");
                    if(!first.Supported)return ProtonRouteProbe.Result(false,"These settings are not supported by protected browsing. Turn off Proton split tunneling, enable WebRTC protection, and recheck.");
                    if(!string.Equals(provider.Environment.UserDataFolder,_environment.UserDataFolder,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The verification environment changed.");
                    string profile=provider.Profile.ProfileName;
                    if(_controller!=null&&(_controllerFaulted||_profileName!=profile))ResetController();
                    if(_controller==null)
                    {
                        var options=_environment.CreateCoreWebView2ControllerOptions();options.ProfileName=profile;
                        _controller=await ProtonRouteProbe.CreateController(_environment.CreateCoreWebView2ControllerAsync(_owner,options),cancel);
                        ControllerCreations++;_profileName=profile;_controllerFaulted=false;
                        _controller.IsVisible=false;
                        var core=_controller.CoreWebView2;
                        if(core.BrowserProcessId!=provider.BrowserProcessId||!string.Equals(core.Profile.ProfilePath,provider.Profile.ProfilePath,StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("The verification profile changed.");
                        core.Settings.AreDevToolsEnabled=false;core.Settings.IsStatusBarEnabled=false;
                        core.Settings.IsScriptEnabled=false;core.Settings.IsWebMessageEnabled=false;
                        core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All,
                            CoreWebView2WebResourceRequestSourceKinds.Document|CoreWebView2WebResourceRequestSourceKinds.SharedWorker|CoreWebView2WebResourceRequestSourceKinds.ServiceWorker);
                        core.WebResourceRequested+=(s,e)=>{
                            if(!ProtonRouteProbe.IsAddressTarget(e.Request.Uri))
                                e.Response=core.Environment.CreateWebResourceResponse(null,503,"Verifier destination denied","Content-Type: text/plain");
                        };
                        core.NavigationStarting+=(s,e)=>{
                            Uri uri;
                            if(e.Uri!="about:blank"&&(!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length!=0||uri.Host!="api.ipify.org"||uri.AbsolutePath!="/"))e.Cancel=true;
                        };
                        core.NewWindowRequested+=(s,e)=>e.Handled=true;
                        core.PermissionRequested+=(s,e)=>{e.Handled=true;e.SavesInProfile=false;e.State=CoreWebView2PermissionState.Deny;};
                        core.DownloadStarting+=(s,e)=>e.Cancel=true;
                        core.ProcessFailed+=(s,e)=>_controllerFaulted=true;
                    }
                    var browserReply=ProtonRouteProbe.Address(_controller.CoreWebView2,cancel,ProbeProgress);
                    var directReply=DirectAddressProvider(cancel);
                    var pair=CompareReplies(browserReply,directReply);
                    var addresses=await ProtonRouteProbe.Bounded(pair,25000,cancel);
                    var last=await ProtonRouteProbe.ReadProxyProof(provider,cancel);
                    cancel.ThrowIfCancellationRequested();
                    if(!last.Owned||!last.Supported)return ProtonRouteProbe.Result(false,"Proton's proxy changed during verification. Browsing remains paused.");
                    return addresses[0].Equals(addresses[1])
                        ?ProtonRouteProbe.Result(false,"The browser matched the direct comparison. A protected route was not demonstrated; browsing remains paused.")
                        :ProtonRouteProbe.Result(true,"Protected browser route verified. Reload paused pages to continue. Browser protection is limited to supported requests; it is not a system-wide kill switch or complete leak audit.");
                }
                catch{linked.Cancel();ResetController();throw;}
                finally{_gate.Release();}
            }
        }
        private static async Task<IPAddress[]> CompareReplies(Task<IPAddress> browser,Task<IPAddress> direct)
        {
            // Reject a failed half immediately instead of waiting for the other
            // transport's timeout. Observe both when cancellation closes it.
            _=browser.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
            _=direct.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
            var first=await Task.WhenAny(browser,direct);await first;
            return new[]{await browser,await direct};
        }
        private void ResetController()
        {
            var controller=_controller;_controller=null;_profileName=null;
            try{controller?.Close();}catch{}
        }
        public void Dispose()
        {if(_disposed)return;_disposed=true;_lifetime.Cancel();ResetController();}
    }
}
