using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static object Field(MainWindow main,string name){return typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);}
    private static BrowserTab AddTab(MainWindow main,string url,bool isPrivate=false,BrowserSessionTab saved=null)
    {return (BrowserTab)typeof(MainWindow).GetMethod("CreateNewTab",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string),typeof(bool),typeof(bool),typeof(BrowserSessionTab)},null).Invoke(main,new object[]{url,false,isPrivate,saved});}
    private static void CloseTab(MainWindow main,BrowserTab tab){typeof(MainWindow).GetMethod("CloseTab",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{tab});}
    private static void Key(MainWindow main,System.Windows.Input.Key key)
    {main.AddressBox.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(main),Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent,Source=main.AddressBox});}
    private static T[] Visuals<T>(DependencyObject root) where T:DependencyObject
    {var list=new System.Collections.Generic.List<T>();if(root is T)list.Add((T)root);for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)list.AddRange(Visuals<T>(VisualTreeHelper.GetChild(root,i)));return list.ToArray();}
    private static void DownloadButton(DownloadsPage page,DownloadEntry entry,string action)
    {
        page.DownloadList.ScrollIntoView(entry);page.UpdateLayout();
        var button=Visuals<Button>(page).First(b=>ReferenceEquals(b.DataContext,entry)&&Convert.ToString(b.Tag)==action);
        Check("download "+action+" button enabled",button.IsEnabled);button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    private static async Task EverydayChecks(MainWindow main,BrowserTab original,BrowserDataStore store,CoreWebView2 core,string output,string fixtures)
    {
        var recent=(RecentlyClosedTabsService)Field(main,"_recentlyClosed");
        int count=recent.Count;var privateTab=AddTab(main,HomePageService.Url,true);CloseTab(main,privateTab);
        Check("closing private app tab leaves restore list unchanged",recent.Count==count);
        var first=AddTab(main,HomePageService.Url,false,new BrowserSessionTab{Url=HomePageService.Url,IsPinned=true,GroupName="Work"});
        int firstIndex=main.BrowserTabs.Items.IndexOf(first.Item);
        var last=AddTab(main,HomePageService.Url);last.PageTitle="Most recently closed";
        CloseTab(main,first);CloseTab(main,last);
        var reopened=main.ReopenClosedTab();Check("actual app reopens newest closed tab",reopened.PageTitle=="Most recently closed"&&!reopened.IsPrivate);
        var restored=main.ReopenClosedTab();Check("actual app restores pin group and position",restored.IsPinned&&restored.GroupName=="Work"&&main.BrowserTabs.Items.IndexOf(restored.Item)==firstIndex);
        Check("reopened tab selected and visible",ReferenceEquals(main.BrowserTabs.SelectedItem,restored.Item)&&restored.Item.Visibility==Visibility.Visible);
        main.BrowserTabs.SelectedItem=original.Item;main.Activate();main.AddressBox.Focus();
        var bookmarks=(System.Collections.Generic.List<Bookmark>)Field(main,"_bookmarks");
        var history=(System.Collections.Generic.List<HistoryEntry>)Field(main,"_history");
        bookmarks.Add(new Bookmark{Title="Everyday bookmark",Url="https://suggestion.test.invalid/video.html?bookmark=1"});
        history.Add(new HistoryEntry{Title="Everyday history",Url="https://suggestion.test.invalid/video.html?history=1",VisitedAt=DateTime.Now});
        core.SetVirtualHostNameToFolderMapping("suggestion.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);
        main.AddressBox.Text="Everyday";await Task.Delay(90);
        Check("address suggestions popup opens from local data",main.AddressSuggestions.IsOpen&&main.AddressSuggestionList.Items.Count==2);
        Key(main,System.Windows.Input.Key.Down);Check("Down selects first suggestion",main.AddressSuggestionList.SelectedIndex==0);
        Key(main,System.Windows.Input.Key.Down);Check("Down selects next suggestion",main.AddressSuggestionList.SelectedIndex==1);
        Key(main,System.Windows.Input.Key.Up);Check("Up returns to first suggestion",main.AddressSuggestionList.SelectedIndex==0);
        await Task.Delay(40);Capture((FrameworkElement)main.AddressSuggestions.Child,Path.Combine(output,"address-suggestions.png"));
        Key(main,System.Windows.Input.Key.Escape);Check("Escape dismisses suggestions",!main.AddressSuggestions.IsOpen);
        main.AddressBox.Text="Everyday ";Key(main,System.Windows.Input.Key.Down);Key(main,System.Windows.Input.Key.Enter);
        await Wait(()=>core.Source.Contains("bookmark=1"));await Task.Delay(200);Check("Enter navigates selected suggestion",!main.AddressSuggestions.IsOpen);
        bool suggestionCapture=main.AddressSuggestions.StaysOpen;main.AddressSuggestions.StaysOpen=true;
        main.AddressBox.Focus();main.AddressBox.Text="Everyday";await Task.Delay(60);
        Check("mouse suggestion list visible",main.AddressSuggestions.IsOpen&&main.AddressSuggestionList.Items.Count==2);
        var clickedSuggestion=SuggestionPart(main,1);SuggestionMouse(clickedSuggestion,true);SuggestionMouse(clickedSuggestion,false);
        main.AddressSuggestions.StaysOpen=suggestionCapture;
        await Wait(()=>core.Source.Contains("history=1"));Check("mouse selection opens history suggestion",!main.AddressSuggestions.IsOpen);
        var privateSuggestions=AddTab(main,HomePageService.Url,true);
        var privateReady=new TaskCompletionSource<bool>();privateSuggestions.View.NavigationCompleted+=(s,e)=>privateReady.TrySetResult(e.IsSuccess);
        main.BrowserTabs.SelectedItem=privateSuggestions.Item;await privateReady.Task;
        main.Activate();main.AddressBox.Focus();main.AddressBox.Text="Everyday";await Task.Delay(80);
        Check("private app tab excludes normal history suggestions",main.AddressSuggestionList.Items.Cast<AddressSuggestion>().All(s=>s.SourceLabel=="Bookmark")&&main.AddressSuggestionList.Items.Count==1);
        typeof(MainWindow).GetMethod("RecordHistory",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{privateSuggestions,"https://private.test.invalid/secret","Private sentinel"});
        Check("private navigation is not saved to history",!store.LoadHistory().Any(h=>h.Title=="Private sentinel"));
        CloseTab(main,privateSuggestions);main.BrowserTabs.SelectedItem=original.Item;
        using(var server=new DownloadFixtureServer())
        {
            var downloads=(ObservableCollection<DownloadEntry>)Field(main,"_downloads");
            core.Navigate(server.Url+"/archive");await Wait(()=>downloads.Any(d=>d.BytesReceived>0&&d.IsActive));
            var entry=downloads.First(d=>d.Operation!=null);
            var pageTab=main.OpenDownloadsPage();var page=(DownloadsPage)pageTab.NativeContent;await Task.Delay(100);
            Check("Downloads opens as native browser tab",ReferenceEquals(main.BrowserContentHost.Content,page)&&main.AddressBox.Text==MainWindow.DownloadsUrl&&!pageTab.Initialized);
            int tabs=main.BrowserTabs.Items.Count;Check("Downloads reuses existing page",ReferenceEquals(main.OpenDownloadsPage(),pageTab)&&main.BrowserTabs.Items.Count==tabs);
            Check("live download appears in Downloads page",page.DownloadList.Items.Contains(entry)&&entry.BytesReceived>0);
            Check("Downloads page is saved in normal session",store.LoadSession().Tabs.Any(t=>t.Url==MainWindow.DownloadsUrl));
            DownloadButton(page,entry,"pause");await Wait(()=>entry.CanResume&&!entry.IsActive);
            Check("paused transfer is labelled Paused",entry.Status=="Paused");
            long pausedBytes=entry.BytesReceived;server.DisconnectTransfers();await Task.Delay(120);
            Check("paused progress stays stopped",entry.BytesReceived==pausedBytes);
            double width=main.Width,height=main.Height;main.Width=659;main.Height=320;await Task.Delay(70);
            var pageScroll=FindVisual<ScrollViewer>(page);pageScroll.ScrollToBottom();await Task.Delay(50);
            Check("short Downloads page scrolls all content",pageScroll.ScrollableHeight>0);
            var resumeButton=Visuals<Button>(page).First(b=>ReferenceEquals(b.DataContext,entry)&&Convert.ToString(b.Tag)=="resume");
            var resumePosition=resumeButton.TransformToAncestor(page).Transform(new Point());
            Check("download controls reachable on small screen",resumePosition.Y>=0&&resumePosition.Y+resumeButton.ActualHeight<=page.ActualHeight+.5);
            Capture(page,Path.Combine(output,"downloads-small-screen.png"));
            main.Width=width;main.Height=height;pageScroll.ScrollToTop();await Task.Delay(60);
            Capture(page,Path.Combine(output,"downloads-dark.png"));
            Console.WriteLine("INFO before resume: "+entry.Operation.State+" "+entry.Operation.InterruptReason+" CanResume="+entry.Operation.CanResume);
            DownloadButton(page,entry,"resume");await Task.Delay(400);
            Console.WriteLine("INFO after resume: "+entry.Operation.State+" "+entry.Operation.InterruptReason+" CanResume="+entry.Operation.CanResume+" UI="+entry.Status);
            await Wait(()=>entry.Status=="Complete"||entry.Status.StartsWith("Interrupted:"));
            Check("resumed transfer completes",entry.Status=="Complete");
            Check("resume retains one download row and original filename",downloads.Count==1&&entry.FileName=="harbor-download.zip");
            Check("resume reconnects after transfer connection closes",server.TransferRequests>1);
            Console.WriteLine("INFO HTTP range requests: "+server.RangeRequests);
            Check("download filename has zip extension",Path.GetExtension(entry.FilePath)==".zip");
            Check("completed download bytes match source",File.ReadAllBytes(entry.FilePath).SequenceEqual(server.Payload));
            using(var zip=ZipFile.OpenRead(entry.FilePath))Check("downloaded ZIP opens",zip.Entries.Count==1);
            Check("completed row exposes open and folder actions",entry.CanOpen&&entry.CanReveal&&!entry.CanCancel&&entry.CanRemove);
            page.DownloadSearch.Text="missing download";Check("Downloads search filters list",page.DownloadList.Items.Count==0);page.DownloadSearch.Clear();
            var privateEntry=new DownloadEntry{FileName="private-sample.zip",IsPrivate=true,Status="Complete"};downloads.Insert(0,privateEntry);
            Check("normal Downloads excludes private entries",!page.DownloadList.Items.Contains(privateEntry));
            var privateOwner=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=privateOwner.Item;
            privateEntry.PrivateSessionId=privateOwner.PrivateSessionId;
            var privatePageTab=main.OpenDownloadsPage();var privatePage=(DownloadsPage)privatePageTab.NativeContent;await Task.Delay(50);
            Check("private Downloads contains only private entries",privatePageTab.IsPrivate&&privatePage.DownloadList.Items.Contains(privateEntry)&&!privatePage.DownloadList.Items.Contains(entry));
            main.BrowserTabs.SelectedItem=pageTab.Item;
            AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode="Light"});await Task.Delay(80);Capture(page,Path.Combine(output,"downloads-light.png"));AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode="Dark"});
            var completedPath=entry.FilePath;
            page.GetType().GetMethod("Clear_Click",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(page,new object[]{page,new RoutedEventArgs()});
            Check("clear completed keeps disk file and private list",!downloads.Contains(entry)&&File.Exists(completedPath)&&downloads.Contains(privateEntry));
            core.Navigate(server.Url+"/cancel");await Wait(()=>downloads.Any(d=>d.IsActive&&d.BytesReceived>0));
            var cancel=downloads.First(d=>d.IsActive);await Task.Delay(70);DownloadButton(page,cancel,"cancel");await Wait(()=>!cancel.IsActive&&!cancel.CanResume);
            Check("cancelled row allows removal",cancel.Status=="Cancelled"&&cancel.CanRemove);DownloadButton(page,cancel,"remove");Check("remove deletes only row",!downloads.Contains(cancel));
            CloseTab(main,privateOwner);CloseTab(main,privatePageTab);await main.WaitForPrivateCleanup();
            Check("last private tab clears its download records",!downloads.Contains(privateEntry));
            var owner=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=owner.Item;await Wait(()=>owner.Initialized);
            owner.View.CoreWebView2.Navigate(server.Url+"/background");await Wait(()=>downloads.Any(d=>d.IsActive&&d.BytesReceived>0));
            var background=downloads.First(d=>d.IsActive);CloseTab(main,owner);
            await Wait(()=>background.Status=="Complete");Check("download survives closing its source tab",File.ReadAllBytes(background.FilePath).SequenceEqual(server.Payload));
            Check("closed source is released after download completes",((System.Collections.Generic.ICollection<BrowserTab>)Field(main,"_retainedDownloadTabs")).Count==0);
            main.BrowserTabs.SelectedItem=pageTab.Item;
            // A native Downloads tab can navigate normally when an address is entered.
            typeof(MainWindow).GetMethod("NavigateCurrent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{HomePageService.Url});
            await Wait(()=>pageTab.Initialized);Check("Downloads tab can navigate back to web content",pageTab.NativeContent==null&&ReferenceEquals(main.BrowserContentHost.Content,pageTab.View));
        }
    }
}
