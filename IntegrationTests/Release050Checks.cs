using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;

internal static partial class Program
{
    private static async Task Release050Checks(MainWindow main,string output)
    {
        var normal=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=normal.Item;await Wait(()=>normal.Initialized);
        var privateTab=AddTab(main,HomePageService.Url,true);main.BrowserTabs.SelectedItem=privateTab.Item;await Wait(()=>privateTab.Initialized);await WaitJs(privateTab.View.CoreWebView2,"document.readyState==='complete'");
        Check("private tab retains visible non-color badge",privateTab.PrivateBadge.Visibility==Visibility.Visible&&((TextBlock)privateTab.PrivateBadge.Child).Text=="P");
        Check("normal tabs have no private badge",normal.PrivateBadge.Visibility==Visibility.Collapsed);
        Check("private tab labels include Private",privateTab.HeaderText.Text.StartsWith("Private · ")&&main.Title.StartsWith("Private · "));
        privateTab.PinCommand.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Check("pinned private tab retains marker with hidden title",privateTab.HeaderText.Visibility==Visibility.Collapsed&&privateTab.PrivateBadge.Visibility==Visibility.Visible);
        privateTab.PinCommand.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        foreach(string mode in new[]{"Light","Dark"})
        {
            AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});main.UpdateLayout();await Task.Delay(50);Capture(privateTab.HeaderLayout,Path.Combine(output,"private-tab-"+mode.ToLowerInvariant()+".png"));Check(mode+" private marker leaves favicon intact",privateTab.HeaderIcon.Source!=null&&privateTab.PrivateBadge.IsVisible);
        }
        string suffix=".integration."+Guid.NewGuid().ToString("N");
        using(var primary=new BrowserInstanceService(suffix))
        {
            Task listener=primary.Listen(values=>main.Dispatcher.BeginInvoke(new Action(()=>main.OpenExternalTargets(values))));int before=main.BrowserTabs.Items.Count;
            bool sent=await Task.Run(()=>{using(var secondary=new BrowserInstanceService(suffix))return !secondary.IsPrimary&&secondary.Forward(new[]{HomePageService.Url});});
            await Wait(()=>main.BrowserTabs.Items.Count==before+1);var opened=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
            Check("Windows link activation uses existing browser window",sent&&main.IsLoaded);
            Check("external link opens a new normal tab from private browsing",!opened.IsPrivate&&opened!=privateTab);CloseTab(main,opened);
        }
        var toolMenu=main.BuildGearMenu().Items.OfType<MenuItem>().First(m=>Label(m)=="Tools");Check("Default browser entry is offered in tools",toolMenu.Items.OfType<MenuItem>().Any(m=>Label(m)=="Default browser…"));
        bool connected=false;int commands=0;
        var fake=new VpnClientService((exe,args,time)=>
        {
            if(exe.EndsWith("rasdial.exe")){commands++;connected=!args.Contains("/disconnect");return Task.FromResult(new CommandResult());}
            return Task.FromResult(new CommandResult{Output="{\"Profiles\":[{\"Name\":\"Fixture VPN\",\"AllUsers\":false,\"Status\":\""+(connected?"Connected":"Disconnected")+"\",\"Tunnel\":\"IKEv2\"}],\"Errors\":[]}"});
        });
        foreach(string mode in new[]{"Light","Dark"})
        {
            AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});var window=Offscreen(new VpnWindow(fake){Width=360,Height=300});window.Show();await Wait(()=>window.ProfileList.Items.Count==1);await Task.Delay(80);
            Check(mode+" VPN enables Connect for disconnected profile",window.ConnectButton.IsEnabled&&!window.DisconnectButton.IsEnabled);
            window.ConnectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>window.DisconnectButton.IsEnabled);Check(mode+" VPN shows observed connected state",((VpnProfile)window.ProfileList.SelectedItem).Status=="Connected"&&commands>0);
            window.DisconnectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>window.ConnectButton.IsEnabled);Check(mode+" VPN disconnect acts on selected profile",!connected);
            ((Panel)window.Content).Background=window.Background;await Task.Delay(40);Capture((FrameworkElement)window.Content,Path.Combine(output,"vpn-small-"+mode.ToLowerInvariant()+".png"));
            Check(mode+" VPN compact footer remains reachable",Visuals<Button>(window).Where(b=>b.IsVisible).All(b=>b.TranslatePoint(new Point(),(FrameworkElement)window.Content).Y+b.ActualHeight<=((FrameworkElement)window.Content).ActualHeight+1));window.Close();
        }
        var probe=await new VpnClientService().Snapshot();Console.WriteLine("INFO read-only Windows VPN discovery: "+probe.Profiles.Count+" profiles; "+probe.Errors.Count+" query errors. No real connection was changed.");
        CloseTab(main,privateTab);await main.WaitForPrivateCleanup();CloseTab(main,normal);
    }
}
