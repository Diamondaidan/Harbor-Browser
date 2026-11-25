using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    public sealed class BrowserVpnWindow:Window
    {
        private readonly TextBlock _status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)};
        private readonly Button _connect=new Button{Content="Connect with Tor"},_remove=new Button{Content="Disconnect and restore"};
        private readonly BrowserVpnService _service;
        private readonly Func<bool,Task> _change;
        private readonly DispatcherTimer _timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
        private bool _closed,_busy;
        public BrowserVpnWindow(BrowserVpnService service,Func<bool,Task> change,Action<string> open)
        {
            _service=service;_change=change;Title="Free browser connection · Tor preview";Width=520;Height=430;MinWidth=360;MinHeight=300;WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(StyleProperty,typeof(Window));WindowFitService.Attach(this);
            var body=new StackPanel{Margin=new Thickness(16)};Content=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            body.Children.Add(new TextBlock{Text="Free Tor connection",FontSize=22,FontWeight=FontWeights.SemiBold});
            body.Children.Add(new TextBlock{Text="Routes Harbor’s normal tabs through the free Tor network. No account is needed. Other apps keep their regular connection. Tor can be slower and some websites may block it.\n\nThis is a Tor connection, not a conventional VPN or Tor Browser. Private tabs and online typing suggestions pause while it is enabled. Connect before browsing.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
            body.Children.Add(_status);var buttons=new WrapPanel();body.Children.Add(buttons);buttons.Children.Add(_connect);buttons.Children.Add(_remove);
            var check=new Button{Content="Check Tor connection"};buttons.Children.Add(check);check.Click+=(s,e)=>open("https://check.torproject.org/");
            _connect.Click+=async(s,e)=>await Change(true);_remove.Click+=async(s,e)=>await Change(false);
            _timer.Tick+=(s,e)=>Update();Loaded+=(s,e)=>{_timer.Start();Update();};Closed+=(s,e)=>{_closed=true;_timer.Stop();};Update();
        }
        private void Update(){if(_closed)return;_status.Text=_service.Status;_connect.IsEnabled=!_busy&&!_service.Enabled;_remove.IsEnabled=_service.Enabled;}
        private async Task Change(bool enabled)
        {
            if(_busy&&enabled)return;_busy=true;Update();
            try{await _change(enabled);}catch(Exception ex){if(!_closed)_status.Text=ex.Message;}
            finally{_busy=false;Update();}
        }
    }
}
