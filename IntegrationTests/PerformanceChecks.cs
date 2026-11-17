using System;
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
    private static Task Sleep(MainWindow main,BrowserTab tab)
    {return (Task)typeof(MainWindow).GetMethod("SuspendTabIfInactiveAsync",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{tab});}
    private static void Activity(MainWindow main)
    {typeof(MainWindow).GetMethod("UpdateTabActivity",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);}
    private static async Task<BrowserTab> PerformanceTab(MainWindow main,string fixtures)
    {
        var tab=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=tab.Item;await Wait(()=>tab.Initialized);
        var core=tab.View.CoreWebView2;core.SetVirtualHostNameToFolderMapping("performance.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);
        var loaded=NextLoad(core,"https://performance.test.invalid/performance.html");core.Navigate("https://performance.test.invalid/performance.html");await loaded;return tab;
    }
    private static async Task PerformanceChecks(MainWindow main,string output,string fixtures)
    {
        var settings=(BrowserSettings)Field(main,"_settings");bool previousEnabled=settings.SuspendInactiveTabs;int previousDelay=settings.InactiveTabSuspendDelaySeconds;
        settings.SuspendInactiveTabs=false;Activity(main);
        var first=await PerformanceTab(main,fixtures);var second=await PerformanceTab(main,fixtures);var foreground=await PerformanceTab(main,fixtures);
        try
        {
            settings.InactiveTabSuspendDelaySeconds=15;settings.SuspendInactiveTabs=true;Activity(main);
            DateTime inactive=first.InactiveSinceUtc.Value;await Task.Delay(80);Activity(main);
            Check("unrelated activity preserves inactivity deadline",first.InactiveSinceUtc==inactive);
            string token=await second.View.CoreWebView2.ExecuteScriptAsync("token");
            await Sleep(main,second);Check("inactive fixture actually suspends",second.IsSuspended&&second.View.CoreWebView2.IsSuspended);
            Check("sleeping tab has a visible moon and dimming",second.HeaderText.Text.StartsWith("☾ ")&&second.Item.Opacity<1);
            main.BrowserTabs.SelectedItem=second.Item;await Wait(()=>!second.View.CoreWebView2.IsSuspended);
            Check("selecting wakes tab without losing in-page state",await second.View.CoreWebView2.ExecuteScriptAsync("token")==token&&second.Item.Opacity==1);
            main.BrowserTabs.SelectedItem=foreground.Item;main.SetTabKeepAwake(second,true);await Sleep(main,second);
            Check("Keep awake prevents automatic and manual sleep",!second.IsSuspended&&main.BuildTabResourceRows().Single(r=>r.Id==second.ResourceId).State=="Kept awake");
            main.SetTabKeepAwake(second,false);await Sleep(main,second);Check("allow sleep removes the exception",second.IsSuspended);
            var backgroundLoad=NextLoad(second.View.CoreWebView2,"https://performance.test.invalid/performance.html?navigated=1");second.View.CoreWebView2.Navigate("https://performance.test.invalid/performance.html?navigated=1");await backgroundLoad;
            Check("background navigation resets sleeping state and restarts timer",!second.IsSuspended&&second.SuspendTimer.IsEnabled);await Sleep(main,second);
            first.InactiveSinceUtc=DateTime.UtcNow.AddSeconds(-20);first.RetrySleepAfterUtc=default(DateTime);Activity(main);await Wait(()=>first.IsSuspended);
            Check("overdue automatic sleep timer really suspends tab",first.View.CoreWebView2.IsSuspended);
            main.SetTabKeepAwake(first,true);main.SetTabKeepAwake(first,false);
            var pending=Sleep(main,first);main.BrowserTabs.SelectedItem=first.Item;await pending;
            Check("switching active during sleep check keeps page awake",!first.View.CoreWebView2.IsSuspended);
            main.BrowserTabs.SelectedItem=foreground.Item;
            var actions=(ResourceMonitorActions)typeof(MainWindow).GetMethod("MonitorActions",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
            Func<string> report=()=>Convert.ToString(typeof(MainWindow).GetMethod("BuildResourceReport",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null));
            foreach(string mode in new[]{"Light","Dark"})
            {
                AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});
                var monitor=Offscreen(new ResourceMonitorWindow(report,actions){Width=680,Height=440});monitor.Show();await Task.Delay(90);
                monitor.TabGrid.SelectedItem=monitor.TabGrid.Items.Cast<TabResourceRow>().Single(r=>r.Id==second.ResourceId);
                Check(mode+" resource monitor shows sleeping tab status",((TabResourceRow)monitor.TabGrid.SelectedItem).State=="Sleeping");
                Click(monitor,"Wake selected");await Wait(()=>!second.IsSuspended);
                Check(mode+" monitor wake preserves selected page",main.BrowserTabs.SelectedItem==foreground.Item&&monitor.TabGrid.SelectedItem is TabResourceRow&&((TabResourceRow)monitor.TabGrid.SelectedItem).Id==second.ResourceId);
                Click(monitor,"Keep awake");Check(mode+" monitor toggles Keep awake",second.KeepAwake&&!monitor.SleepTabButton.IsEnabled);
                var selectedRow=monitor.TabGrid.SelectedItem;Click(monitor,"Refresh now");Check(mode+" refresh preserves row identity and selection",ReferenceEquals(selectedRow,monitor.TabGrid.SelectedItem));
                ((Panel)monitor.Content).Background=monitor.Background;await Task.Delay(40);Capture((FrameworkElement)monitor.Content,Path.Combine(output,"resource-tabs-"+mode.ToLowerInvariant()+".png"));
                Click(monitor,"Allow sleep");Click(monitor,"Sleep selected");await Wait(()=>second.IsSuspended);Check(mode+" monitor sleeps the selected background tab",true);
                monitor.Width=520;monitor.Height=340;await Task.Delay(60);monitor.UpdateLayout();Check(mode+" short monitor keeps controls reachable",monitor.TabGrid.ActualHeight>20&&monitor.SleepInactiveButton.IsVisible&&monitor.SleepInactiveButton.TranslatePoint(new Point(0,monitor.SleepInactiveButton.ActualHeight),(FrameworkElement)monitor.Content).Y<=((FrameworkElement)monitor.Content).ActualHeight);
                monitor.Close();
            }
            AppearanceService.ApplyApplication(settings);
            var video=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=video.Item;await Wait(()=>video.Initialized);
            var core=video.View.CoreWebView2;core.SetVirtualHostNameToFolderMapping("media.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);core.SetVirtualHostNameToFolderMapping("media-frame.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);
            var loaded=NextLoad(core,"https://media.test.invalid/video.html");core.Navigate("https://media.test.invalid/video.html");await loaded;await Wait(()=>video.MediaFrames.Count>0);var frame=video.MediaFrames[0];
            await WaitJsForFrame(frame,"!!document.querySelector('video') && !document.querySelector('video').paused && document.querySelector('video').readyState >= 2");
            main.BrowserTabs.SelectedItem=foreground.Item;await Sleep(main,video);
            Check("muted cross-origin video remains awake",!video.IsSuspended&&video.SleepReason.Contains("media"));
            await Task.Delay(180);Check("protected background video is still playing",await frame.ExecuteScriptAsync("!document.querySelector('video').paused")=="true");CloseTab(main,video);
            using(var server=new DownloadFixtureServer())
            {
                var owner=await PerformanceTab(main,fixtures);var downloads=(ObservableCollection<DownloadEntry>)Field(main,"_downloads");owner.View.CoreWebView2.Navigate(server.Url+"/sleep-protection");await Wait(()=>downloads.Any(d=>d.IsActive&&d.BytesReceived>0));var entry=downloads.First(d=>d.IsActive);
                main.BrowserTabs.SelectedItem=foreground.Item;await Sleep(main,owner);Check("actual active download protects its source tab",!owner.IsSuspended&&owner.SleepReason.Contains("Download"));
                entry.Operation.Pause();await Wait(()=>entry.CanResume);await Sleep(main,owner);Check("paused download also protects its source tab",!owner.IsSuspended&&owner.SleepReason.Contains("Download"));
                report();await Task.Delay(1100);string snapshot=report();File.WriteAllText(Path.Combine(output,"resource-with-download.txt"),snapshot);
                Check("resource monitor samples WebView2 process CPU",snapshot.Contains("  CPU ")&&snapshot.Contains("%")&&!snapshot.Contains("snapshot unavailable"));
                entry.Operation.Cancel();await Wait(()=>!entry.IsActive&&!entry.CanResume);CloseTab(main,owner);
            }
            settings.SuspendInactiveTabs=false;Activity(main);Check("disabling automatic sleep wakes existing sleepers",!second.IsSuspended&&second.View.CoreWebView2.IsSuspended==false);
        }
        finally
        {
            settings.SuspendInactiveTabs=previousEnabled;settings.InactiveTabSuspendDelaySeconds=previousDelay;CloseTab(main,first);CloseTab(main,second);CloseTab(main,foreground);Activity(main);
        }
    }
    private static async Task WaitJsForFrame(CoreWebView2Frame frame,string expression)
    {for(int i=0;i<100;i++){if(await frame.ExecuteScriptAsync(expression)=="true")return;await Task.Delay(50);}throw new Exception("Frame media not ready");}
}
