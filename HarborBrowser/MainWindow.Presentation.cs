using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HarborBrowser.Services;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private WindowPresentationController _presentation;
        private readonly Func<IntPtr,MonitorLayout> _monitorProvider;
        private DateTime _lastFullscreenKey;
        private Visibility _savedDownloadVisibility;
        internal WindowPresentationController Presentation {get{return _presentation;}}
        private void InitializePresentation()
        {
            SourceInitialized+=(s,e)=>_presentation=new WindowPresentationController(this,SetPresentationChrome,_monitorProvider);
            Closed+=(s,e)=>{
                foreach(TabItem item in BrowserTabs.Items){var tab=item.Tag as Views.BrowserTab;if(tab!=null&&tab.VideoPopout!=null)tab.VideoPopout.Close();}
                if(_presentation!=null)_presentation.Dispose();
            };
        }
        private void SetPresentationChrome(bool full)
        {
            if(full&&BrowserChrome.Visibility!=Visibility.Collapsed)_savedDownloadVisibility=DownloadNotificationBar.Visibility;
            BrowserChrome.Visibility=full?Visibility.Collapsed:Visibility.Visible;
            BottomStatus.Visibility=full?Visibility.Collapsed:(_settings.ShowStatusBar?Visibility.Visible:Visibility.Collapsed);
            DownloadNotificationBar.Visibility=full?Visibility.Collapsed:_savedDownloadVisibility;
            WindowFrame.BorderThickness=new Thickness(full?0:1);WindowFrame.Padding=new Thickness(0);
        }
        private async void FullScreen_Click(object sender,RoutedEventArgs e)
        {
            if(_presentation==null)return;
            if(_presentation.VideoFullScreen){await ExitVideoFullScreen();return;}
            _presentation.SetBrowserFullScreen(!_presentation.BrowserFullScreen);
        }
        private async System.Threading.Tasks.Task ExitVideoFullScreen()
        {
            var tab=SelectedTab;
            if(tab!=null&&tab.Initialized)
            {
                try{await tab.View.CoreWebView2.ExecuteScriptAsync("document.fullscreenElement ? document.exitFullscreen().catch(()=>{}) : null");}catch{}
            }
            if(_presentation!=null)_presentation.SetVideoFullScreen(false);
        }
        private async void EscapeFullScreen()
        {
            await ExitVideoFullScreen();if(_presentation!=null)_presentation.SetBrowserFullScreen(false);
        }
        private void FullScreenShortcut()
        {
            if((DateTime.UtcNow-_lastFullscreenKey).TotalMilliseconds<200)return;
            _lastFullscreenKey=DateTime.UtcNow;FullScreen_Click(this,new RoutedEventArgs());
        }
        private void TitleTabStrip_MouseWheel(object sender,MouseWheelEventArgs e)
        {
            var scroll=FindTabScroller(BrowserTabs);if(scroll==null)return;
            if(e.Delta>0)scroll.LineLeft();else scroll.LineRight();e.Handled=true;
        }
        private void TitleTabStrip_MouseDown(object sender,MouseButtonEventArgs e)
        {
            var source=e.OriginalSource as DependencyObject;
            if(TabAncestor<TabItem>(source)!=null||TabAncestor<Button>(source)!=null)return;
            if(e.ClickCount==2)MaximizeWindow_Click(sender,e);
            else if(e.LeftButton==MouseButtonState.Pressed)DragMove();
            e.Handled=true;
        }
    }
}
