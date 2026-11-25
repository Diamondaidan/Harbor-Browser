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
    private static async Task WaitJs(CoreWebView2 core,string expression)
    {for(int i=0;i<200;i++){try{if(await core.ExecuteScriptAsync(expression)=="true")return;}catch(InvalidOperationException){}await Task.Delay(40);}throw new Exception("JavaScript condition: "+expression);}
    private static T Offscreen<T>(T window) where T:Window
    {WindowFitService.SetIsEnabled(window,false);window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-20000;window.Top=0;window.ShowInTaskbar=false;window.ShowActivated=false;return window;}
    private static void Click(DependencyObject root,string text){Visuals<Button>(root).First(b=>Convert.ToString(b.Content)==text).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    private static async Task CustomizationChecks(MainWindow main,BrowserDataStore store,string output)
    {
        var bookmarks=new List<Bookmark>{new Bookmark{Title="First",Url="https://first.test/",Folder="Work/Projects"},new Bookmark{Title="Other",Url="https://other.test/",Folder="Other"},new Bookmark{Title="Last",Url="https://last.test/",Folder="Work/Projects"}};
        var manager=Offscreen(new BookmarksWindow(bookmarks,new[]{"Work/Projects","Empty"}){Width=850,Height=530});manager.Show();await Task.Delay(70);
        manager.FolderFilter.SelectedValue="Work/Projects";Check("bookmark folder filters real grid",manager.BookmarkGrid.Items.Count==2);
        manager.BookmarkGrid.SelectedItem=manager.BookmarkGrid.Items[1];manager.MoveSelected(-1);Check("bookmark order moves within filtered folder",((Bookmark)manager.BookmarkGrid.Items[0]).Title=="Last");
        manager.RenameFolder("Work","Research");manager.FolderFilter.SelectedValue="Research/Projects";Check("folder rename updates nested bookmark paths",manager.BookmarkGrid.Items.Count==2&&manager.BookmarkGrid.Items.Cast<Bookmark>().All(b=>b.Folder=="Research/Projects"));
        manager.ApplyEdit(-1,"Added","https://added.test/","Research/Projects");Check("bookmark editor adds to chosen folder",manager.BookmarkGrid.Items.Count==3);
        manager.ImportText("<DL><p><DT><H3>Imported</H3><DL><p><DT><A HREF='https://imported.test/'>Imported page</A></DL></DL>",false);manager.FolderFilter.SelectedValue="Imported";Check("bookmark manager imports HTML folder",manager.BookmarkGrid.Items.Count==1);
        manager.FolderFilter.SelectedValue="*";manager.SearchBox.Text="Added";Check("bookmark manager search filters grid",manager.BookmarkGrid.Items.Count==1);manager.SearchBox.Clear();
        await Task.Delay(50);var frame=(FrameworkElement)manager.Content;((Panel)frame).Background=manager.Background;await Task.Delay(30);Capture(frame,Path.Combine(output,"bookmarks-dark.png"));
        manager.Close();Check("bookmark draft leaves original data unchanged",bookmarks.Count==3&&bookmarks[0].Folder=="Work/Projects");
        var saved=Offscreen(new BookmarksWindow(bookmarks,new[]{"Work/Projects"}));
        saved.Loaded+=(s,e)=>{saved.ApplyEdit(-1,"Saved","https://saved.test/","Work");Click(saved,"Save");};
        Check("bookmark manager Save returns independent result",saved.ShowDialog()==true&&saved.ResultBookmarks.Count==4&&saved.ResultFolders.Contains("Work")&&bookmarks.Count==3);
        var settings=(BrowserSettings)Field(main,"_settings");
        var preferences=Offscreen(new SettingsWindow(settings,bookmarks){Width=650,Height=552});preferences.Show();await Task.Delay(60);
        ((ComboBox)preferences.FindName("AppearanceModeBox")).SelectedValue="Dark";((TextBox)preferences.FindName("AppearanceHexBox")).Text="#4477AA";
        ((TextBox)preferences.FindName("PresetNameBox")).Text="Ocean test";Click(preferences,"Save current colors as preset");
        var presetBox=(ComboBox)preferences.FindName("PresetBox");Check("preset saved in Settings draft",presetBox.Items.Count==1&&settings.AppearancePresets.Count==0);
        ((TextBox)preferences.FindName("AppearanceHexBox")).Text="#990000";Click(preferences,"Reset this color");presetBox.SelectedIndex=0;Click(preferences,"Apply preset");
        Check("preset restores saved color",((TextBox)preferences.FindName("AppearanceHexBox")).Text=="#4477AA");
        var presetScroll=FindVisual<ScrollViewer>(preferences);presetScroll.ScrollToTop();await Task.Delay(50);var prefRoot=(FrameworkElement)preferences.Content;((Panel)prefRoot).Background=preferences.Background;await Task.Delay(30);Capture(prefRoot,Path.Combine(output,"settings-presets.png"));
        preferences.Close();Check("closing Settings discards draft preset",settings.AppearancePresets.Count==0);
        var saveSettings=Offscreen(new SettingsWindow(settings,bookmarks));
        saveSettings.Loaded+=(s,e)=>{((CheckBox)saveSettings.FindName("OnlineSuggestionsBox")).IsChecked=false;((CheckBox)saveSettings.FindName("UseBookmarkShortcutsBox")).IsChecked=false;((ComboBox)saveSettings.FindName("HomeSceneBox")).SelectedValue="Morning";Click(saveSettings,"Save settings");};
        Check("Settings saves shortcut scene and online preferences",saveSettings.ShowDialog()==true&&!saveSettings.ResultSettings.OnlineSuggestionsEnabled&&saveSettings.ResultSettings.UseCustomHomeShortcuts&&saveSettings.ResultSettings.HomeShortcuts.Count==3&&saveSettings.ResultSettings.HomeScene=="Morning");
        main.Activate();main.AddressBox.Focus();
        foreach(string mode in new[]{"Light","Dark"})
        {
            AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});main.AddressBox.Text="https://harbor.example/readable-search-selection";main.AddressBox.SelectAll();await Task.Delay(70);Capture(main.AddressBox,Path.Combine(output,"search-selection-"+mode.ToLowerInvariant()+".png"));
        }
        AppearanceService.ApplyApplication(settings);
        int requests=0;var firstStarted=new TaskCompletionSource<bool>();var firstReply=new TaskCompletionSource<string>();
        main.OnlineSuggestions=new OnlineSuggestionService((uri,token)=>{requests++;if(uri.Query.Contains("firstphrase")){firstStarted.TrySetResult(true);return firstReply.Task;}return Task.FromResult("[\"query\",[\"second completion\",\"second option\"]]");});
        settings.OnlineSuggestionsEnabled=true;
        main.AddressBox.Text="firstphrase";await firstStarted.Task;main.AddressBox.Text="secondphrase";
        await Wait(()=>main.AddressSuggestionList.Items.Cast<AddressSuggestion>().Any(s=>s.Query=="second completion"));
        firstReply.TrySetResult("[\"firstphrase\",[\"stale completion\"]]");await Task.Delay(70);
        Check("stale online replies cannot replace current suggestions",main.AddressSuggestionList.Items.Cast<AddressSuggestion>().Any(s=>s.Query=="second completion")&&!main.AddressSuggestionList.Items.Cast<AddressSuggestion>().Any(s=>s.Query=="stale completion"));
        Capture((FrameworkElement)main.AddressSuggestions.Child,Path.Combine(output,"online-suggestions.png"));
        var normalService=main.OnlineSuggestions;
        main.OnlineSuggestions=new OnlineSuggestionService((uri,token)=>Task.FromResult("[\"query\",[\"harbor://downloads\"]]"));main.AddressBox.Text="specialphrase";
        await Wait(()=>main.AddressSuggestionList.Items.Cast<AddressSuggestion>().Any(s=>s.Query=="harbor://downloads"));
        var active=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;string navigation=null;
        EventHandler<CoreWebView2NavigationStartingEventArgs> capture=(s,e)=>{navigation=e.Uri;e.Cancel=true;};active.View.CoreWebView2.NavigationStarting+=capture;
        Key(main,System.Windows.Input.Key.Down);Key(main,System.Windows.Input.Key.Enter);await Wait(()=>navigation!=null);active.View.CoreWebView2.NavigationStarting-=capture;
        Check("provider completion cannot invoke internal page",navigation.StartsWith("https://duckduckgo.com/")&&Uri.UnescapeDataString(navigation).Contains("harbor://downloads")&&active.NativeContent==null);
        main.OnlineSuggestions=normalService;main.AddressBox.Focus();
        settings.OnlineSuggestionsEnabled=false;int before=requests;main.AddressBox.Text="disabledphrase";await Task.Delay(360);Check("Settings toggle prevents online address requests",requests==before);
        settings.OnlineSuggestionsEnabled=true;var privateTab=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=privateTab.Item;await Wait(()=>privateTab.Initialized);
        main.Activate();main.AddressBox.Focus();before=requests;main.AddressBox.Text="privatephrase";await Task.Delay(360);Check("private address input makes no online requests",requests==before);CloseTab(main,privateTab);
        var home=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=home.Item;await Wait(()=>home.Initialized);var core=home.View.CoreWebView2;await WaitJs(core,"!!document.getElementById('query')");
        string picture=Path.Combine(output,"background-source.png");var bitmap=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[]{40,80,120,255,40,80,120,255,40,80,120,255,40,80,120,255},8);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(picture))encoder.Save(file);
        settings.HomeBackgroundFile=BackgroundImageService.Import(store.RootFolder,picture);settings.HomeScene="Custom";settings.UseCustomHomeShortcuts=true;settings.HomeShortcuts=new List<Bookmark>{new Bookmark{Title="My shortcut",Url="https://shortcut.test/"}};
        typeof(MainWindow).GetMethod("RefreshHomePages",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
        await WaitJs(core,"document.querySelector('.favorite span:last-child')?.textContent==='My shortcut'&&getComputedStyle(document.body).backgroundImage.includes('data:image/png;base64')");Check("custom background and shortcut reach real Home",true);
        Check("normal Home exposes customization button",await core.ExecuteScriptAsync("!document.getElementById('customize-home').hidden")=="true");
        main.Activate();home.View.Focus();await core.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled","{\"enabled\":true}");
        await core.ExecuteScriptAsync("document.getElementById('query').focus();document.getElementById('query').value='homepagephrase';document.getElementById('query').dispatchEvent(new Event('input',{bubbles:true}));");
        await WaitJs(core,"!document.getElementById('home-suggestions').hidden&&document.querySelector('#home-suggestions button span').textContent==='second completion'");Check("online suggestions reach Home search field",true);
        await core.ExecuteScriptAsync("document.getElementById('query').dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true}));");
        Check("Home keyboard selects search completion",await core.ExecuteScriptAsync("document.querySelector('#home-suggestions button').getAttribute('aria-selected')==='true'")=="true");
        using(var file=File.Create(Path.Combine(output,"home-customized.png")))await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
        await core.ExecuteScriptAsync("document.getElementById('query').dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));");Check("Home Escape dismisses completion list",await core.ExecuteScriptAsync("document.getElementById('home-suggestions').hidden")=="true");
        settings.OnlineSuggestionsEnabled=false;before=requests;await core.ExecuteScriptAsync("document.getElementById('query').value='disabledhome';document.getElementById('query').dispatchEvent(new Event('input',{bubbles:true}));");await Task.Delay(360);Check("online toggle also prevents Home requests",requests==before);
        settings.OnlineSuggestionsEnabled=true;var privateHome=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=privateHome.Item;await Wait(()=>privateHome.Initialized);var privateCore=privateHome.View.CoreWebView2;await WaitJs(privateCore,"!!document.getElementById('query')");privateHome.View.Focus();await privateCore.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled","{\"enabled\":true}");
        before=requests;await privateCore.ExecuteScriptAsync("document.getElementById('query').focus();document.getElementById('query').value='privatehomephrase';document.getElementById('query').dispatchEvent(new Event('input',{bubbles:true}));");await Task.Delay(360);Check("private Home makes no online requests",requests==before);Check("private Home hides customization action",await privateCore.ExecuteScriptAsync("document.getElementById('customize-home').hidden")=="true");
        CloseTab(main,privateHome);settings.OnlineSuggestionsEnabled=false;main.BrowserTabs.SelectedItem=home.Item;
    }
}
