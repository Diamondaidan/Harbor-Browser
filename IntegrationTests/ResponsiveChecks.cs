using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static void Capture(FrameworkElement element,string path)
    {
        int width=(int)Math.Ceiling(element.ActualWidth),height=(int)Math.Ceiling(element.ActualHeight);
        if(width<1||height<1)throw new Exception("Empty preview");
        var drawing=new DrawingVisual();
        var offset=VisualTreeHelper.GetOffset(element);
        using(var context=drawing.RenderOpen())context.DrawRectangle(new VisualBrush(element){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(offset.X,offset.Y,width,height)},null,new Rect(0,0,width,height));
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var file=File.Create(path))encoder.Save(file);
    }
    private static async Task ResponsiveChecks(MainWindow main,BrowserTab tab,CoreWebView2 core,string output)
    {
        NativeWindowGeometry.Rect initial;
        NativeWindowGeometry.GetWindowRect(new WindowInteropHelper(main).Handle,out initial);
        Check("startup window fits available work area",initial.Left>=-20000&&initial.Top>=0&&initial.Right<=-18720&&initial.Bottom<=680);
        double savedWidth=main.Width,savedHeight=main.Height;
        var cases=new[]{
            Tuple.Create(1366,768,1.0),Tuple.Create(1366,768,1.25),Tuple.Create(1366,768,1.5),
            Tuple.Create(1366,768,1.75),Tuple.Create(1366,768,2.0),Tuple.Create(1280,720,1.25),
            Tuple.Create(1024,768,1.25),Tuple.Create(800,600,1.5),Tuple.Create(1920,1080,1.5),Tuple.Create(3840,2160,2.5)
        };
        foreach(var test in cases)
        {
            main.Width=test.Item1/test.Item3-24;main.Height=(test.Item2-40*test.Item3)/test.Item3-24;
            await Task.Delay(45);main.UpdateLayout();
            string label=test.Item1+"x"+test.Item2+" @ "+(test.Item3*100)+"%";
            var frame=main.WindowFrame;
            var close=main.CloseWindowButton.TransformToAncestor(frame).Transform(new Point());
            var cog=main.GearMenuButton.TransformToAncestor(frame).Transform(new Point());
            var address=main.AddressBox.TransformToAncestor(frame).Transform(new Point());
            Check(label+" close fits",close.X>=0&&close.X+main.CloseWindowButton.ActualWidth<=frame.ActualWidth+.5);
            Check(label+" cog fits",cog.X>=0&&cog.X+main.GearMenuButton.ActualWidth<=frame.ActualWidth+.5);
            Check(label+" address usable",main.AddressBox.ActualWidth>=140&&address.X+main.AddressBox.ActualWidth<=frame.ActualWidth);
            Check(label+" page retains usable height",main.BrowserContentHost.ActualHeight>=120);
            Check(label+" compact toolbar applied",main.SearchEngineBox.Visibility==(ResponsiveLayout.ShowEngine(main.ActualWidth)?Visibility.Visible:Visibility.Collapsed));
            if(test.Item1==1366&&(test.Item3==1||test.Item3==1.5||test.Item3==2))
            {
                int width=(int)Math.Ceiling(frame.ActualWidth);
                var bitmap=new RenderTargetBitmap(width,83,96,96,PixelFormats.Pbgra32);bitmap.Render(frame);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using(var file=File.Create(Path.Combine(output,"header-1366-"+(int)(test.Item3*100)+".png")))encoder.Save(file);
            }
        }
        main.Width=savedWidth;main.Height=savedHeight;await Task.Delay(80);
        main.Activate();tab.View.Focus();await Task.Delay(50);
        string url="https://example.com/a/path?search=hello%20world";
        main.AddressBox.Text=url;
        var mouse=new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent,Source=main.AddressBox};
        main.AddressBox.RaiseEvent(mouse);
        Check("first address click selects full URL",mouse.Handled&&main.AddressBox.SelectedText==url);
        main.AddressBox.Select(3,4);
        var second=new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent,Source=main.AddressBox};
        main.AddressBox.RaiseEvent(second);
        Check("second address click permits editing",!second.Handled&&main.AddressBox.SelectionStart==3&&main.AddressBox.SelectionLength==4);
        tab.View.Focus();await Task.Delay(50);main.AddressBox.Focus();
        Check("keyboard address focus selects full URL",main.AddressBox.SelectedText==url);
        main.AddressBox.Text=core.Source;tab.View.Focus();
        await MenuChecks(main,output);
        await DialogFitChecks(main,output);
        await NativeFitChecks();
        main.Activate();tab.View.Focus();
    }
    private static async Task MenuChecks(MainWindow main,string output)
    {
        foreach(string mode in new[]{"Dark","Light"})
        {
            var appearance=new BrowserSettings{ThemeMode=mode};AppearanceService.ApplyApplication(appearance);
            var menu=main.BuildGearMenu();
            menu.IsOpen=true;await Task.Delay(90);menu.UpdateLayout();
            Check(mode+" cog menu compact width",menu.ActualWidth>100&&menu.ActualWidth<300);
            Check(mode+" cog menu bounded height",menu.ActualHeight<350);
            var root=(Border)VisualTreeHelper.GetChild(menu,0);
            Check(mode+" cog background matches palette",((SolidColorBrush)root.Background).Color==AppearanceService.Palette(appearance)["HarborPanel"]);
            var first=menu.Items.OfType<MenuItem>().First();first.ApplyTemplate();
            var marker=(ColumnDefinition)first.Template.FindName("MarkerColumn",first);
            Check(mode+" no empty icon gutter",marker.ActualWidth==0);
            Capture(menu,Path.Combine(output,"cog-"+mode.ToLowerInvariant()+".png"));
            var groups=menu.Items.OfType<MenuItem>().First(i=>Label(i).StartsWith("Tab groups"));
            groups.IsSubmenuOpen=true;await Task.Delay(90);groups.ApplyTemplate();
            var popup=(Popup)groups.Template.FindName("PART_Popup",groups);
            Check(mode+" group flyout opens",popup.IsOpen&&popup.Child!=null);
            var panel=(Border)popup.Child;
            Check(mode+" flyout background matches palette",((SolidColorBrush)panel.Background).Color==AppearanceService.Palette(appearance)["HarborPanel"]);
            Check(mode+" flyout height bounded",panel.ActualHeight<=(double)Application.Current.Resources["HarborMenuMaxHeight"]+.5);
            var surface=(Border)groups.Template.FindName("MenuSurface",groups);
            Check(mode+" open item highlight themed",((SolidColorBrush)surface.Background).Color==AppearanceService.Palette(appearance)["HarborSelected"]);
            Capture(panel,Path.Combine(output,"groups-"+mode.ToLowerInvariant()+".png"));
            groups.IsSubmenuOpen=false;
            var history=menu.Items.OfType<MenuItem>().First(i=>Label(i).StartsWith("History ("));
            history.IsSubmenuOpen=true;await Task.Delay(90);history.ApplyTemplate();
            var historyPopup=(Popup)history.Template.FindName("PART_Popup",history);
            var historyPanel=(Border)historyPopup.Child;
            var scroll=FindVisual<ScrollViewer>(historyPanel);scroll.ApplyTemplate();
            var bar=(ScrollBar)scroll.Template.FindName("PART_VerticalScrollBar",scroll);
            Check(mode+" History scrollbar visible",bar!=null&&bar.Visibility==Visibility.Visible&&scroll.ScrollableHeight>0);
            double barRight=Math.Max(bar.PointToScreen(new Point()).X,bar.PointToScreen(new Point(bar.ActualWidth,0)).X);
            double scrollRight=Math.Max(scroll.PointToScreen(new Point()).X,scroll.PointToScreen(new Point(scroll.ActualWidth,0)).X);
            Check(mode+" History scrollbar on right",Math.Abs(barRight-scrollRight)<2);
            Check(mode+" History flyout still opens toward left",historyPopup.Placement==PlacementMode.Left);
            scroll.ScrollToBottom();await Task.Delay(60);
            Check(mode+" History scroll reaches bottom",scroll.VerticalOffset>0&&Math.Abs(scroll.VerticalOffset-scroll.ScrollableHeight)<.5);
            Capture(historyPanel,Path.Combine(output,"history-scroll-"+mode.ToLowerInvariant()+".png"));
            history.IsSubmenuOpen=false;menu.IsOpen=false;await Task.Delay(20);
        }
        AppearanceService.ApplyApplication(new BrowserSettings{ThemeMode="Dark"});
    }
    private static async Task DialogFitChecks(MainWindow main,string output)
    {
        foreach(var size in new[]{new Size(650,552),new Size(600,448),new Size(500,330)})
        {
            var dialog=new SettingsWindow(new BrowserSettings{ThemeMode="Dark"}){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=0,Width=size.Width,Height=size.Height,MinWidth=300,MinHeight=240,ShowInTaskbar=false,ShowActivated=false};
            WindowFitService.SetIsEnabled(dialog,false);dialog.Show();await Task.Delay(70);
            var root=(FrameworkElement)dialog.Content;
            var save=(Button)dialog.FindName("SaveSettingsButton");
            var cancel=(Button)dialog.FindName("CancelSettingsButton");
            var p=save.TransformToAncestor(root).Transform(new Point());
            Check("settings "+size+" Save fits",p.X>=0&&p.Y>=0&&p.X+save.ActualWidth<=root.ActualWidth+.5&&p.Y+save.ActualHeight<=root.ActualHeight+.5);
            var c=cancel.TransformToAncestor(root).Transform(new Point());
            Check("settings "+size+" Cancel fits",c.X>=0&&c.X+cancel.ActualWidth<=root.ActualWidth+.5);
            var scroll=FindVisual<ScrollViewer>(root);
            Check("settings "+size+" scrolls instead of clipping",scroll!=null&&scroll.ScrollableHeight>0);
            if(size.Height==448){((Panel)root).Background=dialog.Background;await Task.Delay(50);Capture(root,Path.Combine(output,"settings-small-screen.png"));}
            dialog.Close();
        }
    }
    private static async Task NativeFitChecks()
    {
        var layout=new MonitorLayout(new PixelRect(-20000,0,-18634,768),new PixelRect(-20000,0,-18634,728));
        var window=new Window{Width=1320,Height=850,MinWidth=800,MinHeight=560,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20050,Top=-50,ShowInTaskbar=false,ShowActivated=false};
        WindowFitService.Attach(window,h=>layout);window.Show();await Task.Delay(100);
        NativeWindowGeometry.Rect bounds;NativeWindowGeometry.GetWindowRect(new WindowInteropHelper(window).Handle,out bounds);
        Check("native 1366x768 window fit keeps every edge visible",bounds.Left>=layout.Work.Left&&bounds.Top>=0&&bounds.Right<=layout.Work.Right&&bounds.Bottom<=layout.Work.Bottom);
        var dpi=VisualTreeHelper.GetDpi(window);
        Check("native fit adjusts minimum height to available space",window.MinHeight<=layout.Work.Height/dpi.DpiScaleY-24+.5);
        window.Close();
    }
    private static T FindVisual<T>(DependencyObject parent) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);var item=child as T;if(item!=null)return item;
            item=FindVisual<T>(child);if(item!=null)return item;
        }
        return null;
    }
}
