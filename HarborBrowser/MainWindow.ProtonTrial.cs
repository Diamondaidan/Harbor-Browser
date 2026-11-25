using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private ProtonPreviewService _protonPreview;
        private ProtonFallbackGuard _protonFallback;
        private ProtonPreviewWindow _protonPreviewWindow;
        private int _protonChanges;
        private long _protonOperation;
        private CancellationTokenSource _protonProbeCancellation;
        private readonly CancellationTokenSource _protonLifetime = new CancellationTokenSource();
        private CancellationTokenSource _protonObserverLifetime;
        private CoreWebView2Controller _protonObserver;
        private CoreWebView2Environment _protonObserverEnvironment;
        private Task<CoreWebView2> _protonObserverTask, _protonObserverReadyTask;
        private Task<bool> _protonProxyRead;
        private Task<ProtonRouteCheckResult> _protonRouteTask;
        private ProtonRouteSession _protonRouteSession;
        private DispatcherTimer _protonMonitor;
        private bool _protonMonitorBusy;
        private int _protonObserverSerial;
        private DateTime _protonNextCheckUtc = DateTime.MinValue;
        private DateTime _protonNextErrorCheckUtc = DateTime.MinValue;
        private bool _protonRouteRefreshRequested;
        private string _protonLastCheck = "No verified browser route yet.";
        internal bool AutomaticProtonMonitoringEnabled = true;

        private void InitializeProtonPreview()
        {
            _protonPreview = new ProtonPreviewService(_store.RootFolder);
            _protonFallback = new ProtonFallbackGuard(_store.RootFolder);
            ProtonProxyOwnershipValidator = ValidateProtonProxyOwnership;
            _protonPreview.StatusChanged += (s,e) => {
                if (Dispatcher.HasShutdownStarted || _torWindowClosed) return;
                try { Dispatcher.BeginInvoke(new Action(UpdateProtonIndicator)); } catch (InvalidOperationException) { }
            };
            _protonMonitor = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _protonMonitor.Tick += async (s,e) => await MonitorProtonProtection();
            Loaded += (s,e) => { _protonMonitor.Start(); UpdateProtonIndicator(); };
            Closed += (s,e) => { _protonLifetime.Cancel(); _protonMonitor.Stop(); CloseProtonProtectionObserver(); };
        }

        private void UpdateProtonIndicator()
        {
            if (TorIndicatorButton == null || _torWindowClosed) return;
            var phase = ProtonProtection == null ? (_protonPreview.Enabled ? ProtonProtectionPhase.Connecting : ProtonProtectionPhase.Off) : ProtonProtection.State;
            string detail = ProtonProtection == null ? _protonLastCheck : ProtonProtection.Reason;
            TorIndicatorButton.Content = phase == ProtonProtectionPhase.Off ? "VPN Off" : phase.ToString();
            TorIndicatorButton.ToolTip = detail + "\nSupported browser requests only; other applications keep their connection. This is not a system-wide kill switch.";
            AutomationProperties.SetName(TorIndicatorButton,"Proton browser protection: " + phase);
            TorIndicatorButton.SetResourceReference(Control.ForegroundProperty,phase == ProtonProtectionPhase.Protected ? "HarborAccent" : "HarborMuted");
            _protonPreviewWindow?.SetProtectionStatus(phase.ToString(),detail);
        }

        internal async Task ChangeProtonPreview(bool enabled)
        {
            if(enabled)CancelUpdateTraffic();
            if (enabled && _protonChanges > 0) throw new InvalidOperationException("Wait for the current Proton change to finish.");
            if (_downloads.Any(d=>d.IsActive)) throw new InvalidOperationException("Finish or cancel active downloads before changing routing.");
            if (enabled && BrowserTabs.Items.Cast<TabItem>().Any(i=>(i.Tag as BrowserTab)?.IsPrivate==true)) throw new InvalidOperationException("Close private tabs before enabling Proton browser protection.");
            if (_browserVpn.Enabled) throw new InvalidOperationException("Disconnect the Tor test before enabling Proton.");
            long operation=Interlocked.Increment(ref _protonOperation);
            _protonChanges++; _protonProbeCancellation?.Cancel(); CancelOnlineSuggestions();
            foreach (var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab)) CancelHomeSuggestions(tab);
            _protonLastCheck = "No current verified route.";
            bool profilePrepared=false;
            try
            {
                if (enabled)
                {
                    await WaitForPrivateCleanup();
                    RequireCurrentProtonOperation(operation);
                    if (_privateCleanupFailures.Count > 0 || BrowserTabs.Items.Cast<TabItem>().Any(i=>(i.Tag as BrowserTab)?.IsPrivate==true))
                        throw new InvalidOperationException("Finish closing private tabs before enabling Proton browser protection.");
                    await WithProfile(null,profile=>_protonFallback.SetArmed(_environment,profile,new WindowInteropHelper(this).Handle,true));
                    RequireCurrentProtonOperation(operation);
                    BeginProtonProtection(true);
                    var core = await EnsureProtonProtectionObserverAsync();
                    RequireCurrentProtonOperation(operation);
                    await _protonPreview.Enable(core.Profile);
                    RequireCurrentProtonOperation(operation);
                    _protonNextCheckUtc = DateTime.MinValue;
                    SetStatus("Proton browser protection enabled. Browsing is paused until its route is verified.");
                    profilePrepared=true;
                }
                else
                {
                    InvalidateProtonProtection("Removing Proton. Browsing remains paused until removal succeeds.");
                    await WithProfile(null,profile=>_protonPreview.Remove(profile));
                    RequireCurrentProtonOperation(operation);
                    await WithProfile(null,profile=>_protonFallback.SetArmed(_environment,profile,new WindowInteropHelper(this).Handle,false));
                    RequireCurrentProtonOperation(operation);
                    BeginProtonProtection(false);
                    ResetProtonPopupStandby();
                    _protonRouteSession?.Dispose();_protonRouteSession=null;
                    // Keep the profile-wide worker filter alive across off/on.
                    // A filter added after a worker starts misses that worker
                    // on some runtimes. Only unload the provider document.
                    _protonObserverReadyTask=null;_protonProxyRead=null;
                    if(_protonObserver!=null)_protonObserver.CoreWebView2.Navigate("about:blank");
                    SetStatus("Proton browser protection is off. Regular browsing is enabled; reload paused pages to continue.");
                    profilePrepared=true;
                }
            }
            catch
            {
                if(_protonFallback.Armed&&!ProtonProtection.Enabled)BeginProtonProtection(true);
                if (ProtonProtection.Enabled) InvalidateProtonProtection("The Proton change did not finish. Browsing remains paused; retry or turn off and remove.");
                throw;
            }
            finally
            {
                _protonChanges--;
                if(operation==Interlocked.Read(ref _protonOperation))
                {
                    // This profile was already prepared by the completed
                    // operation. Do not create/close another extension-control
                    // view while chrome.tabs.create is waiting for its target.
                    _browserVpnPreparation=profilePrepared?Task.CompletedTask:null;
                    if(profilePrepared)_browserVpnGeneration=_browserGeneration;
                }
                UpdateProtonIndicator();
            }
        }

        private void RequireCurrentProtonOperation(long operation)
        {
            if(operation!=Interlocked.Read(ref _protonOperation)||_shuttingDown||_torWindowClosed)
                throw new OperationCanceledException("The Proton change was superseded or the browser closed.");
        }

        internal ProtonPreviewWindow OpenProtonPreview()
        {
            if (_protonPreviewWindow != null) { _protonPreviewWindow.Activate(); return _protonPreviewWindow; }
            _protonPreviewWindow = new ProtonPreviewWindow(_protonPreview,ChangeProtonPreview,
                async()=>{await EnsureEnvironmentAsync();return _environment;},OpenProtonTarget,CheckProtonPreviewRoute,
                (source,args,cancellation)=>HandleNewWindowRequested(source,args,null,cancellation,true)) { Owner=this };
            _protonPreviewWindow.ProviderPreparer=async profile=>{await _protonPreview.Prepare(profile);await WarmProtonPopupAsync(profile.ProfileName);};
            _protonPreviewWindow.Closed += (s,e)=>_protonPreviewWindow=null;
            UpdateProtonIndicator(); _protonPreviewWindow.Show(); return _protonPreviewWindow;
        }
        private void OpenProtonTarget(string target)
        {
            Uri uri;
            if (ProtonPreviewService.IsProviderPage(target) || ProtonPreviewService.IsAccountPage(target))
                CreateNewTab(target,true,false,null,false,null,true);
            else if (Uri.TryCreate(target,UriKind.Absolute,out uri) && (uri.Scheme=="http" || uri.Scheme=="https")) CreateNewTab(target,true);
        }

        // This controller is about:blank until provider registration is prepared.
        // Visible browsing contexts also guard worker requests; the tested
        // runtime does not consistently send worker events to a blank observer.
        internal async Task<CoreWebView2> EnsureProtonProtectionObserverAsync()
        {
            await EnsureEnvironmentAsync();
            if (_protonObserverEnvironment != null && _protonObserverEnvironment != _environment) CloseProtonProtectionObserver();
            if (_protonObserverTask == null)
            {
                _protonObserverLifetime = CancellationTokenSource.CreateLinkedTokenSource(_protonLifetime.Token);
                _protonObserverEnvironment = _environment;
                _protonObserverTask = CreateProtonObserver(_environment,_protonObserverSerial,_protonObserverLifetime.Token);
            }
            var task = _protonObserverTask;
            try { return await task; }
            catch { if (ReferenceEquals(task,_protonObserverTask)) CloseProtonProtectionObserver(); throw; }
        }
        private async Task<CoreWebView2> CreateProtonObserver(CoreWebView2Environment environment,int serial,CancellationToken cancellation)
        {
            // Empty is WebView2's original default profile. The literal name
            // "Default" creates a different named profile (WV2Profile_default).
            var options=environment.CreateCoreWebView2ControllerOptions();options.ProfileName=string.Empty;
            var controller=await ProtonRouteProbe.CreateController(environment.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(this).Handle,options),cancellation);
            if (serial!=_protonObserverSerial || cancellation.IsCancellationRequested || environment!=_environment || _shuttingDown)
            { controller.Close(); throw new OperationCanceledException(); }
            _protonObserver=controller; controller.IsVisible=false; var core=controller.CoreWebView2;
            core.Settings.AreDevToolsEnabled=false;core.Settings.IsStatusBarEnabled=false;
            core.PermissionRequested+=(s,e)=>{e.Handled=true;e.SavesInProfile=false;e.State=CoreWebView2PermissionState.Deny;};
            core.DownloadStarting+=(s,e)=>e.Cancel=true;
            core.NewWindowRequested+=(s,e)=>e.Handled=true;
            core.WindowCloseRequested+=(s,e)=>{};
            core.NavigationStarting+=(s,e)=>{if(e.Uri!="about:blank" && e.Uri!=ProtonPreviewService.PopupUrl)e.Cancel=true;};
            core.ProcessFailed+=(s,e)=>{
                if(serial!=_protonObserverSerial || _shuttingDown)return;
                InvalidateProtonProtection("Proton's verification view stopped. Browsing is paused.");CloseProtonProtectionObserver();
            };
            ConfigureProtonBackgroundGuard(core);
            await ConfigureProtonWorkerLifecycle(core);
            // A raw controller has no committed document initially. Commit a
            // local blank page so Chromium activates its worker interception.
            var blankLoaded=new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> blankCompleted=(s,e)=>blankLoaded.TrySetResult(e.IsSuccess);
            core.NavigationCompleted+=blankCompleted;
            try
            {
                core.Navigate("about:blank");
                if(!await ProtonRouteProbe.Bounded(blankLoaded.Task,15000,cancellation))throw new InvalidOperationException("The browser request guard did not initialize.");
            }
            finally{core.NavigationCompleted-=blankCompleted;}
            return core;
        }
        private async Task<CoreWebView2> EnsureProtonObserverReady()
        {
            var core=await EnsureProtonProtectionObserverAsync();
            if(_protonObserverReadyTask==null) _protonObserverReadyTask=PrepareProtonObserver(core,_protonObserverSerial,_protonObserverLifetime.Token);
            var task=_protonObserverReadyTask;
            try{return await task;}catch{if(ReferenceEquals(task,_protonObserverReadyTask))_protonObserverReadyTask=null;throw;}
        }
        private async Task<CoreWebView2> PrepareProtonObserver(CoreWebView2 core,int serial,CancellationToken cancellation)
        {
            await _protonPreview.Prepare(core.Profile);
            cancellation.ThrowIfCancellationRequested();
            if(serial!=_protonObserverSerial)throw new OperationCanceledException();
            var loaded=new TaskCompletionSource<bool>();ulong? id=null;
            EventHandler<CoreWebView2NavigationStartingEventArgs> starting=(s,e)=>{if(e.Uri==ProtonPreviewService.PopupUrl)id=e.NavigationId;};
            EventHandler<CoreWebView2NavigationCompletedEventArgs> completed=(s,e)=>{if(e.NavigationId==id)loaded.TrySetResult(e.IsSuccess);};
            core.NavigationStarting+=starting;core.NavigationCompleted+=completed;
            try{core.Navigate(ProtonPreviewService.PopupUrl);if(!await ProtonRouteProbe.Bounded(loaded.Task,15000,cancellation))throw new InvalidOperationException("Proton's verification view did not load.");}
            finally{core.NavigationStarting-=starting;core.NavigationCompleted-=completed;}
            string nonce=Guid.NewGuid().ToString("N");
            core.WebMessageReceived+=(s,e)=>{
                if(serial!=_protonObserverSerial || _shuttingDown || !ProtonPreviewService.IsProviderPage(e.Source))return;
                ProtonProxyEvent change;
                if(ProtonProxyEvents.TryRead(e.WebMessageAsJson,nonce,out change))HandleProtonProxyEvent(change);
            };
            await ProtonRouteProbe.Bounded(core.ExecuteScriptAsync(ProtonProxyEvents.WatchScript(nonce)),5000,cancellation);
            return core;
        }
        internal void HandleProtonProxyEvent(ProtonProxyEvent change)
        {
            if(_shuttingDown||_torWindowClosed||!ProtonProtection.Enabled)return;
            if(change==ProtonProxyEvent.FailedTransaction)
            {
                // Chromium aborted this transaction: it did not fall back to
                // DIRECT. Keep other pages alive under their existing lease
                // and fresh per-request ownership checks. A settings change,
                // unsafe error, failed route check or expiry still closes them.
                if(DateTime.UtcNow>=_protonNextErrorCheckUtc)
                {
                    _protonRouteRefreshRequested=true;
                    _protonNextCheckUtc=DateTime.MinValue;
                    _protonNextErrorCheckUtc=DateTime.UtcNow.AddSeconds(5);
                }
                return;
            }
            string reason=change==ProtonProxyEvent.UnsafeError
                ?"Proton reported an unsafe proxy error. Browsing is paused while its route is checked."
                :change==ProtonProxyEvent.Privacy
                ?"Proton's privacy settings changed. Browsing is paused while its route is checked."
                :"Proton's connection settings changed. Browsing is paused while its route is checked.";
            InvalidateProtonProtection(reason,true);_protonNextCheckUtc=DateTime.MinValue;
        }
        private async Task<bool> ValidateProtonProxyOwnership(CancellationToken cancellation)
        {
            if(_protonProxyRead==null || _protonProxyRead.IsCompleted) _protonProxyRead=ReadProtonOwnership();
            return await ProtonRouteProbe.Bounded(_protonProxyRead,5000,cancellation);
        }
        private async Task<bool> ReadProtonOwnership()
        {var core=await EnsureProtonObserverReady();return await ProtonRouteProbe.HasOwnedProxy(core,_protonObserverLifetime.Token);}

        private async Task MonitorProtonProtection()
        {
            if(!AutomaticProtonMonitoringEnabled || _protonMonitorBusy || _shuttingDown || _torWindowClosed || _protonChanges>0 ||
               ProtonProtection==null || !ProtonProtection.Enabled || !_protonPreview.Enabled)return;
            if(ProtonProtection.State!=ProtonProtectionPhase.Protected && DateTime.UtcNow<_protonNextCheckUtc)return;
            _protonMonitorBusy=true;
            try
            {
                var core=await EnsureProtonObserverReady();
                var proof=await ProtonRouteProbe.ReadProxyProof(core,_protonObserverLifetime.Token);
                if(!proof.Owned || !proof.Supported)
                {
                    string reason=!proof.Owned?"Disconnected. Connect in Proton's controls; browsing is paused.":"Unsupported Proton settings. Turn off split tunneling, enable WebRTC protection and recheck; browsing is paused.";
                    if(ProtonProtection.State!=ProtonProtectionPhase.Disconnected || ProtonProtection.Reason!=reason) InvalidateProtonProtection(reason);
                    return;
                }
                var until=ProtonProtection.VerifiedUntilUtc;
                if(DateTime.UtcNow>=_protonNextCheckUtc && (_protonRouteRefreshRequested || !until.HasValue || until.Value-DateTime.UtcNow<TimeSpan.FromSeconds(12)))
                {
                    _protonRouteRefreshRequested=false;
                    await VerifyProtonRoute();
                }
            }
            catch
            {
                if(!_shuttingDown && ProtonProtection.Enabled) InvalidateProtonProtection("Proton could not be confirmed. Browsing is paused; open its controls to retry.");
                _protonNextCheckUtc=DateTime.UtcNow.AddSeconds(10);
            }
            finally{_protonMonitorBusy=false;UpdateProtonIndicator();}
        }
        private async Task<string> CheckProtonPreviewRoute()
        {
            if(!_protonPreview.Enabled)return "Enable Proton, then sign in and connect before checking.";
            return (await VerifyProtonRoute()).Message;
        }
        private async Task<ProtonRouteCheckResult> VerifyProtonRoute()
        {
            if(_protonRouteTask!=null&&!_protonRouteTask.IsCompleted)return await _protonRouteTask;
            _protonRouteTask=VerifyProtonRouteCore();return await _protonRouteTask;
        }
        private async Task<ProtonRouteCheckResult> VerifyProtonRouteCore()
        {
            if(ProtonProtection.State!=ProtonProtectionPhase.Protected) InvalidateProtonProtection("Connecting. Verifying Proton's browser route…",true);
            long epoch=ProtonProtection.Epoch;
            _protonProbeCancellation?.Cancel();
            using(var cancellation=CancellationTokenSource.CreateLinkedTokenSource(_protonLifetime.Token))
            {
                cancellation.CancelAfter(TimeSpan.FromSeconds(45));_protonProbeCancellation=cancellation;
                _protonNextCheckUtc=DateTime.UtcNow.AddSeconds(10);
                try
                {
                    var core=await EnsureProtonObserverReady();
                    if(_protonRouteSession==null)_protonRouteSession=new ProtonRouteSession(_environment,new WindowInteropHelper(this).Handle);
                    var result=await _protonRouteSession.Compare(core,cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if(epoch!=ProtonProtection.Epoch || !ProtonProtection.Enabled)throw new OperationCanceledException();
                    if(result.Verified)
                    {
                        if(!MarkProtonRouteVerified(epoch))throw new OperationCanceledException();
                        _protonLastCheck=result.Message;_protonNextCheckUtc=DateTime.UtcNow.AddSeconds(15);
                    }
                    else { InvalidateProtonProtection(result.Message);_protonLastCheck=result.Message; }
                    UpdateProtonIndicator();return result;
                }
                catch
                {
                    if(epoch==ProtonProtection.Epoch && ProtonProtection.Enabled && !_shuttingDown)
                    {InvalidateProtonProtection("Route verification did not finish. Browsing remains paused.");_protonLastCheck="Verification failed or expired; no protection claim is made.";}
                    throw;
                }
                finally{if(ReferenceEquals(_protonProbeCancellation,cancellation))_protonProbeCancellation=null;}
            }
        }
        private void InstallTemporaryProtonBackgroundOwner()
        {
            foreach(var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab))
            {
                if(tab==null||tab.IsPrivate||!tab.Initialized||tab.IsClosing)continue;
                try{var core=tab.View?.CoreWebView2;if(core!=null){ConfigureProtonBackgroundGuard(core);return;}}catch(InvalidOperationException){}
            }
        }
        internal void CloseProtonProtectionObserver()
        {
            _protonRouteSession?.Dispose();_protonRouteSession=null;
            ResetProtonPopupStandby();
            _protonFallback?.ResetController();
            if(ProtonProtection!=null&&ProtonProtection.Enabled)
                InvalidateProtonProtection("Proton verification stopped. New browsing requests are paused.");
            _protonObserverSerial++;_protonProbeCancellation?.Cancel();_protonObserverLifetime?.Cancel();
            var controller=_protonObserver;_protonObserver=null;_protonObserverTask=null;_protonObserverReadyTask=null;_protonProxyRead=null;_protonObserverEnvironment=null;
            if(controller!=null)
            {
                if(!_shuttingDown&&!_torWindowClosed&&ProtonProtection!=null&&ProtonProtection.Enabled)InstallTemporaryProtonBackgroundOwner();
                try{ReleaseProtonBackgroundGuard(controller.CoreWebView2);}catch{}
                try{controller.Close();}catch{}
            }
        }
    }
}

