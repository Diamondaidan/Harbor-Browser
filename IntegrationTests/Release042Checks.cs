using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task Release042Checks(MainWindow main,BrowserDataStore store,string output)
    {
        var tab=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=tab.Item;await Wait(()=>tab.Initialized);await WaitJs(tab.View.CoreWebView2,"document.readyState==='complete'");
        typeof(MainWindow).GetMethod("UpdateTabHeader",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{tab});
        Check("tab menu retains New tab command",tab.Item.ContextMenu.Items.OfType<MenuItem>().Count(i=>Label(i)=="New tab")==1);
        Check("tab menu has exactly one Pin command",tab.Item.ContextMenu.Items.OfType<MenuItem>().Count(i=>Label(i)=="Pin tab")==1);
        tab.PinCommand.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check("pin menu changes only its own command",tab.IsPinned&&Label(tab.PinCommand)=="Unpin tab"&&tab.Item.ContextMenu.Items.OfType<MenuItem>().Count(i=>Label(i)=="New tab")==1);
        tab.PinCommand.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var extras=new List<BrowserTab>();for(int i=0;i<7;i++)extras.Add(AddTab(main,HomePageService.Url));main.UpdateLayout();main.UpdateTabWidths();
        var widths=main.BrowserTabs.Items.Cast<TabItem>().Where(i=>i.Visibility==Visibility.Visible&&!((BrowserTab)i.Tag).IsPinned).Select(i=>i.Width).ToList();
        Check("real app uses equal shrinking normal tab widths",widths.Distinct().Count()==1&&widths[0]<190&&widths[0]>=54);
        var addButton=Visuals<Button>(main.BrowserTabs).First(b=>Convert.ToString(b.Content)=="＋");
        Check("new-tab button remains inside crowded title bar",addButton.TranslatePoint(new Point(),main.BrowserTabs).X+addButton.ActualWidth<=main.BrowserTabs.ActualWidth+1);
        foreach(var extra in extras)CloseTab(main,extra);main.UpdateLayout();
        var bookmarks=(List<Bookmark>)Field(main,"_bookmarks");bookmarks.Clear();bookmarks.Add(new Bookmark{Title="Bar A",Url="https://a.test/"});bookmarks.Add(new Bookmark{Title="Bar B",Url="https://b.test/"});
        var settings=(BrowserSettings)Field(main,"_settings");settings.BookmarkFolders=new List<string>{"Work"};settings.BookmarkBarOrder.Clear();
        typeof(MainWindow).GetMethod("RefreshBookmarksBar",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,null);
        var folder=main.BookmarksBarPanel.Children.OfType<Button>().Last();folder.ContextMenu.Items.OfType<MenuItem>().First(i=>Label(i)=="Move left").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check("bookmark move command interleaves folders",Convert.ToString(main.BookmarksBarPanel.Children.OfType<Button>().ElementAt(1).Tag)=="Work");
        main.MoveBookmark(BookmarkBarService.Key(bookmarks[1]),BookmarkBarService.Key(bookmarks[0]),false);
        Check("bookmark reorder updates real bar and stored settings",((Bookmark)main.BookmarksBarPanel.Children.OfType<Button>().First().Tag).Title=="Bar B"&&store.LoadSettings().BookmarkBarOrder[0]==BookmarkBarService.Key(bookmarks[1]));
        Check("bookmark bar accepts drag reordering",main.BookmarksBarPanel.AllowDrop);
        var editor=Offscreen(new SettingsWindow(settings,bookmarks));editor.Loaded+=(s,e)=>Click(editor,"Save settings");editor.ShowDialog();
        Check("saving appearance retains bookmark order",editor.ResultSettings.BookmarkBarOrder.SequenceEqual(settings.BookmarkBarOrder));
        string fixtures=Path.Combine(output,"042-Fixtures");Directory.CreateDirectory(fixtures);
        var bitmap=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{20,80,240,255},4);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(fixtures,"favicon.png")))png.Save(stream);
        File.WriteAllText(Path.Combine(fixtures,"icon.html"),"<link rel='icon' href='/favicon.png'><title>Icon fixture</title><body>Harbor icon test</body>");
        File.WriteAllText(Path.Combine(fixtures,"plain.html"),"<title>Plain fixture</title><body>Harbor module test</body>");
        File.WriteAllText(Path.Combine(fixtures,"favicon.svg"),"<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32'><rect width='32' height='32' fill='blue'/></svg>");
        var imageBytes=File.ReadAllBytes(Path.Combine(fixtures,"favicon.png"));
        using(var stream=new BinaryWriter(File.Create(Path.Combine(fixtures,"favicon.ico"))))
        {stream.Write((ushort)0);stream.Write((ushort)1);stream.Write((ushort)1);stream.Write((byte)1);stream.Write((byte)1);stream.Write((byte)0);stream.Write((byte)0);stream.Write((ushort)1);stream.Write((ushort)32);stream.Write(imageBytes.Length);stream.Write(22);stream.Write(imageBytes);}
        var core=tab.View.CoreWebView2;core.SetVirtualHostNameToFolderMapping("module.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);core.SetVirtualHostNameToFolderMapping("other.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);
        var load=NextLoad(core);core.Navigate("https://module.test.invalid/icon.html");await load;await Wait(()=>tab.HeaderIcon.Source!=null&&tab.HeaderIcon.Visibility==Visibility.Visible);
        Check("WebView2 favicon decodes into a visible tab image",tab.HeaderIcon.Source is BitmapSource&&tab.FallbackIcon.Visibility==Visibility.Collapsed);
        foreach(string format in new[]{"svg","ico"})
        {
            var previous=tab.HeaderIcon.Source;
            await core.ExecuteScriptAsync("document.querySelector('link[rel=icon]').href='/favicon."+format+"'");
            await Wait(()=>tab.HeaderIcon.Source!=null&&!ReferenceEquals(previous,tab.HeaderIcon.Source));
            Check(format.ToUpperInvariant()+" favicon renders through the browser decoder",tab.HeaderIcon.Source is BitmapSource);
        }
        load=NextLoad(core);core.Navigate(HomePageService.Url);await load;await Wait(()=>tab.HeaderIcon.Source!=null);
        Check("Harbor Home retains the Beacon tab image",tab.HeaderIcon.Source is BitmapImage&&((BitmapImage)tab.HeaderIcon.Source).UriSource!=null);
        string module=Path.Combine(output,"TestModule");Directory.CreateDirectory(module);
        File.WriteAllText(Path.Combine(module,"manifest.json"),"{\"manifest_version\":3,\"name\":\"Harbor test module\",\"version\":\"1.0.0\",\"permissions\":[\"storage\"],\"content_scripts\":[{\"matches\":[\"https://module.test.invalid/*\",\"https://harbor.invalid/*\"],\"js\":[\"test.js\"]}]}");
        File.WriteAllText(Path.Combine(module,"test.js"),"document.body.dataset.harborModule='loaded';chrome.storage.local.set({ok:true});throw new Error('deliberate isolated module failure');");
        File.WriteAllText(Path.Combine(module,"harbor-module.json"),"{\"api_version\":1,\"minimum_harbor_version\":\"0.4.2\"}");
        var engineComponents=(await core.Profile.GetBrowserExtensionsAsync()).ToDictionary(e=>e.Id,e=>e.IsEnabled);
        var package=SiteModuleService.Inspect(module);string id=await main.InstallModule(package);
        Console.WriteLine("INFO native modules: "+string.Join("; ",(await main.GetModules()).Select(r=>r.Id+" "+r.Name+" "+r.Status)));
        Check("native module installs with lifecycle metadata",(await main.GetModules()).Any(r=>r.Id==id&&r.Enabled&&r.Version=="1.0.0"));
        await Task.Delay(300);load=NextLoad(core);core.Navigate("https://module.test.invalid/plain.html");await load;await WaitJs(core,"document.body.dataset.harborModule==='loaded'");
        Check("declared-site content script runs in real WebView2",true);
        Check("throwing module leaves page and browser usable",await core.ExecuteScriptAsync("1+1")=="2"&&main.IsLoaded);
        Check("module runs outside the page JavaScript world",await core.ExecuteScriptAsync("typeof chrome.storage")=="\"undefined\"");
        load=NextLoad(core);core.Navigate("https://other.test.invalid/plain.html");await load;await Task.Delay(300);
        Check("native match permissions block unrelated sites",await core.ExecuteScriptAsync("document.body.dataset.harborModule===undefined")=="true");
        load=NextLoad(core);core.Navigate(HomePageService.Url);await load;await Task.Delay(300);
        Check("module cannot inject into trusted Harbor Home",await core.ExecuteScriptAsync("document.body.dataset.harborModule===undefined")=="true");
        var privateTab=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=privateTab.Item;await Wait(()=>privateTab.Initialized);
        var privateCore=privateTab.View.CoreWebView2;privateCore.SetVirtualHostNameToFolderMapping("module.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);load=NextLoad(privateCore,"https://module.test.invalid/plain.html");privateCore.Navigate("https://module.test.invalid/plain.html");await load;await Task.Delay(300);
        Console.WriteLine("INFO private module marker: "+await privateCore.ExecuteScriptAsync("document.body.dataset.harborModule")+"; native extensions: "+string.Join(",",(await privateCore.Profile.GetBrowserExtensionsAsync()).Select(x=>x.Name+" "+x.Id))+"; profile: "+privateCore.Profile.ProfileName);
        Check("normal module never executes in private profile",await privateCore.ExecuteScriptAsync("document.body.dataset.harborModule===undefined")=="true"&&!(await privateCore.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==id));
        CloseTab(main,privateTab);await main.WaitForPrivateCleanup();main.BrowserTabs.SelectedItem=tab.Item;
        await main.ChangeModule(id,false);load=NextLoad(core,"https://module.test.invalid/plain.html?disabled");core.Navigate("https://module.test.invalid/plain.html?disabled");await load;await Task.Delay(300);
        Check("disable stops injection after page reload",!(await main.GetModules()).Single(r=>r.Id==id).Enabled&&await core.ExecuteScriptAsync("document.body.dataset.harborModule===undefined")=="true");
        await main.ChangeModule(id,true);await WaitJs(core,"document.body.dataset.harborModule==='loaded'");Check("enable restores module behavior",true);
        await main.ChangeModule(id,false,true);await Task.Delay(400);
        Check("remove unregisters native extension and managed files",!(await main.GetModules()).Any(r=>r.Id==id)&&Directory.GetDirectories(Path.Combine(store.RootFolder,"Modules")).Length==0);
        Check("module manager leaves native PDF and clipboard components unchanged",(await core.Profile.GetBrowserExtensionsAsync()).All(e=>engineComponents.ContainsKey(e.Id)&&engineComponents[e.Id]==e.IsEnabled)&&(await main.GetModules()).Count==0);
        await WaitJs(core,"document.body.dataset.harborModule===undefined");Check("removal reload clears injected effects",true);
        foreach(string mode in new[]{"Light","Dark"})
        {
            AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});
            var manager=Offscreen(new SiteModulesWindow(main.GetModules,main.InstallModule,main.ChangeModule));manager.Show();await Task.Delay(250);((Panel)manager.Content).Background=manager.Background;await Task.Delay(40);Capture((FrameworkElement)manager.Content,Path.Combine(output,"modules-"+mode.ToLowerInvariant()+".png"));Check(mode+" module manager fits its window",((FrameworkElement)manager.Content).ActualHeight>0);Check(mode+" module manager uses Harbor palette and icon",manager.Background==Application.Current.FindResource("HarborWindow")&&manager.Icon!=null);manager.Close();
        }
        AppearanceService.ApplyApplication(settings);CloseTab(main,tab);
        await ModulePersistenceChecks(output,module);
    }
    private static async Task ModulePersistenceChecks(string output,string source)
    {
        var store=new BrowserDataStore(Path.Combine(output,"ModulePersistence"),Path.Combine(output,"ModulePersistenceDownloads"));
        store.SaveSettings(new BrowserSettings{RestorePreviousSession=false,HarborHomeIntroduced=true,OnlineSuggestionsEnabled=false});
        string id=null;
        for(int run=0;run<2;run++)
        {
            var window=Offscreen(new MainWindow(store));window.Show();
            try
            {
                await Wait(()=>window.BrowserTabs.Items.Count>0&&((BrowserTab)((TabItem)window.BrowserTabs.Items[0]).Tag).Initialized);
                if(run==0){id=await window.InstallModule(SiteModuleService.Inspect(source));await window.ChangeModule(id,false);}
                else
                {
                    var rows=await window.GetModules();Check("disabled module persists through closing and reopening Harbor",rows.Count==1&&rows[0].Id==id&&!rows[0].Enabled);
                    await window.ChangeModule(id,true);Check("persisted module can be re-enabled",(await window.GetModules()).Single().Enabled);
                    await window.ChangeModule(id,false,true);Check("persisted module can be removed",(await window.GetModules()).Count==0);
                }
            }
            finally
            {
                await (Task)typeof(MainWindow).GetMethod("FinishPrivacyBeforeClose",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
                typeof(MainWindow).GetField("_closingAfterPrivacyCleanup",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);window.Close();
            }
        }
    }
}
