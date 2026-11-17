using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task Proton055Checks(MainWindow main,BrowserDataStore store,string output)
    {
        var service=(ProtonPreviewService)Field(main,"_protonPreview");
        var tab=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;var core=tab.View.CoreWebView2;
        var before=(await core.Profile.GetBrowserExtensionsAsync()).ToDictionary(e=>e.Id,e=>e.IsEnabled);
        string proxyBefore=ProxySnapshot();ProtonPreviewWindow window=null;
        try{
            Check("stable Harbor uses its established instance identity",ProtonTrialIdentity.InstanceSuffix==""&&ProtonTrialIdentity.AppUserModelId=="HarborBrowser.Desktop");
            Check("test profile is the explicitly isolated folder",store.RootFolder==Path.GetFullPath(Path.Combine(output,"Profile")));
            Check("provider is off by default",!service.Enabled&&main.ProtonProtection.State==ProtonProtectionPhase.Off);
            service.ValidatePackage();Check("unmodified official Proton manifest/key/version validate",true);
            var settings=(HarborBrowser.Models.BrowserSettings)Field(main,"_settings");settings.OnlineSuggestionsEnabled=true;
            await main.ChangeProtonPreview(true);
            Check("explicit enable registers the actual Proton extension",service.Enabled&&(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==ProtonPreviewService.ExtensionId&&e.IsEnabled));
            Check("enabled intent persists separately in the trial profile",new ProtonPreviewService(store.RootFolder).Enabled);
            Check("enabled intent does not claim a verified connection",main.ProtonProtection.Enabled&&main.ProtonProtection.State!=ProtonProtectionPhase.Protected);
            Check("native online suggestions pause without changing preference",!(bool)typeof(MainWindow).GetProperty("AllowOnlineSuggestions",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(main)&&settings.OnlineSuggestionsEnabled);
            int count=main.BrowserTabs.Items.Count;AddTab(main,HomePageService.Url,true);
            Check("private tab creation is paused while provider is enabled",main.BrowserTabs.Items.Count==count);
            window=main.OpenProtonPreview();Offscreen(window);
            await Wait(()=>window.ControlsReady||window.ControlsError!=null);
            File.WriteAllText(Path.Combine(output,"controls-diagnostic.json"),Json.Serialize(new{window.ControlsReady,window.ControlsError,window.ControlsSummary}));
            Check("actual provider controls render without modifying provider APIs",window.ControlsReady);
            Check("ready provider controls do not promote the host protection state",window.ProtectionState!="Protected"&&main.ProtonProtection.State!=ProtonProtectionPhase.Protected);
            Check("route-check privacy notice is visible before authentication",Visuals<System.Windows.Controls.TextBlock>(window).Any(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Route verification privacy"&&t.Visibility==System.Windows.Visibility.Visible));
            Check("Proton controls receive Harbor's native dark title bar",WindowTitleBarService.ReadDarkCaption(window)==true);
            var provider=window.ProviderView.CoreWebView2;
            string diagnostic=await provider.ExecuteScriptAsync(@"(()=>{
                const visible=s=>{const e=document.querySelector(s);return !!e&&e.getClientRects().length>0&&getComputedStyle(e).visibility!=='hidden';};
                return {signIn:visible('#sign-in-view'),signInButton:visible('.sign-in-button'),logged:visible('#logged-view'),protectedLabel:visible('#protected-label'),requestApi:typeof globalThis.browser?.permissions?.request,containsApi:typeof globalThis.browser?.permissions?.contains,tabsApi:typeof globalThis.browser?.tabs?.query,permissionShim:!!globalThis.browser?.permissions?.harborAlreadyGranted};
            })()");
            File.WriteAllText(Path.Combine(output,"provider-api-diagnostic.json"),diagnostic);
            var state=Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(diagnostic);
            Check("official sign-in control is visible",Convert.ToBoolean(state["signInButton"]));
            Check("signed-out action intersects the available provider viewport",await provider.ExecuteScriptAsync(@"(()=>{if(location.protocol!=='chrome-extension:'||location.hostname!=='jplgfhpmjnbigmhklmmbgecoobifkmpa')return false;const b=document.querySelector('#sign-in-view .sign-in-button'),r=b?.getBoundingClientRect();return !!r&&r.width>0&&r.height>0&&r.bottom>0&&r.top<innerHeight&&r.right>0&&r.left<innerWidth;})()") == "true");
            Check("fresh trial is signed out",!Convert.ToBoolean(state["logged"])&&!Convert.ToBoolean(state["protectedLabel"]));
            Check("old permission shim is absent",!Convert.ToBoolean(state["permissionShim"]));
            using(var screenshot=File.Create(Path.Combine(output,"proton-signed-out.png")))await provider.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,screenshot);
            Check("signed-out provider does not claim an active proxy",!await ProtonRouteProbe.HasOwnedProxy(provider));
            for(int trial=1;trial<=3;trial++){
                window.Close();window=main.OpenProtonPreview();Offscreen(window);await Wait(()=>window.ControlsReady||window.ControlsError!=null);
                Check("provider controls reopen reliably "+trial,window.ControlsReady);
            }
            window.Close();window=null;
            await main.ChangeProtonPreview(false);
            Check("remove restores off and clears only provider registration",!service.Enabled&&!(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==ProtonPreviewService.ExtensionId));
            Check("engine PDF/clipboard registrations are preserved",(await core.Profile.GetBrowserExtensionsAsync()).All(e=>before.ContainsKey(e.Id)&&before[e.Id]==e.IsEnabled));
            Check("online-suggestion preference resumes",(bool)typeof(MainWindow).GetProperty("AllowOnlineSuggestions",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(main)&&settings.OnlineSuggestionsEnabled);
            Check("off intent persists",!new ProtonPreviewService(store.RootFolder).Enabled);
            Check("Windows proxy configuration is unchanged",proxyBefore==ProxySnapshot());
            Check("ordinary browser remains usable after removal",await core.ExecuteScriptAsync("2+2")=="4");
            const string authCanary="https://account.proton.me/authorize?state=harbor-auth-canary-055#test";
            var initializer=main.BrowserViewInitializer;var delayed=new TaskCompletionSource<bool>();
            main.BrowserViewInitializer=(view,environment)=>delayed.Task;
            BrowserTab auth=null;
            try{
                int recent=((RecentlyClosedTabsService)Field(main,"_recentlyClosed")).Count;
                auth=AddTab(main,authCanary);main.BrowserTabs.SelectedItem=auth.Item;await Wait(()=>auth.Initializing);
                Check("account handoff is transient before controller startup",auth.IsProviderAuthentication);
                Check("pending account URL is excluded from saved sessions",store.LoadSession().Tabs.All(t=>t.Url!=authCanary));
                typeof(MainWindow).GetMethod("RecordHistory",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(main,new object[]{auth,authCanary,"Account test"});
                Check("account handoff is excluded from Harbor history",store.LoadHistory().All(h=>h.Url!=authCanary));
                CloseTab(main,auth);auth=null;
                Check("account tab is excluded from recently closed tabs",((RecentlyClosedTabsService)Field(main,"_recentlyClosed")).Count==recent);
            }finally{if(auth!=null)CloseTab(main,auth);main.BrowserViewInitializer=initializer;delayed.TrySetResult(true);}
            Check("sensitive classification is restricted to provider origins",ProtonPreviewService.IsSensitivePage(authCanary)&&ProtonPreviewService.IsSensitivePage("https://account.protonvpn.com/")&&!ProtonPreviewService.IsSensitivePage("https://account.proton.me.evil.invalid/"));
            Check("verification pages are sensitive without accepting host lookalikes",ProtonPreviewService.IsSensitivePage("https://account-api.proton.me/challenge/v4/html")&&!ProtonPreviewService.IsSensitivePage("https://account-api.proton.me.evil.invalid/"));
            string module=Path.Combine(output,"AuthModuleFixture");Directory.CreateDirectory(module);
            File.WriteAllText(Path.Combine(module,"manifest.json"),"{\"manifest_version\":3,\"name\":\"Auth isolation fixture\",\"version\":\"1.0.0\",\"content_scripts\":[{\"matches\":[\"https://*/*\"],\"js\":[\"test.js\"]}]}");
            File.WriteAllText(Path.Combine(module,"test.js"),"document.body.dataset.authFixture='should-be-excluded';");
            var inspected=SiteModuleService.Inspect(module);
            var scripts=(object[])inspected.Manifest["content_scripts"];var script=(System.Collections.Generic.Dictionary<string,object>)scripts[0];
            var exclusions=(string[])script["exclude_matches"];
            Check("managed site modules exclude all Proton account and verification origins",exclusions.Contains("https://account.proton.me/*")&&exclusions.Contains("https://account.protonvpn.com/*")&&exclusions.Contains("https://account-api.proton.me/*"));
        }finally{window?.Close();if(service.Enabled)await main.ChangeProtonPreview(false);}
    }
}
