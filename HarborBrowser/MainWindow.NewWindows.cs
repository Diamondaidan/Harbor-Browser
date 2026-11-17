using System;
using System.Threading;
using System.Threading.Tasks;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        internal TimeSpan NewWindowStartupTimeout = TimeSpan.FromSeconds(30);
        internal Action<BrowserTab, CoreWebView2> NewWindowBoundForTesting;
        internal Action<string,string,int> NewWindowFailureForTesting;

        // Bind the engine's new browsing context to a fresh controller. Copying
        // only its URL loses opener, window.name, session storage and POST state.
        internal async void HandleNewWindowRequested(CoreWebView2 source,
            CoreWebView2NewWindowRequestedEventArgs args, BrowserTab sourceTab = null,
            CancellationToken cancellation = default(CancellationToken), bool providerAuthentication = false)
        {
            CoreWebView2Deferral deferral = null;
            BrowserTab target = null;
            string stage="request";
            int workerGeneration=_browserGeneration;
            try
            {
                deferral = args.GetDeferral();
                args.Handled = true;
                if (_shuttingDown || _torWindowClosed || cancellation.IsCancellationRequested || sourceTab?.IsClosing == true ||
                    source == null || !AllowedNewWindowTarget(args.Uri,source.Source)) return;
                int generation = _browserGeneration;
                string profile = source.Profile.ProfileName;
                bool privateMode = source.Profile.IsInPrivateModeEnabled;
                bool provider = providerAuthentication || sourceTab?.IsProviderAuthentication == true || ProtonPreviewService.IsSensitivePage(source.Source) || ProtonPreviewService.IsSensitivePage(args.Uri);
                bool trustedAuthentication = providerAuthentication || sourceTab?.AllowProtonAuthentication == true || ProtonPreviewService.IsProviderPage(source.Source);
                if(trustedAuthentication&&!privateMode)
                {
                    target=TakeProtonPopupStandby(profile,provider);
                    if(target==null){SetStatus("Reload Proton controls to prepare a sign-in window, then retry.");return;}
                }
                else target = CreateNewTab(null, true, privateMode, null, true, profile, provider);
                if (target == null) return;
                target.AllowProtonAuthentication = trustedAuthentication;
                stage="initialize";
                var view = target.View;
                // Once accepted, the new tab belongs to Harbor. Closing its
                // opener (as Proton does after tabs.create) must not cancel it.
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(_protonRequestLifetime.Token,trustedAuthentication?CancellationToken.None:cancellation))
                {
                    deadline.CancelAfter(NewWindowStartupTimeout);
                    if(!target.Initialized)await StartupStep(EnsureTabInitializedAsync(target), deadline.Token, NewWindowStartupTimeout, "new window creation");
                    deadline.Token.ThrowIfCancellationRequested();
                    if (_shuttingDown || _torWindowClosed || (!trustedAuthentication&&sourceTab?.IsClosing==true) || target.IsClosing ||
                        generation != _browserGeneration || !ReferenceEquals(view, target.View) || !target.Initialized || target.NeedsNewView ||
                        target.View.CoreWebView2 == null)
                        throw new OperationCanceledException("The new window was cancelled.");
                    var core = target.View.CoreWebView2;
                    stage="profile";
                    if (core.Profile.ProfileName != profile || core.Profile.IsInPrivateModeEnabled != privateMode)
                        throw new InvalidOperationException("The new window profile does not match its opener.");
                    // No host Navigate call precedes this assignment. WebView2
                    // completes the original request, preserving its context.
                    stage="bind";args.NewWindow = core;args.Handled=false;
                    // WebResourceRequested belongs to the replacement web
                    // contents, so register its handlers/filters after binding.
                    stage="filters";ConfigureTrackerProtection(target);
                    ConfigureProtonRequestGuard(target, core);
                    ConfigureTorRequestGuard(target, core);
                    stage="mapping";
                    core.SetVirtualHostNameToFolderMapping(HomePageService.Host,
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Home"),
                        CoreWebView2HostResourceAccessKind.DenyCors);
                    NewWindowBoundForTesting?.Invoke(target, core);
                    target.IsPopupPending = false;
                    target.SuppressInitialNavigation = false;
                    if(!privateMode)
                    {
                        EventHandler<CoreWebView2NavigationCompletedEventArgs> firstDocument=null;
                        firstDocument=(s,e)=>
                        {
                            if(core.Source=="about:blank")return;
                            core.NavigationCompleted-=firstDocument;
                            Dispatcher.BeginInvoke(new Action(()=>{_=InitializePopupWorkerGuard(target,core,workerGeneration);}),System.Windows.Threading.DispatcherPriority.Background);
                            if(trustedAuthentication)Dispatcher.BeginInvoke(new Action(async()=>{try{await WarmProtonPopupAsync(profile);}catch(Exception){}}),System.Windows.Threading.DispatcherPriority.Background);
                        };
                        core.NavigationCompleted+=firstDocument;
                    }
                }
            }
            catch (Exception error)
            {
                try{args.Handled=true;}catch(Exception){}
                NewWindowFailureForTesting?.Invoke(stage,error.GetType().Name,error.HResult);
                if (target != null && !target.IsClosing) try { CloseTab(target); } catch { }
                if (!_shuttingDown && !_torWindowClosed)
                    SetStatus("The requested window could not open. Retry it from the original page.");
            }
            finally
            {
                try { deferral?.Complete(); } catch { }
                // chrome.tabs.create can hold navigation until this deferral
                // completes. Awaiting ServiceWorker.enable here can in turn
                // wait for navigation, leaving the sign-in window stuck blank.
                // Request filters are already attached before releasing it.
            }
        }

        private async Task InitializePopupWorkerGuard(BrowserTab tab,CoreWebView2 core,int generation)
        {
            try
            {
                if(tab.IsClosing||generation!=_browserGeneration||_shuttingDown)return;
                await ConfigureProtonWorkerLifecycle(core);
            }
            catch(Exception)
            {
                if(!tab.IsClosing&&!_shuttingDown&&ProtonProtection.Enabled)
                    InvalidateProtonProtection("The new page's background guard could not start. Browsing is paused.");
            }
        }

        private static bool AllowedNewWindowTarget(string value,string source)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "about:blank") return true;
            Uri uri;
            if(!Uri.TryCreate(value,UriKind.Absolute,out uri))return false;
            if(uri.Scheme=="chrome-extension"){
                Uri opener;
                return ProtonPreviewService.IsProviderPage(value)||
                    Uri.TryCreate(source,UriKind.Absolute,out opener)&&opener.Scheme=="chrome-extension"&&opener.Host==uri.Host;
            }
            return uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "blob" ||
                uri.Scheme == "about" && uri.AbsolutePath == "blank";
        }
    }
}
