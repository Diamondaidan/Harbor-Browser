using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Views;
using HarborBrowser.Services;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private BrowserTab _protonPopupStandby;
        private Border _protonPopupStandbyHost;
        private Task _protonPopupPreparation;
        private int _protonPopupGeneration;

        internal async Task WarmProtonPopupAsync(string profileName)
        {
            if(_shuttingDown||_torWindowClosed)return;
            if(_protonPopupStandby!=null&&_protonPopupStandby.Initialized&&_protonPopupGeneration==_browserGeneration)return;
            if(_protonPopupPreparation==null)_protonPopupPreparation=PrepareProtonPopup(profileName);
            var preparing=_protonPopupPreparation;
            try{await preparing;}
            catch{if(ReferenceEquals(_protonPopupPreparation,preparing))ResetProtonPopupStandby();throw;}
            finally{if(ReferenceEquals(_protonPopupPreparation,preparing))_protonPopupPreparation=null;}
        }
        private async Task PrepareProtonPopup(string profileName)
        {
            var tab=CreateNewTab(null,false,false,null,true,profileName,true);
            BrowserTabs.Items.Remove(tab.Item);tab.Item.Visibility=Visibility.Collapsed;
            _protonPopupStandby=tab;_protonPopupGeneration=_browserGeneration;
            if(_protonPopupStandbyHost==null)
            {
                _protonPopupStandbyHost=new Border{Width=0,Height=0,Visibility=Visibility.Hidden,IsHitTestVisible=false};
                DockPanel.SetDock(_protonPopupStandbyHost,Dock.Top);
                ((Panel)BrowserContentHost.Parent).Children.Insert(0,_protonPopupStandbyHost);
            }
            _protonPopupStandbyHost.Child=tab.View;
            await EnsureTabInitializedAsync(tab);
            if(tab.IsClosing||!tab.Initialized||tab.FailureMessage!=null||_protonPopupGeneration!=_browserGeneration)
                throw new InvalidOperationException("The sign-in window could not be prepared. Reload Proton controls and retry.");
        }
        private BrowserTab TakeProtonPopupStandby(string profileName,bool provider)
        {
            var tab=_protonPopupStandby;
            if(tab==null||!tab.Initialized||tab.IsClosing||_protonPopupGeneration!=_browserGeneration||tab.View.CoreWebView2.Profile.ProfileName!=profileName)return null;
            _protonPopupStandby=null;_protonPopupPreparation=null;
            _protonPopupStandbyHost.Child=null;
            tab.IsProviderAuthentication=provider;tab.Item.Visibility=Visibility.Visible;
            int index=TabOrderService.NewTabIndex(BrowserTabs.Items.Cast<TabItem>().Select(i=>((BrowserTab)i.Tag).IsPinned).ToList(),false);
            BrowserTabs.Items.Insert(index,tab.Item);ApplyTabGroupFilter();BrowserTabs.SelectedItem=tab.Item;
            return tab;
        }
        private void ResetProtonPopupStandby()
        {
            var tab=_protonPopupStandby;_protonPopupStandby=null;_protonPopupPreparation=null;
            if(_protonPopupStandbyHost!=null)_protonPopupStandbyHost.Child=null;
            if(tab==null)return;
            tab.IsClosing=true;CancelTabStartup(tab);tab.SuspendTimer?.Stop();
            try{tab.View.Dispose();}catch(Exception){}
        }
    }
}
