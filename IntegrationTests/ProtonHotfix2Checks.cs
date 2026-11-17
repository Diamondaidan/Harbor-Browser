using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private const string Hotfix2PermissionSite = "https://permission-hotfix2.test.invalid";
    private const string Hotfix2OwnershipFile = ".harbor-hotfix2-synthetic-profile";

    // This entry point only accepts a profile created by the parent test below.
    // It never selects the portable application's TrialData or an installed profile.
    private static int ProtonHotfix2Host(string[] args)
    {
        string root = Path.GetFullPath(args[0]), mode = args[1];
        if (!File.Exists(Path.Combine(root, Hotfix2OwnershipFile)) ||
            (mode != "enable" && mode != "restore" && mode != "seed-off" && mode != "resume-off"))
            throw new InvalidOperationException("Only an explicitly owned synthetic restart profile is permitted.");
        int exit = 1;
        var results = new Dictionary<string, bool>();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/HarborBrowser;component/Themes/Controls.xaml") });
        var store = new BrowserDataStore(Path.Combine(root, "Profile"), Path.Combine(root, "Downloads"));
        if (mode == "enable" || mode == "seed-off") store.SaveSettings(new BrowserSettings { RestorePreviousSession = false, HarborHomeIntroduced = true, ThemeMode = "Dark", OnlineSuggestionsEnabled = false });
        var main = Offscreen(new MainWindow(store));
        ProtonPreviewWindow popup = null;
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(150) };
        watchdog.Tick += (s, e) => app.Shutdown(3);
        main.Loaded += async (s, e) =>
        {
            try
            {
                await Wait(() => main.BrowserTabs.Items.Count > 0 &&
                    (((BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag).Initialized ||
                     ((BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag).FailureMessage != null));
                var tab = (BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag;
                results["main tab initializes without startup failure"] = tab.Initialized && tab.FailureMessage == null;
                if (!results.Values.All(v => v)) throw new Exception("Synthetic cold-start tab did not initialize.");
                var core = tab.View.CoreWebView2;
                await WaitJs(core, "document.readyState==='complete'&&location.href==='https://harbor.invalid/index.html'");
                results["Harbor Home renders after process startup"] = await core.ExecuteScriptAsync("document.getElementById('app-version')!==null") == "true";
                var service = (ProtonPreviewService)Field(main, "_protonPreview");
                if (mode == "resume-off")
                {
                    results["off intent survives the full process restart"] = !service.Enabled && !main.ProtonProtection.Enabled && !((ProtonFallbackGuard)Field(main,"_protonFallback")).Armed;
                    results["retained provider remains installed and disabled after restart"] = service.HasSavedRegistration && (await core.Profile.GetBrowserExtensionsAsync()).Any(x=>x.Id==ProtonPreviewService.ExtensionId&&!x.IsEnabled);
                }
                if (mode == "enable" || mode == "seed-off" || mode == "resume-off") await main.ChangeProtonPreview(true);
                results["Proton enabled intent and registration survive"] = service.Enabled &&
                    (await core.Profile.GetBrowserExtensionsAsync()).Any(x => x.Id == ProtonPreviewService.ExtensionId && x.IsEnabled);
                var permissions = await core.Profile.GetNonDefaultPermissionSettingsAsync();
                results["Proton internal notification grant is preserved"] = permissions.Any(x =>
                    x.PermissionKind == CoreWebView2PermissionKind.Notifications &&
                    x.PermissionState == CoreWebView2PermissionState.Allow &&
                    ProtonPreviewService.IsProviderPage(x.PermissionOrigin));
                if (mode == "restore")
                    results["saved website permission is reset during startup"] = !permissions.Any(x =>
                        x.PermissionKind == CoreWebView2PermissionKind.Geolocation &&
                        SitePermissionService.Origin(x.PermissionOrigin) == Hotfix2PermissionSite);
                popup = main.OpenProtonPreview(); Offscreen(popup);
                await Wait(() => popup.ControlsReady || popup.ControlsError != null);
                results["Proton controls report ready after startup"] = popup.ControlsReady && popup.ControlsError == null;
                var provider = popup.ProviderView.CoreWebView2;
                string domSummary = provider == null ? null : await provider.ExecuteScriptAsync(ProtonControlsReadiness.ProbeScript);
                string source = provider == null ? null : provider.Source;
                File.WriteAllText(Path.Combine(root, mode + "-controls.json"), Json.Serialize(new {
                    popup.ControlsReady, popup.ControlsError, popup.ControlsSummary, domSummary,
                    sourceCategory = ProtonPreviewService.IsProviderPage(source) ? "provider" : ProtonPreviewService.IsAccountPage(source) ? "account" : source == "about:blank" ? "blank" : "other"
                }));
                var visible = String.IsNullOrEmpty(domSummary) ? null : Json.Deserialize<Dictionary<string, object>>(domSummary);
                results["actual signed-out button is visible and spinner is absent"] =
                    visible != null && Convert.ToBoolean(visible["signInAction"]) && !Convert.ToBoolean(visible["loggedAction"]) && !Convert.ToBoolean(visible["spinner"]);
                if (!results["Proton controls report ready after startup"] || !results["actual signed-out button is visible and spinner is absent"])
                    throw new Exception("Synthetic provider controls did not reach usable sign-in; see " + mode + "-controls.json.");
                string key = "harbor-hotfix2-local-" + File.ReadAllText(Path.Combine(root, Hotfix2OwnershipFile)).Trim();
                if (mode == "enable" || mode == "seed-off")
                {
                    results["own storage canary is written before shutdown"] = await Hotfix2StorageCanary(provider, key, true);
                    await core.Profile.SetPermissionStateAsync(CoreWebView2PermissionKind.Geolocation, Hotfix2PermissionSite, CoreWebView2PermissionState.Allow);
                    results["website permission grant is seeded before shutdown"] = (await core.Profile.GetNonDefaultPermissionSettingsAsync()).Any(x =>
                        x.PermissionKind == CoreWebView2PermissionKind.Geolocation && x.PermissionState == CoreWebView2PermissionState.Allow &&
                        SitePermissionService.Origin(x.PermissionOrigin) == Hotfix2PermissionSite);
                }
                else
                {
                    results["own extension-local canary survives the full restart"] = await Hotfix2StorageCanary(provider, key, false);
                }
                if (mode == "seed-off" || mode == "resume-off")
                {
                    await main.ChangeProtonPreview(false);
                    results["ordinary off disables without uninstalling the provider"] = !service.Enabled && service.HasSavedRegistration && (await core.Profile.GetBrowserExtensionsAsync()).Any(x=>x.Id==ProtonPreviewService.ExtensionId&&!x.IsEnabled);
                    results["ordinary off closes both protection gates"] = !main.ProtonProtection.Enabled && !((ProtonFallbackGuard)Field(main,"_protonFallback")).Armed;
                }
                exit = results.Values.All(v => v) ? 0 : 1;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(root, mode + "-error.json"), Json.Serialize(new { type = error.GetType().FullName, error.Message }));
            }
            finally
            {
                watchdog.Stop();
                var environment = (CoreWebView2Environment)Field(main, "_environment");
                var engineExit = new TaskCompletionSource<bool>();
                if (environment != null) environment.BrowserProcessExited += (ss, ee) => engineExit.TrySetResult(true);
                var windowClosed = new TaskCompletionSource<bool>(); main.Closed += (ss, ee) => windowClosed.TrySetResult(true);
                popup?.Close(); main.Close();
                bool closed = await Task.WhenAny(windowClosed.Task, Task.Delay(15000)) == windowClosed.Task;
                results["synthetic browser window closes cleanly"] = closed;
                results["browser engine exits before the next cold start"] = environment == null || await Task.WhenAny(engineExit.Task, Task.Delay(15000)) == engineExit.Task;
                if (!results.Values.All(v => v)) exit = 1;
                File.WriteAllText(Path.Combine(root, mode + "-results.json"), Json.Serialize(results));
                Console.WriteLine("SYNTHETIC " + mode + ": " + Json.Serialize(results));
                app.Shutdown(exit);
            }
        };
        watchdog.Start(); main.Show(); app.Run(); return exit;
    }

    private static async Task ProtonHotfix2Checks(MainWindow main, BrowserDataStore store, string output)
    {
        string fakeLocalData = Path.Combine(Path.GetPathRoot(Path.GetFullPath(output)), "Users", "HarborSynthetic", "AppData", "Local");
        Check("stable data preserves the per-user folder independently of extraction depth",
            ProtonTrialIdentity.DataFolder(fakeLocalData) == Path.Combine(fakeLocalData, "HarborBrowser"));
        // Chromium's unpacked-extension storage can fail in a deeply nested data
        // folder. Keep this owned cold profile compact, just as the application
        // now does independently of its executable/extraction folder.
        string testWorkspace = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\.."));
        string coldRoot = Path.Combine(testWorkspace, "H2T", Guid.NewGuid().ToString("N").Substring(0, 8));
        if (Directory.Exists(coldRoot)) throw new InvalidOperationException("Refusing an existing synthetic restart folder.");
        Directory.CreateDirectory(coldRoot);
        File.WriteAllText(Path.Combine(output, "cold-test-path.txt"), coldRoot);
        File.WriteAllText(Path.Combine(coldRoot, Hotfix2OwnershipFile), Guid.NewGuid().ToString("N"));
        foreach (string mode in new[] { "enable", "restore", "restore" })
        {
            string arguments = String.Join(" ", new[] { coldRoot, mode, "--proton-hotfix2-host" }.Select(WindowsCommandRunner.Quote));
            using (var child = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, arguments)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
                if (!await Task.Run(() => child.WaitForExit(180000))) { try { child.Kill(); } catch { } throw new TimeoutException("Synthetic Proton restart host did not exit."); }
                string childOutput = await stdout, childError = await stderr;
                File.AppendAllText(Path.Combine(coldRoot, "child-output.log"), childOutput + childError);
                string resultPath = Path.Combine(coldRoot, mode + "-results.json");
                if (child.ExitCode != 0) throw new Exception("Synthetic Proton " + mode + " failed: " + childOutput + childError);
                var results = Json.Deserialize<Dictionary<string, bool>>(File.ReadAllText(resultPath));
                foreach (var result in results) Check("cold process " + mode + ": " + result.Key, result.Value);
            }
        }

        var service = (ProtonPreviewService)Field(main, "_protonPreview");
        var environment = (CoreWebView2Environment)Field(main, "_environment");
        var guardValidator = main.ProtonProxyOwnershipValidator;
        var guardLease = main.ProtonVerificationLease;
        bool guardMonitoring = main.AutomaticProtonMonitoringEnabled;
        ProtonPreviewWindow window = null;
        BrowserTab fixture = null;
        try
        {
            await main.ChangeProtonPreview(true);
            int routeChecks = 0;
            Func<ProtonPreviewWindow> fresh = () => Offscreen(new ProtonPreviewWindow(service, main.ChangeProtonPreview,
                () => Task.FromResult(environment), target => { }, () => { routeChecks++; return Task.FromResult("synthetic route check"); }));

            window = fresh();
            var failedView = window.ProviderView;
            window.ProviderViewInitializer = (view, env) => Task.FromException(new InvalidOperationException("Synthetic controller startup failure."));
            window.Show(); await Wait(() => window.ControlsError != null);
            Check("controller startup failure is visible and does not enable route check", !window.ControlsReady && !ProtonButton(window, "Check browser route").IsEnabled);
            Check("controller startup failure leaves Reload available", ProtonButton(window, "Reload Proton controls").IsEnabled);
            window.ProviderViewInitializer = null;
            await window.ShowProvider();
            Check("Reload replaces a failed provider controller", !ReferenceEquals(failedView, window.ProviderView));
            Check("replacement provider controller reaches real sign-in controls", window.ControlsReady && window.ControlsError == null);
            window.Close(); window = null;

            window = fresh();
            int preparations = 0;
            window.ProviderPreparer = profile => ++preparations == 1
                ? Task.FromException(new InvalidOperationException("Synthetic provider restoration failure.")) : service.Prepare(profile);
            window.Show(); await Wait(() => window.ControlsError != null);
            Check("provider restoration failure is reported without a route claim", !window.ControlsReady && !ProtonButton(window, "Check browser route").IsEnabled);
            await window.ShowProvider();
            Check("Reload retries provider restoration and reaches sign-in", preparations == 2 && window.ControlsReady && window.ControlsError == null);
            window.Close(); window = null;

            // This legacy suite tests rendering/retry behavior, not network privacy.
            // Use explicit synthetic proof in its owned profile for the HTML fixture;
            // the beta request-count suite separately exercises the closed guard.
            main.AutomaticProtonMonitoringEnabled = false;
            main.ProtonProxyOwnershipValidator = cancellation => Task.FromResult(true);
            main.ProtonVerificationLease = TimeSpan.FromMinutes(2);
            if (!main.MarkProtonRouteVerified(main.ProtonProtection.Epoch)) throw new Exception("Owned rendering fixture could not receive synthetic proof.");
            fixture = AddTab(main, HomePageService.Url); main.BrowserTabs.SelectedItem = fixture.Item; await Wait(() => fixture.Initialized);
            var fixtureCore = fixture.View.CoreWebView2;
            string fixtureFolder = Path.Combine(output, "OwnedControlsFixture"); Directory.CreateDirectory(fixtureFolder);
            File.WriteAllText(Path.Combine(fixtureFolder, "spinner.html"), "<!doctype html><html><body><div id='spinner'>Loading</div><div id='sign-in-view' style='display:none'><button class='sign-in-button'>Sign in</button></div><a href='#help'>Help with Proton</a><a href='#privacy'>Privacy information</a></body></html>");
            fixtureCore.SetVirtualHostNameToFolderMapping("hotfix2-controls.test.invalid", fixtureFolder, CoreWebView2HostResourceAccessKind.DenyCors);
            fixtureCore.Navigate("https://hotfix2-controls.test.invalid/spinner.html");
            await WaitJs(fixtureCore, "document.readyState==='complete'&&location.hostname==='hotfix2-controls.test.invalid'&&!!document.getElementById('spinner')");
            string fixtureProbe = ProtonControlsReadiness.ProbeScriptForOrigin("https://hotfix2-controls.test.invalid");
            var fixtureSummary = Json.Deserialize<Dictionary<string, object>>(await fixtureCore.ExecuteScriptAsync(fixtureProbe));
            Check("owned DOM fixture actually reports a visible spinner and hidden sign-in", fixtureSummary != null &&
                Convert.ToString(fixtureSummary["ready"]) == "complete" && Convert.ToBoolean(fixtureSummary["spinner"]) && !Convert.ToBoolean(fixtureSummary["signInAction"]));
            Check("production readiness probe refuses a different origin", await fixtureCore.ExecuteScriptAsync(ProtonControlsReadiness.ProbeScript) == "null");
            window = fresh(); window.ControlsLoadTimeout = TimeSpan.FromMilliseconds(450);
            window.ControlsProbe = provider => fixtureCore.ExecuteScriptAsync(fixtureProbe);
            var age = Stopwatch.StartNew(); window.Show(); await Wait(() => window.ControlsError != null);
            Check("spinner with footer links is not mistaken for usable controls", !window.ControlsReady && !ProtonButton(window, "Check browser route").IsEnabled);
            Check("spinner reaches a bounded actionable error and Reload remains available", age.Elapsed < TimeSpan.FromSeconds(10) && ProtonButton(window, "Reload Proton controls").IsEnabled);
            window.ControlsProbe = null; window.ControlsLoadTimeout = TimeSpan.FromSeconds(15); await window.ShowProvider();
            Check("Reload recovers after a spinner timeout", window.ControlsReady && window.ControlsError == null);
            int routeBefore = routeChecks;
            window.ControlsProbe = provider => fixtureCore.ExecuteScriptAsync(fixtureProbe);
            ProtonButton(window, "Check browser route").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => window.ControlsError != null);
            Check("route check re-probes stale ready controls and refuses the spinner", routeChecks == routeBefore && !window.ControlsReady && !ProtonButton(window, "Check browser route").IsEnabled);
            window.Close(); window = null;

            window = fresh(); window.ControlsLoadTimeout = TimeSpan.FromMilliseconds(350);
            var delayed = new TaskCompletionSource<string>();
            window.ControlsProbe = provider => delayed.Task;
            window.Show(); await Wait(() => window.ControlsError != null);
            string before = window.ControlsError;
            delayed.SetResult("{\"ready\":\"complete\",\"spinner\":false,\"signInAction\":true,\"loggedAction\":false}");
            await Task.Delay(120);
            Check("late probe completion cannot turn a timed-out window ready", !window.ControlsReady && window.ControlsError == before && !ProtonButton(window, "Check browser route").IsEnabled);
            window.ControlsProbe = null; window.ControlsLoadTimeout = TimeSpan.FromSeconds(15); await window.ShowProvider();
            Check("new load succeeds after an abandoned readiness probe", window.ControlsReady && window.ControlsError == null);
        }
        finally
        {
            window?.Close();
            if (fixture != null) CloseTab(main, fixture);
            if (service.Enabled) await main.ChangeProtonPreview(false);
            main.ProtonProxyOwnershipValidator = guardValidator;
            main.ProtonVerificationLease = guardLease;
            main.AutomaticProtonMonitoringEnabled = guardMonitoring;
        }
    }

    private static Button ProtonButton(ProtonPreviewWindow window, string content)
    {
        foreach(var options in Visuals<Expander>(window))options.IsExpanded=true;
        window.UpdateLayout();
        return Visuals<Button>(window).Single(button => Convert.ToString(button.Content) == content);
    }

    private static async Task<bool> Hotfix2StorageCanary(CoreWebView2 core, string key, bool write)
    {
        string nonce = Guid.NewGuid().ToString("N");
        var completed = new TaskCompletionSource<bool>();
        EventHandler<CoreWebView2WebMessageReceivedEventArgs> received = (s, e) =>
        {
            if (!ProtonPreviewService.IsProviderPage(e.Source)) return;
            try
            {
                var reply = Json.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson);
                object channel, ok;
                if (reply != null && reply.TryGetValue("nonce", out channel) && Convert.ToString(channel) == nonce &&
                    reply.TryGetValue("ok", out ok) && ok is bool) completed.TrySetResult((bool)ok);
            }
            catch { }
        };
        core.WebMessageReceived += received;
        try
        {
            string script = "(()=>{const key=" + Json.Serialize(key) + ",nonce=" + Json.Serialize(nonce) + ";" +
                "const finish=ok=>chrome.webview.postMessage({nonce,ok:!!ok});" +
                "const read=()=>chrome.storage.local.get(key,v=>finish(!chrome.runtime.lastError&&v[key]===true));" +
                "try{" + (write ? "chrome.storage.local.set({[key]:true},()=>{if(chrome.runtime.lastError)finish(false);else read();});" : "read();") +
                "}catch{finish(false);}})()";
            await core.ExecuteScriptAsync(script);
            if (await Task.WhenAny(completed.Task, Task.Delay(10000)) != completed.Task) throw new TimeoutException("Own synthetic extension canary callback did not finish.");
            return await completed.Task;
        }
        finally { core.WebMessageReceived -= received; }
    }
}
