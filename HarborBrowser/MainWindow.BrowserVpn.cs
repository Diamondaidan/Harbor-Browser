using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private BrowserVpnService _browserVpn;
        private Task _browserVpnPreparation;
        private int _browserVpnGeneration=-1;
        private DispatcherTimer _torMonitor,_torDebounce;
        private bool _torWindowClosed,_torHealthInFlight;
        private int _torChanges;
        private bool AllowOnlineSuggestions {get{return _settings.OnlineSuggestionsEnabled&&!(_browserVpn?.Enabled??false)&&!(_protonPreview?.Enabled??false)&&!(ProtonProtection?.Enabled??false);}}
        private Task PrepareBrowserVpn(CoreWebView2Profile profile)
        {
            if(profile.IsInPrivateModeEnabled)return Task.CompletedTask;
            if(_browserVpnGeneration!=_browserGeneration||_browserVpnPreparation==null)
                {_browserVpnGeneration=_browserGeneration;_browserVpnPreparation=PrepareProtonForBrowsing(profile);}
            return _browserVpnPreparation;
        }
        private async Task PrepareProtonForBrowsing(CoreWebView2Profile profile)
        {
            try{await _protonFallback.Prepare(_environment,profile,new WindowInteropHelper(this).Handle,_protonPreview.Enabled||ProtonProtection.Enabled);}
            catch{if(_protonFallback.Armed&&!ProtonProtection.Enabled)BeginProtonProtection(true);throw;}
            if(_protonPreview.Enabled)await EnsureProtonProtectionObserverAsync();
            await _protonPreview.Prepare(profile);
        }
        private void InitializeBrowserVpnMonitoring()
        {
            _browserVpn.NotifyConnectionChange(NetworkInterface.GetIsNetworkAvailable(),"Network unavailable.");
            _browserVpn.StatusChanged+=TorStatusChanged;
            _torMonitor=new DispatcherTimer{Interval=TimeSpan.FromMinutes(5)};
            _torMonitor.Tick+=async(s,e)=>{if(_browserVpn.Enabled&&_torChanges==0&&!_torWindowClosed&&!_shuttingDown)await CheckBrowserVpnHealth();};
            _torDebounce=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
            _torDebounce.Tick+=async(s,e)=>{_torDebounce.Stop();await RecheckTorAfterChange(NetworkInterface.GetIsNetworkAvailable(),"Network or wake change detected.");};
            NetworkChange.NetworkAvailabilityChanged+=TorNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged+=TorNetworkAddressChanged;
            try{SystemEvents.PowerModeChanged+=TorPowerModeChanged;}catch(InvalidOperationException){}
            Loaded+=(s,e)=>{UpdateTorIndicator();_torMonitor.Start();};
            Closed+=(s,e)=>{
                _torWindowClosed=true;_torMonitor.Stop();_torDebounce.Stop();
                _browserVpn.StatusChanged-=TorStatusChanged;
                NetworkChange.NetworkAvailabilityChanged-=TorNetworkAvailabilityChanged;
                NetworkChange.NetworkAddressChanged-=TorNetworkAddressChanged;
                try{SystemEvents.PowerModeChanged-=TorPowerModeChanged;}catch(InvalidOperationException){}
            };
            UpdateTorIndicator();
        }
        private void TorStatusChanged(object sender,EventArgs args)
        {
            if(_torWindowClosed||Dispatcher.HasShutdownStarted)return;
            try{Dispatcher.BeginInvoke(new Action(()=>{
                if(_torWindowClosed)return;UpdateTorIndicator();if(_shuttingDown)return;
                if(_browserVpn.Enabled&&_browserVpn.RouteVerified){
                    var tab=SelectedTab;
                    if(tab!=null&&!tab.IsPrivate&&tab.Initialized&&!tab.IsClosing&&!tab.NeedsNewView&&tab.TorBlockedUrl!=null){var target=tab.TorBlockedUrl;tab.TorBlockedUrl=null;tab.View.CoreWebView2.Navigate(target);}
                }
            }));}catch(InvalidOperationException){}
        }
        private void UpdateTorIndicator()
        {
            if(_torWindowClosed||TorIndicatorButton==null)return;
            if(_protonPreview!=null){UpdateProtonIndicator();return;}
            string label=!_browserVpn.Enabled?"Tor Off":_browserVpn.State==BrowserConnectionState.Connected&&_browserVpn.RouteVerified?"Tor On":_browserVpn.State==BrowserConnectionState.Failed?"Tor !":_browserVpn.State==BrowserConnectionState.Checking?"Tor ?":"Tor …";
            TorIndicatorButton.Content=label;
            TorIndicatorButton.ToolTip=_browserVpn.Status+(_browserVpn.LastVerifiedUtc.HasValue?"\nLast successful check: "+_browserVpn.LastVerifiedUtc.Value.ToLocalTime().ToString("t"):"");
            AutomationProperties.SetName(TorIndicatorButton,"Tor connection: "+_browserVpn.State);
            TorIndicatorButton.SetResourceReference(Control.ForegroundProperty,_browserVpn.Enabled?"HarborAccent":"HarborMuted");
        }
        private void QueueTorEnvironmentChange(bool available,string reason)
        {
            if(_torWindowClosed||Dispatcher.HasShutdownStarted)return;
            try{Dispatcher.BeginInvoke(new Action(()=>{
                if(_torWindowClosed||_shuttingDown)return;_torDebounce.Stop();
                if (ProtonProtection != null && ProtonProtection.Enabled)
                    InvalidateProtonProtection(available ? "The network changed. Browsing is paused until Proton is verified again." : "The network is unavailable. Browsing is paused.", available);
                _browserVpn.NotifyConnectionChange(available,reason);
                if(available&&_browserVpn.Enabled)_torDebounce.Start();
            }));}catch(InvalidOperationException){}
        }
        private void TorNetworkAvailabilityChanged(object sender,NetworkAvailabilityEventArgs args){QueueTorEnvironmentChange(args.IsAvailable,args.IsAvailable?"Network returned.":"Network unavailable.");}
        private void TorNetworkAddressChanged(object sender,EventArgs args){QueueTorEnvironmentChange(NetworkInterface.GetIsNetworkAvailable(),"Network address changed.");}
        private void TorPowerModeChanged(object sender,PowerModeChangedEventArgs args)
        {
            if(args.Mode==PowerModes.Suspend)QueueTorEnvironmentChange(false,"Computer suspended.");
            else if(args.Mode==PowerModes.Resume)QueueTorEnvironmentChange(NetworkInterface.GetIsNetworkAvailable(),"Computer resumed.");
        }
        internal async Task RecheckTorAfterChange(bool available,string reason)
        {
            if(_torWindowClosed||_shuttingDown)return;_browserVpn.NotifyConnectionChange(available,reason);
            if(!available||!_browserVpn.Enabled||_torChanges!=0)return;
            try{
                if(_browserVpn.HelperProcessId==0||!_browserVpn.Bootstrapped)await ChangeBrowserVpn(true);
                else await CheckBrowserVpnHealth();
            }catch(Exception ex){if(!_torWindowClosed)SetStatus("Tor recovery needs attention. "+ex.Message);}
        }
        internal async Task<bool> CheckBrowserVpnHealth()
        {
            if(_torWindowClosed||_shuttingDown||!_browserVpn.Enabled||_torChanges!=0)return false;
            if(_torHealthInFlight)return _browserVpn.RouteVerified;
            _torHealthInFlight=true;bool verified=false;
            try{await WithProfile(null,async profile=>verified=await _browserVpn.CheckHealth(profile,_environment,new WindowInteropHelper(this).Handle));return verified;}
            catch(Exception ex){if(!_torWindowClosed)SetStatus("Tor check did not finish. "+ex.Message);return false;}
            finally{_torHealthInFlight=false;if(!_torWindowClosed)UpdateTorIndicator();}
        }
        internal bool TorBlocksRequest(string target)
        {
            if(!(_browserVpn?.Enabled??false)||_browserVpn.RouteVerified)return false;
            Uri uri;if(!Uri.TryCreate(target,UriKind.Absolute,out uri))return false;
            return (uri.Scheme=="http"||uri.Scheme=="https")&&uri.Host!=HomePageService.Host;
        }
        private void ConfigureTorRequestGuard(BrowserTab tab,CoreWebView2 core)
        {
            if(tab.IsPrivate)return;
            int generation=_browserGeneration;var view=tab.View;var environment=_environment;
            // Worker requests use the shared protection callback registered
            // on each normal browsing context.
            core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All,CoreWebView2WebResourceRequestSourceKinds.Document);
            core.WebResourceRequested+=(s,e)=>{
                if(tab.IsClosing||generation!=_browserGeneration||!ReferenceEquals(view,tab.View))return;
                if(TorBlocksRequest(e.Request.Uri))e.Response=environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("Tor is not verified. Check or reconnect Tor, or disconnect to use the regular route.")),503,"Tor connection paused","Content-Type: text/plain; charset=utf-8");
            };
        }
        internal async Task ChangeBrowserVpn(bool enabled)
        {
            if(enabled)CancelUpdateTraffic();
            if(enabled&&_torChanges>0)throw new InvalidOperationException("Wait for the current connection change to finish.");
            if(_downloads.Any(d=>d.IsActive))throw new InvalidOperationException("Finish or cancel active downloads before changing browser routing.");
            if(enabled&&BrowserTabs.Items.Cast<TabItem>().Any(i=>(i.Tag as BrowserTab)?.IsPrivate==true))throw new InvalidOperationException("Close private tabs before enabling Tor. Private tabs are unavailable while it is enabled.");
            if(enabled)_browserVpn.NotifyConnectionChange(NetworkInterface.GetIsNetworkAvailable(),"Checking the current network.");
            _torChanges++;CancelOnlineSuggestions();foreach(var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab))CancelHomeSuggestions(tab);
            try{await WithProfile(null,profile=>enabled?_browserVpn.Enable(profile,_environment,new WindowInteropHelper(this).Handle):_browserVpn.Remove(profile));}
            finally{_torChanges--;_browserVpnPreparation=null;if(!_torWindowClosed)UpdateTorIndicator();}
            SetStatus(enabled?"Tor route verified · normal Harbor tabs only.":"Tor disconnected. Regular browser connection restored.");
        }
        private void BrowserVpn_Click(object sender,RoutedEventArgs e)
        {
            OpenProtonPreview();
        }
        private void OpenVpnTarget(string target)
        {if(Uri.TryCreate(target,UriKind.Absolute,out var uri)&&(uri.Scheme=="https"||uri.Scheme=="http"))CreateNewTab(target,true);}
    }
}
