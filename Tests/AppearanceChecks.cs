using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HarborBrowser.Services;
internal static partial class Program {

    private static void NewTabAndAppearanceChecks()
    {
        Equal("new ordinary tab appends right","4",TabOrderService.NewTabIndex(new[]{true,false,false,false},false).ToString());
        Equal("new pinned tab stays with pinned group","2",TabOrderService.NewTabIndex(new[]{true,true,false},true).ToString());
        Equal("first tab index","0",TabOrderService.NewTabIndex(new bool[0],false).ToString());
        Equal("move regular tab to right","3",TabOrderService.MoveIndex(new[]{true,false,false,false},1,3,true).ToString());
        Equal("move regular tab to left","1",TabOrderService.MoveIndex(new[]{true,false,false,false},3,1,false).ToString());
        Equal("regular tab cannot enter pinned group","2",TabOrderService.MoveIndex(new[]{true,true,false,false},3,0,false).ToString());
        Equal("pinned tab cannot enter regular group","1",TabOrderService.MoveIndex(new[]{true,true,false,false},0,3,true).ToString());
        Equal("pinned order can change","1",TabOrderService.MoveIndex(new[]{true,true,false},0,1,true).ToString());
        Equal("drop on self preserves order","1",TabOrderService.MoveIndex(new[]{false,false},1,1,true).ToString());
        bool invariant=true;
        for(int n=1;n<=8;n++)for(int p=0;p<=n;p++)for(int from=0;from<n;from++)for(int target=0;target<n;target++)foreach(bool after in new[]{false,true})
        {
            var flags=Enumerable.Range(0,n).Select(i=>i<p).ToList();
            var order=Enumerable.Range(0,n).ToList();int insertion=TabOrderService.MoveIndex(flags,from,target,after);
            int moving=order[from];order.RemoveAt(from);order.Insert(insertion,moving);
            invariant &= order.Distinct().Count()==n && order.Take(p).All(i=>i<p) &&
                order.Skip(p).All(i=>i>=p) &&
                order.Where(i=>i!=moving).SequenceEqual(Enumerable.Range(0,n).Where(i=>i!=moving));
        }
        Check("exhaustive reorder permutation, stable neighbors and pin-boundary invariants",invariant);
        Color color;
        Check("hex RGB accepted",AppearanceService.TryColor("#1aB2c3",out color));
        Equal("hex canonical form","#1AB2C3",AppearanceService.Hex(color));
        Check("short hex accepted",AppearanceService.TryColor("#abc",out color));
        Equal("short hex expansion","#AABBCC",AppearanceService.Hex(color));
        Check("invalid hex rejected",!AppearanceService.TryColor("#xx3456",out color));
        Check("alpha input rejected",!AppearanceService.TryColor("#80112233",out color));
        Check("empty hex rejected",!AppearanceService.TryColor("",out color));
        Equal("wheel red","#FF0000",AppearanceService.Hex(AppearanceService.FromHsv(0,1,1)));
        Equal("wheel green","#00FF00",AppearanceService.Hex(AppearanceService.FromHsv(120,1,1)));
        Equal("wheel blue","#0000FF",AppearanceService.Hex(AppearanceService.FromHsv(240,1,1)));
        Equal("wheel hue wrapping","#FF0000",AppearanceService.Hex(AppearanceService.FromHsv(360,1,1)));
        Equal("wheel brightness grayscale","#808080",AppearanceService.Hex(AppearanceService.FromHsv(80,0,.5)));
        double h,s,v;var original=Color.FromRgb(123,51,200);AppearanceService.ToHsv(original,out h,out s,out v);
        Equal("HSV round trip",AppearanceService.Hex(original),AppearanceService.Hex(AppearanceService.FromHsv(h,s,v)));
        var settings=new HarborBrowser.Models.BrowserSettings();
        var light=AppearanceService.Palette(settings,"Light");
        var dark=AppearanceService.Palette(settings,"Dark");
        Check("dark background darker than light",AppearanceService.Luminance(dark["HarborWindow"])<AppearanceService.Luminance(light["HarborWindow"]));
        Check("light default text readable",AppearanceService.Contrast(light["HarborText"],light["HarborInput"])>=4.5);
        Check("dark default text readable",AppearanceService.Contrast(dark["HarborText"],dark["HarborInput"])>=4.5);
        settings.LightColors.Accent="#123456";settings.DarkColors.Accent="#FEDCBA";
        Equal("light custom color independent","#123456",AppearanceService.Hex(AppearanceService.Palette(settings,"Light")["HarborAccent"]));
        Equal("dark custom color independent","#FEDCBA",AppearanceService.Hex(AppearanceService.Palette(settings,"Dark")["HarborAccent"]));
        Check("accent foreground contrast selected automatically",AppearanceService.Contrast(
            AppearanceService.Palette(settings,"Light")["HarborAccent"],AppearanceService.Palette(settings,"Light")["HarborAccentText"])>=4.5);
        var clone=settings.Copy();clone.DarkColors.Accent="#FF0000";
        Equal("palette draft is a deep copy","#FEDCBA",settings.DarkColors.Accent);
        var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
        settings.ThemeMode="Dark";
        var roundtrip=serializer.Deserialize<HarborBrowser.Models.BrowserSettings>(serializer.Serialize(settings));
        Equal("theme mode JSON round trip","Dark",roundtrip.ThemeMode);
        Equal("custom palette JSON round trip","#FEDCBA",roundtrip.DarkColors.Accent);
        var legacy=serializer.Deserialize<HarborBrowser.Models.BrowserSettings>("{\"HomePage\":\"https://example.com/\"}");
        AppearanceService.Normalize(legacy);
        Check("legacy profile gets valid appearance",legacy.ThemeMode=="Light"&&legacy.LightColors!=null&&legacy.DarkColors!=null);
        settings.DarkColors.Accent="not-a-color";
        Check("bad stored color falls back safely",AppearanceService.Hex(AppearanceService.Palette(settings,"Dark")["HarborAccent"])=="#EEB273");
        Equal("home theme message accepted","Dark",HomePageService.ReadThemeMessage(HomePageService.Url,"{\"type\":\"home-theme\",\"value\":\"Dark\"}"));
        Equal("external theme message rejected",null,HomePageService.ReadThemeMessage("https://example.com/","{\"type\":\"home-theme\",\"value\":\"Dark\"}"));
        Equal("invalid theme value rejected",null,HomePageService.ReadThemeMessage(HomePageService.Url,"{\"type\":\"home-theme\",\"value\":\"Red\"}"));
        SettingsChecks();
    }

    private static void SettingsChecks()
    {
        var input=new HarborBrowser.Models.BrowserSettings();
        string global=AppearanceService.Hex(((SolidColorBrush)Application.Current.Resources["HarborWindow"]).Color);
        var dialog=new HarborBrowser.Views.SettingsWindow(input);
        WindowFitService.SetIsEnabled(dialog,false);
        dialog.Left=-10000;dialog.Top=-10000;dialog.ShowInTaskbar=false;
        Exception callbackError=null;
        dialog.Loaded+=(sender,args)=>{
            try
            {
                ((ComboBox)dialog.FindName("AppearanceModeBox")).SelectedValue="Dark";
                var wheel=(HarborBrowser.Views.ColorWheel)dialog.FindName("AppearanceWheel");
                wheel.SelectedColor=Color.FromRgb(120,80,210);
                Equal("wheel updates hex field","#7850D2",((TextBox)dialog.FindName("AppearanceHexBox")).Text);
                Equal("draft leaves original mode unchanged","Light",input.ThemeMode);
                Equal("draft leaves app palette unchanged",global,AppearanceService.Hex(((SolidColorBrush)Application.Current.Resources["HarborWindow"]).Color));
                var hex=(TextBox)dialog.FindName("AppearanceHexBox");hex.Text="invalid";
                ((Button)dialog.FindName("SaveSettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check("invalid color prevents settings save",dialog.ResultSettings==null&&dialog.IsVisible);
                hex.Text="#765432";
                ((Button)dialog.FindName("SaveSettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch(Exception ex){callbackError=ex;dialog.Close();}
        };
        bool? saved=dialog.ShowDialog();if(callbackError!=null)throw callbackError;
        Check("settings Save completes",saved==true&&dialog.ResultSettings!=null);
        Equal("saved mode reflects selection","Dark",dialog.ResultSettings.ThemeMode);
        Equal("saved color reflects hex input","#765432",dialog.ResultSettings.DarkColors.Accent);
        Equal("saved light palette unchanged",null,dialog.ResultSettings.LightColors.Accent);
        var cancel=new HarborBrowser.Views.SettingsWindow(input);
        WindowFitService.SetIsEnabled(cancel,false);
        cancel.Left=-10000;cancel.Top=-10000;cancel.ShowInTaskbar=false;
        cancel.Loaded+=(sender,args)=>{
            try{
                ((ComboBox)cancel.FindName("AppearanceModeBox")).SelectedValue="Dark";
                ((Button)cancel.FindName("CancelSettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }catch(Exception ex){callbackError=ex;cancel.Close();}
        };
        bool? canceled=cancel.ShowDialog();if(callbackError!=null)throw callbackError;
        Check("Cancel discards preview",canceled==false&&cancel.ResultSettings==null&&input.ThemeMode=="Light");
        var preview=new HarborBrowser.Views.SettingsWindow(new HarborBrowser.Models.BrowserSettings {ThemeMode="Dark"});
        var previewWheel=(HarborBrowser.Views.ColorWheel)preview.FindName("AppearanceWheel");
        previewWheel.SelectedColor=Color.FromRgb(220,100,80);
        ((Button)preview.FindName("ResetColorButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Equal("reset color restores mode default","#EEB273",((TextBox)preview.FindName("AppearanceHexBox")).Text);
        Equal("settings dark background applied","#101D27",AppearanceService.Hex(((SolidColorBrush)preview.Background).Color));
        var frame=(FrameworkElement)preview.Content;
        ((Panel)frame).Background=preview.Background;
        preview.Content=null;frame.Resources.MergedDictionaries.Add(preview.Resources);
        frame.Measure(new Size(630,700));frame.Arrange(new Rect(0,0,630,700));frame.UpdateLayout();
        var bitmap=new RenderTargetBitmap(630,700,96,96,PixelFormats.Pbgra32);bitmap.Render(frame);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var stream=File.Create(Path.Combine(root,"settings-dark.png")))encoder.Save(stream);
        preview.Close();
    }

}
