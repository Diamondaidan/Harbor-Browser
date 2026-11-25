using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static void SuggestionMouse(UIElement target,bool down)
    {
        var input=new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=down?Mouse.PreviewMouseDownEvent:Mouse.PreviewMouseUpEvent};
        target.RaiseEvent(input);input.RoutedEvent=down?Mouse.MouseDownEvent:Mouse.MouseUpEvent;target.RaiseEvent(input);
    }
    private static FrameworkElement SuggestionPart(MainWindow main,int index,string part="title")
    {
        main.AddressSuggestionList.UpdateLayout();var item=(ListBoxItem)main.AddressSuggestionList.ItemContainerGenerator.ContainerFromIndex(index);item.ApplyTemplate();
        var suggestion=(AddressSuggestion)main.AddressSuggestionList.Items[index];
        if(part=="surface")return (FrameworkElement)item.Template.FindName("SuggestionSurface",item);
        return Visuals<TextBlock>(item).First(t=>t.Text==(part=="source"?suggestion.SourceLabel:part=="url"?suggestion.Url:suggestion.Title));
    }
    private static async Task PrepareClicks(MainWindow main,string query)
    {main.Activate();main.AddressBox.Focus();main.AddressBox.Text="";main.AddressBox.Text=query;await Wait(()=>main.AddressSuggestions.IsOpen);main.AddressSuggestionList.UpdateLayout();await Task.Delay(60);}
    private static async Task SuggestionClickChecks(MainWindow main)
    {
        var settings=(BrowserSettings)Field(main,"_settings");settings.OnlineSuggestionsEnabled=false;
        // Routed input has no physical cursor position; keep the popup open while
        // exercising row routing. Native popup capture is checked separately.
        bool staysOpen=main.AddressSuggestions.StaysOpen;main.AddressSuggestions.StaysOpen=true;
        var bookmarks=(List<Bookmark>)Field(main,"_bookmarks");var history=(List<HistoryEntry>)Field(main,"_history");
        bookmarks.Add(new Bookmark{Title="clicksentinel bookmark",Url="https://click.test.invalid/bookmark"});history.Insert(0,new HistoryEntry{Title="clicksentinel history",Url="https://click.test.invalid/history",VisitedAt=DateTime.Now});
        var tab=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;var core=tab.View.CoreWebView2;var navigations=new List<string>();
        EventHandler<CoreWebView2NavigationStartingEventArgs> capture=(s,e)=>{navigations.Add(e.Uri);e.Cancel=true;};core.NavigationStarting+=capture;
        try
        {
            await PrepareClicks(main,"clicksentinel");Check("fresh popup has no keyboard selection",main.AddressSuggestionList.SelectedIndex==-1);
            var target=SuggestionPart(main,0);SuggestionMouse(target,true);SuggestionMouse(target,false);await Task.Delay(250);
            await Wait(()=>navigations.Count>0);Check("clicking suggestion text without preselection navigates",navigations.Count==1&&navigations[0]=="https://click.test.invalid/bookmark");
            foreach(string part in new[]{"title","url","source","surface"})
            {
                await PrepareClicks(main,"clicksentinel");main.AddressSuggestionList.SelectedIndex=0;target=SuggestionPart(main,1,part);int before=navigations.Count;
                SuggestionMouse(target,true);SuggestionMouse(target,false);await Wait(()=>navigations.Count>before);
                Check("clicked "+part+" opens its own row rather than keyboard selection",navigations.Count==before+1&&navigations.Last()=="https://click.test.invalid/history");
            }
            await PrepareClicks(main,"clicksentinel");main.AddressSuggestionList.SelectedIndex=0;int count=navigations.Count;
            SuggestionMouse(main.AddressSuggestionList,true);SuggestionMouse(main.AddressSuggestionList,false);await Task.Delay(80);Check("list background does not activate a selected suggestion",navigations.Count==count);
            await PrepareClicks(main,"clicksentinel");target=SuggestionPart(main,0);count=navigations.Count;SuggestionMouse(target,true);SuggestionMouse(main.AddressSuggestionList,false);await Task.Delay(80);Check("pressing row then releasing outside it does not navigate",navigations.Count==count);
            await PrepareClicks(main,"clicksentinel");Key(main,System.Windows.Input.Key.Down);Key(main,System.Windows.Input.Key.Enter);await Task.Delay(100);Check("keyboard navigation remains available",navigations.Count==count+1&&navigations.Last()=="https://click.test.invalid/bookmark");
            settings.OnlineSuggestionsEnabled=true;main.OnlineSuggestions=new OnlineSuggestionService((uri,token)=>Task.FromResult("[\"query\",[\"cloud completion\",\"harbor://downloads\"]]"));await PrepareClicks(main,"cloudphrase");
            await Wait(()=>main.AddressSuggestionList.Items.Cast<AddressSuggestion>().Any(s=>s.Query=="cloud completion"));target=SuggestionPart(main,0);count=navigations.Count;SuggestionMouse(target,true);SuggestionMouse(target,false);await Task.Delay(120);
            Check("clicking online suggestion performs selected-provider search",navigations.Count==count+1&&navigations.Last().StartsWith("https://duckduckgo.com/")&&Uri.UnescapeDataString(navigations.Last()).Contains("cloud completion"));
            await PrepareClicks(main,"cloudphrase");await Wait(()=>main.AddressSuggestionList.Items.Count==2);target=SuggestionPart(main,1);count=navigations.Count;SuggestionMouse(target,true);SuggestionMouse(target,false);await Task.Delay(120);
            Check("online suggestion click cannot open an internal browser page",navigations.Count==count+1&&navigations.Last().StartsWith("https://duckduckgo.com/")&&Uri.UnescapeDataString(navigations.Last()).Contains("harbor://downloads")&&tab.NativeContent==null);
            var online=main.OnlineSuggestions;var started=new TaskCompletionSource<bool>();var reply=new TaskCompletionSource<string>();
            main.OnlineSuggestions=new OnlineSuggestionService((uri,token)=>{started.TrySetResult(true);return reply.Task;});await PrepareClicks(main,"clicksentinel");await started.Task;
            target=SuggestionPart(main,0);count=navigations.Count;SuggestionMouse(target,true);reply.TrySetResult("[\"query\",[\"late replacement\"]]");await Task.Delay(100);
            Check("online reply cannot replace rows during a click",((AddressSuggestion)main.AddressSuggestionList.Items[0]).Url=="https://click.test.invalid/bookmark");
            SuggestionMouse(target,false);await Wait(()=>navigations.Count>count);Check("press/release still opens original row after late reply",navigations.Last()=="https://click.test.invalid/bookmark");main.OnlineSuggestions=online;
            var privateTab=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=privateTab.Item;await Wait(()=>privateTab.Initialized);string privateNavigation=null;
            EventHandler<CoreWebView2NavigationStartingEventArgs> privateCapture=(s,e)=>{privateNavigation=e.Uri;e.Cancel=true;};privateTab.View.CoreWebView2.NavigationStarting+=privateCapture;
            await PrepareClicks(main,"clicksentinel");Check("private click popup contains bookmarks without history",main.AddressSuggestionList.Items.Cast<AddressSuggestion>().All(s=>s.SourceLabel=="Bookmark"));
            target=SuggestionPart(main,0);SuggestionMouse(target,true);SuggestionMouse(target,false);await Wait(()=>privateNavigation!=null);Check("private bookmark suggestion click navigates",privateNavigation=="https://click.test.invalid/bookmark");
            privateTab.View.CoreWebView2.NavigationStarting-=privateCapture;CloseTab(main,privateTab);await main.WaitForPrivateCleanup();main.BrowserTabs.SelectedItem=tab.Item;
            await HomeSuggestionPointerChecks(main,tab,core,navigations);
        }
        finally{core.NavigationStarting-=capture;settings.OnlineSuggestionsEnabled=false;main.AddressSuggestions.StaysOpen=staysOpen;}
    }
    private static async Task HomeSuggestionPointerChecks(MainWindow main,BrowserTab tab,CoreWebView2 core,List<string> navigations)
    {
        // The navigation capture is briefly removed to load only the local Home page.
        // A fresh Home tab avoids changing the original capture's delegate lifetime.
        var home=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=home.Item;await Wait(()=>home.Initialized);var browser=home.View.CoreWebView2;await WaitJs(browser,"!!document.getElementById('query')");
        var results=new List<string>();EventHandler<CoreWebView2NavigationStartingEventArgs> capture=(s,e)=>{if(!HomePageService.IsHomeUrl(e.Uri)){results.Add(e.Uri);e.Cancel=true;}};browser.NavigationStarting+=capture;
        await browser.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled","{\"enabled\":true}");
        foreach(string mode in new[]{"Light","Dark"})
        {
            AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});
            await browser.ExecuteScriptAsync("document.getElementById('query').focus();document.getElementById('query').value='homepagephrase';document.getElementById('query').dispatchEvent(new Event('input',{bubbles:true}));");
            await WaitJs(browser,"!document.getElementById('home-suggestions').hidden && document.querySelector('#home-suggestions button span').textContent==='cloud completion'");
            string point=await browser.ExecuteScriptAsync("(()=>{const r=document.querySelector('#home-suggestions button span').getBoundingClientRect();return {x:r.left+r.width/2,y:r.top+r.height/2};})()");var location=Json.Deserialize<Dictionary<string,double>>(point);int before=results.Count;
            await browser.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",Json.Serialize(new{type="mousePressed",x=location["x"],y=location["y"],button="left",clickCount=1}));
            await browser.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",Json.Serialize(new{type="mouseReleased",x=location["x"],y=location["y"],button="left",clickCount=1}));await Task.Delay(150);
            Check(mode+" Home suggestion accepts actual browser pointer events",results.Count==before+1&&Uri.UnescapeDataString(results.Last()).Contains("cloud completion"));
        }
        browser.NavigationStarting-=capture;CloseTab(main,home);main.BrowserTabs.SelectedItem=tab.Item;
    }
}
