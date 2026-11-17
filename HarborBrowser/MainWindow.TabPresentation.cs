using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private bool _updatingTabWidths;
        private void BringTitleTabIntoView(BrowserTab tab)
        {
            if(SelectedTab!=tab||tab.IsClosing||tab.Item.Visibility!=Visibility.Visible)return;
            tab.Item.BringIntoView();
            var last=BrowserTabs.Items.Cast<TabItem>().LastOrDefault(i=>i.Visibility==Visibility.Visible);
            if(ReferenceEquals(last,tab.Item))
            {
                var button=BrowserTabs.Template.FindName("NewTabButton",BrowserTabs) as FrameworkElement;
                button?.BringIntoView();
            }
        }
        internal void UpdateTabWidths()
        {
            if(_updatingTabWidths||BrowserTabs==null||BrowserTabs.ActualWidth<=0)return;
            _updatingTabWidths=true;
            try
            {
                var tabs=BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Where(t=>t!=null&&t.Item.Visibility==Visibility.Visible).ToList();
                double width=TabStripLayout.Width(BrowserTabs.ActualWidth,tabs.Count(t=>!t.IsPinned),tabs.Count(t=>t.IsPinned));
                foreach(var tab in tabs)
                {
                    double size=tab.IsPinned?TabStripLayout.PinnedWidth:width;tab.Item.Width=size;
                    tab.Item.Padding=new Thickness(size<95?4:7,3,size<95?4:7,3);
                    tab.HeaderText.Visibility=tab.IsPinned||size<78?Visibility.Collapsed:Visibility.Visible;
                    tab.HeaderText.MaxWidth=Math.Max(0,size-58);
                    tab.CloseButton.Visibility=tab.IsPinned||size<95?Visibility.Collapsed:Visibility.Visible;
                    if(tab.HeaderLayout!=null)tab.HeaderLayout.Width=Math.Max(16,size-(size<95?12:18));
                }
            }
            finally{_updatingTabWidths=false;}
        }
        private void ResetTabFavicon(BrowserTab tab,string address=null)
        {
            tab.FaviconRevision++;tab.HeaderIcon.Source=null;tab.HeaderIcon.Visibility=Visibility.Collapsed;tab.FallbackIcon.Visibility=Visibility.Visible;
            tab.FallbackIcon.Text=tab.NativeContent is DownloadsPage?"↓":"◉";
            if(HomePageService.IsHomeUrl(address??tab.PendingUrl??tab.LastKnownUrl))
            {
                tab.HeaderIcon.Source=new BitmapImage(new Uri("pack://application:,,,/HarborBrowser;component/Assets/Home/beacon.png"));tab.HeaderIcon.Visibility=Visibility.Visible;tab.FallbackIcon.Visibility=Visibility.Collapsed;
            }
        }
        private void UpdateTabFavicon(BrowserTab tab){_ = LoadTabFavicon(tab);}
        private async Task LoadTabFavicon(BrowserTab tab)
        {
            if(tab==null||tab.IsClosing||!tab.Initialized||tab.NeedsNewView||tab.View?.CoreWebView2==null)return;
            var view=tab.View;var core=view.CoreWebView2;int navigation=tab.NavigationGeneration,revision=++tab.FaviconRevision;
            try
            {
                if(HomePageService.IsHomeUrl(core.Source)){ResetTabFavicon(tab,core.Source);return;}
                if(string.IsNullOrWhiteSpace(core.FaviconUri)){ResetTabFavicon(tab,core.Source);return;}
                using(var image=await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png))
                {
                    if(image==null)return;
                    using(var bytes=new MemoryStream())
                    {
                        var buffer=new byte[8192];int read;
                        while((read=await image.ReadAsync(buffer,0,buffer.Length))>0){if(bytes.Length+read>2*1024*1024)return;bytes.Write(buffer,0,read);}
                        if(tab.IsClosing||tab.View!=view||tab.NavigationGeneration!=navigation||tab.FaviconRevision!=revision)return;
                        bytes.Position=0;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=32;bitmap.StreamSource=bytes;bitmap.EndInit();bitmap.Freeze();
                        tab.HeaderIcon.Source=bitmap;tab.HeaderIcon.Visibility=Visibility.Visible;tab.FallbackIcon.Visibility=Visibility.Collapsed;
                    }
                }
            }
            catch(Exception ex){System.Diagnostics.Debug.WriteLine("Favicon unavailable: "+ex.Message);}
        }
    }
}
