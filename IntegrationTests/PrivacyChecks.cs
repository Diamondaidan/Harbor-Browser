using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private static async Task LoadPrivacy(BrowserTab tab,string fixture,string suffix="")
    {
        var core=tab.View.CoreWebView2;core.SetVirtualHostNameToFolderMapping("privacy.test.invalid",fixture,CoreWebView2HostResourceAccessKind.DenyCors);
        string url="https://privacy.test.invalid/privacy.html"+suffix;var loaded=NextLoad(core,url);core.Navigate(url);await loaded;
        await core.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled","{\"enabled\":true}");
    }
    private static async Task NextLoad(CoreWebView2 core,string expected=null)
    {
        ulong navigation=0;var result=new TaskCompletionSource<bool>();EventHandler<CoreWebView2NavigationStartingEventArgs> starting=(s,e)=>{if(expected==null||e.Uri==expected)navigation=e.NavigationId;};
        EventHandler<CoreWebView2NavigationCompletedEventArgs> handler=(s,e)=>{if(navigation==0||navigation!=e.NavigationId)return;if(e.IsSuccess)result.TrySetResult(true);else result.TrySetException(new Exception("Privacy fixture navigation failed: "+e.WebErrorStatus));};
        core.NavigationStarting+=starting;core.NavigationCompleted+=handler;
        try{if(await Task.WhenAny(result.Task,Task.Delay(20000))!=result.Task)throw new TimeoutException("Privacy navigation timed out");await result.Task;}
        finally{core.NavigationStarting-=starting;core.NavigationCompleted-=handler;}
    }
    private static Task Gesture(CoreWebView2 core,string expression)
    {return core.CallDevToolsProtocolMethodAsync("Runtime.evaluate",Json.Serialize(new{expression=expression,userGesture=true,returnByValue=true}));}
    private static async Task PrivacyChecks(MainWindow main,BrowserDataStore store,string output,string fixtures)
    {
        var settings=(BrowserSettings)Field(main,"_settings");settings.OnlineSuggestionsEnabled=false;
        var normal=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=normal.Item;await Wait(()=>normal.Initialized);await LoadPrivacy(normal,fixtures);
        var core=normal.View.CoreWebView2;const string origin="https://privacy.test.invalid";
        await Gesture(core,"requestMedia('Camera')");await Wait(()=>main.PermissionRequestBar.Visibility==Visibility.Visible);
        Check("camera request uses Harbor permission banner",main.PermissionRequestText.Text.Contains(origin)&&main.PermissionRequestText.Text.Contains("camera"));
        Capture(main.PermissionRequestBar,Path.Combine(output,"permission-banner.png"));
        main.ChoosePermission(false,false);await WaitJs(core,"result === 'NotAllowedError'");
        Check("Block once denies camera without saving a rule",((SitePermissionService)Field(main,"_normalPermissions")).Get(origin,origin,"Camera")=="Ask");
        await Gesture(core,"requestMedia('Microphone')");await Wait(()=>main.PermissionRequestBar.Visibility==Visibility.Visible);main.ChoosePermission(false,true);await WaitJs(core,"result === 'NotAllowedError'");
        Check("remembered microphone block persists in normal profile",store.LoadPermissions().Any(r=>r.Kind=="Microphone"&&r.Decision=="Block"&&r.Origin==origin));
        await LoadPrivacy(normal,fixtures,"?repeat=1");await Gesture(core,"requestMedia('Microphone')");await WaitJs(core,"result === 'NotAllowedError'");
        Check("remembered block does not prompt again",main.PermissionRequestBar.Visibility==Visibility.Collapsed);
        await Gesture(core,"requestNotifications()");await Wait(()=>main.PermissionRequestBar.Visibility==Visibility.Visible);main.ChoosePermission(true,false);await WaitJs(core,"result === 'granted'");
        Check("Allow once grants notifications without a saved rule",!store.LoadPermissions().Any(r=>r.Kind=="Notifications"));
        await LoadPrivacy(normal,fixtures,"?cancel=1");await Gesture(core,"requestMedia('Camera')");await Wait(()=>main.PermissionRequestBar.Visibility==Visibility.Visible);
        var other=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=other.Item;await Wait(()=>other.Initialized);
        Check("switching tabs dismisses and denies pending permission",main.PermissionRequestBar.Visibility==Visibility.Collapsed&&Field(main,"_activePermission")==null);
        main.BrowserTabs.SelectedItem=normal.Item;await WaitJs(core,"result === 'NotAllowedError'");
        var rules=store.LoadPermissions();rules.Add(new SitePermissionRule{Origin=origin,TopLevelOrigin=origin,Kind="Camera",Decision="Allow",UpdatedUtc=DateTime.UtcNow});
        var reloaded=NextLoad(core);await main.ApplyPermissionRules(rules);await reloaded;await Gesture(core,"requestMedia('Camera')");await WaitJs(core,"result === 'allowed'");
        Check("remembered camera allow uses test-only fake device",main.PermissionRequestBar.Visibility==Visibility.Collapsed&&store.LoadPermissions().Any(r=>r.Kind=="Camera"&&r.Decision=="Allow"));
        Check("Harbor choices are not cached as native profile grants",!(await core.Profile.GetNonDefaultPermissionSettingsAsync()).Any(p=>p.PermissionState==CoreWebView2PermissionState.Allow));
        await core.ExecuteScriptAsync("document.cookie='scope=normal;path=/;Secure;SameSite=Lax';localStorage.setItem('scope','normal');");
        var privateTab=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=privateTab.Item;await Wait(()=>privateTab.Initialized);await LoadPrivacy(privateTab,fixtures);
        var privateCore=privateTab.View.CoreWebView2;string privateSession=privateTab.PrivateSessionId;
        Check("private profile does not see normal cookies or storage",await privateCore.ExecuteScriptAsync("!document.cookie.includes('scope=normal') && localStorage.getItem('scope') === null")=="true");
        await privateCore.ExecuteScriptAsync("document.cookie='scope=private;path=/;Secure;SameSite=Lax';localStorage.setItem('scope','private');");
        await Gesture(privateCore,"requestMedia('Camera')");await Wait(()=>main.PermissionRequestBar.Visibility==Visibility.Visible);main.ChoosePermission(false,true);await WaitJs(privateCore,"result === 'NotAllowedError'");
        Check("private remembered choices stay out of normal permission file",store.LoadPermissions().Single(p=>p.Kind=="Camera").Decision=="Allow");
        var privateSecond=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=privateSecond.Item;await Wait(()=>privateSecond.Initialized);await LoadPrivacy(privateSecond,fixtures);
        Check("simultaneous private tabs share the same session",privateSecond.PrivateSessionId==privateSession&&await privateSecond.View.CoreWebView2.ExecuteScriptAsync("document.cookie.includes('scope=private') && localStorage.getItem('scope') === 'private'")=="true");
        await Gesture(privateSecond.View.CoreWebView2,"requestMedia('Camera')");await WaitJs(privateSecond.View.CoreWebView2,"result === 'NotAllowedError'");Check("private remembered block is shared within private session",main.PermissionRequestBar.Visibility==Visibility.Collapsed);
        var privateRules=(Dictionary<string,SitePermissionService>)Field(main,"_privatePermissions");Check("private rules exist only in memory",privateRules[privateSession].Get(origin,origin,"Camera")=="Block");
        main.BrowserTabs.SelectedItem=normal.Item;
        var history=(List<HistoryEntry>)Field(main,"_history");history.Add(new HistoryEntry{Url="https://old-history.test",Title="Old test history",VisitedAt=DateTime.UtcNow.AddDays(-2)});history.Add(new HistoryEntry{Url="https://recent-history.test",Title="Recent test history",VisitedAt=DateTime.UtcNow.AddMinutes(-5)});store.SaveHistory(history);
        var bookmarks=(List<Bookmark>)Field(main,"_bookmarks");var existingBookmarks=bookmarks.Count;int tabsBefore=main.BrowserTabs.Items.Count;
        var lastHour=new BrowsingDataRequest{Range=BrowsingTimeRange.LastHour,History=true};lastHour.SetBounds(DateTime.UtcNow);await main.ClearBrowsingData(lastHour);
        Check("actual history cleanup respects time range",history.Any(h=>h.Url=="https://old-history.test")&&!history.Any(h=>h.Url=="https://recent-history.test"));
        Check("history cleanup resets recently closed tabs and recovery",((RecentlyClosedTabsService)Field(main,"_recentlyClosed")).Count==0&&!store.LoadRecovery().Pending);
        var clear=new BrowsingDataRequest{Range=BrowsingTimeRange.AllTime,CookiesAndSiteData=true,Cache=true};clear.SetBounds(DateTime.UtcNow);await main.ClearBrowsingData(clear);
        Check("actual normal cleanup clears cookies and DOM storage",await core.ExecuteScriptAsync("document.cookie === '' && localStorage.getItem('scope') === null")=="true");
        Check("normal cleanup leaves private cookies and storage intact",await privateCore.ExecuteScriptAsync("document.cookie.includes('scope=private') && localStorage.getItem('scope') === 'private'")=="true");
        Check("cleanup preserves bookmarks and open tabs",bookmarks.Count==existingBookmarks&&main.BrowserTabs.Items.Count==tabsBefore);
        await core.ExecuteScriptAsync("document.cookie='scope=normal;path=/;Secure;SameSite=Lax';localStorage.setItem('scope','normal');");
        await main.ClearBrowsingData(clear,privateSession);
        Check("private-scoped cleanup clears private site data",await privateCore.ExecuteScriptAsync("document.cookie === '' && localStorage.getItem('scope') === null")=="true");
        Check("private cleanup leaves normal browsing intact",await core.ExecuteScriptAsync("document.cookie.includes('scope=normal') && localStorage.getItem('scope') === 'normal'")=="true");
        var downloads=(ObservableCollection<DownloadEntry>)Field(main,"_downloads");var download=new DownloadEntry{IsPrivate=true,PrivateSessionId=privateSession,IsActive=true,FileName="private-active.zip",Status="Downloading"};downloads.Add(download);
        CloseTab(main,privateSecond);main.ConfirmPrivateDownloadClose=()=>false;CloseTab(main,privateTab);Check("last private tab can cancel closing to preserve its download",main.BrowserTabs.Items.Contains(privateTab.Item)&&downloads.Contains(download));
        main.ConfirmPrivateDownloadClose=()=>true;CloseTab(main,privateTab);await main.WaitForPrivateCleanup();Check("closing final private tab removes private downloads and rules",!downloads.Contains(download)&&!privateRules.ContainsKey(privateSession));
        var fresh=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=fresh.Item;await Wait(()=>fresh.Initialized);await LoadPrivacy(fresh,fixtures);
        Check("new private session gets a new profile identity",fresh.PrivateSessionId!=privateSession);
        Check("new private session starts without old site data or rules",await fresh.View.CoreWebView2.ExecuteScriptAsync("document.cookie === '' && localStorage.getItem('scope') === null")=="true"&&privateRules[fresh.PrivateSessionId].Snapshot().Count==0);
        CloseTab(main,fresh);await main.WaitForPrivateCleanup();main.BrowserTabs.SelectedItem=normal.Item;
        var clearPermissions=new BrowsingDataRequest{Range=BrowsingTimeRange.AllTime,Permissions=true};clearPermissions.SetBounds(DateTime.UtcNow);reloaded=NextLoad(core);await main.ClearBrowsingData(clearPermissions);await reloaded;
        Check("clear remembered permissions resets normal policy and file",store.LoadPermissions().Count==0&&((SitePermissionService)Field(main,"_normalPermissions")).Snapshot().Count==0);
        foreach(string mode in new[]{"Light","Dark"})
        {
            AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});
            var dialog=Offscreen(new SitePermissionsWindow(rules,origin,false){Width=700,Height=480});dialog.Show();await Task.Delay(70);Check(mode+" site-permission editor exposes all four core permissions",dialog.PermissionGrid.Items.Cast<PermissionRow>().Select(r=>r.Kind).Distinct().Count()==4);((Panel)dialog.Content).Background=dialog.Background;await Task.Delay(40);Capture((FrameworkElement)dialog.Content,Path.Combine(output,"site-permissions-"+mode.ToLowerInvariant()+".png"));
            Check(mode+" permission dialog uses Harbor palette and icon",dialog.Background!=null&&dialog.Foreground!=null&&dialog.Icon!=null);
            dialog.PermissionGrid.SelectedIndex=0;dialog.PermissionGrid.CurrentCell=new DataGridCellInfo(dialog.PermissionGrid.Items[0],dialog.PermissionGrid.Columns[3]);dialog.PermissionGrid.Focus();dialog.PermissionGrid.BeginEdit();
            Click(dialog,"Reset selected to Ask");Check(mode+" reset permission finishes active grid edit",((PermissionRow)dialog.PermissionGrid.Items[0]).Decision=="Ask");dialog.Close();
            int clears=0;var panel=Offscreen(new ClearBrowsingDataWindow(false,r=>{clears++;return Task.CompletedTask;}){Width=570,Height=430});panel.Show();panel.RangeBox.SelectedValue=BrowsingTimeRange.AllTime;panel.PermissionsBox.IsChecked=true;panel.ClearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(50);Check(mode+" clear-data panel submits chosen range and categories",clears==1&&panel.BuildRequest().Range==BrowsingTimeRange.AllTime&&panel.BuildRequest().Permissions);Check(mode+" clear-data dialog uses Harbor palette and icon",panel.Background!=null&&panel.Foreground!=null&&panel.Icon!=null);((Panel)panel.Content).Background=panel.Background;await Task.Delay(40);Capture((FrameworkElement)panel.Content,Path.Combine(output,"clear-data-"+mode.ToLowerInvariant()+".png"));panel.Close();
        }
        AppearanceService.ApplyApplication(settings);
        Func<string> report=()=>Convert.ToString(typeof(MainWindow).GetMethod("BuildResourceReport",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null));
        var monitor=Offscreen(new ResourceMonitorWindow(report));monitor.Show();await Task.Delay(60);
        var reportBox=(TextBox)monitor.FindName("ReportBox");Check("resource monitor reports actual app and engine processes",reportBox.Text.Contains("HARBOR APPLICATION")&&reportBox.Text.Contains("WEBVIEW2 PROCESSES")&&reportBox.Text.Contains("Combined working set")&&!reportBox.Text.Contains("snapshot unavailable"));
        Click(monitor,"Refresh now");Check("resource monitor refresh includes current tab count",reportBox.Text.Contains("Open: "+main.BrowserTabs.Items.Count));((Panel)monitor.Content).Background=monitor.Background;await Task.Delay(40);Capture((FrameworkElement)monitor.Content,Path.Combine(output,"resource-monitor.png"));monitor.Close();
        await RecoveryWindowChecks(output);
        var oldView=normal.View;
        var crash=core.CallDevToolsProtocolMethodAsync("Page.crash","{}");var observed=crash.ContinueWith(t=>{var error=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
        await Wait(()=>normal.FailureMessage!=null);Check("actual renderer crash shows a recoverable page",!normal.NeedsNewView&&main.BrowserContentHost.Content is FrameworkElement&&!(main.BrowserContentHost.Content is Microsoft.Web.WebView2.Wpf.WebView2));
        reloaded=NextLoad(core);await main.RecoverTab(normal);await reloaded;Check("renderer reload restores original tab and URL",ReferenceEquals(oldView,normal.View)&&normal.FailureMessage==null&&core.Source.StartsWith(origin));
        crash=core.CallDevToolsProtocolMethodAsync("Page.crash","{}");observed=crash.ContinueWith(t=>{var error=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);await Wait(()=>normal.FailureMessage!=null);
        reloaded=NextLoad(core,HomePageService.Url);typeof(MainWindow).GetMethod("NavigateCurrent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{HomePageService.Url});await reloaded;
        Check("address navigation also recovers a failed renderer",normal.FailureMessage==null&&ReferenceEquals(main.BrowserContentHost.Content,normal.View)&&core.Source==HomePageService.Url);
        await LoadPrivacy(normal,fixtures,"?before-engine=1");
        crash=core.CallDevToolsProtocolMethodAsync("Browser.crash","{}");observed=crash.ContinueWith(t=>{var error=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
        await Wait(()=>normal.NeedsNewView);Check("actual browser-engine crash preserves normal tabs for recovery",store.LoadSession().Tabs.Any(t=>t.Url.StartsWith(origin))&&normal.FailureMessage!=null);
        var failedRows=main.BuildTabResourceRows();Check("resource monitor distinguishes failed tabs from sleeping tabs",main.BrowserTabs.Items.Cast<TabItem>().Select(i=>(BrowserTab)i.Tag).Where(t=>t.NeedsNewView).All(t=>failedRows.Single(r=>r.Id==t.ResourceId).State=="Needs reload"&&!failedRows.Single(r=>r.Id==t.ResourceId).CanWake));
        await main.RecoverTab(normal);await Wait(()=>normal.Initialized&&normal.FailureMessage==null);Check("engine recovery replaces broken control",!ReferenceEquals(oldView,normal.View));
        // Test virtual-host mappings belong to the old engine and must be reinstalled after a crash.
        await Task.Delay(300);await LoadPrivacy(normal,fixtures,"?engine=recovered");Check("browser engine can navigate after recreation",await normal.View.CoreWebView2.ExecuteScriptAsync("document.title")=="\"Harbor privacy fixture\"");
        Check("resource report works after engine recovery",report().Contains("Combined working set"));
    }
    private static async Task RecoveryWindowChecks(string output)
    {
        string directory=Path.Combine(output,"RecoveryUiProfile");var data=new BrowserDataStore(directory,Path.Combine(directory,"Downloads"));
        data.SaveSettings(new BrowserSettings{RestorePreviousSession=true,HarborHomeIntroduced=false,OnlineSuggestionsEnabled=false});
        string url=HomePageService.Url+"?recovered=1";
        data.SaveSession(new BrowserSession{GroupsConfigured=true,Groups=new List<string>{"Recovery"},Tabs=new List<BrowserSessionTab>{new BrowserSessionTab{Url=url,GroupName="Recovery",IsPinned=true}}});
        data.SaveRunState(new SessionRunState{Running=true,StartedUtc=DateTime.UtcNow.AddMinutes(-2)});
        var layout=new MonitorLayout(new PixelRect(-20000,0,-18720,720),new PixelRect(-20000,0,-18720,680),3);
        var window=Offscreen(new MainWindow(data,h=>layout){Width=800,Height=560});
        Check("profile preferences are not saved before writer lock",!data.LoadSettings().HarborHomeIntroduced);window.Show();
        try
        {
            await Wait(()=>window.BrowserTabs.Items.Count>0&&((BrowserTab)((TabItem)window.BrowserTabs.Items[0]).Tag).Initialized);
            Check("default preference upgrade saves after writer lock",data.LoadSettings().HarborHomeIntroduced);
            Check("unclean startup offers recovery before restoring pages",window.RecoveryBanner.Visibility==Visibility.Visible&&window.BrowserTabs.Items.Count==1);
            Capture(window.RecoveryBanner,Path.Combine(output,"recovery-banner.png"));
            typeof(MainWindow).GetMethod("RestoreRecovery_Click",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[]{window,new RoutedEventArgs()});
            var recovered=window.BrowserTabs.Items.Cast<TabItem>().Select(i=>(BrowserTab)i.Tag).Single(t=>t.IsPinned);
            await Wait(()=>recovered.Initialized&&recovered.LastKnownUrl==url);
            Check("recovery action restores saved pin and group",recovered.GroupName=="Recovery");
            Check("restored checkpoint is dismissed and stays available in Tools",window.RecoveryBanner.Visibility==Visibility.Collapsed&&!data.LoadRecovery().Pending&&data.LoadRecovery().Session.Tabs.Count==1);
        }
        finally{window.Close();}
        Check("actual app shutdown marks recovery journal clean",!data.LoadRunState().Running);
    }
}
