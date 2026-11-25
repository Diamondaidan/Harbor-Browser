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
        private readonly Func<Task> _removeProvider;
        private readonly Func<Task<CoreWebView2Environment>> _environment;
        private readonly Action<CoreWebView2, CoreWebView2NewWindowRequestedEventArgs, CancellationToken> _newWindow;
        private readonly Func<Task<string>> _verify;
        private readonly Grid _root;
        private readonly Border _offPanel;
        private readonly Expander _options;
        private readonly TextBlock _protectionStatus = new TextBlock { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _protectionDetail = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        private readonly TextBlock _offDescription = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) };
        private readonly Button _enable = new Button { Content = "Enable Proton browser protection" };
        private readonly Button _turnOff = new Button { Content = "Turn off protection — keep sign-in", ToolTip = "Use your regular connection after Reload. Proton stays installed but disabled." };
        private readonly Button _remove = new Button { Content = "Remove Proton and reset sign-in", ToolTip = "Uninstall Harbor's Proton extension and clear its saved sign-in. Your Proton account and website login are kept." };
        private readonly Button _reload = new Button { Content = "Reload Proton controls" };
        private readonly Button _check = new Button { Content = "Check browser route" };
        private readonly CancellationTokenSource _closedToken = new CancellationTokenSource();
        private bool _closed, _changing, _removing, _showing, _wired, _needsNewView;
        private int _navigation;
        private string _operationError, _verification, _loadingStage;
        private bool _hostStateProvided;
        private long _protectionStatusRevision;
        private double _providerHeight=640;

        public ProtonPreviewWindow(ProtonPreviewService service, Func<bool, Task> change,
            Func<Task<CoreWebView2Environment>> environment, Action<string> open, Func<Task<string>> verify = null,
            Action<CoreWebView2, CoreWebView2NewWindowRequestedEventArgs, CancellationToken> newWindow = null, Func<Task> remove = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _change = change ?? throw new ArgumentNullException(nameof(change));
            _removeProvider = remove;
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            if (open == null) throw new ArgumentNullException(nameof(open));
            _verify = verify; _newWindow = newWindow;
            Title = ProtonTrialIdentity.DisplayName+" · Proton"; Width = 404; Height = 696; MinWidth = 320; MinHeight = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(StyleProperty, typeof(Window)); WindowFitService.Attach(this);
            var root = new Grid { Margin = new Thickness(8) }; _root = root; Content = root;
            root.LayoutUpdated += (s,e) => UpdateProviderScale();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            heading.Children.Add(_protectionStatus); heading.Children.Add(_status); root.Children.Add(heading);
            _protectionStatus.TextWrapping = TextWrapping.NoWrap; _protectionStatus.TextTrimming = TextTrimming.CharacterEllipsis;
            AutomationProperties.SetName(_protectionStatus, "Harbor protection status");
            AttachProviderLayout(); Grid.SetRow(ProviderView, 1); root.Children.Add(ProviderView);
            var offContent = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16) };
            offContent.Children.Add(new TextBlock { Text = "Proton browser protection", FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            offContent.Children.Add(_offDescription);
            offContent.Children.Add(_enable);
            _offPanel = new Border { Child = offContent }; _offPanel.SetResourceReference(Border.BackgroundProperty, "HarborPanel"); Grid.SetRow(_offPanel, 1); root.Children.Add(_offPanel);
            var details = new StackPanel(); details.Children.Add(_protectionDetail);
            var verificationNotice = new TextBlock { Text = "Route checks compare browser and direct ipify requests. Addresses are compared in memory and aren't saved. Protection covers supported browser requests, not other apps.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5) };
            verificationNotice.SetResourceReference(TextBlock.ForegroundProperty, "HarborMuted");
            AutomationProperties.SetName(verificationNotice, "Route verification details"); details.Children.Add(verificationNotice);
            details.Children.Add(new TextBlock { Text = "Private tabs cannot use browser-only Proton in this engine. Choose ‘Turn off protection — keep sign-in’ for private tabs on your regular connection. Remove resets the extension sign-in.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5) });
            var buttons = new WrapPanel(); details.Children.Add(buttons);
            buttons.Children.Add(_remove); buttons.Children.Add(_reload); if (_verify != null) buttons.Children.Add(_check);
            _options = new Expander { Header = "Options and connection details", Content = details, Margin = new Thickness(0, 5, 0, 0) };
            var footer = new StackPanel(); footer.Children.Add(_turnOff); footer.Children.Add(_options);
            var privacy = new TextBlock { Text = "Route checks contact ipify; IP addresses aren't saved.", FontSize = 10, Margin = new Thickness(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = verificationNotice.Text };
            privacy.SetResourceReference(TextBlock.ForegroundProperty, "HarborMuted");
            AutomationProperties.SetName(privacy, "Route verification privacy"); footer.Children.Add(privacy);
            Grid.SetRow(footer, 2); root.Children.Add(footer);
            _enable.Click += async (s, e) => await Change(true);
            _turnOff.Click += async (s, e) => await Change(false);
            _remove.Click += async (s, e) => await Change(false, true);
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
            _status.Visibility = _operationError != null || ControlsError != null || _showing || ContainsAccountPage ? Visibility.Visible : Visibility.Collapsed;
            _protectionStatus.ToolTip = ProtectionDetail;
            _offPanel.Visibility = !_service.Enabled && !_changing ? Visibility.Visible : Visibility.Collapsed;
            _offDescription.Text = _service.HasSavedRegistration ? "Protection is off. Enable it to reconnect using Proton's saved sign-in. Other applications keep their connection." : "Enable Proton to sign in and connect. Other applications keep their connection.";
            ProviderView.Visibility = _service.Enabled ? Visibility.Visible : Visibility.Collapsed;
            UpdateProviderScale();
            _enable.IsEnabled = !_changing && !_removing && !_service.Enabled;
            _turnOff.Visibility = _service.Enabled || ProtectionState != "Off" ? Visibility.Visible : Visibility.Collapsed;
            _turnOff.IsEnabled = !_removing && (_service.Enabled || ProtectionState != "Off");
            // Removal can cancel an in-progress enable or page load.
            _remove.Visibility = _removeProvider == null ? Visibility.Collapsed : Visibility.Visible;
            _remove.IsEnabled = (_service.Enabled || _service.HasSavedRegistration || ProtectionState != "Off") && !_removing;
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
                default: return "Ordinary pages are paused. Reconnect in Proton, or turn off protection to use your regular connection while keeping sign-in.";
            }
        }

        private async Task Change(bool enabled, bool remove = false)
        {
            if (enabled && (_changing || _removing) || !enabled && _removing) return;
            if (enabled) _changing = true; else { _removing = true; _navigation++; }
            _operationError = null; _verification = null; ControlsError = null; Update();
            try
            {
                if (remove) await _removeProvider(); else await _change(enabled);
                if (_closed) return;
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
                    ControlsError = "Proton controls are not ready. " + Friendly(ex) + " Reload Proton controls, turn off protection, or remove/reset Proton.";
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
                        { ControlsReady = true; ControlsError = null; await MeasureProvider(view,navigation); return; }
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
            AttachProviderLayout(); Grid.SetRow(ProviderView, 1); _root.Children.Add(ProviderView);
            _wired = false; _needsNewView = false;
        }
        private void AttachProviderLayout()
        {
            ProviderView.HorizontalAlignment=HorizontalAlignment.Center;
            ProviderView.VerticalAlignment=VerticalAlignment.Top;
        }
        private async Task MeasureProvider(WebView2 view,int navigation)
        {
            try
            {
                await Task.Delay(80,_closedToken.Token);
                if(!Current(view,navigation)||ContainsAccountPage||!ProtonPreviewService.IsProviderPage(view.CoreWebView2.Source))return;
                string json=await Limit(view.CoreWebView2.ExecuteScriptAsync("(()=>{const c=document.querySelector('#sign-in-view .content'),r=c?.getBoundingClientRect();return Math.ceil(Math.max(640,document.body.scrollHeight,document.querySelector('.main-area')?.scrollHeight||0,r?.height>0?r.bottom+scrollY+24:0));})()"),TimeSpan.FromSeconds(2),_closedToken.Token);
                double height;
                if(Current(view,navigation)&&!ContainsAccountPage&&double.TryParse(json,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out height)&&height>=560&&height<=1600)
                {_providerHeight=height;UpdateProviderScale();}
            }catch(OperationCanceledException){}catch(Exception){}
        }
        private void UpdateProviderScale()
        {
            // Its sign-in artwork and footer exceed the declared 560-pixel
            // minimum after loading. Reserve 640 CSS pixels, then fit the
            // unmodified provider page on smaller displays.
            double width=_root.ActualWidth,height=_root.RowDefinitions[1].ActualHeight;
            if(_closed)return;
            if(width<=0||height<=0)return;
            if(ContainsAccountPage)
            {ProviderView.Width=double.NaN;ProviderView.Height=double.NaN;ProviderView.HorizontalAlignment=HorizontalAlignment.Stretch;ProviderView.VerticalAlignment=VerticalAlignment.Stretch;if(ProviderView.ZoomFactor!=1)ProviderView.ZoomFactor=1;return;}
            ProviderView.HorizontalAlignment=HorizontalAlignment.Center;ProviderView.VerticalAlignment=VerticalAlignment.Top;
            double scale=Math.Max(0.25,Math.Min(1,Math.Min(width/370.0,height/_providerHeight)));
            if(double.IsNaN(ProviderView.Width)||Math.Abs(ProviderView.Width-370*scale)>0.5)ProviderView.Width=370*scale;
            if(double.IsNaN(ProviderView.Height)||Math.Abs(ProviderView.Height-_providerHeight*scale)>0.5)ProviderView.Height=_providerHeight*scale;
            if(Math.Abs(ProviderView.ZoomFactor-scale)>0.005)ProviderView.ZoomFactor=scale;
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
                { ControlsReady = false; ControlsError = (ContainsAccountPage ? "Proton's account page" : "The Proton controls page") + " could not load (" + e.WebErrorStatus + "). Reload Proton controls, turn off protection, or remove/reset Proton."; Update(); }
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

