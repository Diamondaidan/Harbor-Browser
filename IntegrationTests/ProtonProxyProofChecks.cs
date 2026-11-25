using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Interop;
using HarborBrowser;
using HarborBrowser.Services;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    // The public manifest key supplies only the extension ID. This fixture is
    // separate test code in a new signed-out native profile, never the provider
    // package or the user's profile. It returns only callback success booleans.
    private static async Task ProtonProxyProofChecks(MainWindow main, string output)
    {
        string workspace = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
        string root = Path.Combine(workspace, "B6P", Guid.NewGuid().ToString("N").Substring(0, 8));
        if (Directory.Exists(root)) throw new InvalidOperationException("Use a new owned proxy-proof profile.");
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, ".owned-proxy-proof"), "Synthetic Chrome proxy settings only; no login.");
        string fixture = Path.Combine(output, "OwnedProxyProofFixture"); Directory.CreateDirectory(fixture);
        var providerManifest = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "BrowserVpn", "Proton", "manifest.json")));
        File.WriteAllText(Path.Combine(fixture, "manifest.json"), Json.Serialize(new Dictionary<string, object> {
            { "manifest_version", 3 }, { "name", "Harbor owned proxy proof fixture" }, { "version", "0.1.0" },
            { "key", providerManifest["key"] }, { "permissions", new[] { "proxy", "privacy" } }
        }));
        File.WriteAllText(Path.Combine(fixture, "proof.html"), "<!doctype html><html><title>Owned proxy proof fixture</title><body>Harbor synthetic settings proof</body></html>");
        var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(root, "WebView2"), new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = true });
        var controller = await environment.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(main).Handle); controller.IsVisible = false;
        var core = controller.CoreWebView2; CoreWebView2BrowserExtension extension = null;
        try
        {
            extension = await core.Profile.AddBrowserExtensionAsync(fixture);
            Check("proxy-proof fixture has only the expected public extension identity", extension.Id == ProtonPreviewService.ExtensionId);
            await AuthNavigate(core, "chrome-extension://" + extension.Id + "/proof.html");
            Check("owned proxy-proof fixture starts without an active proxy", !await ProtonRouteProbe.HasOwnedProxy(core));

            await BetaProxySet(core, new { mode = "fixed_servers", rules = new { singleProxy = new { scheme = "https", host = "proxy-proof.test.invalid", port = 443 }, bypassList = new[] { "account.proton.me", "localhost", "127.0.0.1" } } });
            if (!await BetaProxyOperation(core, "chrome.privacy.network.webRTCIPHandlingPolicy.set({scope:'regular',value:'default'},()=>finish(!chrome.runtime.lastError));"))
                throw new InvalidOperationException("Owned WebRTC policy positive control could not be set.");
            var unprotectedRtc = await ProtonRouteProbe.ReadProxyProof(core);
            Check("an owned proxy without UDP privacy protection is rejected", unprotectedRtc.Owned && !unprotectedRtc.Supported);
            if (!await BetaProxyOperation(core, "chrome.privacy.network.webRTCIPHandlingPolicy.set({scope:'regular',value:'disable_non_proxied_udp'},()=>finish(!chrome.runtime.lastError));"))
                throw new InvalidOperationException("Owned WebRTC policy proof could not be set.");
            var fixedProof = await ProtonRouteProbe.ReadProxyProof(core);
            Check("native fixed HTTPS configuration with expected exclusions and UDP policy is recognized", fixedProof.Owned && fixedProof.Supported);

            await BetaProxySet(core, new { mode = "fixed_servers", rules = new { singleProxy = new { scheme = "http", host = "proxy-proof.test.invalid", port = 443 }, bypassList = new[] { "localhost" } } });
            var insecure = await ProtonRouteProbe.ReadProxyProof(core);
            Check("an owned HTTP proxy is not sufficient protected-route evidence", insecure.Owned && !insecure.Supported && !await ProtonRouteProbe.HasOwnedProxy(core));
            var unsupported = await ProtonRouteProbe.CompareDetailed(environment, core, new WindowInteropHelper(main).Handle, Path.Combine(root, "UnusedControl"));
            Check("unsupported proxy shape returns a typed negative result before address requests", !unsupported.Verified);

            await BetaProxySet(core, new { mode = "fixed_servers", rules = new { singleProxy = new { scheme = "https", host = "127.0.0.1", port = 9 }, bypassList = new[] { "localhost" } } });
            var waiter = await ProtonRouteProbe.ReadProxyProof(core);
            Check("a localhost startup waiter cannot be described as protected", waiter.Owned && !waiter.Supported);

            await BetaProxySet(core, new { mode = "pac_script", pacScript = new { data = "function FindProxyForURL(url,host){return 'DIRECT';}" } });
            var pac = await ProtonRouteProbe.ReadProxyProof(core);
            Check("PAC and split-tunnel configurations are rejected by the beta guard", pac.Owned && !pac.Supported);

            await BetaProxySet(core, new { mode = "fixed_servers", rules = new { singleProxy = new { scheme = "https", host = "proxy-proof.test.invalid", port = 443 }, bypassList = new[] { "localhost", "unreviewed-bypass.test.invalid" } } });
            var bypass = await ProtonRouteProbe.ReadProxyProof(core);
            Check("an unreviewed direct-routing bypass is not supported evidence", bypass.Owned && !bypass.Supported);

            await BetaProxySet(core, new { mode = "direct" });
            var direct = await ProtonRouteProbe.ReadProxyProof(core);
            Check("direct mode cannot qualify as an active protected proxy", !direct.Owned && !direct.Supported);
            var directResult = await ProtonRouteProbe.CompareDetailed(environment, core, new WindowInteropHelper(main).Handle, Path.Combine(root, "UnusedControl"));
            Check("a disconnected provider returns a typed negative route result", !directResult.Verified);

            await AuthNavigate(core, "about:blank");
            var wrongOrigin = await ProtonRouteProbe.ReadProxyProof(core);
            Check("proxy proof refuses a document outside the provider origin", !wrongOrigin.Owned && !wrongOrigin.Supported);
            File.WriteAllText(Path.Combine(output, "proxy-proof-scope.json"), Json.Serialize(new { SeparateOwnedProfile = true, RealProxyApis = true, PublicManifestKeyOnly = true, ProviderPackageChanged = false, ExternalRequests = false, AccountLogin = false, RawConfigurationLogged = false }));
        }
        finally
        {
            try
            {
                if (extension != null)
                {
                    await AuthNavigate(core, "chrome-extension://" + extension.Id + "/proof.html");
                    await BetaProxyOperation(core, "chrome.proxy.settings.clear({scope:'regular'},()=>finish(!chrome.runtime.lastError));");
                    await BetaProxyOperation(core, "chrome.privacy.network.webRTCIPHandlingPolicy.clear({scope:'regular'},()=>finish(!chrome.runtime.lastError));");
                    await extension.RemoveAsync();
                }
            }
            finally
            {
                var ended = new TaskCompletionSource<bool>(); environment.BrowserProcessExited += (s, e) => ended.TrySetResult(true);
                controller.Close(); await Task.WhenAny(ended.Task, Task.Delay(15000));
            }
        }
    }
    private static async Task BetaProxySet(CoreWebView2 core, object value)
    {
        if (!await BetaProxyOperation(core, "chrome.proxy.settings.set({scope:'regular',value:" + Json.Serialize(value) + "},()=>finish(!chrome.runtime.lastError));"))
            throw new InvalidOperationException("The owned native proxy fixture could not apply its synthetic settings.");
    }
    private static async Task<bool> BetaProxyOperation(CoreWebView2 core, string operation)
    {return await OwnedProxyOperation(core,ProtonPreviewService.ExtensionId,operation);}
    private static async Task<bool> OwnedProxyOperation(CoreWebView2 core,string extensionId,string operation)
    {
        string nonce = Guid.NewGuid().ToString("N"); var completed = new TaskCompletionSource<bool>();
        EventHandler<CoreWebView2WebMessageReceivedEventArgs> reply = (s, e) =>
        {
            Uri source;if(!Uri.TryCreate(e.Source,UriKind.Absolute,out source)||source.Scheme!="chrome-extension"||source.Host!=extensionId)return;
            try
            {
                var data = Json.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson); object channel, ok;
                if (data != null && data.TryGetValue("nonce", out channel) && Convert.ToString(channel) == nonce && data.TryGetValue("ok", out ok) && ok is bool)
                    completed.TrySetResult((bool)ok);
            }
            catch { }
        };
        core.WebMessageReceived += reply;
        try
        {
            await core.ExecuteScriptAsync("(()=>{const finish=ok=>chrome.webview.postMessage({nonce:" + Json.Serialize(nonce) + ",ok:!!ok});try{" + operation + "}catch{finish(false);}})()");
            if (await Task.WhenAny(completed.Task, Task.Delay(5000)) != completed.Task) throw new TimeoutException("Owned proxy fixture callback did not complete.");
            return await completed.Task;
        }
        finally { core.WebMessageReceived -= reply; }
    }
}
