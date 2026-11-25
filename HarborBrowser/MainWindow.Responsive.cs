using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HarborBrowser.Services;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private void InitializeResponsiveLayout()
        {
            SizeChanged+=(s,e)=>UpdateResponsiveLayout();
            Loaded+=(s,e)=>UpdateResponsiveLayout();
            BrowserTabs.SizeChanged+=(s,e)=>UpdateTabWidths();
            BookmarksBar.SizeChanged+=(s,e)=>BookmarksBarPanel.MinWidth=Math.Max(0,e.NewSize.Width-2);
            WindowFitService.Attach(this,_monitorProvider,()=>_presentation==null||!_presentation.IsFullScreen);
        }
        internal void UpdateResponsiveLayout()
        {
            double width=ActualWidth>0?ActualWidth:Width;
            StatusText.MaxWidth=Math.Max(100,width-190);
            bool brand=ResponsiveLayout.ShowWordmark(width),engine=ResponsiveLayout.ShowEngine(width),secondary=ResponsiveLayout.ShowSecondaryNavigation(width);
            HeaderBrandWordmark.Visibility=brand?Visibility.Visible:Visibility.Collapsed;BrandColumn.Width=new GridLength(brand?118:44);
            TitleDragColumn.Width=new GridLength(width<650?20:46);
            SearchEngineBox.Visibility=engine?Visibility.Visible:Visibility.Collapsed;EngineColumn.Width=new GridLength(engine?145:0);
            GoButton.Visibility=engine?Visibility.Visible:Visibility.Collapsed;GoColumn.Width=new GridLength(engine?46:0);
            HomeButton.Visibility=secondary?Visibility.Visible:Visibility.Collapsed;HomeColumn.Width=new GridLength(secondary?34:0);
            BookmarkToggleButton.Visibility=secondary?Visibility.Visible:Visibility.Collapsed;BookmarkColumn.Width=new GridLength(secondary?35:0);
            UpdateTabWidths();
        }
        private void AddressBox_PreviewMouseLeftButtonDown(object sender,MouseButtonEventArgs e)
        {
            if(AddressBox.IsKeyboardFocusWithin)return;
            AddressBox.Focus();AddressBox.SelectAll();e.Handled=true;
        }
        private void AddressBox_GotKeyboardFocus(object sender,KeyboardFocusChangedEventArgs e)
        {
            AddressBox.SelectAll();
        }
    }
}
