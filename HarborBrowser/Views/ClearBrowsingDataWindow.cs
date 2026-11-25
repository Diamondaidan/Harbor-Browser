using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Models;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    public sealed class ClearBrowsingDataWindow : Window
    {
        public readonly ComboBox RangeBox=new ComboBox{SelectedValuePath="Tag"};
        public readonly CheckBox HistoryBox=new CheckBox{Content="Browsing history",IsChecked=true};
        public readonly CheckBox CookiesBox=new CheckBox{Content="Cookies and site storage (may sign you out)"};
        public readonly CheckBox CacheBox=new CheckBox{Content="Cached images and files",IsChecked=true};
        public readonly CheckBox PermissionsBox=new CheckBox{Content="Remembered site permissions"};
        public readonly Button ClearButton=new Button{Content="Clear selected data",Padding=new Thickness(14,6,14,6)};
        private readonly TextBlock status=new TextBlock{TextWrapping=TextWrapping.Wrap};
        private bool busy;
        public ClearBrowsingDataWindow(bool isPrivate,Func<BrowsingDataRequest,Task> clear)
        {
            SetResourceReference(StyleProperty,typeof(Window));
            Title="Clear browsing data";Width=570;Height=490;MinWidth=400;MinHeight=280;WindowStartupLocation=WindowStartupLocation.CenterOwner;FontFamily=new System.Windows.Media.FontFamily("Segoe UI");FontSize=13;WindowFitService.Attach(this);
            var root=new DockPanel{Margin=new Thickness(18)};Content=root;
            var footer=new DockPanel{Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);var buttons=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(buttons,Dock.Right);footer.Children.Add(buttons);var close=new Button{Content="Close",Width=80};buttons.Children.Add(close);buttons.Children.Add(ClearButton);footer.Children.Add(status);
            var form=new StackPanel();root.Children.Add(new SmoothScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
            form.Children.Add(new TextBlock{Text="Clear browsing data",FontSize=23,FontWeight=FontWeights.SemiBold});
            form.Children.Add(new TextBlock{Text=isPrivate?"Scope: current private session":"Scope: normal browsing",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,12)});
            form.Children.Add(new TextBlock{Text="Time range",Margin=new Thickness(0,0,0,5)});form.Children.Add(RangeBox);
            string[] labels={"Last hour","Last 24 hours","Last 7 days","Last 4 weeks","All time"};int i=0;foreach(BrowsingTimeRange range in Enum.GetValues(typeof(BrowsingTimeRange)))RangeBox.Items.Add(new ComboBoxItem{Content=labels[i++],Tag=range});RangeBox.SelectedIndex=0;
            foreach(var box in new[]{HistoryBox,CookiesBox,CacheBox,PermissionsBox}){box.Margin=new Thickness(0,12,0,0);form.Children.Add(box);}
            form.Children.Add(new TextBlock{Text=isPrivate?"Private tabs do not save Harbor history. Open tabs and downloaded files are kept.":"Open tabs, bookmarks and downloaded files are kept. History cleanup also resets closed-tab and crash-recovery lists.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,0)});
            form.Children.Add(new TextBlock{Text="Open pages may save new data after cleanup. Some site storage is removed for an entire matching site.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
            close.Click+=(s,e)=>{if(!busy)Close();};Closing+=(s,e)=>{if(busy)e.Cancel=true;};
            ClearButton.Click+=async(s,e)=>{
                var request=BuildRequest();if(!request.HasSelection){status.Text="Choose at least one data type.";return;}
                busy=true;ClearButton.IsEnabled=false;close.IsEnabled=false;status.Text="Clearing…";
                try{await clear(request);status.Text="Selected data cleared.";}catch(Exception ex){status.Text="Cleanup did not finish: "+ex.Message;}
                finally{busy=false;ClearButton.IsEnabled=true;close.IsEnabled=true;}
            };
        }
        internal BrowsingDataRequest BuildRequest()
        {var request=new BrowsingDataRequest{Range=(BrowsingTimeRange)(RangeBox.SelectedValue??BrowsingTimeRange.LastHour),History=HistoryBox.IsChecked==true,CookiesAndSiteData=CookiesBox.IsChecked==true,Cache=CacheBox.IsChecked==true,Permissions=PermissionsBox.IsChecked==true};request.SetBounds(DateTime.UtcNow);return request;}
    }
}
