using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using HarborBrowser.Services;
using HarborBrowser.Views;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private async Task InitializeVideoFeatures(BrowserTab tab,CoreWebView2 core)
        {
            string script=VideoScriptService.Create(Guid.NewGuid().ToString("N"));
            await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
            core.FrameCreated+=(s,e)=>TrackMediaFrame(tab,e.Frame);
            core.ContainsFullScreenElementChanged+=(s,e)=>{
                if(SelectedTab==tab&&_presentation!=null&&tab.VideoPopout==null)
                    _presentation.SetVideoFullScreen(core.ContainsFullScreenElement);
            };
            var pipItem=_environment.CreateContextMenuItem("Picture in picture",null,CoreWebView2ContextMenuItemKind.Command);
            pipItem.CustomItemSelected+=(s,e)=>Dispatcher.BeginInvoke(new Action(async()=>await TogglePictureInPictureAsync(tab)));
            core.ContextMenuRequested+=(s,e)=>{
                if(e.ContextMenuTarget.Kind==CoreWebView2ContextMenuTargetKind.Video)e.MenuItems.Add(pipItem);
            };

        }
        private void TrackMediaFrame(BrowserTab tab,CoreWebView2Frame frame)
        {
            tab.MediaFrames.Add(frame);frame.Destroyed+=(s,e)=>tab.MediaFrames.Remove(frame);
            frame.FrameCreated+=(s,e)=>TrackMediaFrame(tab,e.Frame);
        }
        private IEnumerable<Func<string,Task<string>>> VideoContexts(BrowserTab tab)
        {
            yield return script=>tab.View.CoreWebView2.ExecuteScriptAsync(script);
            foreach(var frame in tab.MediaFrames.ToArray())yield return script=>frame.ExecuteScriptAsync(script);
        }
        private async void PictureInPicture_Click(object sender,RoutedEventArgs e)
        {
            await TogglePictureInPictureAsync(SelectedTab);
        }
        internal async Task TogglePictureInPictureAsync(BrowserTab tab)
        {
            if(tab==null||!tab.Initialized||tab.IsClosing)return;
            if(tab.VideoPopout!=null){tab.VideoPopout.Close();return;}
            if(tab.OpeningPopout)return;tab.OpeningPopout=true;
            try
            {
                ResumeTabIfNeeded(tab);
                Func<string,Task<string>> selected=null;long best=0;
                foreach(var context in VideoContexts(tab))
                {
                    try{long score=VideoScriptService.Score(await context(VideoScriptService.Command("probe")));if(score>best){best=score;selected=context;}}catch{}
                }
                if(selected==null){SetStatus("No ready video found on this page. Start a video first.");return;}
                await ExitVideoFullScreen();
                if(tab.IsClosing)return;
                string entered=await selected(VideoScriptService.Command("enter"));
                if(!entered.Contains("true")){SetStatus("This video could not be opened in picture in picture.");return;}
                var popout=new PictureInPictureWindow(()=>{ BrowserTabs.SelectedItem=tab.Item; tab.VideoPopout.Close(); if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal; Activate(); },
                    ()=>RunVideoCommand(selected,"togglePlayback"),()=>RunVideoCommand(selected,"toggleMute"));
                tab.VideoPopout=popout;tab.ActiveVideoContext=selected;
                if(ReferenceEquals(BrowserContentHost.Content,tab.View))BrowserContentHost.Content=null;
                popout.PlayerHost.Content=tab.View;
                var monitor=_presentation.Monitor.Work;
                var source=PresentationSource.FromVisual(this);double scale=source==null?1:source.CompositionTarget.TransformToDevice.M11;
                popout.Left=monitor.Left/scale+10;popout.Top=monitor.Top/scale+10;
                popout.Closed+=(s,e)=>ReturnVideoToTab(tab,popout);
                popout.Show();
                NativeWindowGeometry.SetBounds(new WindowInteropHelper(popout).Handle,
                    WindowFitService.BottomRight(monitor,500,330,scale,scale));
                ShowTabContent();
                SetStatus("Picture in picture opened. Close it or choose Return to tab to put the video back.");
            }
            catch(Exception ex){if(tab.VideoPopout!=null)tab.VideoPopout.Close();SetStatus("Picture in picture unavailable: "+ex.Message);await RestoreVideoStyles(tab);}
            finally{tab.OpeningPopout=false;}
        }
        private static async void RunVideoCommand(Func<string,Task<string>> context,string command)
        {
            try{await context(VideoScriptService.Command(command));}catch{}
        }
        private async Task RestoreVideoStyles(BrowserTab tab)
        {
            foreach(var context in VideoContexts(tab)){try{await context(VideoScriptService.Command("exit"));}catch{}}
        }
        private async void ReturnVideoToTab(BrowserTab tab,PictureInPictureWindow popout)
        {
            popout.PlayerHost.Content=null;tab.VideoPopout=null;tab.ActiveVideoContext=null;
            if(!tab.IsClosing){ShowTabContent();await RestoreVideoStyles(tab);}
        }
        private void ShowTabContent()
        {
            var tab=SelectedTab;
            if(_presentation!=null)
            {
                bool videoFull=tab!=null&&!tab.IsClosing&&tab.FailureMessage==null&&tab.Initialized&&tab.VideoPopout==null&&tab.View.CoreWebView2!=null&&tab.View.CoreWebView2.ContainsFullScreenElement;
                _presentation.SetVideoFullScreen(videoFull);
            }
            if(tab==null){BrowserContentHost.Content=null;return;}
            if(tab.FailureMessage!=null){BrowserContentHost.Content=BuildTabRecoveryView(tab);return;}
            if(tab.NativeContent!=null){BrowserContentHost.Content=tab.NativeContent;return;}
            if(tab.VideoPopout==null){BrowserContentHost.Content=tab.View;return;}
            var panel=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            panel.Children.Add(new TextBlock{Text="This video is playing in picture in picture.",FontSize=18,Margin=new Thickness(15)});
            var button=new Button{Content="Return video to this tab",Padding=new Thickness(16,8,16,8)};
            button.Click+=(s,e)=>tab.VideoPopout.Close();panel.Children.Add(button);BrowserContentHost.Content=panel;
        }
    }
}
