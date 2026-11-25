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
    private static object OffField(object target,string name) { return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target); }
    private static async Task ProtonOff061Checks(MainWindow main, BrowserDataStore store, string output)
    {
        var tab=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var core=tab.View.CoreWebView2;
        var service=(ProtonPreviewService)Field(main,"_protonPreview");
        bool monitoring=main.AutomaticProtonMonitoringEnabled;
        main.AutomaticProtonMonitoringEnabled=false;
        string key="harbor-off-canary-"+Guid.NewGuid().ToString("N");
        string cookieValue=Guid.NewGuid().ToString("N");
        var cookie=core.CookieManager.CreateCookie("harbor-off-canary",cookieValue,"off-canary.test.invalid","/");
        core.CookieManager.AddOrUpdateCookie(cookie);
        ProtonPreviewWindow popup=null;
        using(var server=new BetaGuardFixtureServer())
        try
        {
            string target=server.Url("off061/document");
            await AuthNavigate(core,target);
            int before=server.Count("off061/document");
            Check("off test starts with a functional ordinary route",before==1);
            await main.ChangeProtonPreview(true);
            await Wait(()=>tab.ProtonPaused);
            Check("disconnected protection continues to pause ordinary browsing",main.ProtonProtection.Enabled&&main.ProtonProtection.State!=ProtonProtectionPhase.Protected);
            popup=Offscreen(main.OpenProtonPreview());
            await Wait(()=>popup.ControlsReady||popup.ControlsError!=null);
            Check("real provider controls load before the retention test",popup.ControlsReady);
            Check("synthetic provider-local canary is seeded",await Hotfix2StorageCanary(popup.ProviderView.CoreWebView2,key,true));
            var unrelated=(await core.Profile.GetBrowserExtensionsAsync()).Where(e=>e.Id!=ProtonPreviewService.ExtensionId).ToDictionary(e=>e.Id,e=>e.IsEnabled);
            string registration=File.ReadAllText(Path.Combine(store.RootFolder,"proton-preview-registration.txt"));

            main.BrowserTabs.SelectedItem=tab.Item;main.UpdateLayout();await Task.Delay(50);
            var off=Visuals<Button>(main).Single(b=>Convert.ToString(b.Content)=="Turn off protection — keep sign-in");
            off.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(()=>!service.Enabled&&!main.ProtonProtection.Enabled);
            var retained=(await core.Profile.GetBrowserExtensionsAsync()).Single(e=>e.Id==ProtonPreviewService.ExtensionId);
            Check("paused-page off action retains a disabled native extension",!retained.IsEnabled&&service.HasSavedRegistration);
            Check("off keeps the package registration marker unchanged",File.ReadAllText(Path.Combine(store.RootFolder,"proton-preview-registration.txt"))==registration);
            Check("off intent is persisted",!new ProtonPreviewService(store.RootFolder).Enabled);
            Check("off disarms the native fallback",!((ProtonFallbackGuard)Field(main,"_protonFallback")).Armed);
            Check("off restores updater availability",main.UpdateNetworkAllowed);
            Check("off indicator and paused-page explanation agree",Convert.ToString(main.TorIndicatorButton.Content)=="VPN Off"&&Visuals<TextBlock>(main).Any(t=>t.Text=="Ready to resume"));
            Check("off does not automatically replay the paused address",tab.ProtonPaused&&server.Count("off061/document")==before);
            typeof(MainWindow).GetMethod("Reload_Click",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{null,null});
            await Wait(()=>!tab.ProtonPaused&&server.Count("off061/document")==before+1);
            Check("explicit Reload restores ordinary browsing",core.Source==target);
            Check("off leaves unrelated native extensions unchanged",(await core.Profile.GetBrowserExtensionsAsync()).Where(e=>e.Id!=ProtonPreviewService.ExtensionId).All(e=>unrelated.ContainsKey(e.Id)&&unrelated[e.Id]==e.IsEnabled));
            Capture(popup,Path.Combine(output,"proton-off-keeps-registration.png"));

            await main.ChangeProtonPreview(true);await popup.ShowProvider();
            Check("on reuses retained provider-local data",popup.ControlsReady&&await Hotfix2StorageCanary(popup.ProviderView.CoreWebView2,key,false));
            Check("on requires fresh protection evidence",main.ProtonProtection.Enabled&&main.ProtonProtection.State!=ProtonProtectionPhase.Protected);
            var popupOff=(Button)OffField(popup,"_turnOff");
            Check("ordinary off is visible without expanding Options",popupOff.IsVisible&&popupOff.IsEnabled);
            popupOff.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(()=>!service.Enabled&&!main.ProtonProtection.Enabled);
            await main.ChangeProtonPreview(true);await popup.ShowProvider();
            Check("popup off/on also retains provider-local data",await Hotfix2StorageCanary(popup.ProviderView.CoreWebView2,key,false));

            bool persistenceFailed=false;
            using(var locked=File.Open(Path.Combine(store.RootFolder,"proton-preview.txt"),FileMode.Open,FileAccess.Read,FileShare.None))
            {try{await main.ChangeProtonPreview(false);}catch(IOException){persistenceFailed=true;}}
            Check("failed off persistence leaves protection blocking",persistenceFailed&&service.Enabled&&main.ProtonProtection.ShouldBlock(target,false));
            Check("failed off never uninstalls the provider",(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==ProtonPreviewService.ExtensionId)&&service.HasSavedRegistration);
            await main.ChangeProtonPreview(true);await popup.ShowProvider();
            Check("data survives recovery from failed off",await Hotfix2StorageCanary(popup.ProviderView.CoreWebView2,key,false));

            bool guardFailed=false;
            using(var locked=File.Open(Path.Combine(store.RootFolder,"browser-protection.txt"),FileMode.Open,FileAccess.Read,FileShare.None))
            {try{await main.ChangeProtonPreview(false);}catch(IOException){guardFailed=true;}}
            Check("failed fallback-off persistence keeps ordinary requests blocked",guardFailed&&!service.Enabled&&main.ProtonProtection.ShouldBlock(target,false)&&((ProtonFallbackGuard)Field(main,"_protonFallback")).Armed);
            Check("ordinary off remains available to recover a failed fallback",((Button)OffField(popup,"_turnOff")).IsEnabled);
            await main.ChangeProtonPreview(false);
            Check("retrying off clears both gates without removing sign-in data",!main.ProtonProtection.Enabled&&!((ProtonFallbackGuard)Field(main,"_protonFallback")).Armed&&service.HasSavedRegistration);

            await main.ChangeProtonPreview(true);await popup.ShowProvider();
            var gate=(SemaphoreSlim)OffField(service,"_gate");await gate.WaitAsync();
            Task stopping=null,resuming=null;
            try{stopping=service.Disable(core.Profile);resuming=service.Enable(core.Profile);}finally{gate.Release();}
            bool superseded=false;try{await stopping;}catch(OperationCanceledException){superseded=true;}
            await resuming;await popup.ShowProvider();
            Check("a superseded queued off cannot disable a later on",superseded&&service.Enabled&&(await core.Profile.GetBrowserExtensionsAsync()).Single(e=>e.Id==ProtonPreviewService.ExtensionId).IsEnabled);
            Check("queued off/on never clears provider-local data",await Hotfix2StorageCanary(popup.ProviderView.CoreWebView2,key,false));
            await main.ChangeProtonPreview(false);
            var options=(Expander)OffField(popup,"_options");options.IsExpanded=true;popup.UpdateLayout();
            var remove=(Button)OffField(popup,"_remove");
            Check("explicit reset is separately labeled and available while off",remove.IsVisible&&remove.IsEnabled&&Convert.ToString(remove.Content)=="Remove Proton and reset sign-in");
            remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(()=>!service.HasSavedRegistration&&(int)Field(main,"_protonChanges")==0&&!(bool)OffField(popup,"_removing"));
            Check("explicit reset removes the provider",!(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==ProtonPreviewService.ExtensionId));
            Check("reset retains website cookies",(await core.CookieManager.GetCookiesAsync("https://off-canary.test.invalid/")).Any(c=>c.Name=="harbor-off-canary"&&c.Value==cookieValue));
            await main.ChangeProtonPreview(true);await popup.ShowProvider();
            Check("only explicit reset clears the old provider-local canary",!await Hotfix2StorageCanary(popup.ProviderView.CoreWebView2,key,false));
            Check("reinstalling the provider retains the website cookie",(await core.CookieManager.GetCookiesAsync("https://off-canary.test.invalid/")).Any(c=>c.Name=="harbor-off-canary"&&c.Value==cookieValue));
            await main.RemoveProtonPreview();
            var nativeReady=new TaskCompletionSource<CoreWebView2BrowserExtension>();
            var releaseAdd=new TaskCompletionSource<CoreWebView2BrowserExtension>();
            service.ExtensionAdder=async(profile,path)=>{var added=await profile.AddBrowserExtensionAsync(path);nativeReady.TrySetResult(added);return await releaseAdd.Task;};
            try
            {
                var enabling=main.ChangeProtonPreview(true);
                var installed=await ProtonRouteProbe.Bounded(nativeReady.Task,15000,CancellationToken.None);
                var disabling=main.ChangeProtonPreview(false);
                await Task.Delay(200);
                Check("off waits for an unfinished native registration",!disabling.IsCompleted);
                Check("pending registration never reports an unprotected regular route",main.ProtonProtection.Enabled&&main.ProtonProtection.ShouldBlock(target,false));
                releaseAdd.TrySetResult(installed);
                try{await enabling;}catch(OperationCanceledException){}
                await disabling;
                Check("late native registration is retained but disabled before off completes",!service.Enabled&&service.HasSavedRegistration&&(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==ProtonPreviewService.ExtensionId&&!e.IsEnabled));
                Check("settled registration restores the regular route only after both gates close",!main.ProtonProtection.Enabled&&!((ProtonFallbackGuard)Field(main,"_protonFallback")).Armed);
            }
            finally
            {
                if(nativeReady.Task.Status==TaskStatus.RanToCompletion)releaseAdd.TrySetResult(nativeReady.Task.Result);
                else releaseAdd.TrySetCanceled();
                service.ExtensionAdder=null;
            }
        }
        finally
        {
            popup?.Close();await main.RemoveProtonPreview();main.AutomaticProtonMonitoringEnabled=monitoring;
        }
        await ProtonOffColdRestart(output);
    }

    private static async Task ProtonOffColdRestart(string output)
    {
        // Also works when this runner is copied into a fixture installation.
        // Derive the owned checkout from evidence, not executable depth.
        var checkout=new DirectoryInfo(Path.GetFullPath(output));
        while(checkout!=null&&!(File.Exists(Path.Combine(checkout.FullName,"HarborBrowser.sln"))&&File.Exists(Path.Combine(checkout.FullName,"VERSION.txt"))))checkout=checkout.Parent;
        if(checkout?.Parent==null)throw new InvalidOperationException("Cold off/on requires evidence inside its owned checkout.");
        string workspace=checkout.Parent.FullName;
        string root=Path.Combine(workspace,"OFT",Guid.NewGuid().ToString("N").Substring(0,8));
        if(Directory.Exists(root))throw new InvalidOperationException("Refusing an existing cold-test profile.");
        Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,Hotfix2OwnershipFile),Guid.NewGuid().ToString("N"));
        File.WriteAllText(Path.Combine(output,"cold-off-test-path.txt"),root);
        foreach(string mode in new[]{"seed-off","resume-off"})
        {
            string args=String.Join(" ",new[]{root,mode,"--proton-hotfix2-host"}.Select(WindowsCommandRunner.Quote));
            using(var child=Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}))
            {
                var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
                if(!await Task.Run(()=>child.WaitForExit(180000))){child.Kill();throw new TimeoutException("Owned cold off/on test timed out.");}
                string diagnostic=await stdout+await stderr;File.AppendAllText(Path.Combine(root,"child-output.log"),diagnostic);
                if(child.ExitCode!=0)throw new Exception("Cold off/on failed: "+diagnostic);
                var results=Json.Deserialize<Dictionary<string,bool>>(File.ReadAllText(Path.Combine(root,mode+"-results.json")));
                foreach(var result in results)Check("cold "+mode+": "+result.Key,result.Value);
            }
        }
    }
}
