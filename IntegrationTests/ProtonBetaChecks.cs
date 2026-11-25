using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private const string BetaOwnedMarker = ".harbor-beta-guard-owned";
    private static readonly string[] BetaRequestKinds = { "fetch", "frame", "image", "worker", "service-worker", "websocket" };
    private static readonly List<string> BetaGuardFailures=new List<string>();
    private static void BetaCounterCheck(string name,bool value)
    {
        if(value)Check(name,true);
        else { BetaGuardFailures.Add(name);Console.WriteLine("FAIL "+name+" (recorded; remaining independent checks continue)"); }
    }
    private static void FinishBetaCounterChecks(string output)
    {
        File.WriteAllText(Path.Combine(output,"beta-guard-failures.json"),Json.Serialize(BetaGuardFailures));
        Check("all request-counter protection assertions pass",BetaGuardFailures.Count==0);
    }

    private static async Task BetaSocketBlockCheck(CoreWebView2 core)
    {
        using(var server=new BetaGuardFixtureServer())
        {
            await BetaNavigate(core,server.Url("socket-block/page"));
            await WaitJs(core,"betaFixture.ready&&betaFixture.workerReady&&betaFixture.swReady");
            await core.CallDevToolsProtocolMethodAsync("Network.enable","{}");
            await core.CallDevToolsProtocolMethodAsync("Network.setBlockedURLs","{\"urls\":[\"ws://*\",\"wss://*\"]}");
            await core.ExecuteScriptAsync("betaRequests('socket-block')");
            await Task.Delay(1200);
            Check("native URL blocking stops a new WebSocket handshake",server.Count("socket-block/websocket")==0);
        }
    }

    private static void ProtonBetaStateChecks()
    {
        var state = new ProtonProtectionState(false);
        var clock = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        state.UtcNow = () => clock;
        Check("beta protection is off before the user enables it", state.State == ProtonProtectionPhase.Off && !state.Enabled);
        long epoch = state.Begin(true);
        Check("enabled intent alone cannot mark a route protected", state.Enabled && state.State != ProtonProtectionPhase.Protected);
        Check("ordinary HTTPS is blocked until verification", state.ShouldBlock("https://normal.test.invalid/page", false));
        Check("ordinary HTTP and WebSocket requests are classified for protection", state.ShouldBlock("http://normal.test.invalid/", false) && state.ShouldBlock("wss://normal.test.invalid/socket", false));
        Check("Harbor Home remains available while requests are paused", !state.ShouldBlock(HomePageService.Url, false));
        Check("an ordinary account tab has no automatic authentication exemption", state.ShouldBlock("https://account.proton.me/auth-ext?state=synthetic", false));
        Check("explicit trusted authentication permits the exact HTTPS account origin", !state.ShouldBlock("https://account.proton.me/auth-ext?state=synthetic", true));
        Check("trusted authentication cannot permit unrelated destinations", state.ShouldBlock("https://normal.test.invalid/", true) && state.ShouldBlock("https://account.proton.me.evil.invalid/", true));
        Check("authentication exception does not permit HTTP or user-info URLs", state.ShouldBlock("http://account.proton.me/", true) && state.ShouldBlock("https://user@account.proton.me/", true));
        const string challenge="https://account-api.proton.me/challenge/v4/html?Type=0&Name=unauth&Lang=en-US&Dir=ltr";
        Check("trusted sign-in permits the exact verification origin", !state.ShouldBlock(challenge,true));
        Check("verification origin requires explicit authentication capability", state.ShouldBlock(challenge,false));
        Check("verification exception rejects lookalikes and subdomains", state.ShouldBlock("https://account-api.proton.me.evil.invalid/",true)&&state.ShouldBlock("https://extra.account-api.proton.me/",true));
        Check("verification exception rejects HTTP and WebSockets",state.ShouldBlock("http://account-api.proton.me/",true)&&state.ShouldBlock("wss://account-api.proton.me/",true));
        Check("verification exception rejects custom ports and user information",state.ShouldBlock("https://account-api.proton.me:8443/",true)&&state.ShouldBlock("https://user@account-api.proton.me/",true));
        Check("a current verification epoch can protect ordinary requests", state.TryProtect(epoch, TimeSpan.FromSeconds(30)) && !state.ShouldBlock("https://normal.test.invalid/", false));
        clock = clock.AddSeconds(31);
        Check("verification expiry closes the gate and removes Protected", state.ShouldBlock("https://normal.test.invalid/", false) && state.State == ProtonProtectionPhase.Disconnected);
        Check("an expired verification result cannot reopen the gate", !state.TryProtect(epoch, TimeSpan.FromSeconds(30)));
        epoch = state.Begin(true); state.TryProtect(epoch, TimeSpan.FromSeconds(30));
        state.Invalidate("Synthetic network change", true);
        Check("a network-change epoch rejects a late successful route result", !state.TryProtect(epoch, TimeSpan.FromSeconds(30)) && state.ShouldBlock("https://normal.test.invalid/", false));
        state.Begin(false);
        Check("explicit protection off restores ordinary browsing", state.State == ProtonProtectionPhase.Off && !state.ShouldBlock("https://normal.test.invalid/", false));
    }

    private static async Task ProtonBetaChecks(MainWindow main, BrowserDataStore store, string output)
    {
        ProtonBetaStateChecks();
        using (var server = new BetaGuardFixtureServer())
        {
            await ProtonBetaColdStarts(server, output);
            var tab = (BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
            var core = tab.View.CoreWebView2;
            var observer = await main.EnsureProtonProtectionObserverAsync();
            Check("worker guard and visible tabs use the same native profile", observer.Profile.ProfileName == core.Profile.ProfileName && observer.Profile.ProfilePath == core.Profile.ProfilePath);
            var validator = main.ProtonProxyOwnershipValidator;
            var lease = main.ProtonVerificationLease;
            var clock = main.ProtonProtection.UtcNow;
            bool monitoring = main.AutomaticProtonMonitoringEnabled;
            bool proxyOwned = false;
            var evidence = new Dictionary<string, Dictionary<string, int>>();
            try
            {
                main.ProtonProxyOwnershipValidator = cancellation => Task.FromResult(proxyOwned);
                main.AutomaticProtonMonitoringEnabled = false;
                await BetaNavigate(core, server.Url("warm/page"));
                await WaitJs(core, "betaFixture.ready&&betaFixture.workerReady&&betaFixture.swReady");
                await core.ExecuteScriptAsync("betaRequests('off-control')");
                await Task.Delay(1400);
                evidence["off-control"] = server.Snapshot("off-control");
                foreach (string kind in BetaRequestKinds)
                    Check("off positive control reaches the server: " + kind, server.Count("off-control/" + kind) > 0);
                await Gesture(core, "betaDownload('off-download')");
                await Wait(() => server.Count("off-download/download") > 0);
                Check("off positive control reaches the native download endpoint", true);
                await Wait(() => !((IEnumerable<DownloadEntry>)Field(main, "_downloads")).Any(d => d.IsActive));

                // Arm while scheduled document/worker/SW requests are still pending.
                // The SW's waitUntil task can outlive the page and is counted at the server.
                await core.ExecuteScriptAsync("betaRequests('armed',1200)");
                await WaitJs(core, "betaFixture.swScheduledPrefix==='armed'");
                await main.ChangeProtonPreview(true);
                await Task.Delay(1700);
                evidence["armed"] = server.Snapshot("armed");
                SaveBetaCoverage(output, evidence);
                Check("provider enable leaves ordinary browsing paused", main.ProtonProtection.Enabled && main.ProtonProtection.State != ProtonProtectionPhase.Protected);
                await Wait(() => core.Source == "about:blank");
                Check("arming unloads the ordinary document instead of leaving its scripts active", core.Source == "about:blank" && !String.IsNullOrEmpty(tab.ProtonBlockedUrl));
                foreach (string kind in BetaRequestKinds)
                    BetaCounterCheck("armed request never reaches the server: " + kind, server.Count("armed/" + kind) == 0);

                // Native routing must change too. A host-only boolean leaves
                // Chromium's existing direct route intact and does not model
                // the provider's real settings.clear disconnection behavior.
                ((CoreWebView2Controller)Field(main,"_protonObserver")).Bounds=new System.Drawing.Rectangle(0,0,700,600);
                await AuthNavigate(observer,ProtonPreviewService.PopupUrl);
                for(int attempt=0;attempt<100&&!ProtonControlsReadiness.IsReady(await observer.ExecuteScriptAsync(ProtonControlsReadiness.ProbeScript));attempt++)await Task.Delay(100);
                Check("owned profile provider reaches signed-out controls before synthetic route changes",ProtonControlsReadiness.IsReady(await observer.ExecuteScriptAsync(ProtonControlsReadiness.ProbeScript)));
                await BetaProxySet(observer,new{mode="direct"});
                proxyOwned = true; main.ProtonVerificationLease = TimeSpan.FromMinutes(2);
                long verifiedEpoch = main.ProtonProtection.Epoch;
                Check("synthetic owned-route evidence opens the current gate", main.MarkProtonRouteVerified(verifiedEpoch));
                await BetaNavigate(core, server.Url("verified/page")); await WaitJs(core, "betaFixture.ready&&betaFixture.workerReady&&betaFixture.swReady");
                await core.ExecuteScriptAsync("betaRequests('verified')");
                await Task.Delay(1300);
                evidence["verified"] = server.Snapshot("verified");
                SaveBetaCoverage(output, evidence);
                foreach (string kind in BetaRequestKinds)
                    Check("verified request reaches the server: " + kind, server.Count("verified/" + kind) > 0);

                Check("synthetic loss clears the real provider proxy preference",await BetaProxyOperation(observer,"chrome.proxy.settings.clear({scope:'regular'},()=>finish(!chrome.runtime.lastError));"));
                proxyOwned = false;
                await core.ExecuteScriptAsync("betaRequests('lost-proxy')");
                await Task.Delay(1200);
                evidence["lost-proxy"] = server.Snapshot("lost-proxy");
                SaveBetaCoverage(output, evidence);
                Check("fresh proxy-ownership failure removes Protected", main.ProtonProtection.State != ProtonProtectionPhase.Protected);
                await Wait(() => core.Source == "about:blank");
                foreach (string kind in BetaRequestKinds)
                    BetaCounterCheck("lost-proxy request never reaches the server: " + kind, server.Count("lost-proxy/" + kind) == 0);

                await BetaProxySet(observer,new{mode="direct"});
                proxyOwned = true;
                var fakeNow = DateTime.UtcNow; main.ProtonProtection.UtcNow = () => fakeNow;
                main.ProtonVerificationLease = TimeSpan.FromSeconds(1);
                long expiredEpoch = main.ProtonProtection.Epoch;
                Check("current epoch can receive a bounded verification lease", main.MarkProtonRouteVerified(expiredEpoch));
                await BetaNavigate(core, server.Url("lease/page")); await WaitJs(core, "betaFixture.ready&&betaFixture.workerReady&&betaFixture.swReady");
                await core.ExecuteScriptAsync("betaRequests('expired',1000)");
                await WaitJs(core, "betaFixture.swScheduledPrefix==='expired'");
                fakeNow = fakeNow.AddSeconds(2);
                var expiredState = main.ProtonProtection.State;
                await Task.Delay(1300);
                evidence["expired"] = server.Snapshot("expired");
                SaveBetaCoverage(output, evidence);
                Check("expired evidence cannot be reused to mark Protected", !main.MarkProtonRouteVerified(expiredEpoch));
                Check("lease expiry unloads the previous ordinary document", core.Source == "about:blank");
                foreach (string kind in BetaRequestKinds)
                    BetaCounterCheck("expired-lease request never reaches the server: " + kind, server.Count("expired/" + kind) == 0);
                main.ProtonProtection.UtcNow = clock; main.ProtonVerificationLease = TimeSpan.FromMinutes(2);

                long networkEpoch = main.ProtonProtection.Epoch;
                Check("new route proof can protect again after expiry", main.MarkProtonRouteVerified(networkEpoch));
                await BetaNavigate(core, server.Url("network/page")); await WaitJs(core, "betaFixture.ready&&betaFixture.workerReady&&betaFixture.swReady");
                await core.ExecuteScriptAsync("betaRequests('network-change',1000)");
                await WaitJs(core, "betaFixture.swScheduledPrefix==='network-change'");
                main.InvalidateProtonProtection("Synthetic network transition", true);
                Check("network transition rejects a late route-verification success", !main.MarkProtonRouteVerified(networkEpoch));
                await Task.Delay(1300);
                evidence["network-change"] = server.Snapshot("network-change");
                SaveBetaCoverage(output, evidence);
                foreach (string kind in BetaRequestKinds)
                    BetaCounterCheck("network-change request never reaches the server: " + kind, server.Count("network-change/" + kind) == 0);

                Check("new proof opens only the gate without replaying old destinations", main.MarkProtonRouteVerified(main.ProtonProtection.Epoch));
                await BetaNavigate(core, server.Url("late/page")); await WaitJs(core, "betaFixture.ready&&betaFixture.workerReady&&betaFixture.swReady");
                await BetaLateOwnershipCheck(main, core, server);
                core.Navigate(server.Url("paused-download/download")); await Task.Delay(500);
                Check("a paused native download does not reach the server", server.Count("paused-download/download") == 0);

                // A real owned-file write failure occurs after native extension removal.
                // The intent file must remain on and the host gate must stay closed.
                bool removeFailed = false;
                using (var intentLock = File.Open(Path.Combine(store.RootFolder, "proton-preview.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
                { try { await main.ChangeProtonPreview(false); } catch (IOException) { removeFailed = true; } }
                Check("failed explicit removal does not silently open the regular route", removeFailed && main.ProtonProtection.Enabled && main.ProtonProtection.State != ProtonProtectionPhase.Off);
                Check("failed removal still blocks ordinary destinations", main.ProtonProtection.ShouldBlock(server.Url("remove-failed"), false));
                bool guardOffFailed=false;
                using(var guardIntentLock=File.Open(Path.Combine(store.RootFolder,"browser-protection.txt"),FileMode.Open,FileAccess.Read,FileShare.None))
                {try{await main.ChangeProtonPreview(false);}catch(IOException){guardOffFailed=true;}}
                Check("failed fallback-off persistence leaves the host armed after provider removal",guardOffFailed&&main.ProtonProtection.Enabled&&!((ProtonPreviewService)Field(main,"_protonPreview")).Enabled);
                await FallbackSocket(core,server,"failed-guard-off");
                Check("failed fallback-off persistence restores native WebSocket blocking",server.Count("failed-guard-off/websocket")==0);
                var recoveryControls=main.OpenProtonPreview();Offscreen(recoveryControls);
                Check("turn-off recovery remains available when provider is removed but fallback is armed",((Button)typeof(ProtonPreviewWindow).GetField("_remove",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(recoveryControls)).IsEnabled);
                recoveryControls.Close();
                await main.ChangeProtonPreview(false);
                Check("successful explicit off permits the regular route", main.ProtonProtection.State == ProtonProtectionPhase.Off);
                await Task.Delay(300);
                Check("explicit off does not replay a paused download automatically", server.Count("paused-download/download") == 0);
                Check("successful explicit off restores a functional WebSocket in the same context",await FallbackSocket(core,server,"off-socket-control"));
                await BetaNavigate(core, server.Url("off-again/page"));
                Check("ordinary navigation returns only after successful explicit off", server.Count("off-again/page") == 1);
            }
            finally
            {
                SaveBetaCoverage(output, evidence);
                main.ProtonProxyOwnershipValidator = validator; main.ProtonVerificationLease = lease; main.ProtonProtection.UtcNow = clock; main.AutomaticProtonMonitoringEnabled = monitoring;
                if (((ProtonPreviewService)Field(main, "_protonPreview")).Enabled) await main.ChangeProtonPreview(false);
            }
        }
    }

    private static async Task BetaLateOwnershipCheck(MainWindow main, CoreWebView2 core, BetaGuardFixtureServer server)
    {
        var started = new TaskCompletionSource<bool>(); var delayed = new TaskCompletionSource<bool>();
        main.ProtonProxyOwnershipValidator = cancellation => { started.TrySetResult(true); return delayed.Task; };
        long epoch = main.ProtonProtection.Epoch; Check("current evidence can precede a delayed ownership callback", main.MarkProtonRouteVerified(epoch));
        await core.ExecuteScriptAsync("fetch('/late-ownership/fetch',{cache:'no-store'}).catch(()=>{})");
        if (await Task.WhenAny(started.Task, Task.Delay(5000)) != started.Task) throw new TimeoutException("The native request did not ask for current proxy ownership.");
        main.InvalidateProtonProtection("Synthetic loss while ownership check was pending"); delayed.TrySetResult(true);
        await Task.Delay(500);
        Check("late successful ownership cannot release a request from an old epoch", server.Count("late-ownership/fetch") == 0 && main.ProtonProtection.State != ProtonProtectionPhase.Protected);
        main.ProtonProxyOwnershipValidator = cancellation => Task.FromResult(false);
    }

    private static void SaveBetaCoverage(string output, Dictionary<string, Dictionary<string, int>> evidence)
    { File.WriteAllText(Path.Combine(output, "beta-request-counts.json"), Json.Serialize(evidence)); }
    private static async Task BetaNavigate(CoreWebView2 core, string target)
    { await AuthNavigate(core, target); }

    private static async Task ProtonBetaColdStarts(BetaGuardFixtureServer server, string output)
    {
        string workspace = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
        string root = Path.Combine(workspace, "B6T", Guid.NewGuid().ToString("N").Substring(0, 8));
        if (Directory.Exists(root)) throw new InvalidOperationException("Use a new short synthetic cold profile.");
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, BetaOwnedMarker), Guid.NewGuid().ToString("N"));
        File.WriteAllText(Path.Combine(output, "beta-cold-profile.txt"), root);
        foreach (string mode in new[] { "seed", "restore" })
        {
            int before = server.Count("cold/page");
            string arguments = String.Join(" ", new[] { root, mode, "--proton-beta-host", server.Url("cold/page") }.Select(WindowsCommandRunner.Quote));
            using (var child = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
                if (!await Task.Run(() => child.WaitForExit(150000))) { try { child.Kill(); } catch { } throw new TimeoutException("Synthetic beta cold-start host timed out."); }
                string text = await stdout + await stderr; File.AppendAllText(Path.Combine(root, "child-output.log"), text);
                if (child.ExitCode != 0) throw new Exception("Synthetic beta " + mode + " failed: " + text);
                var results = Json.Deserialize<Dictionary<string, bool>>(File.ReadAllText(Path.Combine(root, mode + "-results.json")));
                foreach (var result in results) Check("beta cold " + mode + ": " + result.Key, result.Value);
            }
            Check(mode == "seed" ? "cold fixture has a real off-mode network positive control" : "cold enabled startup sends zero ordinary document requests", mode == "seed" ? server.Count("cold/page") > before : server.Count("cold/page") == before);
        }
    }

    private static int ProtonBetaHost(string[] args)
    {
        string root = Path.GetFullPath(args[0]), mode = args[1]; Uri target;
        if (!File.Exists(Path.Combine(root, BetaOwnedMarker)) || (mode != "seed" && mode != "restore") || args.Length != 4 ||
            !Uri.TryCreate(args[3], UriKind.Absolute, out target) || target.Scheme != "http" || target.Host != "127.0.0.1")
            throw new InvalidOperationException("Only an owned synthetic profile and loopback counter are allowed.");
        int exit = 1; var results = new Dictionary<string, bool>();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/HarborBrowser;component/Themes/Controls.xaml") });
        var store = new BrowserDataStore(Path.Combine(root, "Profile"), Path.Combine(root, "Downloads"));
        if (mode == "seed") store.SaveSettings(new BrowserSettings { RestorePreviousSession = true, HarborHomeIntroduced = true, OnlineSuggestionsEnabled = false });
        var main = Offscreen(new MainWindow(store));
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(120) };
        timer.Tick += (s, e) => app.Shutdown(3);
        main.Loaded += async (s, e) =>
        {
            try
            {
                await Wait(() => main.BrowserTabs.Items.Count > 0 && ((BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag).Initialized);
                var tab = (BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag; var core = tab.View.CoreWebView2;
                results["tab initializes without the old startup permission error"] = tab.FailureMessage == null;
                var service = (ProtonPreviewService)Field(main, "_protonPreview");
                if (mode == "seed")
                {
                    await BetaNavigate(core, target.AbsoluteUri); await WaitJs(core, "betaFixture.ready");
                    await main.ChangeProtonPreview(true);
                    await Wait(() => core.Source == "about:blank");
                    results["paused destination remains in the saved session"] = store.LoadSession().Tabs.Any(t => t.Url == target.AbsoluteUri);
                    await core.Profile.SetPermissionStateAsync(CoreWebView2PermissionKind.Geolocation, "https://beta-permission.test.invalid", CoreWebView2PermissionState.Allow);
                }
                else
                {
                    await Task.Delay(900);
                    results["persisted enabled intent starts paused rather than Protected"] = service.Enabled && main.ProtonProtection.Enabled && main.ProtonProtection.State != ProtonProtectionPhase.Protected;
                    results["the saved ordinary destination is actually restored as paused"] = main.BrowserTabs.Items.Cast<TabItem>().Select(i => i.Tag as BrowserTab).Any(t => t != null && t.ProtonPaused && t.ProtonBlockedUrl == target.AbsoluteUri);
                    var permissions = await core.Profile.GetNonDefaultPermissionSettingsAsync();
                    results["website permissions still reset on cold startup"] = !permissions.Any(x => x.PermissionKind == CoreWebView2PermissionKind.Geolocation && SitePermissionService.Origin(x.PermissionOrigin) == "https://beta-permission.test.invalid");
                    results["internal Proton notification permission survives startup"] = permissions.Any(x => x.PermissionKind == CoreWebView2PermissionKind.Notifications && x.PermissionState == CoreWebView2PermissionState.Allow && ProtonPreviewService.IsProviderPage(x.PermissionOrigin));
                }
                results["provider intent persists for the next process"] = service.Enabled;
                exit = results.Values.All(v => v) ? 0 : 1;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(root, mode + "-error.json"), Json.Serialize(new { type = error.GetType().FullName, error.Message })); Console.WriteLine("HOST ERROR " + error.GetType().Name + ": " + error.Message); }
            finally
            {
                timer.Stop(); var environment = (CoreWebView2Environment)Field(main, "_environment");
                var ended = new TaskCompletionSource<bool>(); if (environment != null) environment.BrowserProcessExited += (ss, ee) => ended.TrySetResult(true);
                var closed = new TaskCompletionSource<bool>(); main.Closed += (ss, ee) => closed.TrySetResult(true); main.Close();
                results["owned window closes cleanly"] = await Task.WhenAny(closed.Task, Task.Delay(15000)) == closed.Task;
                results["owned engine exits before the next process"] = environment == null || await Task.WhenAny(ended.Task, Task.Delay(15000)) == ended.Task;
                if (!results.Values.All(v => v)) exit = 1;
                File.WriteAllText(Path.Combine(root, mode + "-results.json"), Json.Serialize(results)); Console.WriteLine(Json.Serialize(results)); app.Shutdown(exit);
            }
        };
        timer.Start(); main.Show(); app.Run(); return exit;
    }
}
