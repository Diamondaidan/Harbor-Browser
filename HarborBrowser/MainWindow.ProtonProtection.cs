using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        internal ProtonProtectionState ProtonProtection { get; private set; }
        internal Func<CancellationToken, Task<bool>> ProtonProxyOwnershipValidator;
        internal TimeSpan ProtonVerificationLease = TimeSpan.FromSeconds(30);
        private readonly CancellationTokenSource _protonRequestLifetime = new CancellationTokenSource();
        private DispatcherTimer _protonLeaseTimer;
        private readonly ConditionalWeakTable<CoreWebView2, object> _protonBackgroundGuards = new ConditionalWeakTable<CoreWebView2, object>();

        private void InitializeProtonProtection()
        {
            ProtonProtection = new ProtonProtectionState(_protonPreview.Enabled||_protonFallback.Armed);
            ProtonProtection.Changed += (s, e) =>
            {
                if (Dispatcher.HasShutdownStarted) return;
                if (Dispatcher.CheckAccess()) ApplyProtonProtectionChange();
                else try { Dispatcher.BeginInvoke(new Action(ApplyProtonProtectionChange)); } catch (InvalidOperationException) { }
            };
            _protonLeaseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _protonLeaseTimer.Tick += (s, e) => { var phase = ProtonProtection.State; };
            Loaded += (s, e) => _protonLeaseTimer.Start();
            Closed += (s, e) => { _protonRequestLifetime.Cancel(); _protonLeaseTimer.Stop(); };
        }
        internal long BeginProtonProtection(bool enabled) { return ProtonProtection.Begin(enabled); }
        internal long InvalidateProtonProtection(string reason, bool connecting = false) { return ProtonProtection.Invalidate(reason, connecting); }
        internal bool MarkProtonRouteVerified(long epoch) { return _protonNetworkAvailable && ProtonProtection.TryProtect(epoch, ProtonVerificationLease); }

        private void ApplyProtonProtectionChange()
        {
            if (_torWindowClosed || _shuttingDown) return;
            if (ProtonProtection.Enabled && ProtonProtection.State != ProtonProtectionPhase.Protected)
            {
                StopOrdinaryProtonWorkers();
                foreach (var tab in BrowserTabs.Items.Cast<TabItem>().Select(i => i.Tag as BrowserTab).Where(t => t != null && t.Initialized && !t.IsClosing))
                {
                    try
                    {
                        string source = tab.View?.CoreWebView2?.Source;
                        if (ProtonProtectionState.IsOrdinaryRequest(source, !tab.IsPrivate && tab.AllowProtonAuthentication)) PauseProtonTab(tab, source);
                    }
                    catch (Exception) { }
                }
                foreach (var entry in _downloads.Where(d => d.IsActive && d.Operation != null).ToList())
                {
                    if (!ProtonProtectionState.IsOrdinaryRequest(entry.SourceUrl, false)) continue;
                    try { entry.Operation.Pause(); } catch (Exception) { }
                }
            }
            UpdateProtonIndicator();
            if (SelectedTab?.ProtonPaused == true) ShowTabContent();
        }

        private void PauseProtonTab(BrowserTab tab, string target)
        {
            if (tab.IsClosing || tab.NativeContent != null || !ProtonProtectionState.IsOrdinaryRequest(target, !tab.IsPrivate && tab.AllowProtonAuthentication)) return;
            if (tab.ProtonPaused) return;
            tab.PageDiagnostics.Blocked(null, false);
            tab.ProtonBlockedUrl = target; tab.ProtonPaused = true;
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_shuttingDown || tab.IsClosing || !tab.ProtonPaused) return;
                    try
                    {
                        var core = tab.View?.CoreWebView2;
                        if (tab.VideoPopout != null) tab.VideoPopout.Close();
                        if (core != null && core.Source != "about:blank")
                        { core.Stop(); tab.ProtonPauseNavigation = true; core.Navigate("about:blank"); }
                    }
                    catch (Exception) { }
                    if (SelectedTab == tab)
                    {
                        SetAddressText(HomePageService.DisplayUrl(tab.ProtonBlockedUrl)); ShowTabContent();
                        SetStatus("Browsing is paused. Open Proton's controls, connect and verify the route.");
                    }
                }), DispatcherPriority.Normal);
            }
            catch (InvalidOperationException) { }
        }

        private FrameworkElement BuildProtonPausedView(BrowserTab tab)
        {
            var panel = new StackPanel { MaxWidth = 540, Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock { Text = ProtonProtection.Enabled ? "Browsing paused" : "Ready to resume", FontSize = 24, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            panel.Children.Add(new TextBlock { Text = ProtonProtection.Reason, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
            panel.Children.Add(new TextBlock { Text = (ProtonProtection.Enabled ? "Reconnect in Proton, or turn off protection to use your regular connection while keeping sign-in. " : "Proton protection is off. Your regular connection is ready. ") + "Retry opens this address again; forms and downloads are never resumed automatically.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
            var controls = new Button { Content = "Open Proton controls", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 0, 8) };
            controls.Click += (s, e) => OpenProtonPreview(); panel.Children.Add(controls);
            if (ProtonProtection.Enabled)
            {
                var turnOff = new Button { Content = "Turn off protection — keep sign-in", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 0, 8) };
                turnOff.Click += async (s, e) =>
                {
                    turnOff.IsEnabled = false;
                    try { await ChangeProtonPreview(false); }
                    catch (Exception ex) { SetStatus("Turning off protection did not finish. " + ex.Message); turnOff.IsEnabled = true; }
                };
                panel.Children.Add(turnOff);
            }
            var retry = new Button { Content = "Retry address", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 7, 14, 7), IsEnabled = !(tab.IsPrivate && ProtonProtection.Enabled) && !ProtonProtection.ShouldBlock(tab.ProtonBlockedUrl, tab.AllowProtonAuthentication) };
            retry.Click += (s, e) => { BrowserTabs.SelectedItem = tab.Item; Reload_Click(s, e); }; panel.Children.Add(retry);
            return panel;
        }

        internal async Task<bool> CanSendProtonRequestAsync(string target, bool trustedAuthentication, CancellationToken cancellation)
        {
            if (!ProtonProtectionState.IsOrdinaryRequest(target, trustedAuthentication) || !ProtonProtection.Enabled) return true;
            if (ProtonProtection.ShouldBlock(target, trustedAuthentication)) return false;
            long epoch = ProtonProtection.Epoch;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _protonRequestLifetime.Token))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    if (ProtonProxyOwnershipValidator == null) throw new InvalidOperationException("Proxy verification is unavailable.");
                    var validating = ProtonProxyOwnershipValidator(deadline.Token);
                    if (await Task.WhenAny(validating, Task.Delay(Timeout.Infinite, deadline.Token)) != validating)
                    {
                        ObserveAbandonedTask(validating); throw new OperationCanceledException(deadline.Token);
                    }
                    bool owned = await validating;
                    deadline.Token.ThrowIfCancellationRequested();
                    if (epoch != ProtonProtection.Epoch || !ProtonProtection.Enabled || ProtonProtection.ShouldBlock(target, trustedAuthentication)) return false;
                    if (!owned)
                    {
                        InvalidateProtonProtection("Proton no longer controls the proxy. Browsing is paused."); return false;
                    }
                    return true;
                }
                catch
                {
                    if (!_shuttingDown && !_torWindowClosed && epoch == ProtonProtection.Epoch && ProtonProtection.Enabled)
                        InvalidateProtonProtection("Proton's proxy could not be confirmed. Browsing is paused.");
                    return false;
                }
            }
        }

        private void ConfigureProtonRequestGuard(BrowserTab tab, CoreWebView2 core)
        {
            if(!tab.IsPrivate)ConfigureProtonBackgroundGuard(core);
            int generation = _browserGeneration; var view = tab.View;
            var environment = core.Environment;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.Document);
            core.WebResourceRequested += async (s, args) =>
            {
                if (args.RequestedSourceKind != CoreWebView2WebResourceRequestSourceKinds.Document) return;
                bool trustedAuthentication = !tab.IsPrivate && tab.AllowProtonAuthentication;
                if (!ProtonProtection.Enabled || !ProtonProtectionState.IsOrdinaryRequest(args.Request.Uri, trustedAuthentication)) return;
                var deferral = args.GetDeferral();
                try
                {
                    bool current = !tab.IsClosing && generation == _browserGeneration && ReferenceEquals(view, tab.View);
                    bool allowed = current && !tab.IsPrivate && await CanSendProtonRequestAsync(args.Request.Uri, trustedAuthentication, _protonRequestLifetime.Token);
                    if (!allowed || tab.IsClosing || generation != _browserGeneration || !ReferenceEquals(view, tab.View))
                    {
                        DenyProtonRequest(environment, args);
                        tab.PageDiagnostics.Blocked(args.Request.Uri, false);
                        // Document includes child frames. Deny that resource
                        // without replacing its parent or remembering the
                        // frame URL as the tab's address. Top-level navigation
                        // and protection-state changes pause the tab itself.
                    }
                }
                catch { try { DenyProtonRequest(environment, args); tab.PageDiagnostics.Blocked(args.Request.Uri, false); } catch { } }
                finally { try { deferral.Complete(); } catch { } }
            };
        }

        internal void ConfigureProtonBackgroundGuard(CoreWebView2 core)
        {
            object existing;
            if (_protonBackgroundGuards.TryGetValue(core, out existing)) return;
            _protonBackgroundGuards.Add(core, new object());
            var environment = core.Environment;
            int generation=_browserGeneration;
            // The tested runtime does not deliver worker events to a raw blank
            // observer consistently. Register on each real browsing context as
            // well; every callback enforces the same gate. Duplicate deferrals
            // may deny but never override a denial with an allowed response.
            core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All,
                CoreWebView2WebResourceRequestSourceKinds.SharedWorker|CoreWebView2WebResourceRequestSourceKinds.ServiceWorker);
            core.WebResourceRequested += async (s, args) =>
            {
                var source = args.RequestedSourceKind;
                if (source != CoreWebView2WebResourceRequestSourceKinds.SharedWorker && source != CoreWebView2WebResourceRequestSourceKinds.ServiceWorker) return;
                if(TorBlocksRequest(args.Request.Uri))
                {
                    args.Response=environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("Tor is not verified. Reconnect Tor or disconnect to use the regular route.")),503,"Tor connection paused","Content-Type: text/plain; charset=utf-8");
                    return;
                }
                // Proton's account API is a documented direct-routing exception
                // needed for its background authentication/control process.
                if (!ProtonProtection.Enabled || ProtonProtectionState.IsProviderApiRequest(args.Request.Uri) ||
                    !ProtonProtectionState.IsOrdinaryRequest(args.Request.Uri, false)) return;
                var deferral = args.GetDeferral();
                try { if(generation!=_browserGeneration || !await CanSendProtonRequestAsync(args.Request.Uri,false,_protonRequestLifetime.Token) || generation!=_browserGeneration)DenyProtonRequest(environment,args); }
                catch { try { DenyProtonRequest(environment, args); } catch { } }
                finally { try { deferral.Complete(); } catch { } }
            };
        }

        internal void ReleaseProtonBackgroundGuard(CoreWebView2 core)
        {
            try{core.RemoveWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All,CoreWebView2WebResourceRequestSourceKinds.SharedWorker|CoreWebView2WebResourceRequestSourceKinds.ServiceWorker);}catch{}
            _protonBackgroundGuards.Remove(core);
        }

        private static void DenyProtonRequest(CoreWebView2Environment environment, CoreWebView2WebResourceRequestedEventArgs args)
        {
            args.Response = environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(
                "Harbor paused this request because Proton's browser route is not verified. Open Proton's controls, connect, and verify the route.")),
                503, "Proton browsing paused", "Content-Type: text/plain; charset=utf-8\r\nCache-Control: no-store");
        }
    }
}
