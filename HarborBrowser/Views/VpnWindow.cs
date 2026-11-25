using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    public sealed class VpnWindow:Window
    {
        public readonly ListBox ProfileList=new ListBox{DisplayMemberPath="Display",Height=140};
        public readonly TextBlock Summary=new TextBlock{TextWrapping=TextWrapping.Wrap},Details=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)};
        public readonly Button ConnectButton=new Button{Content="Connect"},DisconnectButton=new Button{Content="Disconnect selected"};
        private readonly Button _refresh=new Button{Content="Refresh"};
        private readonly VpnClientService _service;
        private readonly DispatcherTimer _timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};
        private bool _busy,_refreshing,_closed;
        public VpnWindow(VpnClientService service=null)
        {
            _service=service??new VpnClientService();Title="Windows VPN connections";Width=690;Height=540;MinWidth=360;MinHeight=300;WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(StyleProperty,typeof(Window));WindowFitService.Attach(this);
            var root=new Grid{Margin=new Thickness(14)};Content=root;root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var body=new StackPanel();root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
            body.Children.Add(new TextBlock{Text="Windows VPN",FontSize=23,FontWeight=FontWeights.SemiBold});body.Children.Add(new TextBlock{Text="Use an existing Windows VPN profile. These connections affect Windows networking, including other apps. Harbor does not provide a VPN service; profiles from separate VPN apps may not appear here.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,12)});
            body.Children.Add(ProfileList);body.Children.Add(Details);body.Children.Add(Summary);
            var buttons=new WrapPanel();Grid.SetRow(buttons,1);root.Children.Add(buttons);buttons.Children.Add(ConnectButton);buttons.Children.Add(DisconnectButton);buttons.Children.Add(_refresh);
            var settings=new Button{Content="Add / edit profiles in Windows…"};buttons.Children.Add(settings);settings.Click+=(s,e)=>{try{VpnService.OpenWindowsVpnSettings();}catch(Exception ex){Summary.Text="Open Windows Settings > Network & Internet > VPN. "+ex.Message;}};
            ConnectButton.Click+=async(s,e)=>await Change(true);DisconnectButton.Click+=async(s,e)=>await Change(false);_refresh.Click+=async(s,e)=>await Refresh();ProfileList.SelectionChanged+=(s,e)=>UpdateSelection();
            _timer.Tick+=async(s,e)=>{if(!_busy)await Refresh();};Loaded+=async(s,e)=>{await Refresh();if(!_closed)_timer.Start();};Closed+=(s,e)=>{_closed=true;_timer.Stop();};UpdateSelection();
        }
        private void UpdateSelection()
        {
            var p=ProfileList.SelectedItem as VpnProfile;ConnectButton.IsEnabled=!_busy&&!_refreshing&&p!=null&&p.CanConnect;DisconnectButton.IsEnabled=!_busy&&!_refreshing&&p!=null&&p.CanDisconnect;_refresh.IsEnabled=!_busy&&!_refreshing;
            Details.Text=p==null?"Choose a profile to see its Windows status.":p.Name+"\n"+p.Status+" · "+(p.AllUsers?"All users":"Current user")+" · "+p.Tunnel+(p.SplitTunnel?"\nSplit tunneling is enabled; some traffic can bypass this VPN.":"");
        }
        public async Task Refresh()
        {
            if(_refreshing||_closed)return;_refreshing=true;UpdateSelection();
            try
            {
                string key=(ProfileList.SelectedItem as VpnProfile)?.Key;var snapshot=await _service.Snapshot();if(_closed)return;
                ProfileList.ItemsSource=snapshot.Profiles;ProfileList.SelectedItem=snapshot.Profiles.FirstOrDefault(p=>p.Key==key)??snapshot.Profiles.FirstOrDefault();
                Summary.Text=snapshot.Errors.Count>0?string.Join("\n",snapshot.Errors):snapshot.Profiles.Count==0?"No Windows VPN profiles found. Add one in Windows settings.":"Windows status checked at "+DateTime.Now.ToShortTimeString()+". Refreshes every 5 seconds while this window is open.";
            }
            finally{_refreshing=false;if(!_closed)UpdateSelection();}
        }
        private async Task Change(bool connect)
        {
            var profile=ProfileList.SelectedItem as VpnProfile;if(profile==null||_busy)return;_busy=true;UpdateSelection();Summary.Text=(connect?"Connecting ":"Disconnecting ")+profile.Name+"…";
            try{string result=await _service.Change(profile,connect);if(_closed)return;await Refresh();if(!_closed)Summary.Text=result;}
            finally{_busy=false;if(!_closed)UpdateSelection();}
        }
    }
}
