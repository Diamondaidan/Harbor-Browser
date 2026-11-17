using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;

internal static partial class Program
{
    private static async Task PrivacyEdgeChecks(MainWindow main,string output,string fixtures)
    {
        var normal=await PerformanceTab(main,fixtures);await LoadPrivacy(normal,fixtures,"?edge=timeout");var core=normal.View.CoreWebView2;
        await Gesture(core,"requestMedia('Camera')");await Wait(()=>main.PermissionRequestBar.Visibility==Visibility.Visible);
        var timer=(DispatcherTimer)Field(main,"_permissionTimer");Check("permission prompt timeout is configured for one minute",timer.Interval==TimeSpan.FromSeconds(60));
        timer.Interval=TimeSpan.FromMilliseconds(150);await Wait(()=>main.PermissionRequestBar.Visibility==Visibility.Collapsed);await WaitJs(core,"result === 'NotAllowedError'");Check("actual dispatcher timeout denies the pending request",Field(main,"_activePermission")==null);
        await LoadPrivacy(normal,fixtures,"?edge=navigation");await Gesture(core,"requestMedia('Camera')");await Wait(()=>main.PermissionRequestBar.Visibility==Visibility.Visible);
        var loaded=NextLoad(core,HomePageService.Url);core.Navigate(HomePageService.Url);await loaded;Check("navigation cancels pending permission and banner",Field(main,"_activePermission")==null&&main.PermissionRequestBar.Visibility==Visibility.Collapsed);CloseTab(main,normal);
        main.ConfirmPrivateDownload=()=>true;main.ConfirmPrivateDownloadClose=()=>false;
        using(var server=new DownloadFixtureServer())
        {
            var owner=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=owner.Item;await Wait(()=>owner.Initialized);
            var entries=(ObservableCollection<DownloadEntry>)Field(main,"_downloads");DownloadEntry entry=null;
            NotifyCollectionChangedEventHandler pause=(s,e)=>{if(e.NewItems==null)return;foreach(DownloadEntry added in e.NewItems)if(added.IsPrivate&&added.PrivateSessionId==owner.PrivateSessionId){entry=added;added.Operation.Pause();}};
            entries.CollectionChanged+=pause;
            try{owner.View.CoreWebView2.Navigate(server.Url+"/private-cancel");await Wait(()=>entry!=null&&entry.CanResume);}finally{entries.CollectionChanged-=pause;}
            Check("real private paused download is scoped and resumable",entry.PrivateSessionId==owner.PrivateSessionId&&entry.Operation.State==Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Interrupted&&entry.Operation.CanResume);
            CloseTab(main,owner);Check("declining last-private-tab close preserves real paused download",main.BrowserTabs.Items.Contains(owner.Item)&&entries.Contains(entry)&&entry.CanResume);
            main.ConfirmPrivateDownloadClose=()=>true;CloseTab(main,owner);await main.WaitForPrivateCleanup();await Wait(()=>server.OpenConnections==0);
            Check("confirming close ends real private transfer and removes row",!main.BrowserTabs.Items.Contains(owner.Item)&&!entries.Contains(entry));
            var completedOwner=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=completedOwner.Item;await Wait(()=>completedOwner.Initialized);completedOwner.View.CoreWebView2.Navigate(server.Url+"/private-complete");
            await Wait(()=>entries.Any(d=>d.IsPrivate&&d.Status=="Complete"));var complete=entries.First(d=>d.IsPrivate&&d.Status=="Complete");string file=complete.FilePath;CloseTab(main,completedOwner);await main.WaitForPrivateCleanup();
            Check("private completion cleanup retains downloaded file",File.Exists(file)&&!entries.Contains(complete)&&File.ReadAllBytes(file).SequenceEqual(server.Payload));
        }
        await ExitCleanupChecks(output,fixtures);
    }
    private static async Task ExitCleanupChecks(string output,string fixtures)
    {
        string root=Path.Combine(output,"ExitCleanupProfile");var data=new BrowserDataStore(root,Path.Combine(root,"Downloads"));data.SaveSettings(new BrowserSettings{HarborHomeIntroduced=true,OnlineSuggestionsEnabled=false,RestorePreviousSession=false,ClearCookiesOnExit=true,ClearCacheOnExit=true});
        var layout=new MonitorLayout(new PixelRect(-20000,0,-18720,720),new PixelRect(-20000,0,-18720,680),3);
        for(int run=0;run<2;run++)
        {
            var window=Offscreen(new MainWindow(data,h=>layout){Width=800,Height=560});bool closed=false;window.Closed+=(s,e)=>closed=true;window.Show();
            await Wait(()=>window.BrowserTabs.Items.Count>0&&((BrowserTab)((System.Windows.Controls.TabItem)window.BrowserTabs.Items[0]).Tag).Initialized);
            var tab=(BrowserTab)((System.Windows.Controls.TabItem)window.BrowserTabs.Items[0]).Tag;await LoadPrivacy(tab,fixtures);
            if(run==0){await tab.View.CoreWebView2.ExecuteScriptAsync("document.cookie='exitScope=normal;path=/;Secure';localStorage.setItem('exitScope','normal');");Check("clear-on-exit fixture saves real site data",await tab.View.CoreWebView2.ExecuteScriptAsync("document.cookie.includes('exitScope=normal') && localStorage.getItem('exitScope') === 'normal'")=="true");}
            else Check("reopened app confirms exit cookies and DOM storage were cleared",await tab.View.CoreWebView2.ExecuteScriptAsync("document.cookie === '' && localStorage.getItem('exitScope') === null")=="true");
            window.Close();await Wait(()=>closed);Check("privacy exit waits for cleanup and marks journal clean "+run,!data.LoadRunState().Running);
        }
    }
}
