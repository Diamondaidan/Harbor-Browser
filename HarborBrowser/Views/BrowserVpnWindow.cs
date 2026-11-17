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
        private readonly TextBlock _status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,6)};
        private readonly TextBlock _lastCheck=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        private readonly Button _connect=new Button{Content="Connect with Tor"},_remove=new Button{Content="Disconnect and restore"},_check=new Button{Content="Check connection now"};
        private readonly BrowserVpnService _service;
        private readonly Func<bool,Task> _change;
        private readonly Func<Task> _verify;
        private readonly DispatcherTimer _timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
        private bool _closed,_disconnecting;
        private int _busy;
        private string _operationError;
        public BrowserVpnWindow(BrowserVpnService service,Func<bool,Task> change,Action<string> open,Func<Task> verify=null)
        {
            _service=service;_change=change;_verify=verify;Title="Free browser connection · Tor preview";Width=560;Height=520;MinWidth=360;MinHeight=320;WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(StyleProperty,typeof(Window));WindowFitService.Attach(this);
            var body=new StackPanel{Margin=new Thickness(16)};Content=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            body.Children.Add(new TextBlock{Text="Free Tor connection",FontSize=22,FontWeight=FontWeights.SemiBold});
            body.Children.Add(new TextBlock{Text="Routes Harbor’s normal tabs through the free Tor network. No account is needed. Other apps keep their regular connection. Tor can be slower and websites may block it.\n\nThis is not Tor Browser or a conventional VPN. Private tabs and online typing suggestions pause while enabled. Connect before browsing.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
            body.Children.Add(_status);body.Children.Add(_lastCheck);
            var buttons=new WrapPanel();body.Children.Add(buttons);buttons.Children.Add(_connect);buttons.Children.Add(_remove);buttons.Children.Add(_check);
            var page=new Button{Content="Open Tor verification page"};buttons.Children.Add(page);page.Click+=(s,e)=>open("https://check.torproject.org/");
            body.Children.Add(new TextBlock{Text="While enabled, Harbor checks its route through the Tor Project at connection time, every five minutes, and after network/wake changes. Checks do not include your typed searches. A failed check pauses new page requests; existing pages may need a reload after recovery.\n\nThese checks are not a complete traffic-leak or anonymity audit.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)});
            _connect.Click+=async(s,e)=>await Change(true);_remove.Click+=async(s,e)=>await Change(false);
            _check.Click+=async(s,e)=>{if(_busy>0||_verify==null)return;_busy++;_operationError=null;Update();try{await _verify();}catch(Exception ex){_operationError=ex.Message;}finally{_busy--;Update();}};
            _timer.Tick+=(s,e)=>Update();Loaded+=(s,e)=>{_timer.Start();Update();};Closed+=(s,e)=>{_closed=true;_timer.Stop();};Update();
        }
        private void Update()
        {
            if(_closed)return;_status.Text=_operationError??_service.Status;
            _lastCheck.Text=_service.LastVerifiedUtc.HasValue?"Last successful route check: "+_service.LastVerifiedUtc.Value.ToLocalTime().ToString("t"):"No successful route check yet.";
            _connect.Content=_service.Enabled?"Reconnect with Tor":"Connect with Tor";
            _connect.IsEnabled=_busy==0;_remove.IsEnabled=_service.Enabled&&!_disconnecting;
            _check.IsEnabled=_busy==0&&_service.Enabled&&_service.HelperProcessId!=0&&_verify!=null;
        }
        private async Task Change(bool enabled)
        {
            if(enabled&&_busy>0||!enabled&&_disconnecting)return;
            _busy++;if(!enabled)_disconnecting=true;_operationError=null;Update();
            try{await _change(enabled);}catch(Exception ex){if(!_closed)_operationError=ex.Message;}
            finally{_busy--;if(!enabled)_disconnecting=false;Update();}
        }
    }
}
