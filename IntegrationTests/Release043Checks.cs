using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task Release043Checks(MainWindow main,BrowserDataStore store,string output)
    {
        var tab=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=tab.Item;await Wait(()=>tab.Initialized);
        var crowded=new List<BrowserTab>();for(int i=0;i<24;i++)crowded.Add(AddTab(main,HomePageService.Url));main.UpdateLayout();main.BrowserTabs.SelectedItem=crowded.Last().Item;await Task.Delay(100);
        var tabScroll=(ScrollViewer)typeof(MainWindow).GetMethod("FindTabScroller",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{main.BrowserTabs});
        Check("selecting an overflow tab scrolls it into view",tabScroll.ScrollableWidth>0&&tabScroll.HorizontalOffset>0);
        main.BrowserTabs.SelectedItem=tab.Item;await Task.Delay(80);Check("returning to an earlier tab scrolls back",tabScroll.HorizontalOffset<tabScroll.ScrollableWidth);
        foreach(var extra in crowded)CloseTab(main,extra);
        var bookmarks=(List<Bookmark>)Field(main,"_bookmarks");bookmarks.Clear();bookmarks.AddRange(new[]{new Bookmark{Title="Drag A",Url="https://a.test/"},new Bookmark{Title="Drag B",Url="https://b.test/"}});
        ((BrowserSettings)Field(main,"_settings")).BookmarkFolders.Clear();((BrowserSettings)Field(main,"_settings")).BookmarkBarOrder.Clear();
        typeof(MainWindow).GetMethod("RefreshBookmarksBar",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,null);main.UpdateLayout();
        Check("bookmark bar has an empty-area drop surface",main.BookmarksBarPanel.Background!=null&&main.BookmarksBarPanel.ActualWidth>=main.BookmarksBar.ActualWidth-2.5);
        var type=typeof(MainWindow).GetNestedType("BookmarkDrag",BindingFlags.NonPublic);var payload=Activator.CreateInstance(type,true);type.GetField("Owner").SetValue(payload,main);type.GetField("Key").SetValue(payload,BookmarkBarService.Key(bookmarks[0]));
        var data=new DataObject(type,payload);var p=new Point(main.BookmarksBarPanel.ActualWidth-2,10);
        Func<IDataObject,DragEventArgs> drag=value=>(DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{value,DragDropKeyStates.LeftMouseButton,DragDropEffects.Move,main.BookmarksBarPanel,p},null);
        var over=drag(data);over.RoutedEvent=DragDrop.DragOverEvent;typeof(MainWindow).GetMethod("BookmarkBarDragOver",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{main.BookmarksBarPanel,over});
        Check("bookmark drag-over accepts its local payload",over.Effects==DragDropEffects.Move);
        var drop=drag(data);drop.RoutedEvent=DragDrop.DropEvent;typeof(MainWindow).GetMethod("BookmarkBarDrop",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{main.BookmarksBarPanel,drop});
        Check("dropping into empty bar space moves bookmark to end",drop.Handled&&((Bookmark)main.BookmarksBarPanel.Children.OfType<Button>().Last().Tag).Title=="Drag A"&&store.LoadSettings().BookmarkBarOrder.Last()==BookmarkBarService.Key(bookmarks[0]));
        var foreign=drag(new DataObject(DataFormats.Text,"https://untrusted.test/"));foreign.RoutedEvent=DragDrop.DragOverEvent;typeof(MainWindow).GetMethod("BookmarkBarDragOver",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{main.BookmarksBarPanel,foreign});Check("external drag data is refused",foreign.Effects==DragDropEffects.None);
        string fixtures=Path.Combine(output,"043-Sites");Directory.CreateDirectory(fixtures);File.WriteAllText(Path.Combine(fixtures,"index.html"),"<title>Site access fixture</title><body>Module site access</body>");
        var core=tab.View.CoreWebView2;foreach(string host in new[]{"a.module.test","b.module.test"})core.SetVirtualHostNameToFolderMapping(host,fixtures,CoreWebView2HostResourceAccessKind.DenyCors);
        string module=Path.Combine(output,"Module043");Directory.CreateDirectory(module);File.WriteAllText(Path.Combine(module,"manifest.json"),"{\"manifest_version\":3,\"name\":\"Site choices\",\"version\":\"1.0.0\",\"content_scripts\":[{\"matches\":[\"https://a.module.test/*\",\"https://b.module.test/*\"],\"js\":[\"a.js\"]}]}");File.WriteAllText(Path.Combine(module,"a.js"),"document.body.dataset.siteChoice='allowed';");
        string id=await main.InstallModule(SiteModuleService.Inspect(module));
        await main.SetModuleSites(id,new[]{"https://a.module.test/*"});var row=(await main.GetModules()).Single(r=>r.Id==id);
        Check("site manager separates requested and granted sites",row.Enabled&&row.RequestedSites.Count==2&&row.AllowedSites.SequenceEqual(new[]{"https://a.module.test/*"}));
        Func<string,Task> load=async host=>{string url="https://"+host+"/index.html?run="+Guid.NewGuid().ToString("N");var done=NextLoad(core,url);core.Navigate(url);await done;await Task.Delay(180);};
        await load("a.module.test");Check("allowed site receives native extension script",await core.ExecuteScriptAsync("document.body.dataset.siteChoice==='allowed'")=="true");
        var foreground=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=foreground.Item;await Wait(()=>foreground.Initialized);await Sleep(main,tab);
        Check("site-access revocation fixture is genuinely sleeping",core.IsSuspended);
        await main.SetModuleSites(id,new[]{"https://b.module.test/*"});main.BrowserTabs.SelectedItem=tab.Item;await Wait(()=>!core.IsSuspended);await WaitJs(core,"document.readyState==='complete'&&document.body.dataset.siteChoice===undefined");
        Check("revoked site loses injected effects when a sleeping tab wakes",true);CloseTab(main,foreground);await main.SetModuleSites(id,new[]{"https://a.module.test/*"});
        await load("b.module.test");Check("unchecked site cannot receive native extension script",await core.ExecuteScriptAsync("document.body.dataset.siteChoice===undefined")=="true");
        bool refused=false;try{await main.SetModuleSites(id,new[]{"https://unrequested.test/*"});}catch(ArgumentException){refused=true;}Check("attempted grant expansion leaves prior grants intact",refused&&(await main.GetModules()).Single(r=>r.Id==id).AllowedSites.SequenceEqual(row.AllowedSites));
        await main.SetModuleSites(id,new string[0]);row=(await main.GetModules()).Single(r=>r.Id==id);Check("deny all leaves a visible disabled module",!row.Enabled&&row.AllowedSites.Count==0&&row.Status=="No sites allowed");
        refused=false;try{await main.ChangeModule(id,true);}catch(InvalidOperationException){refused=true;}Check("Enable cannot bypass deny-all grants",refused);
        await main.SetModuleSites(id,new[]{"https://b.module.test/*"});Check("editing disabled access never transiently registers extension",!(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==id)&&!(await main.GetModules()).Single(r=>r.Id==id).Enabled);
        await main.ChangeModule(id,true);await load("b.module.test");Check("explicit Enable applies newly selected site",await core.ExecuteScriptAsync("document.body.dataset.siteChoice==='allowed'")=="true");
        await load("a.module.test");Check("previously allowed site is revoked after re-enabling",await core.ExecuteScriptAsync("document.body.dataset.siteChoice===undefined")=="true");
        foreach(string mode in new[]{"Light","Dark"})
        {
            AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});row=(await main.GetModules()).Single(r=>r.Id==id);
            var dialog=Offscreen(new ModuleSiteAccessWindow(row){Width=360,Height=300});dialog.Loaded+=(s,e)=>{Check(mode+" site choices reflect persisted access",dialog.SiteChoices.Count==2&&dialog.SiteChoices.Count(c=>c.IsChecked==true)==1);Click(dialog,"Save site access");};Check(mode+" access editor saves selected patterns",dialog.ShowDialog()==true&&dialog.AllowedSites.SequenceEqual(row.AllowedSites));
            var manager=Offscreen(new SiteModulesWindow(main.GetModules,main.InstallModule,main.ChangeModule,main.SetModuleSites){Width=360,Height=300});manager.Show();await Wait(()=>manager.ModuleList.Items.Count>0);manager.ModuleList.SelectedIndex=0;await Task.Delay(80);((Panel)manager.Content).Background=manager.Background;await Task.Delay(40);Capture((FrameworkElement)manager.Content,Path.Combine(output,"module-manager-small-"+mode.ToLowerInvariant()+".png"));
            Check(mode+" compact module manager keeps footer controls in bounds",Visuals<Button>(manager).Where(b=>b.IsVisible).All(b=>b.TranslatePoint(new Point(),(FrameworkElement)manager.Content).Y+b.ActualHeight<=((FrameworkElement)manager.Content).ActualHeight+1));manager.Close();
        }
        await main.ChangeModule(id,false,true);Check("removing a grant-restricted module clears saved policy",!File.ReadAllText(Path.Combine(store.RootFolder,"module-permissions.json")).Contains("a.module.test"));
        CloseTab(main,tab);await ModuleSitePersistenceChecks(output,module);
    }
    private static async Task ModuleSitePersistenceChecks(string output,string source)
    {
        var store=new BrowserDataStore(Path.Combine(output,"GrantPersistence"),Path.Combine(output,"GrantDownloads"));store.SaveSettings(new BrowserSettings{RestorePreviousSession=false,HarborHomeIntroduced=true,OnlineSuggestionsEnabled=false});string id=null;
        for(int run=0;run<3;run++)
        {
            if(run==2)File.WriteAllText(Path.Combine(store.RootFolder,"module-permissions.json"),"broken json");
            var window=Offscreen(new MainWindow(store));window.Show();
            try
            {
                await Wait(()=>window.BrowserTabs.Items.Count>0&&((BrowserTab)((TabItem)window.BrowserTabs.Items[0]).Tag).Initialized);
                if(run==0){id=await window.InstallModule(SiteModuleService.Inspect(source));await window.SetModuleSites(id,new[]{"https://b.module.test/*"});}
                else if(run==1){var row=(await window.GetModules()).Single();Console.WriteLine("INFO restored module: same ID="+(row.Id==id)+"; enabled="+row.Enabled+"; requested="+row.RequestedSites.Count+"; allowed="+string.Join(",",row.AllowedSites)+"; status="+row.Status);Check("site grants and enabled state survive app restart",row.Id==id&&row.Enabled&&row.RequestedSites.Count==2&&row.AllowedSites.SequenceEqual(new[]{"https://b.module.test/*"}));}
                else{var row=(await window.GetModules()).Single();Check("corrupt permission file disables managed native extension",!row.Enabled&&row.Status.StartsWith("Blocked:")&&!(await ((BrowserTab)((TabItem)window.BrowserTabs.Items[0]).Tag).View.CoreWebView2.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==id&&e.IsEnabled));await window.ChangeModule(id,false,true);}
            }
            finally{await (Task)typeof(MainWindow).GetMethod("FinishPrivacyBeforeClose",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null);typeof(MainWindow).GetField("_closingAfterPrivacyCleanup",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,true);window.Close();}
        }
    }
}
