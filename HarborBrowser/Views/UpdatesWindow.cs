using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarborBrowser.Updates;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    internal sealed class UpdateInstallChoice {internal VerifiedUpdate Verified;internal UpdatePackage Package;internal string Path;internal bool Rollback;}
    internal sealed class UpdatesWindow : Window
    {
        internal readonly Button CheckButton=new Button{Content="Check for updates"},DownloadButton=new Button{Content="Download update"},PreviousButton=new Button{Content="Download previous version"},InstallButton=new Button{Content="Close Harbor and open setup"},CancelButton=new Button{Content="Pause download"};
        internal readonly TextBlock Status=new TextBlock{Text="Choose Check for updates when you're ready.",TextWrapping=TextWrapping.Wrap},ReleaseLabel=new TextBlock{TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,Visibility=Visibility.Collapsed};
        private readonly TextBox _notes=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=90,MaxHeight=180,Visibility=Visibility.Collapsed};
        private readonly ProgressBar _progress=new ProgressBar{Minimum=0,Maximum=100,Height=7,Margin=new Thickness(0,8,0,8)};
        private readonly UpdateService _service;private readonly string _current;private readonly BuildIdentity _build;private readonly CancellationToken _traffic;private readonly bool _readOnly;
        private readonly DispatcherTimer _timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
        private CancellationTokenSource _operation;private VerifiedUpdate _verified;private bool _busy,_closed;private UpdateInstallChoice _ready;
        internal UpdateInstallChoice InstallChoice {get;private set;}
        internal UpdatesWindow(UpdateService service,string current,CancellationToken traffic,bool readOnly=false,string buildIdentity=null)
        {
            _service=service;_current=current;_build=BuildIdentity.Parse(buildIdentity??current);_traffic=traffic;_readOnly=readOnly;
            if(_build.Number!=current)throw new InvalidDataException("Installed version and build identity do not match.");
            Title="Harbor updates and recovery";Width=620;Height=570;MinWidth=360;MinHeight=350;WindowStartupLocation=WindowStartupLocation.CenterOwner;WindowFitService.Attach(this);SetResourceReference(StyleProperty,typeof(Window));
            var stack=new StackPanel{Margin=new Thickness(18)};Content=new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            stack.Children.Add(new TextBlock{Text="Updates and recovery",FontSize=23,FontWeight=FontWeights.SemiBold});
            stack.Children.Add(new TextBlock{Text="Installed: Harbor "+_build.DisplayVersion,Margin=new Thickness(0,5,0,10)});
            stack.Children.Add(new TextBlock{Text="Checks contact Harbor's public GitHub releases using the Windows connection. Checks and downloads pause while Proton or Tor is enabled. Nothing installs automatically.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
            var actions=new WrapPanel();actions.Children.Add(CheckButton);actions.Children.Add(DownloadButton);actions.Children.Add(CancelButton);stack.Children.Add(actions);
            stack.Children.Add(_progress);stack.Children.Add(Status);stack.Children.Add(ReleaseLabel);stack.Children.Add(_notes);
            stack.Children.Add(InstallButton);
            stack.Children.Add(new Separator{Margin=new Thickness(0,15,0,12)});
            stack.Children.Add(new TextBlock{Text="Previous version and backups",FontSize=17,FontWeight=FontWeights.SemiBold});
            stack.Children.Add(new TextBlock{Text="Only a previous version approved for this release and profile format can be offered. Setup backs up bookmarks, settings and other Harbor data files before replacement. Account databases remain in place and are not copied.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,8)});
            var recovery=new WrapPanel();recovery.Children.Add(PreviousButton);var backups=new Button{Content="Open profile backups"};recovery.Children.Add(backups);stack.Children.Add(recovery);
            var close=new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};close.Click+=(s,e)=>Close();stack.Children.Add(close);
            CheckButton.Click+=async(s,e)=>await Run(async cancel=>{Status.Text="Checking Harbor's public releases…";_verified=await _service.Check(cancel);ShowRelease();var latest=UpdateTrust.VersionOf(_verified.Manifest.Latest.Version);Status.Text=_build.CanUpdateTo(_verified.Manifest.Latest.Version)?(latest==_build.Version?"The full release is available to replace this test build.":"A newer stable release is available."):(latest<_build.Version?"This build is newer than the latest published release.":"You're using the latest published Harbor release.");});
            DownloadButton.Click+=async(s,e)=>await Download(false);
            PreviousButton.Click+=async(s,e)=>await Download(true);
            CancelButton.Click+=(s,e)=>_operation?.Cancel();
            backups.Click+=(s,e)=>{try{string path=Path.Combine(Path.GetDirectoryName(_service.Root),"UpgradeBackups");if(Directory.Exists(path))Process.Start(new ProcessStartInfo(path){UseShellExecute=true});else Status.Text="No upgrade backups have been created yet.";}catch{Status.Text="The backup folder could not be opened.";}};
            InstallButton.Click+=(s,e)=>
            {
                if(_ready==null)return;
                string message=(_ready.Rollback?"Return to Harbor ":"Install Harbor ")+_ready.Package.Version+"? Save web forms and finish downloads first. Harbor will close, back up its data files, and open Windows setup. You must approve any Windows prompt yourself.";
                if(MessageBox.Show(this,message,"Harbor setup",MessageBoxButton.OKCancel,MessageBoxImage.Information)!=MessageBoxResult.OK)return;
                InstallChoice=_ready;DialogResult=true;
            };
            Loaded+=(s,e)=>{try{_verified=_service.Cached();if(_verified!=null){ShowRelease();Status.Text="Saved signed release information. Check again for the latest release.";}string last=_service.LastAttempt();if(last!=null)Status.Text=last;}catch(Exception ex){Status.Text=ex.Message;}Refresh();_timer.Start();};
            _timer.Tick+=(s,e)=>Refresh();
            Closed+=(s,e)=>{_closed=true;_operation?.Cancel();_timer.Stop();};Refresh();
        }
        private void ShowRelease(){ReleaseLabel.Text="Published: Harbor "+_verified.Manifest.Latest.Version;ReleaseLabel.Visibility=Visibility.Visible;_notes.Text=_verified.Manifest.Notes;_notes.Visibility=Visibility.Visible;DownloadButton.Content="Download Harbor "+_verified.Manifest.Latest.Version;if(_verified.Manifest.Previous!=null)PreviousButton.Content="Download previous ("+_verified.Manifest.Previous.Version+")";_ready=null;}
        private async Task Download(bool previous)
        {
            if(_verified==null)return;var package=previous?_verified.Manifest.Previous:_verified.Manifest.Latest;if(package==null)return;
            await Run(async cancel=>
            {
                Status.Text="Downloading Harbor "+package.Version+"…";
                var progress=new Progress<UpdateProgress>(p=>{if(_closed)return;_progress.Value=100.0*p.Done/p.Total;Status.Text="Downloading: "+(p.Done/1048576.0).ToString("F1")+" / "+(p.Total/1048576.0).ToString("F1")+" MB";});
                string path=await _service.Download(_verified,package,progress,cancel);
                _ready=new UpdateInstallChoice{Verified=_verified,Package=package,Path=path,Rollback=previous};
                Status.Text=string.IsNullOrEmpty(package.CertificateSha256)?"Harbor release signature and installer hash verified. This Windows installer is unsigned; Windows may show an unknown publisher.":"Harbor release signature, installer hash and Windows publisher signature verified.";
            });
        }
        private async Task Run(Func<CancellationToken,Task> action)
        {
            if(_busy)return;_busy=true;Refresh();
            using(var cancel=CancellationTokenSource.CreateLinkedTokenSource(_traffic))
            {
                _operation=cancel;
                try{await action(cancel.Token);}catch(OperationCanceledException){Status.Text="Paused. Choose Download again to resume when possible.";}catch(Exception ex){Status.Text=ex.Message;}
                finally{_operation=null;_busy=false;Refresh();}
            }
        }
        private void Refresh()
        {
            bool network=_service.NetworkAllowed&&!_traffic.IsCancellationRequested;
            _progress.Visibility=_busy?Visibility.Visible:Visibility.Collapsed;
            CheckButton.IsEnabled=!_busy&&network;CancelButton.IsEnabled=_busy;
            DownloadButton.IsEnabled=!_busy&&network&&!_readOnly&&_verified!=null&&_build.CanUpdateTo(_verified.Manifest.Latest.Version);
            PreviousButton.IsEnabled=!_busy&&network&&!_readOnly&&_verified?.Manifest.Previous!=null&&_verified.Manifest.RollbackFrom==_current;
            InstallButton.IsEnabled=!_busy&&!_readOnly&&_ready!=null;
            if(!_busy&&!network)Status.Text="Update checks and downloads are paused while a browser connection is enabled. Turn it off in its controls yourself when you want to check.";
        }
    }
}
