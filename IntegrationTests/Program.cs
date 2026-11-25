using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HarborBrowser;
using HarborBrowser.Services;
using HarborBrowser.Models;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static int checks;
    private static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    private static void Check(string name,bool value){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);Console.Out.Flush();checks++;}
    private static async Task Wait(Func<bool> predicate){for(int i=0;i<250;i++){if(predicate())return;await Task.Delay(100);}throw new TimeoutException("Timed out waiting for app state.");}
    private static string Label(MenuItem item){var text=item.Header as TextBlock;return text==null?Convert.ToString(item.Header):text.Text;}
    private static MenuItem Find(MenuItem parent,string name){return parent.Items.OfType<MenuItem>().First(i=>Label(i)==name);}
    private static MenuItem Groups(MainWindow main){return (MenuItem)typeof(MainWindow).GetMethod("CreateTabGroupsMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);}
    [STAThread]
    private static int Main(string[] args)
    {
        int exit=1;Directory.CreateDirectory(args[0]);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/HarborBrowser;component/Themes/Controls.xaml")});
        var store=new BrowserDataStore(Path.Combine(args[0],"Profile"),Path.Combine(args[0],"Downloads"));
        store.SaveSettings(new BrowserSettings{RestorePreviousSession=false,HarborHomeIntroduced=true,ThemeMode="Dark",OnlineSuggestionsEnabled=false});
        store.SaveHistory(Enumerable.Range(1,30).Select(i=>new HistoryEntry{Title="History sample "+i,Url="https://history.test.invalid/page-"+i,VisitedAt=new DateTime(2026,11,12,12,0,0).AddMinutes(-i)}).ToList());
        var layout=new MonitorLayout(new PixelRect(-20000,0,-18720,720),new PixelRect(-20000,0,-18720,680),3);
        bool interactive=args.Length>2&&args[2]=="--interactive-clicks";
        Func<IntPtr,MonitorLayout> monitors=interactive?null:new Func<IntPtr,MonitorLayout>(h=>layout);
        var main=new MainWindow(store,monitors){WindowStartupLocation=WindowStartupLocation.Manual,Left=interactive?80:-20000,Top=20,Width=1000,Height=600,ShowInTaskbar=interactive,ShowActivated=interactive};
        main.TestBrowserArguments="--use-fake-device-for-media-stream";
        var timeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(240)};
        timeout.Tick+=(s,e)=>{Console.WriteLine("FAIL integration timed out");app.Shutdown(1);};
        main.Loaded+=async(s,e)=>{
            try
            {
                await Wait(()=>main.BrowserTabs.Items.Count>0&&((BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag).Initialized);
                var tab=(BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag;
                var core=tab.View.CoreWebView2;await Task.Delay(400);
                if(interactive)
                {
                    core.SetVirtualHostNameToFolderMapping("click.test.invalid",args[1],CoreWebView2HostResourceAccessKind.DenyCors);
                    ((System.Collections.Generic.List<Bookmark>)Field(main,"_bookmarks")).Add(new Bookmark{Title="clicksentinel bookmark",Url="https://click.test.invalid/performance.html?bookmark=1"});
                    ((System.Collections.Generic.List<HistoryEntry>)Field(main,"_history")).Insert(0,new HistoryEntry{Title="clicksentinel history",Url="https://click.test.invalid/performance.html?history=1",VisitedAt=DateTime.Now});
                    core.NavigationStarting+=(ss,ee)=>{File.AppendAllText(Path.Combine(args[0],"interactive-navigations.log"),ee.Uri+Environment.NewLine);if(!HomePageService.IsHomeUrl(ee.Uri)&&!ee.Uri.StartsWith("https://click.test.invalid/"))ee.Cancel=true;};
                    core.DocumentTitleChanged+=(ss,ee)=>main.Title="Isolated Harbor suggestion click test";main.Title="Isolated Harbor suggestion click test";
                    main.Activate();main.AddressBox.Focus();main.AddressBox.Text="clicksentinel";
                    var done=new TaskCompletionSource<bool>();main.Closed+=(ss,ee)=>done.TrySetResult(true);await done.Task;exit=0;return;
                }
                if(args.Length>2&&args[2]=="--suggestion-clicks")
                {await SuggestionClickChecks(main);Console.WriteLine("ALL PASSED: "+checks+" focused suggestion-click checks.");exit=0;return;}
                if(args.Length>2&&args[2]=="--privacy-edge")
                {await PrivacyEdgeChecks(main,args[0],args[1]);Console.WriteLine("ALL PASSED: "+checks+" focused privacy edge checks.");exit=0;return;}
                core.SetVirtualHostNameToFolderMapping("media.test.invalid",args[1],CoreWebView2HostResourceAccessKind.DenyCors);
                core.SetVirtualHostNameToFolderMapping("media-frame.test.invalid",args[1],CoreWebView2HostResourceAccessKind.DenyCors);
                var loaded=new TaskCompletionSource<bool>();
                core.NavigationCompleted+=(ss,ee)=>{if(core.Source.StartsWith("https://media.test.invalid/")){if(ee.IsSuccess)loaded.TrySetResult(true);else loaded.TrySetException(new Exception("Media fixture load: "+ee.WebErrorStatus));}};
                core.Navigate("https://media.test.invalid/video.html");await loaded.Task;
                await Wait(()=>tab.MediaFrames.Count>0);
                var frame=tab.MediaFrames[0];
                for(int i=0;i<60;i++){if(VideoScriptService.Score(await frame.ExecuteScriptAsync(VideoScriptService.Command("probe")))>0)break;await Task.Delay(100);}
                Check("cross-origin iframe video discovered",VideoScriptService.Score(await frame.ExecuteScriptAsync(VideoScriptService.Command("probe")))>0);
                Check("group bar absent",main.FindName("TabGroupFilterBox")==null);
                Check("caption and tabs share same row",Math.Abs(main.BrowserTabs.TransformToAncestor(main.WindowFrame).Transform(new Point()).Y-main.CloseWindowButton.TransformToAncestor(main.WindowFrame).Transform(new Point()).Y)<5);
                Check("Beacon window icon loaded",main.Icon!=null&&main.HeaderBrandLogo.Source!=null);
                await ResponsiveChecks(main,tab,core,args[0]);
                main.Presentation.SetBrowserFullScreen(true);
                await Task.Delay(200);
                NativeWindowGeometry.Rect bounds;NativeWindowGeometry.GetWindowRect(new WindowInteropHelper(main).Handle,out bounds);
                Check("browser fullscreen uses monitor work area",bounds.Pixels.Height==680&&bounds.Pixels.Width==1280);
                Check("browser fullscreen hides toolbars",main.BrowserChrome.Visibility==Visibility.Collapsed);
                main.Presentation.SetBrowserFullScreen(false);await Task.Delay(200);
                Check("browser fullscreen restores chrome",main.BrowserChrome.Visibility==Visibility.Visible);
                main.Activate();tab.View.Focus();await Task.Delay(200);
                await core.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled","{\"enabled\":true}");
                var tree=Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(await core.CallDevToolsProtocolMethodAsync("Page.getFrameTree","{}"));
                var treeRoot=(System.Collections.Generic.Dictionary<string,object>)tree["frameTree"];
                var child=(System.Collections.Generic.Dictionary<string,object>)((System.Collections.IList)treeRoot["childFrames"])[0];
                var childInfo=(System.Collections.Generic.Dictionary<string,object>)child["frame"];
                var world=Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(await core.CallDevToolsProtocolMethodAsync("Page.createIsolatedWorld",Json.Serialize(new{frameId=(string)childInfo["id"],worldName="HarborIntegration"})));
                int contextId=Convert.ToInt32(world["executionContextId"]);
                string request="document.querySelector('video').requestFullscreen()";
                string result=await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate",Json.Serialize(new {expression=request,contextId=contextId,userGesture=true,awaitPromise=true,returnByValue=true}));

                Check("fullscreen request succeeded",!result.Contains("exceptionDetails"));
                await Wait(()=>core.ContainsFullScreenElement&&main.Presentation.VideoFullScreen);
                await Task.Delay(250);
                NativeWindowGeometry.GetWindowRect(new WindowInteropHelper(main).Handle,out bounds);
                Check("HTML fullscreen fills complete monitor",bounds.Pixels.Width==1280&&bounds.Pixels.Height==720);
                Check("video fullscreen removes header/status/frame",main.BrowserChrome.Visibility==Visibility.Collapsed&&main.BottomStatus.Visibility==Visibility.Collapsed&&main.WindowFrame.BorderThickness.Top==0);
                using(var file=File.Create(Path.Combine(args[0],"video-fullscreen.png")))await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
                var create=typeof(MainWindow).GetMethod("CreateNewTab",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string),typeof(bool)},null);
                var second=(BrowserTab)create.Invoke(main,new object[]{HomePageService.Url,false});
                main.BrowserTabs.SelectedItem=second.Item;await Task.Delay(150);
                Check("switching away from video restores browser chrome",!main.Presentation.VideoFullScreen&&main.BrowserChrome.Visibility==Visibility.Visible);
                main.BrowserTabs.SelectedItem=tab.Item;main.Activate();tab.View.Focus();
                await core.ExecuteScriptAsync("document.fullscreenElement ? document.exitFullscreen() : null");
                await Wait(()=>!core.ContainsFullScreenElement);
                await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate",Json.Serialize(new {expression=request,contextId=contextId,userGesture=true,awaitPromise=true,returnByValue=true}));
                await Wait(()=>core.ContainsFullScreenElement&&main.Presentation.VideoFullScreen);
                main.Presentation.SetBrowserFullScreen(true);
                await core.ExecuteScriptAsync("document.exitFullscreen()");
                await Wait(()=>!core.ContainsFullScreenElement&&!main.Presentation.VideoFullScreen);
                await Task.Delay(150);
                NativeWindowGeometry.GetWindowRect(new WindowInteropHelper(main).Handle,out bounds);
                Check("exiting video retains prior browser fullscreen",main.Presentation.BrowserFullScreen&&bounds.Pixels.Height==680);
                main.Presentation.SetBrowserFullScreen(false);
                Check("fullscreen restores normal resize minimums",main.MinWidth==480&&main.MinHeight==280);
                await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate",Json.Serialize(new {expression=request,contextId=contextId,userGesture=true,awaitPromise=true,returnByValue=true}));
                await Wait(()=>core.ContainsFullScreenElement&&main.Presentation.VideoFullScreen);
                var originalView=tab.View;
                double before=Json.Deserialize<double>(await frame.ExecuteScriptAsync("document.querySelector('video').currentTime"));
                await main.TogglePictureInPictureAsync(tab);await Task.Delay(400);
                Check("picture in picture window opened",tab.VideoPopout!=null&&tab.VideoPopout.IsVisible);
                Check("PiP window stays on top",tab.VideoPopout.Topmost);
                Check("PiP keeps original browser/player",ReferenceEquals(tab.VideoPopout.PlayerHost.Content,originalView)&&ReferenceEquals(tab.View,originalView));
                Check("PiP exits video fullscreen cleanly",!main.Presentation.VideoFullScreen&&!core.ContainsFullScreenElement);
                Check("iframe focus expanded in popout",await core.ExecuteScriptAsync("document.querySelector('iframe').classList.contains('harbor-pip-focus')")=="true");
                Check("video controls available in popout",await frame.ExecuteScriptAsync("document.querySelector('video').controls")=="true");
                Check("PiP page has no scrollbar",await core.ExecuteScriptAsync("document.documentElement.scrollHeight <= innerHeight && document.documentElement.scrollWidth <= innerWidth")=="true");
                Check("PiP iframe has no scrollbar",await frame.ExecuteScriptAsync("document.documentElement.scrollHeight <= innerHeight && document.documentElement.scrollWidth <= innerWidth")=="true");
                var controls=(StackPanel)((DockPanel)tab.VideoPopout.Content).Children[0];
                var play=controls.Children.OfType<Button>().First(b=>Convert.ToString(b.Content)=="Play / pause");
                var mute=controls.Children.OfType<Button>().First(b=>Convert.ToString(b.Content)=="Mute / unmute");
                play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(150);
                Check("PiP play/pause button pauses video",await frame.ExecuteScriptAsync("document.querySelector('video').paused")=="true");
                play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(150);
                Check("PiP play/pause button resumes video",await frame.ExecuteScriptAsync("document.querySelector('video').paused")=="false");
                mute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);
                Check("PiP mute button unmutes video",await frame.ExecuteScriptAsync("document.querySelector('video').muted")=="false");
                mute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);
                Check("PiP mute button restores mute",await frame.ExecuteScriptAsync("document.querySelector('video').muted")=="true");
                double after=Json.Deserialize<double>(await frame.ExecuteScriptAsync("document.querySelector('video').currentTime"));
                Check("video playback position retained",after>=before);
                Check("PiP tab not suspended",!tab.IsSuspended);
                using(var file=File.Create(Path.Combine(args[0],"picture-in-picture.png")))await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
                tab.VideoPopout.Close();await Task.Delay(400);
                Check("PiP close returns original browser to tab",tab.VideoPopout==null&&ReferenceEquals(main.BrowserContentHost.Content,originalView));
                Check("iframe layout restored",await core.ExecuteScriptAsync("!document.querySelector('iframe').classList.contains('harbor-pip-focus')")=="true");
                Check("original video controls restored",await frame.ExecuteScriptAsync("document.querySelector('video').controls")=="false");
                var groups=Groups(main);var move=Find(groups,"Move current tab to");
                Find(move,"Work").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Check("cog assigns current tab to group",tab.GroupName=="Work");
                Find(Groups(main),"Show Personal").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Check("cog filters tab group",tab.Item.Visibility==Visibility.Collapsed);
                Find(Groups(main),"Show all tabs").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Check("cog restores all tabs",tab.Item.Visibility==Visibility.Visible);

                Check("new actual app tab appended right",ReferenceEquals(main.BrowserTabs.Items[1],second.Item));
                typeof(MainWindow).GetMethod("MoveTabRelative",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{second,-1});
                Check("actual app reorder changes tab order",ReferenceEquals(main.BrowserTabs.Items[0],second.Item));
                var session=store.LoadSession();Check("reordered session persisted",session.Tabs.Count==2&&session.Tabs[0].Url==HomePageService.Url);
                var bitmap=new RenderTargetBitmap((int)main.WindowFrame.ActualWidth,83,96,96,PixelFormats.Pbgra32);bitmap.Render(main.WindowFrame);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using(var file=File.Create(Path.Combine(args[0],"compact-header.png")))encoder.Save(file);
                await EverydayChecks(main,tab,store,core,args[0],args[1]);
                await CustomizationChecks(main,store,args[0]);
                await PerformanceChecks(main,args[0],args[1]);
                await PrivacyChecks(main,store,args[0],args[1]);
                await PrivacyEdgeChecks(main,args[0],args[1]);
                Console.WriteLine("ALL PASSED: "+checks+" isolated app/media integration checks.");
                exit=0;
            }
            catch(Exception ex){Console.WriteLine("FAIL "+ex);}
            finally
            {
                timeout.Stop();
                typeof(MainWindow).GetField("_confirmedCloseWithActiveDownloads",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,true);
                // Close only the isolated test app and its test browser controls.
                try{await (Task)typeof(MainWindow).GetMethod("FinishPrivacyBeforeClose",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);typeof(MainWindow).GetField("_closingAfterPrivacyCleanup",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,true);}catch(Exception ex){Console.WriteLine("FAIL test cleanup "+ex);exit=1;}
                try{main.Close();}catch{}
                app.Shutdown(exit);
            }
        };
        timeout.Start();main.Show();app.Run();return exit;
    }
}
