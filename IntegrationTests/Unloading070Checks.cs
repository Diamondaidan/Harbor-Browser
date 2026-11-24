using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    private static async Task Unloading070Checks(MainWindow main, string output, string fixtures)
    {
        var settings = (BrowserSettings)Field(main,"_settings");
        Check("unloading is opt-in and defaults to fifteen minutes", !settings.UnloadInactiveTabs && settings.InactiveTabUnloadDelaySeconds == 900);
        settings.SuspendInactiveTabs = false;
        var previousInitializer = main.BrowserViewInitializer;
        main.BrowserViewInitializer = async (view,env) => {
            if(previousInitializer == null) await view.EnsureCoreWebView2Async(env); else await previousInitializer(view,env);
            foreach(string host in new[]{"unload.test.invalid","unload-frame.test.invalid"})
                view.CoreWebView2.SetVirtualHostNameToFolderMapping(host,fixtures,CoreWebView2HostResourceAccessKind.DenyCors);
        };
        var foreground = (BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var tab = AddTab(main,"https://unload.test.invalid/unload.html");
        main.BrowserTabs.SelectedItem = tab.Item;
        await Wait(()=>tab.Initialized && !tab.IsLoading);
        await WaitJs(tab.View.CoreWebView2,"typeof fixtureToken==='string'");
        main.BrowserTabs.SelectedItem=foreground.Item;
        await main.UnloadTabIfInactiveAsync(tab);
        Check("disabled setting prevents unloading",tab.Initialized&&!tab.IsUnloaded);
        settings.UnloadInactiveTabs=true;
        var copy=settings.Copy();
        Check("settings copy retains unloading and delay",copy.UnloadInactiveTabs&&copy.InactiveTabUnloadDelaySeconds==900);
        var store=new BrowserDataStore(Path.Combine(output,"Settings"),Path.Combine(output,"SettingsDownloads"));
        store.SaveSettings(copy);
        Check("unloading preference survives settings storage",store.LoadSettings().UnloadInactiveTabs&&store.LoadSettings().InactiveTabUnloadDelaySeconds==900);
        var settingsDialog=Offscreen(new SettingsWindow(settings));
        settingsDialog.Loaded+=(s,e)=>{settingsDialog.UnloadInactiveTabsBox.IsChecked=true;settingsDialog.UnloadDelayBox.SelectedValue="1800";Click(settingsDialog,"Save settings");};
        settingsDialog.ShowDialog();
        Check("Settings Save retains chosen unloading option and delay",settingsDialog.ResultSettings.UnloadInactiveTabs&&settingsDialog.ResultSettings.InactiveTabUnloadDelaySeconds==1800);
        main.BrowserTabs.SelectedItem=tab.Item;
        await main.UnloadTabIfInactiveAsync(tab);
        Check("active tab cannot unload",tab.Initialized&&tab.UnloadReason=="Active");
        main.BrowserTabs.SelectedItem=foreground.Item;
        main.SetTabKeepAwake(tab,true);await main.UnloadTabIfInactiveAsync(tab);
        Check("keep awake prevents unloading",tab.Initialized&&tab.UnloadReason=="Kept awake");
        main.SetTabKeepAwake(tab,false);
        tab.IsPinned=true;await main.UnloadTabIfInactiveAsync(tab);
        Check("pinned tab cannot unload",tab.Initialized&&tab.UnloadReason=="Pinned tab");tab.IsPinned=false;
        tab.IsProviderAuthentication=true;await main.UnloadTabIfInactiveAsync(tab);
        Check("provider sign-in is protected",tab.Initialized&&tab.UnloadReason.Contains("Proton"));tab.IsProviderAuthentication=false;
        tab.ProtonPaused=true;await main.UnloadTabIfInactiveAsync(tab);
        Check("paused protection is not bypassed by unloading",tab.Initialized&&tab.UnloadReason=="Browsing paused");tab.ProtonPaused=false;
        var privateTab=AddTab(main,HomePageService.Url,true);await main.UnloadTabIfInactiveAsync(privateTab);
        Check("private session is never discarded",!privateTab.IsUnloaded&&privateTab.UnloadReason=="Private session");CloseTab(main,privateTab);await main.WaitForPrivateCleanup();

        await tab.View.CoreWebView2.ExecuteScriptAsync("field.value='typed';field.dispatchEvent(new Event('input',{bubbles:true}));field.value='clean'");
        await main.UnloadTabIfInactiveAsync(tab);
        Check("editing remains protected even when the field returns to its default",tab.Initialized&&tab.UnloadReason=="Unsaved input");
        await ReloadUnloadFixture(tab);
        await tab.View.CoreWebView2.ExecuteScriptAsync("note.value='programmatic unsaved draft'");await main.UnloadTabIfInactiveAsync(tab);
        Check("changed textarea is detected without an input event",tab.Initialized&&tab.UnloadReason=="Unsaved input");
        await ReloadUnloadFixture(tab);
        await tab.View.CoreWebView2.ExecuteScriptAsync("choice.selectedIndex=1");await main.UnloadTabIfInactiveAsync(tab);
        Check("changed selection is protected",tab.Initialized&&tab.UnloadReason=="Unsaved input");
        await ReloadUnloadFixture(tab);
        await tab.View.CoreWebView2.ExecuteScriptAsync("let root=document.createElement('div');document.body.append(root);root.attachShadow({mode:'open'}).innerHTML='<input value=initial>';root.shadowRoot.querySelector('input').value='draft'");
        await main.UnloadTabIfInactiveAsync(tab);Check("shadow DOM form state is protected",tab.Initialized&&tab.UnloadReason=="Unsaved input");
        await ReloadUnloadFixture(tab);
        await tab.View.CoreWebView2.ExecuteScriptAsync("document.body.append(Object.assign(document.createElement('div'),{contentEditable:'true',textContent:'draft'}))");
        await main.UnloadTabIfInactiveAsync(tab);Check("contenteditable pages remain loaded",tab.Initialized&&tab.UnloadReason=="Unsaved input");
        await ReloadUnloadFixture(tab,"?frame=1");await Wait(()=>tab.MediaFrames.Count>0);
        await WaitJsForFrame(tab.MediaFrames[0],"typeof fixtureToken==='string'");
        await tab.MediaFrames[0].ExecuteScriptAsync("field.value='frame draft'");await main.UnloadTabIfInactiveAsync(tab);
        Check("cross-origin frame input prevents unloading",tab.Initialized&&tab.UnloadReason=="Unsaved input");
        await ReloadUnloadFixture(tab);
        foreach(string host in new[]{"media.test.invalid","media-frame.test.invalid"})tab.View.CoreWebView2.SetVirtualHostNameToFolderMapping(host,fixtures,CoreWebView2HostResourceAccessKind.DenyCors);
        var videoLoad=NextLoad(tab.View.CoreWebView2,"https://media.test.invalid/video.html");tab.View.CoreWebView2.Navigate("https://media.test.invalid/video.html");await videoLoad;
        await Wait(()=>tab.MediaFrames.Count>0);await WaitJsForFrame(tab.MediaFrames[0],"!!document.querySelector('video')&&!document.querySelector('video').paused&&document.querySelector('video').readyState>=2");
        await main.UnloadTabIfInactiveAsync(tab);
        Check("muted cross-origin video prevents unloading",tab.Initialized&&tab.UnloadReason=="Playing media");
        await ReloadUnloadFixture(tab);
        using(var posted=new UnloadFixtureServer())
        {
            var request=((CoreWebView2Environment)Field(main,"_environment")).CreateWebResourceRequest(posted.Url,"POST",new MemoryStream(new byte[]{120}),"Content-Type: application/x-www-form-urlencoded");
            var loaded=NextLoad(tab.View.CoreWebView2,posted.Url);tab.View.CoreWebView2.NavigateWithWebResourceRequest(request);await loaded;
            await main.UnloadTabIfInactiveAsync(tab);Check("native POST document is not discarded",posted.Posts==1&&tab.NonGetDocument&&tab.Initialized&&tab.UnloadReason=="Submitted page");
        }
        await ReloadUnloadFixture(tab);

        var pending=new TaskCompletionSource<string>();main.UnloadActivityReader=t=>pending.Task;
        var action=main.UnloadTabIfInactiveAsync(tab);main.BrowserTabs.SelectedItem=tab.Item;pending.SetResult(null);await action;
        Check("selecting during the asynchronous probe cancels unloading",tab.Initialized&&!tab.IsUnloaded);
        main.BrowserTabs.SelectedItem=foreground.Item;
        pending=new TaskCompletionSource<string>();main.UnloadActivityReader=t=>pending.Task;action=main.UnloadTabIfInactiveAsync(tab);settings.UnloadInactiveTabs=false;pending.SetResult(null);await action;
        Check("disabling unloading during a pending probe is respected",tab.Initialized&&!tab.IsUnloaded);
        settings.UnloadInactiveTabs=true;
        pending=new TaskCompletionSource<string>();main.UnloadActivityReader=t=>pending.Task;action=main.UnloadTabIfInactiveAsync(tab);await ReloadUnloadFixture(tab);pending.SetResult(null);await action;
        Check("navigation during a pending probe is not discarded",tab.Initialized&&!tab.IsUnloaded);
        var closing=AddTab(main,"https://unload.test.invalid/unload.html");main.BrowserTabs.SelectedItem=closing.Item;await Wait(()=>closing.Initialized&&!closing.IsLoading);
        main.BrowserTabs.SelectedItem=foreground.Item;pending=new TaskCompletionSource<string>();main.UnloadActivityReader=t=>pending.Task;
        action=main.UnloadTabIfInactiveAsync(closing);CloseTab(main,closing);pending.SetResult(null);await action;
        Check("closing during a pending probe does not recreate the view",closing.IsClosing&&!closing.IsUnloaded&&!closing.UnloadTimer.IsEnabled);
        main.UnloadActivityReader=t=>Task.FromResult("Page activity unavailable");
        settings.SuspendInactiveTabs=true;await Sleep(main,tab);await main.UnloadTabIfInactiveAsync(tab);
        Check("unavailable probe retains the page and restores sleep",tab.Initialized&&tab.IsSuspended&&tab.View.CoreWebView2.IsSuspended);
        settings.SuspendInactiveTabs=false;Activity(main);main.UnloadActivityReader=null;

        using(var server=new DownloadFixtureServer(30))
        {
            var downloads=(ObservableCollection<DownloadEntry>)Field(main,"_downloads");tab.View.CoreWebView2.Navigate(server.Url+"/unload-download");
            await Wait(()=>downloads.Any(d=>d.IsActive&&d.BytesReceived>0));var entry=downloads.First(d=>d.IsActive);
            await main.UnloadTabIfInactiveAsync(tab);Check("active native transfer protects its owner",tab.Initialized&&tab.UnloadReason.Contains("Download"));
            entry.Operation.Pause();await Wait(()=>entry.CanResume);await main.UnloadTabIfInactiveAsync(tab);
            Check("paused resumable transfer protects its owner",tab.Initialized&&tab.UnloadReason.Contains("Download"));
            entry.Operation.Cancel();await Wait(()=>!entry.IsActive&&!entry.CanResume);
        }
        await ReloadUnloadFixture(tab);
        var before=tab.View;Guid identity=tab.ResourceId;string token=await tab.View.CoreWebView2.ExecuteScriptAsync("fixtureToken");
        settings.SuspendInactiveTabs=true;await Sleep(main,tab);
        Check("normal automatic-sleep state is ready for unloading",tab.IsSuspended&&tab.View.CoreWebView2.IsSuspended);
        await main.UnloadTabIfInactiveAsync(tab);
        bool disposed=false;try{var previousCore=before.CoreWebView2;}catch(ObjectDisposedException){disposed=true;}
        Check("eligible native view is actually disposed and replaced",tab.IsUnloaded&&!tab.Initialized&&!ReferenceEquals(before,tab.View)&&disposed);
        Check("unloaded tab retains stable identity and its address",tab.ResourceId==identity&&tab.PendingUrl.Contains("unload.test.invalid"));
        Check("unloaded header distinguishes it from a sleeper",tab.HeaderText.Text.StartsWith("◇ ")&&tab.Item.Opacity<0.65);
        var row=main.BuildTabResourceRows().Single(r=>r.Id==identity);
        Check("monitor distinguishes unloaded and offers reload",row.IsUnloaded&&row.CanWake&&!row.CanSleep&&!row.CanUnload&&row.State.StartsWith("Unloaded"));
        settings.UnloadInactiveTabs=false;settings.SuspendInactiveTabs=false;Activity(main);
        Check("turning unloading off does not reload every discarded tab",tab.IsUnloaded&&!tab.Initialized);
        main.BrowserTabs.SelectedItem=tab.Item;await Wait(()=>tab.Initialized&&!tab.IsLoading);await WaitJs(tab.View.CoreWebView2,"typeof fixtureToken==='string'");
        Check("selecting unloaded tab reloads its page",!tab.IsUnloaded&&await tab.View.CoreWebView2.ExecuteScriptAsync("fixtureToken")!=token);
        main.BrowserTabs.SelectedItem=foreground.Item;settings.UnloadInactiveTabs=true;settings.InactiveTabUnloadDelaySeconds=60;
        tab.InactiveSinceUtc=DateTime.UtcNow.AddMinutes(-2);tab.RetryUnloadAfterUtc=default(DateTime);Activity(main);await Wait(()=>tab.IsUnloaded);
        Check("overdue inactivity timer unloads without selecting the tab",tab.IsUnloaded&&main.BrowserTabs.SelectedItem==foreground.Item);
        var actions=(ResourceMonitorActions)typeof(MainWindow).GetMethod("MonitorActions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,null);
        Func<string> report=()=>Convert.ToString(typeof(MainWindow).GetMethod("BuildResourceReport",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,null));
        foreach(string mode in new[]{"Light","Dark"})
        {
            AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});
            var monitor=Offscreen(new ResourceMonitorWindow(report,actions){Width=850,Height=540});monitor.Show();await Task.Delay(100);
            monitor.TabGrid.SelectedItem=monitor.TabGrid.Items.Cast<TabResourceRow>().Single(r=>r.Id==identity);
            Check(mode+" monitor offers Reload for unloaded view",Convert.ToString(monitor.WakeTabButton.Content)=="Reload selected"&&monitor.WakeTabButton.IsEnabled&&!monitor.UnloadTabButton.IsEnabled);
            ((Panel)monitor.Content).Background=monitor.Background;await Task.Delay(80);monitor.UpdateLayout();
            Capture((FrameworkElement)monitor.Content,Path.Combine(output,"unloaded-monitor-"+mode+".png"));monitor.Close();
        }
        Check("process report explains commit and shared renderer memory",report().Contains("Unloaded: 1")&&report().Contains("not a measurement of physical RAM")&&report().Contains("not guessed per tab"));
        actions.Wake(identity);await Wait(()=>tab.Initialized&&!tab.IsLoading);await WaitJs(tab.View.CoreWebView2,"typeof fixtureToken==='string'");
        Check("monitor Reload selects and initializes the discarded view",!tab.IsUnloaded&&main.BrowserTabs.SelectedItem==tab.Item);
        using(var server=new UnloadFixtureServer())
        {
            var loaded=NextLoad(tab.View.CoreWebView2,server.Url);tab.View.CoreWebView2.Navigate(server.Url);await loaded;
            main.BrowserTabs.SelectedItem=foreground.Item;await main.UnloadTabIfInactiveAsync(tab);
            Check("ordinary page can unload before a protection change",tab.IsUnloaded);
            int requests=server.Requests;var validator=main.ProtonProxyOwnershipValidator;
            bool monitoring=main.AutomaticProtonMonitoringEnabled;main.AutomaticProtonMonitoringEnabled=false;
            try
            {
                main.ProtonProxyOwnershipValidator=c=>Task.FromResult(true);
                main.BeginProtonProtection(true);main.BrowserTabs.SelectedItem=tab.Item;
                await Wait(()=>tab.Initialized&&tab.ProtonPaused&&!tab.Initializing);
                Check("selecting an unloaded tab while unverified initializes a paused view",!tab.IsUnloaded&&tab.FailureMessage==null&&server.Requests==requests);
                typeof(MainWindow).GetMethod("Reload_Click",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,new object[]{null,new RoutedEventArgs()});
                await Task.Delay(120);Check("Retry stays blocked on a newly recreated view",tab.ProtonPaused&&server.Requests==requests);
                Check("fresh synthetic proof protects the recreated context",main.MarkProtonRouteVerified(main.ProtonProtection.Epoch));
                await Task.Delay(120);Check("route recovery does not replay the unloaded address",tab.ProtonPaused&&server.Requests==requests);
                var retry=NextLoad(tab.View.CoreWebView2,server.Url);
                typeof(MainWindow).GetMethod("Reload_Click",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,new object[]{null,new RoutedEventArgs()});await retry;
                Check("explicit Retry restores the recreated page after verification",!tab.ProtonPaused&&server.Requests>requests&&tab.FailureMessage==null);
            }
            finally{main.BeginProtonProtection(false);main.ProtonProxyOwnershipValidator=validator;main.AutomaticProtonMonitoringEnabled=monitoring;}
        }
        settings.UnloadInactiveTabs=false;CloseTab(main,tab);Activity(main);main.BrowserViewInitializer=previousInitializer;
        AppearanceService.ApplyApplication(settings);
        await RestoreStartup070Checks(output,fixtures);
    }

    private static async Task RestoreStartup070Checks(string output,string fixtures)
    {
        var store=new BrowserDataStore(Path.Combine(output,"Restore"),Path.Combine(output,"RestoreDownloads"));
        store.SaveSettings(new BrowserSettings{RestorePreviousSession=true,HarborHomeIntroduced=true,OnlineSuggestionsEnabled=false});
        store.SaveSession(new BrowserSession{SelectedTabIndex=19,Tabs=Enumerable.Range(0,20).Select(i=>new BrowserSessionTab{Url="https://unload.test.invalid/unload.html?restored="+i}).ToList()});
        int initialized=0;
        var window=Offscreen(new MainWindow(store));
        window.BrowserViewInitializer=async(view,environment)=>{initialized++;await view.EnsureCoreWebView2Async(environment);view.CoreWebView2.SetVirtualHostNameToFolderMapping("unload.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);};
        try
        {
            window.Show();await Wait(()=>window.BrowserTabs.Items.Count==20&&((BrowserTab)((TabItem)window.BrowserTabs.SelectedItem).Tag).Initialized);
            var selected=(BrowserTab)((TabItem)window.BrowserTabs.SelectedItem).Tag;
            await WaitJs(selected.View.CoreWebView2,"typeof fixtureToken==='string'");await Task.Delay(100);
            Check("restoring twenty tabs initializes only the saved selection",initialized==1&&window.BrowserTabs.SelectedIndex==19);
            var first=(BrowserTab)((TabItem)window.BrowserTabs.Items[0]).Tag;
            Check("restored background tab retains address without a controller",!first.Initialized&&first.PendingUrl.EndsWith("restored=0"));
            Check("restored background tab has no hidden startup or timeout pending",!first.Initializing&&first.InitializationTask==null&&first.View.CoreWebView2==null);
            window.BrowserTabs.SelectedItem=first.Item;await Wait(()=>first.Initialized);
            Check("first use initializes only the selected deferred tab",initialized==2);
        }
        finally{window.Close();}
    }

    private static async Task ReloadUnloadFixture(BrowserTab tab,string query=null)
    {
        if(tab.View.CoreWebView2.IsSuspended)tab.View.CoreWebView2.Resume();
        string url="https://unload.test.invalid/unload.html"+(query??("?fresh="+Guid.NewGuid().ToString("N")));
        var load=NextLoad(tab.View.CoreWebView2,url);tab.View.CoreWebView2.Navigate(url);await load;
        await WaitJs(tab.View.CoreWebView2,"typeof fixtureToken==='string'");
    }
}
