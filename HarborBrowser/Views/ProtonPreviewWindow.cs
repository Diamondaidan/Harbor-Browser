using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using HarborBrowser.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HarborBrowser.Views
{
    public sealed class ProtonPreviewWindow : Window
    {
        internal WebView2 ProviderView = new WebView2();
        internal bool ControlsReady { get; private set; }
        internal string ControlsError { get; private set; }
        internal string ControlsSummary { get; private set; }
        internal bool ContainsAccountPage { get; private set; }
        internal TimeSpan ControlsLoadTimeout = TimeSpan.FromSeconds(15);
        internal Func<WebView2, CoreWebView2Environment, Task> ProviderViewInitializer;
        internal Func<CoreWebView2Profile, Task> ProviderPreparer;
        internal Func<CoreWebView2, Task<string>> ControlsProbe;
        internal string ProtectionState { get; private set; }
        internal string ProtectionDetail { get; private set; }

        private readonly ProtonPreviewService _service;
        private readonly Func<bool, Task> _change;
        private readonly Func<Task<CoreWebView2Environment>> _environment;
        private readonly Action<CoreWebView2, CoreWebView2NewWindowRequestedEventArgs, CancellationToken> _newWindow;
        private readonly Func<Task<string>> _verify;
        private readonly Grid _root;
        private readonly TextBlock _protectionStatus = new TextBlock { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _protectionDetail = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        private readonly Button _enable = new Button { Content = "Enable Proton browser protection" };
        private readonly Button _remove = new Button { Content = "Turn off and remove" };
        private readonly Button _reload = new Button { Content = "Reload Proton controls" };
        private readonly Button _check = new Button { Content = "Check browser route" };
        private readonly CancellationTokenSource _closedToken = new CancellationTokenSource();
        private bool _closed, _changing, _removing, _showing, _wired, _needsNewView;
        private int _navigation;
        private string _operationError, _verification, _loadingStage;
        private bool _hostStateProvided;
        private long _protectionStatusRevision;

        public ProtonPreviewWindow(ProtonPreviewService service, Func<bool, Task> change,
            Func<Task<CoreWebView2Environment>> environment, Action<string> open, Func<Task<string>> verify = null,
            Action<CoreWebView2, CoreWebView2NewWindowRequestedEventArgs, CancellationToken> newWindow = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _change = change ?? throw new ArgumentNullException(nameof(change));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            if (open == null) throw new ArgumentNullException(nameof(open));
            _verify = verify; _newWindow = newWindow;
            Title = ProtonTrialIdentity.DisplayName+" · Proton"; Width = 520; Height = 720; MinWidth = 360; MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(StyleProperty, typeof(Window)); WindowFitService.Attach(this);
            var root = new Grid { Margin = new Thickness(12) }; _root = root; Content = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(new TextBlock
            {
                Text = "Proton browser protection\nSign in and connect below. This optional connection applies to supported Harbor browser requests.",
                TextWrapping = TextWrapping.Wrap
            });
            var commands = new StackPanel(); Grid.SetRow(commands, 1); root.Children.Add(commands);
            var protection = new StackPanel(); protection.Children.Add(_protectionStatus); protection.Children.Add(_protectionDetail);
            var protectionPanel = new Border { Child = protection, Padding = new Thickness(9, 7, 9, 7), Margin = new Thickness(0, 9, 0, 0), CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1) };
            protectionPanel.SetResourceReference(Border.BackgroundProperty, "HarborPanel");
            protectionPanel.SetResourceReference(Border.BorderBrushProperty, "HarborLine");
            commands.Children.Add(protectionPanel); commands.Children.Add(_status);
            AutomationProperties.SetName(_protectionStatus, "Harbor protection status");
            var verificationNotice = new TextBlock { Text = "Automatic route checks contact ipify through the browser and separately without Proton for comparison. Addresses are compared in memory and aren't saved.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 5) };
            verificationNotice.SetResourceReference(TextBlock.ForegroundProperty, "HarborMuted");
            AutomationProperties.SetName(verificationNotice, "Route verification privacy");
            commands.Children.Add(verificationNotice);
            var buttons = new WrapPanel(); commands.Children.Add(buttons);
            buttons.Children.Add(_enable); buttons.Children.Add(_remove); buttons.Children.Add(_reload);
            if (_verify != null) buttons.Children.Add(_check);
            var coverage = new TextBlock { Text = "Coverage: supported browser page requests. Other applications keep their connection.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5) };
            coverage.SetResourceReference(TextBlock.ForegroundProperty, "HarborMuted");
            commands.Children.Add(coverage);
            Grid.SetRow(ProviderView, 2); root.Children.Add(ProviderView);
            _enable.Click += async (s, e) => await Change(true);
            _remove.Click += async (s, e) => await Change(false);
            _reload.Click += async (s, e) => await ShowProvider();
            _check.Click += async (s, e) => await Verify();
            _service.StatusChanged += StatusChanged;
            Loaded += async (s, e) => { Update(); if (_service.Enabled) await ShowProvider(); };
            Closed += (s, e) =>
            {
                _closed = true; _closedToken.Cancel(); _service.StatusChanged -= StatusChanged;
                ProviderView.Dispose();
            };
            Update();
        }

        private void StatusChanged(object sender, EventArgs args)
        {
            if (_closed || Dispatcher.HasShutdownStarted) return;
            try { Dispatcher.BeginInvoke(new Action(Update)); } catch (InvalidOperationException) { }
        }

        private void Update()
        {
            if (_closed) return;
            if (!_hostStateProvided)
            {
                ProtectionState = _service.Enabled ? "Disconnected" : "Off";
                ProtectionDetail = DefaultProtectionDetail(ProtectionState);
            }
            _protectionStatus.Text = ProtectionState == "Protected" ? "Protected — verified browser requests" : "Browser protection: " + ProtectionState;
            _protectionDetail.Text = ProtectionDetail;
            _protectionStatus.SetResourceReference(TextBlock.ForegroundProperty, ProtectionState == "Protected" ? "HarborAccent" : "HarborText");
            AutomationProperties.SetHelpText(_protectionStatus, ProtectionDetail);
            _status.Text = _operationError ?? ControlsError ?? _verification ?? (_showing && !ContainsAccountPage
                ? _loadingStage : ContainsAccountPage
                ? "Complete Proton's account flow in this view. Reload Proton controls when you are ready to return."
                : _service.Status);
            _enable.IsEnabled = !_changing && !_removing && !_service.Enabled;
            // Removal can cancel an in-progress enable or page load.
            _remove.IsEnabled = (_service.Enabled || ProtectionState != "Off") && !_removing;
            _reload.IsEnabled = _service.Enabled && !_changing && !_removing && !_showing;
            _check.IsEnabled = _service.Enabled && ControlsReady && !ContainsAccountPage && !_showing && !_changing && !_removing && _verify != null;
        }

        // Only the host's route verification/guard may supply these states.
        // Extension registration and rendered provider controls are not proof of protection.
        internal void SetProtectionStatus(string state, string detail)
        {
            if (_closed || Dispatcher.HasShutdownStarted) return;
            long revision = Interlocked.Increment(ref _protectionStatusRevision);
            if (!Dispatcher.CheckAccess())
            {
                try { Dispatcher.BeginInvoke(new Action(() => ApplyProtectionStatus(revision, state, detail))); } catch (InvalidOperationException) { }
                return;
            }
            ApplyProtectionStatus(revision, state, detail);
        }

        private void ApplyProtectionStatus(long revision, string state, string detail)
        {
            if (_closed || revision != Interlocked.Read(ref _protectionStatusRevision)) return;
            bool known = state == "Off" || state == "Connecting" || state == "Protected" || state == "Disconnected";
            ProtectionState = known ? state : "Disconnected";
            ProtectionDetail = !known || String.IsNullOrWhiteSpace(detail) ? DefaultProtectionDetail(ProtectionState) : detail.Trim();
            if (ProtectionDetail.Length > 500) ProtectionDetail = ProtectionDetail.Substring(0, 500);
            _hostStateProvided = true;
            Update();
        }

        private static string DefaultProtectionDetail(string state)
        {
            switch (state)
            {
                case "Off": return "Regular browsing is enabled. Turn on Proton when you want to check a protected browser route.";
                case "Connecting": return "Ordinary pages are paused while Harbor checks the browser route.";
                case "Protected": return "The browser route was checked. Supported page requests are allowed.";
                default: return "Ordinary pages are paused. Connect in Proton's controls, then check the browser route.";
            }
        }

        private async Task Change(bool enabled)
        {
            if (enabled && (_changing || _removing) || !enabled && _removing) return;
            if (enabled) _changing = true; else { _removing = true; _navigation++; }
            _operationError = null; _verification = null; ControlsError = null; Update();
            try
            {
                await _change(enabled); if (_closed) return;
                if (enabled) await ShowProvider();
                else
                {
                    _operationError = null; ControlsError = null;
                    ControlsReady = false; ControlsSummary = null; ContainsAccountPage = false;
                    if (ProviderView.CoreWebView2 != null) ProviderView.CoreWebView2.Navigate("about:blank");
                }
            }
            catch (Exception ex) { if (!_closed && !(enabled && (_removing || !_service.Enabled))) _operationError = "The Proton change did not finish. " + Friendly(ex); }
            finally { if (enabled) _changing = false; else _removing = false; Update(); }
        }

        private async Task Verify()
        {
            if (_verify == null || _changing || _removing || _showing || !_service.Enabled || !ControlsReady || ContainsAccountPage) return;
            _changing = true; _operationError = null; _verification = null; Update();
            var view = ProviderView; int navigation = _navigation;
            try
            {
                var core = view.CoreWebView2;
                if (core == null || !ProtonPreviewService.IsProviderPage(core.Source)) return;
                string summary = await Limit(Probe(core), ControlsLoadTimeout, _closedToken.Token);
                if (!Current(view, navigation) || ContainsAccountPage) return;
                ControlsSummary = summary;
                if (!ProtonControlsReadiness.IsReady(summary))
                {
                    ControlsReady = false;
                    ControlsError = "Proton controls are still loading. Reload Proton controls before checking the browser route.";
                    return;
                }
                string result = await Limit(_verify(), TimeSpan.FromSeconds(45), _closedToken.Token);
                if (Current(view, navigation) && !ContainsAccountPage) _verification = result;
            }
            catch (Exception ex) { if (Current(view, navigation) && !ContainsAccountPage) _operationError = "Route check did not finish. " + Friendly(ex); }
            finally { _changing = false; Update(); }
        }

        internal async Task ShowProvider()
        {
            if (!_service.Enabled || _closed || _removing || _showing) return;
            _showing = true; _operationError = null; ControlsError = null; ControlsReady = false; ControlsSummary = null; _verification = null;
            int navigation = ++_navigation;
            var view = ProviderView;
            _loadingStage = "Starting Proton controls…"; Update();
            try
            {
                if (_needsNewView) { ReplaceProviderView(); view = ProviderView; }
                var environment = await Limit(_environment(), TimeSpan.FromSeconds(30), _closedToken.Token);
                if (!Current(view, navigation)) return;
                try
                {
                    await Limit(Complete(ProviderViewInitializer == null ? view.EnsureCoreWebView2Async(environment) : ProviderViewInitializer(view, environment)), TimeSpan.FromSeconds(30), _closedToken.Token);
                }
                catch { if (ReferenceEquals(view, ProviderView)) _needsNewView = true; throw; }
                if (!Current(view, navigation)) return;
                if (!_wired) Wire(view.CoreWebView2);
                _loadingStage = "Restoring the Proton integration…"; Update();
                await Limit(Complete(ProviderPreparer == null ? _service.Prepare(view.CoreWebView2.Profile) : ProviderPreparer(view.CoreWebView2.Profile)), TimeSpan.FromSeconds(30), _closedToken.Token);
                if (!Current(view, navigation)) return;
                ContainsAccountPage = false;
                _loadingStage = "Loading Proton's sign-in and connection controls…"; Update();
                await WaitForProviderNavigation(view, navigation, null, true);
            }
            catch (Exception ex)
            {
                if (Current(view, navigation))
                    ControlsError = "Proton controls are not ready. " + Friendly(ex) + " Reload Proton controls or turn off and remove Proton.";
            }
            finally { _showing = false; Update(); }
        }

        private bool Current(WebView2 view, int navigation)
        { return !_closed && _service.Enabled && navigation == _navigation && ReferenceEquals(view, ProviderView); }

        private Task<string> Probe(CoreWebView2 core)
        { return ControlsProbe == null ? core.ExecuteScriptAsync(ProtonControlsReadiness.ProbeScript) : ControlsProbe(core); }

        private async Task WaitForProviderNavigation(WebView2 view, int navigation, ulong? navigationId, bool navigate)
        {
            var core = view.CoreWebView2;
            var completion = new TaskCompletionSource<bool>();
            bool loading = true, failed = false;
            EventHandler<CoreWebView2NavigationStartingEventArgs> starting = (s, e) =>
            { if (ProtonPreviewService.IsProviderPage(e.Uri)) { navigationId = e.NavigationId; loading = true; failed = false; } };
            EventHandler<CoreWebView2NavigationCompletedEventArgs> completed = (s, e) =>
            { if (navigationId == e.NavigationId) { loading = false; failed = !e.IsSuccess; completion.TrySetResult(e.IsSuccess); } };
            core.NavigationStarting += starting; core.NavigationCompleted += completed;
            try
            {
                if (navigate) core.Navigate(ProtonPreviewService.PopupUrl);
                bool succeeded = await Limit(completion.Task, ControlsLoadTimeout, _closedToken.Token);
                if (!Current(view, navigation) || ContainsAccountPage) return;
                if (!succeeded) throw new InvalidOperationException("The Proton controls page did not finish loading.");
                var readiness = Stopwatch.StartNew();
                while (readiness.Elapsed < ControlsLoadTimeout)
                {
                    _closedToken.Token.ThrowIfCancellationRequested();
                    if (!Current(view, navigation) || ContainsAccountPage) return;
                    if (failed) throw new InvalidOperationException("The Proton controls page did not finish loading.");
                    if (!loading && ProtonPreviewService.IsProviderPage(core.Source))
                    {
                        // Observe provider rendering only, never its authentication or proxy state.
                        ulong? probedNavigation = navigationId;
                        string summary = await Limit(Probe(core), ControlsLoadTimeout - readiness.Elapsed, _closedToken.Token);
                        if (!Current(view, navigation) || ContainsAccountPage) return;
                        if (loading || probedNavigation != navigationId) continue;
                        ControlsSummary = summary;
                        if (ProtonControlsReadiness.IsReady(summary))
                        { ControlsReady = true; ControlsError = null; return; }
                    }
                    await Task.Delay(200, _closedToken.Token);
                }
                throw new TimeoutException("The Proton page opened but sign-in or connection controls did not become ready in time.");
            }
            finally
            {
                core.NavigationStarting -= starting; core.NavigationCompleted -= completed;
            }
        }

        private async Task ObserveProviderNavigation(WebView2 view, int navigation, ulong navigationId)
        {
            try { await WaitForProviderNavigation(view, navigation, navigationId, false); }
            catch (Exception ex)
            {
                if (Current(view, navigation) && !ContainsAccountPage)
                    ControlsError = "Proton controls are not ready. " + Friendly(ex) + " Reload Proton controls or turn off and remove Proton.";
            }
            finally { _showing = false; Update(); }
        }

        private void ReplaceProviderView()
        {
            var previous = ProviderView;
            _root.Children.Remove(previous);
            try { previous.Dispose(); } catch { }
            ProviderView = new WebView2();
            Grid.SetRow(ProviderView, 2); _root.Children.Add(ProviderView);
            _wired = false; _needsNewView = false;
        }

        private void Wire(CoreWebView2 core)
        {
            _wired = true;
            var view = ProviderView;
            Func<bool> current = () => !_closed && ReferenceEquals(view, ProviderView);
            ulong latestNavigation = 0;
            core.Settings.AreDevToolsEnabled = false; core.Settings.IsStatusBarEnabled = false;
            core.NewWindowRequested += (s, e) =>
            {
                if (!current() || !_service.Enabled || _newWindow == null) { e.Handled = true; return; }
                _newWindow(core, e, _closedToken.Token);
            };
            core.NavigationStarting += (s, e) =>
            {
                if (!current()) { e.Cancel = true; return; }
                if (ProtonPreviewService.IsProviderPage(e.Uri))
                {
                    latestNavigation = e.NavigationId; ControlsReady = false; ControlsSummary = null;
                    if (!_showing && _service.Enabled && !ContainsAccountPage)
                    {
                        _showing = true; _loadingStage = "Loading Proton's sign-in and connection controls…";
                        _operationError = null; ControlsError = null; _verification = null;
                        int navigation = ++_navigation; Update();
                        _ = ObserveProviderNavigation(view, navigation, e.NavigationId);
                    }
                    else Update();
                    return;
                }
                if (e.Uri == "about:blank") { latestNavigation = e.NavigationId; return; }
                Uri uri;
                if (!_closed && _service.Enabled && Uri.TryCreate(e.Uri, UriKind.Absolute, out uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    // Keep account navigations in their actual browsing context.
                    // Cancel-and-copy loses opener/session/POST auth state.
                    latestNavigation = e.NavigationId; ContainsAccountPage = true; ControlsReady = false; ControlsSummary = null;
                    ControlsError = null; _verification = null; _navigation++; Update();
                    return;
                }
                e.Cancel = true;
            };
            // Proton closes its popup during the native handoff. The host
            // prepares the destination first and owns that tab independently.
            core.WindowCloseRequested += (s, e) => { };
            core.PermissionRequested += (s, e) => { e.Handled = true; e.SavesInProfile = false; e.State = CoreWebView2PermissionState.Deny; };
            core.DownloadStarting += (s, e) => e.Cancel = true;
            core.ProcessFailed += (s, e) =>
            {
                if (!current()) return;
                ControlsReady = false; _needsNewView = true; _navigation++;
                ControlsError = "Proton's controls stopped. Reload Proton controls to open a fresh view."; Update();
            };
            core.NavigationCompleted += (s, e) =>
            {
                if (current() && e.NavigationId == latestNavigation && _service.Enabled && !e.IsSuccess && core.Source != "about:blank")
                { ControlsReady = false; ControlsError = (ContainsAccountPage ? "Proton's account page" : "The Proton controls page") + " could not load (" + e.WebErrorStatus + "). Reload Proton controls or turn off and remove Proton."; Update(); }
            };
        }

        private static string Friendly(Exception error)
        { return error is OperationCanceledException ? "The operation was cancelled." : error is TimeoutException ? error.Message : error.Message; }
        private static async Task<bool> Complete(Task task) { await task; return true; }
        private static async Task<T> Limit<T>(Task<T> task, TimeSpan timeout, CancellationToken cancellation)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try
                {
                    if (await Task.WhenAny(task, Task.Delay(timeout < TimeSpan.Zero ? TimeSpan.Zero : timeout, deadline.Token)) != task)
                    {
                        _ = task.ContinueWith(t => { var ignored = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        cancellation.ThrowIfCancellationRequested(); throw new TimeoutException("The operation timed out.");
                    }
                    var result = await task; cancellation.ThrowIfCancellationRequested(); return result;
                }
                finally { deadline.Cancel(); }
            }
        }
    }
}

