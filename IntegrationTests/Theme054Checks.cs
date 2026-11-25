using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;

internal static partial class Program
{
    private static async Task Theme054Checks(MainWindow main,string output)
    {
        var dark=new BrowserSettings{ThemeMode="Dark"};var light=new BrowserSettings{ThemeMode="Light"};
        AppearanceService.ApplyApplication(dark);
        var service=(BrowserVpnService)Field(main,"_browserVpn");
        var windows=new List<Window>{
            new BookmarksWindow(new Bookmark[0],new string[0]),
            new SettingsWindow(dark),
            new BookmarkEditorWindow(new Bookmark{Title="Test",Url="https://example.com/"},new string[0]),
            new ResourceMonitorWindow(()=>"Test resources"),
            new ClearBrowsingDataWindow(false,r=>Task.CompletedTask),
            new SitePermissionsWindow(new SitePermissionRule[0],null,false),
            new SiteModulesWindow(()=>Task.FromResult(new List<SiteModuleRow>()),p=>Task.FromResult(""),(id,enabled,remove)=>Task.CompletedTask),
            new ModuleSiteAccessWindow(new SiteModuleRow{Name="Test module",RequestedSites=new List<string>{"https://example.com/*"},AllowedSites=new List<string>{"https://example.com/*"}}),
            new BrowserVpnWindow(service,b=>Task.CompletedTask,s=>{},()=>Task.CompletedTask)
        };
        foreach(var window in windows){
            Offscreen(window);window.Show();await Task.Delay(60);
            Check(window.GetType().Name+" receives a native dark title bar",WindowTitleBarService.ReadDarkCaption(window)==true);
            AppearanceService.Apply(window.Resources,light);
            Check(window.GetType().Name+" updates its existing title bar to light",WindowTitleBarService.ReadDarkCaption(window)==false);
            AppearanceService.Apply(window.Resources,dark);
            Check(window.GetType().Name+" updates its existing title bar back to dark",WindowTitleBarService.ReadDarkCaption(window)==true);
            window.Close();
        }
        Exception historyError=null;bool historyChecked=false;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(s,e)=>{
            var window=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.Title=="Browsing history");if(window==null||!window.IsLoaded)return;
            timer.Stop();try{
                Check("History receives the shared native dark title bar",WindowTitleBarService.ReadDarkCaption(window)==true);
                AppearanceService.ApplyApplication(light);Check("History title bar follows live light-mode changes",WindowTitleBarService.ReadDarkCaption(window)==false);
                AppearanceService.ApplyApplication(dark);Check("History title bar follows live dark-mode changes",WindowTitleBarService.ReadDarkCaption(window)==true);historyChecked=true;
            }catch(Exception ex){historyError=ex;}finally{window.Close();}
        };
        timer.Start();typeof(MainWindow).GetMethod("ShowHistoryManager_Click",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{main,new RoutedEventArgs()});timer.Stop();if(historyError!=null)throw historyError;if(!historyChecked)throw new Exception("History was not checked.");
        Check("Tor status is visible while off",Convert.ToString(main.TorIndicatorButton.Content)=="Tor Off");
        foreach(double width in new[]{480.0,600.0,1366.0}){
            main.Width=width;main.UpdateResponsiveLayout();main.UpdateLayout();
            Check("Tor controls fit at width "+width,main.TorIndicatorButton.ActualWidth>0&&main.TorIndicatorButton.TranslatePoint(new Point(),main.WindowFrame).X>=0&&main.GearMenuButton.TranslatePoint(new Point(),main.WindowFrame).X+main.GearMenuButton.ActualWidth<=main.WindowFrame.ActualWidth+1&&main.AddressBox.ActualWidth>=120);
        }
    }
}
