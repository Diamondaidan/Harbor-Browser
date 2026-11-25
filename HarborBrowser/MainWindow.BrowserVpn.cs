using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;
using System.Windows.Interop;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private BrowserVpnService _browserVpn;
        private Task _browserVpnPreparation;
        private int _browserVpnGeneration=-1;
        private BrowserVpnWindow _browserVpnWindow;
        private bool AllowOnlineSuggestions {get{return _settings.OnlineSuggestionsEnabled&&!(_browserVpn?.Enabled??false);}}
        private Task PrepareBrowserVpn(CoreWebView2Profile profile)
        {
            if(profile.IsInPrivateModeEnabled)return Task.CompletedTask;
            if(_browserVpnGeneration!=_browserGeneration||_browserVpnPreparation==null)
            {_browserVpnGeneration=_browserGeneration;_browserVpnPreparation=_browserVpn.Prepare(profile,_environment,new WindowInteropHelper(this).Handle);}
            return _browserVpnPreparation;
        }
        internal async Task ChangeBrowserVpn(bool enabled)
        {
            if(_downloads.Any(d=>d.IsActive))throw new InvalidOperationException("Finish or cancel active downloads before changing browser routing.");
            if(enabled&&BrowserTabs.Items.Cast<TabItem>().Any(i=>(i.Tag as BrowserTab)?.IsPrivate==true))throw new InvalidOperationException("Close private tabs before enabling this preview. Private tabs are unavailable while it is enabled.");
            CancelOnlineSuggestions();foreach(var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab))CancelHomeSuggestions(tab);
            try{await WithProfile(null,profile=>enabled?_browserVpn.Enable(profile,_environment,new WindowInteropHelper(this).Handle):_browserVpn.Remove(profile));}
            finally{_browserVpnPreparation=null;}
            SetStatus(enabled?"Tor connected · normal Harbor tabs only.":"Tor disconnected. Regular browser connection restored.");
        }
        private void BrowserVpn_Click(object sender,RoutedEventArgs e)
        {
            if(_browserVpnWindow!=null){_browserVpnWindow.Activate();return;}
            _browserVpnWindow=new BrowserVpnWindow(_browserVpn,ChangeBrowserVpn,OpenVpnTarget){Owner=this};
            _browserVpnWindow.Closed+=(s,args)=>_browserVpnWindow=null;_browserVpnWindow.Show();
        }
        private void OpenVpnTarget(string target)
        {
            Uri uri;
            if(Uri.TryCreate(target,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http"))CreateNewTab(target,true);
        }
    }
}

