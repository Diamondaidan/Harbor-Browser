using System;
using System.ComponentModel;
using System.IO;
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
    private static async Task Ui061Checks(MainWindow main,string output)
    {
        VpnStatusLabelChecks(main,output);
        var windows=new Window[]{new BookmarksWindow(new Bookmark[0],new string[0]),new SettingsWindow(new BrowserSettings()),new ResourceMonitorWindow(()=>"Owned resources"),new ClearBrowsingDataWindow(false,r=>Task.CompletedTask),new SitePermissionsWindow(new SitePermissionRule[0],null,false)};
        foreach(var window in windows)
        {
            Offscreen(window);window.Show();await Task.Delay(60);window.ApplyTemplate();
            var caption=window.Template.FindName("HarborCaptionBar",window) as Grid;
            var title=window.Template.FindName("HarborCaptionText",window) as TextBlock;
            Check(window.GetType().Name+" uses an app-rendered caption",caption!=null&&title!=null);
            foreach(string mode in new[]{"Dark","Light","Dark"})
            {
                AppearanceService.Apply(window.Resources,new BrowserSettings{ThemeMode=mode});window.UpdateLayout();
                Check(window.GetType().Name+" caption follows "+mode,((SolidColorBrush)caption.Background).Color==((SolidColorBrush)window.FindResource("HarborPanel")).Color&&((SolidColorBrush)title.Foreground).Color==((SolidColorBrush)window.FindResource("HarborText")).Color);
                if(mode=="Light")Capture(window,Path.Combine(output,window.GetType().Name+"-caption-light.png"));
            }
            Capture(window,Path.Combine(output,window.GetType().Name+"-caption-dark.png"));
            var close=(Button)window.Template.FindName("CaptionClose",window);
            bool requested=false;CancelEventHandler cancel=(s,e)=>{requested=true;e.Cancel=true;};window.Closing+=cancel;
            ((RoutedCommand)close.Command).Execute(close.CommandParameter,close);
            await Task.Delay(80);
            Check(window.GetType().Name+" close command respects cancellation",requested&&window.IsVisible);
            window.Closing-=cancel;window.Close();
        }
        AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode="Dark"});
        Exception historyError=null;bool historyChecked=false;
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(s,e)=>{
            var window=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.Title=="Browsing history");
            if(window==null||!window.IsLoaded)return;
            timer.Stop();Offscreen(window);
            try{
                var caption=window.Template.FindName("HarborCaptionBar",window) as Grid;
                Check("History uses the shared app caption",caption!=null);
                foreach(string mode in new[]{"Dark","Light","Dark"}){
                    AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});window.UpdateLayout();
                    Check("History caption follows live "+mode,((SolidColorBrush)caption.Background).Color==((SolidColorBrush)window.FindResource("HarborPanel")).Color);
                }
                Capture(window,Path.Combine(output,"History-caption-dark.png"));historyChecked=true;
            }catch(Exception ex){historyError=ex;}finally{window.Close();}
        };
        timer.Start();typeof(MainWindow).GetMethod("ShowHistoryManager_Click",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{main,new RoutedEventArgs()});timer.Stop();
        if(historyError!=null)throw historyError;if(!historyChecked)throw new Exception("History caption check did not execute.");
        var popup=main.OpenProtonPreview();Offscreen(popup);await Task.Delay(80);
        Check("VPN window has a themed caption",popup.Template.FindName("HarborCaptionBar",popup)!=null);
        Check("VPN options start collapsed",!((Expander)typeof(ProtonPreviewWindow).GetField("_options",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(popup)).IsExpanded);
        Check("VPN route privacy remains visible with options collapsed",Visuals<TextBlock>(popup).Any(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Route verification privacy"&&t.IsVisible));
        var options=(Expander)typeof(ProtonPreviewWindow).GetField("_options",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(popup);
        options.IsExpanded=true;popup.UpdateLayout();
        Check("VPN secondary actions are available under Options",Visuals<Button>(popup).Any(b=>Convert.ToString(b.Content)=="Reload Proton controls"));
        options.IsExpanded=false;
        await main.ChangeProtonPreview(true);await popup.ShowProvider();
        await Wait(()=>popup.ControlsReady||popup.ControlsError!=null);
        Check("compact VPN hosts the genuine signed-out extension controls",popup.ControlsReady&&!popup.ContainsAccountPage);
        foreach(var size in new[]{new Size(404,696),new Size(360,540),new Size(404,440)})
        {
            popup.Width=size.Width;popup.Height=size.Height;await Task.Delay(100);popup.UpdateLayout();
            var view=popup.ProviderView;
            Check("VPN extension fits at "+size,view.ActualWidth+1>=370*view.ZoomFactor&&view.ActualHeight+1>=560*view.ZoomFactor);
            File.WriteAllText(Path.Combine(output,"vpn-layout-"+size.Width+"-"+size.Height+".json"),await view.CoreWebView2.ExecuteScriptAsync("JSON.stringify({width:innerWidth,height:innerHeight,body:document.body.scrollHeight,content:document.querySelector('#sign-in-view .content')?.getBoundingClientRect().bottom})"));
            Check("VPN sign-in content is not clipped at "+size,await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#sign-in-view .content').getBoundingClientRect().bottom<=innerHeight+1")=="true");
        }
        popup.Width=404;popup.Height=696;await Task.Delay(100);
        File.WriteAllText(Path.Combine(output,"vpn-layout.json"),await popup.ProviderView.CoreWebView2.ExecuteScriptAsync("JSON.stringify({width:innerWidth,height:innerHeight,body:document.body.scrollHeight,main:document.querySelector('.main-area').scrollHeight,content:document.querySelector('#sign-in-view .content')?.getBoundingClientRect().bottom,links:[...document.querySelectorAll('#sign-in-view a')].map(e=>e.getBoundingClientRect().bottom)})"));
        Capture(popup,Path.Combine(output,"vpn-compact-frame.png"));
        using(var image=File.Create(Path.Combine(output,"vpn-extension.png")))await popup.ProviderView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
        await main.ChangeProtonPreview(false);popup.Close();
        main.Width=1180;main.UpdateResponsiveLayout();
        var add=main.BrowserTabs.Template.FindName("NewTabButton",main.BrowserTabs) as Button;
        Check("new-tab control is available",add!=null);
        for(int i=0;i<3;i++)
        {
            main.UpdateLayout();await Task.Delay(50);
            var last=main.BrowserTabs.Items.Cast<TabItem>().Last(t=>t.Visibility==Visibility.Visible);
            double right=last.TranslatePoint(new Point(last.ActualWidth,0),main.BrowserTabs).X;
            double plus=add.TranslatePoint(new Point(),main.BrowserTabs).X;
            Check("plus follows the last tab at count "+main.BrowserTabs.Items.Count,plus>=right-1&&plus-right<12);
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        main.Width=600;for(int i=0;i<10;i++)AddTab(main,HomePageService.Url);
        await Task.Delay(150);main.UpdateResponsiveLayout();main.UpdateLayout();await Task.Delay(80);
        var tail=main.BrowserTabs.Items.Cast<TabItem>().Last(t=>t.Visibility==Visibility.Visible);
        main.BrowserTabs.SelectedItem=tail;await Task.Delay(100);main.UpdateLayout();
        double tailRight=tail.TranslatePoint(new Point(tail.ActualWidth,0),main.BrowserTabs).X;
        double plusLeft=add.TranslatePoint(new Point(),main.BrowserTabs).X;
        Check("plus remains adjacent when tabs overflow",plusLeft>=tailRight-1&&plusLeft-tailRight<12);
        Check("newest tab exposes the plus button inside the viewport",plusLeft>=0&&plusLeft+add.ActualWidth<=main.BrowserTabs.ActualWidth+1);
        Capture(main.TitleBar,Path.Combine(output,"trailing-new-tab-overflow.png"));
    }

    private static void VpnStatusLabelChecks(MainWindow main,string output)
    {
        double savedWidth=main.Width;
        var button=main.TorIndicatorButton;
        object savedContent=button.Content;
        try
        {
            foreach(string mode in new[]{"Dark","Light"})
            {
                AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode=mode});
                foreach(double width in new[]{1366d,1093d,911d,683d,600d,480d})
                {
                    main.Width=width;main.UpdateResponsiveLayout();
                    foreach(string label in new[]{"VPN Off","Connecting","Protected","Disconnected"})
                    {
                        button.Content=label;main.UpdateLayout();button.ApplyTemplate();
                        var text=new TextBlock{Text=label,FontFamily=button.FontFamily,FontSize=button.FontSize,FontWeight=button.FontWeight};
                        text.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
                        var rendered=Visuals<TextBlock>(button).First(t=>t.Text==label);
                        double left=rendered.TranslatePoint(new Point(),button).X;
                        double right=button.TranslatePoint(new Point(button.ActualWidth,0),main.WindowFrame).X;
                        double cogLeft=main.GearMenuButton.TranslatePoint(new Point(),main.WindowFrame).X;
                        Check(mode+" VPN label "+label+" fits with usable navigation at "+width,
                            left>=button.Padding.Left-.5&&left+text.DesiredSize.Width<=button.ActualWidth-button.Padding.Right+.5&&
                            right<=cogLeft+.5&&main.AddressBox.ActualWidth>=120&&
                            main.GearMenuButton.TranslatePoint(new Point(main.GearMenuButton.ActualWidth,0),main.WindowFrame).X<=main.WindowFrame.ActualWidth+.5);
                    }
                    if(width==1366||width==480)
                        Capture((FrameworkElement)VisualTreeHelper.GetParent(button),Path.Combine(output,"vpn-status-"+mode.ToLowerInvariant()+"-"+width+".png"));
                }
            }
        }
        finally
        {
            button.Content=savedContent;main.Width=savedWidth;
            AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode="Dark"});
            main.UpdateResponsiveLayout();main.UpdateLayout();
        }
    }
}
