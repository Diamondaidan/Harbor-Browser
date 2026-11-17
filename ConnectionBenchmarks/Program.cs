using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using HarborBrowser.Services;
using Microsoft.Web.WebView2.Core;

internal static class ConnectionBenchmark
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1048576 };
    private const int TabCount = 5;
    private const int SamplesPerPhase = 5;
    private const string WorkloadHost = "harbor-connection-benchmark.invalid";
    private sealed class Proc
    { public int Id; public long Started; public string Role; public double Cpu; public long Working, Private; }
    private sealed class Sample
    {
        public string Phase; public int Index, Processes, WebViewProcesses, TorProcesses;
        public double CpuPercent, HostWorkingMB, HostPrivateMB, WebViewWorkingMB, WebViewPrivateMB, TorWorkingMB, TorPrivateMB, TotalWorkingMB, TotalPrivateMB;
    }
    private sealed class Result
    {
        public string Mode, TrialLabel, AppVersion, Runtime, Workload, ConnectionEvidence, ProviderControlsEvidence, Error, CleanupError;
        public int Trial, Tabs, SamplesPerPhase, LogicalProcessors, ProcessBits;
        public double EnvironmentStartupMs, ProviderSetupMs, WorkloadStartupMs;
        public List<double> ExternalNavigationMs = new List<double>();
        public List<string> ExternalNavigationErrors = new List<string>();
        public List<Sample> Samples = new List<Sample>();
        public bool Passed, CleanupPassed, WindowsProxyUnchanged;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 3 || !new[] { "direct", "tor", "proton-signed-out" }.Contains(args[1]))
        { Console.Error.WriteLine("Usage: HarborIntegrationTests.exe <new-output-directory> <direct|tor|proton-signed-out> <trial-number> [--latency]"); return 2; }
        string output = Path.GetFullPath(args[0]); string mode = args[1]; int trial;
        if (!int.TryParse(args[2], out trial) || trial < 1) return 2;
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        { Console.Error.WriteLine("Refusing a nonempty benchmark directory; every case needs a fresh profile."); return 2; }
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new Window { Title = "Harbor isolated connection benchmark", Width = 900, Height = 600, Left = -20000, Top = 20, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false };
        int exit = 1; bool started = false;
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        timeout.Tick += (s, e) => { Console.WriteLine("FAIL benchmark exceeded five minutes"); app.Shutdown(1); };
        window.Loaded += async (s, e) =>
        {
            if (started) return; started = true;
            try { exit = await Run(window, output, mode, trial, args.Contains("--latency")) ? 0 : 1; }
            catch (Exception ex) { Console.WriteLine("FAIL " + ex.GetType().Name + ": " + ex.Message); }
            finally { timeout.Stop(); window.Close(); app.Shutdown(exit); }
        };
        timeout.Start(); window.Show(); app.Run(); return exit;
    }

    private static async Task<bool> Run(Window window, string output, string mode, int trial, bool latency)
    {
        string beforeProxy = ProxySnapshot();
        var result = new Result
        {
            Mode = mode, Trial = trial, TrialLabel = mode == "proton-signed-out" ? "Extension enabled in a fresh signed-out profile; NOT a connected Proton VPN benchmark" : mode,
            Tabs = TabCount, SamplesPerPhase = SamplesPerPhase, LogicalProcessors = Environment.ProcessorCount, ProcessBits = IntPtr.Size * 8,
            AppVersion = typeof(BrowserVpnService).Assembly.GetName().Version.ToString(),
            Workload = "Five local virtual-host documents, each with 8 MiB touched memory and 300 DOM rows. Idle has no page timers. Active explicitly invokes eight ms of JavaScript work per page approximately every 100 ms. This measures fixed connection/background cost and local script work, not encrypted network throughput."
        };
        CoreWebView2Environment environment = null; CoreWebView2Controller providerController = null;
        var controllers = new List<CoreWebView2Controller>(); BrowserVpnService tor = null; ProtonPreviewService proton = null;
        CoreWebView2Profile profile = null;
        string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "BrowserVpn");
        var owner = new WindowInteropHelper(window).Handle;
        try
        {
            var watch = Stopwatch.StartNew();
            environment = await Limit(CoreWebView2Environment.CreateAsync(null, Path.Combine(output, "WebView2"), new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = true }), 30000);
            var first = await Create(environment, owner); controllers.Add(first); profile = first.CoreWebView2.Profile;
            result.EnvironmentStartupMs = watch.Elapsed.TotalMilliseconds; result.Runtime = environment.BrowserVersionString;
            watch.Restart();
            if (mode == "tor")
            {
                tor = new BrowserVpnService(output, assets);
                await tor.Enable(profile, environment, owner);
                if (!tor.RouteVerified || !tor.Connected || tor.HelperProcessId == 0) throw new Exception("Tor route/helper was not verified.");
                result.ConnectionEvidence = "Actual Tor service startup completed its same-profile raw HTTPS IsTor proof. Owned tor.exe is included in every resource sample.";
            }
            else if (mode == "proton-signed-out")
            {
                proton = new ProtonPreviewService(output, Path.Combine(assets, "Proton"));
                await proton.Enable(profile);
                providerController = await Create(environment, owner);
                await Navigate(providerController.CoreWebView2, ProtonPreviewService.PopupUrl);
                bool rendered = false;
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    string state = await providerController.CoreWebView2.ExecuteScriptAsync(@"(()=>{const visible=id=>{const e=document.getElementById(id);return !!e&&e.getClientRects().length>0&&getComputedStyle(e).visibility!=='hidden';};return {signIn:visible('sign-in-view'),authenticated:visible('logged-view')};})()");
                    var value = Json.Deserialize<Dictionary<string, object>>(state);
                    if (value != null && value.TryGetValue("authenticated", out var authenticated) && authenticated is bool && (bool)authenticated)
                        throw new InvalidOperationException("Fresh signed-out benchmark unexpectedly has an authenticated provider UI.");
                    if (value != null && value.TryGetValue("signIn", out var signIn) && signIn is bool && (bool)signIn) { rendered = true; break; }
                    await Task.Delay(200);
                }
                result.ProviderControlsEvidence = rendered ? "Actual sign-in view visible; authenticated view not visible. Controls closed before steady samples in all modes." : "Sign-in UI did not confirm readiness; only extension-registration overhead was measured.";
                result.ConnectionEvidence = "Fresh profile, no account login or connected Proton session. These samples cannot establish Proton-connected overhead, speed or privacy.";
                providerController.Close(); providerController = null;
            }
            else result.ConnectionEvidence = "Fresh profile without Tor/Proton extension. No Windows network settings changed.";
            result.ProviderSetupMs = watch.Elapsed.TotalMilliseconds;
            if (latency && mode != "proton-signed-out")
            {
                for (int request = 0; request < 3; request++)
                {
                    watch.Restart();
                    try { await Navigate(first.CoreWebView2, "https://example.com/?harbor-connection-benchmark=" + Guid.NewGuid().ToString("N")); result.ExternalNavigationMs.Add(watch.Elapsed.TotalMilliseconds); }
                    catch (Exception ex) { result.ExternalNavigationErrors.Add(ex.GetType().Name + ": " + ex.Message); }
                }
            }
            watch.Restart();
            string fixture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            for (int tab = 0; tab < TabCount; tab++)
            {
                var controller = tab == 0 ? first : await Create(environment, owner); if (tab > 0) controllers.Add(controller);
                controller.CoreWebView2.SetVirtualHostNameToFolderMapping(WorkloadHost, fixture, CoreWebView2HostResourceAccessKind.DenyCors);
                await Navigate(controller.CoreWebView2, "https://" + WorkloadHost + "/workload.html?tab=" + tab);
                if (await controller.CoreWebView2.ExecuteScriptAsync("memory.length===8*1024*1024&&document.querySelectorAll('#rows p').length===300") != "true") throw new Exception("Local workload did not initialize.");
            }
            result.WorkloadStartupMs = watch.Elapsed.TotalMilliseconds;
            await Task.Delay(1500);
            result.Samples.AddRange(await Measure(environment, tor, "Idle"));
            using (var active = new CancellationTokenSource())
            {
                var work = RunWork(controllers, active.Token);
                try { await Task.Delay(1000); result.Samples.AddRange(await Measure(environment, tor, "Active")); }
                finally { active.Cancel(); await work; }
            }
            foreach (var controller in controllers)
                if (await controller.CoreWebView2.ExecuteScriptAsync("benchmarkTicks>0&&memory.length===8*1024*1024") != "true") throw new Exception("Active workload failed to run.");
            result.Passed = result.Samples.Count == SamplesPerPhase * 2;
        }
        catch (Exception ex) { result.Error = ex.GetType().Name + ": " + ex.Message; result.Passed = false; }
        finally
        {
            try
            {
                if (tor != null && profile != null && tor.Enabled) await tor.Remove(profile);
                if (proton != null && profile != null && proton.Enabled) await proton.Remove(profile);
                if (tor != null && tor.HelperProcessId != 0) throw new Exception("Owned Tor helper remained after disconnect.");
                result.CleanupPassed = true;
            }
            catch (Exception ex) { result.CleanupError = ex.GetType().Name + ": " + ex.Message; result.CleanupPassed = false; result.Passed = false; }
            finally
            {
                tor?.Dispose(); proton?.Dispose();
                try { providerController?.Close(); } catch { }
                foreach (var controller in controllers) try { controller.Close(); } catch { }
            }
            result.WindowsProxyUnchanged = beforeProxy == ProxySnapshot();
            if (!result.WindowsProxyUnchanged) { result.Passed = false; result.CleanupError = "Windows proxy snapshot changed unexpectedly."; }
            File.WriteAllText(Path.Combine(output, "measurements.json"), Json.Serialize(result));
            Console.WriteLine((result.Passed ? "PASS" : "FAIL") + " " + mode + " trial=" + trial + " samples=" + result.Samples.Count + (result.Error == null ? "" : " " + result.Error));
        }
        return result.Passed;
    }

    private static async Task RunWork(List<CoreWebView2Controller> controllers, CancellationToken cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                await Task.WhenAll(controllers.Select(c => c.CoreWebView2.ExecuteScriptAsync("benchmarkWork()")));
                await Task.Delay(100, cancellation);
            }
        }
        catch (OperationCanceledException) { }
    }
    private static List<Proc> Processes(CoreWebView2Environment environment, BrowserVpnService tor)
    {
        int host = Process.GetCurrentProcess().Id, helper = tor?.HelperProcessId ?? 0;
        if (tor != null && (!tor.RouteVerified || helper == 0)) throw new Exception("Tor lost verification/helper during measurement.");
        var ids = new HashSet<int>(environment.GetProcessInfos().Select(p => p.ProcessId)); ids.Add(host); if (helper != 0) ids.Add(helper);
        var result = new List<Proc>();
        foreach (int id in ids)
        {
            try
            {
                using (var process = Process.GetProcessById(id))
                {
                    if (id == helper && !string.Equals(process.ProcessName, "tor", StringComparison.OrdinalIgnoreCase)) throw new Exception("Owned helper process identity changed.");
                    result.Add(new Proc { Id = id, Started = process.StartTime.ToUniversalTime().Ticks, Role = id == host ? "Host" : id == helper ? "Tor" : "WebView", Cpu = process.TotalProcessorTime.TotalSeconds, Working = process.WorkingSet64, Private = process.PrivateMemorySize64 });
                }
            }
            catch (ArgumentException) { if (id == helper) throw; }
            catch (InvalidOperationException) { if (id == helper) throw; }
        }
        if (helper != 0 && !result.Any(p => p.Role == "Tor")) throw new Exception("Tor helper missing from resource accounting.");
        return result;
    }
    private static async Task<List<Sample>> Measure(CoreWebView2Environment environment, BrowserVpnService tor, string phase)
    {
        var result = new List<Sample>(); var previous = Processes(environment, tor); long before = Stopwatch.GetTimestamp();
        for (int sample = 0; sample < SamplesPerPhase; sample++)
        {
            await Task.Delay(1000); var current = Processes(environment, tor); long now = Stopwatch.GetTimestamp(); double elapsed = (now - before) / (double)Stopwatch.Frequency;
            double cpu = current.Sum(p => { var old = previous.FirstOrDefault(o => o.Id == p.Id && o.Started == p.Started); return old == null ? 0 : Math.Max(0, p.Cpu - old.Cpu); });
            Func<string, bool, double> memory = (role, working) => current.Where(p => p.Role == role).Sum(p => working ? p.Working : p.Private) / 1048576.0;
            result.Add(new Sample { Phase = phase, Index = sample + 1, Processes = current.Count, WebViewProcesses = current.Count(p => p.Role == "WebView"), TorProcesses = current.Count(p => p.Role == "Tor"), CpuPercent = Math.Max(0, Math.Min(100, cpu / elapsed / Environment.ProcessorCount * 100)), HostWorkingMB = memory("Host", true), HostPrivateMB = memory("Host", false), WebViewWorkingMB = memory("WebView", true), WebViewPrivateMB = memory("WebView", false), TorWorkingMB = memory("Tor", true), TorPrivateMB = memory("Tor", false), TotalWorkingMB = current.Sum(p => p.Working) / 1048576.0, TotalPrivateMB = current.Sum(p => p.Private) / 1048576.0 });
            previous = current; before = now;
        }
        return result;
    }
    private static async Task<CoreWebView2Controller> Create(CoreWebView2Environment environment, IntPtr owner)
    {
        var creation = environment.CreateCoreWebView2ControllerAsync(owner);
        CoreWebView2Controller controller;
        try { controller = await Limit(creation, 20000); }
        catch { _ = CloseLate(creation); throw; }
        controller.IsVisible = false; controller.Bounds = new System.Drawing.Rectangle(0, 0, 800, 500);
        controller.CoreWebView2.Settings.AreDevToolsEnabled = false; controller.CoreWebView2.Settings.IsStatusBarEnabled = false;
        return controller;
    }
    private static async Task CloseLate(Task<CoreWebView2Controller> task) { try { (await task).Close(); } catch { } }
    private static async Task Navigate(CoreWebView2 core, string target)
    {
        ulong? id = null; var done = new TaskCompletionSource<bool>();
        EventHandler<CoreWebView2NavigationStartingEventArgs> starting = (s, e) => { if (e.Uri == target) id = e.NavigationId; };
        EventHandler<CoreWebView2NavigationCompletedEventArgs> completed = (s, e) => { if (id == e.NavigationId) { if (e.IsSuccess) done.TrySetResult(true); else done.TrySetException(new Exception("Navigation failed: " + e.WebErrorStatus)); } };
        core.NavigationStarting += starting; core.NavigationCompleted += completed;
        try { core.Navigate(target); await Limit(done.Task, 30000); }
        finally { core.NavigationStarting -= starting; core.NavigationCompleted -= completed; }
    }
    private static async Task<T> Limit<T>(Task<T> task, int milliseconds)
    {
        if (await Task.WhenAny(task, Task.Delay(milliseconds)) != task)
        { _ = task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); throw new TimeoutException("Benchmark operation timed out."); }
        return await task;
    }
    private static string ProxySnapshot()
    {
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
            return string.Join("|", new[] { "ProxyEnable", "ProxyServer", "ProxyOverride", "AutoConfigURL" }.Select(n => n + "=" + Convert.ToString(key?.GetValue(n))));
    }
}
