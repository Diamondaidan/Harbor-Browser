using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private SessionRecoveryService _sessionRecovery;
        private bool _sessionWritesAllowed,_lastSessionSaveSucceeded;
        private void InitializeRecovery()
        {
            Closed+=(s,e)=>{CancelPermissions();if(_sessionRecovery!=null)_sessionRecovery.Dispose();};
        }
        private bool BeginSessionRun()
        {
            _sessionRecovery=new SessionRecoveryService(_store);
            try{_sessionRecovery.Begin();_sessionWritesAllowed=_sessionRecovery.Ready;return true;}
            catch(Exception ex){MessageBox.Show(this,"Harbor could not open this profile for saving. Another Harbor window may already be using it.\n\n"+ex.Message,"Harbor profile",MessageBoxButton.OK,MessageBoxImage.Information);return false;}
        }
        private void DismissRecovery_Click(object sender,RoutedEventArgs e)
        {
            try{_sessionRecovery.Dismiss();RecoveryBanner.Visibility=Visibility.Collapsed;}catch(Exception ex){SetStatus("Could not dismiss recovery: "+ex.Message);}
        }
        private void RestoreRecovery_Click(object sender,RoutedEventArgs e)
        {
            var snapshot=_sessionRecovery==null?null:_sessionRecovery.Snapshot;var session=snapshot==null?null:snapshot.Session;
            if(session==null||session.Tabs==null||session.Tabs.Count==0){SetStatus("No recovery snapshot is available");return;}
            _restoringSession=true;var tabs=new List<BrowserTab>();
            try
            {
                LoadTabGroups(session);
                foreach(var saved in session.Tabs.Take(500))if(saved!=null&&IsRestorableUrl(saved.Url))tabs.Add(CreateNewTab(saved.Url,false,false,saved));
                _activeGroupFilter="*";ApplyTabGroupFilter();if(tabs.Count>0)BrowserTabs.SelectedItem=tabs[Math.Max(0,Math.Min(session.SelectedTabIndex,tabs.Count-1))].Item;
                _sessionRecovery.Dismiss();RecoveryBanner.Visibility=Visibility.Collapsed;
            }
            catch(Exception ex){SetStatus("Recovery could not finish: "+ex.Message);}
            finally{_restoringSession=false;}
            SaveSession();
        }
        private string SafeTabUrl(BrowserTab tab)
        {
            if(tab.NativeContent is DownloadsPage)return DownloadsUrl;
            if(tab.PendingUrl!=null)return tab.PendingUrl;
            try{if(tab.View.Source!=null)return tab.View.Source.AbsoluteUri;}catch{}
            return tab.LastKnownUrl;
        }
        private void HandleProcessFailure(BrowserTab tab,CoreWebView2ProcessFailedKind kind)
        {
            if(tab.IsClosing||_shuttingDown)return;
            if(kind!=CoreWebView2ProcessFailedKind.BrowserProcessExited&&kind!=CoreWebView2ProcessFailedKind.RenderProcessExited&&kind!=CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
            {SetStatus("Part of a page stopped. Reload the page if needed.");return;}
            CancelPermissions(tab);CancelHomeSuggestions(tab);if(tab.VideoPopout!=null)try{tab.VideoPopout.Close();}catch{}
            if(kind==CoreWebView2ProcessFailedKind.BrowserProcessExited)
            {
                if(tab.NeedsNewView)return;
                _browserGeneration++;_pendingBrowserExit=_environmentExited==null?Task.CompletedTask:_environmentExited.Task;
                foreach(var item in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Where(t=>t!=null&&t.NativeContent==null))
                {
                    CancelPermissions(item);item.PendingUrl=SafeTabUrl(item)??_settings.HomePage;item.NeedsNewView=true;item.Initialized=false;item.IsSuspended=false;item.SleepReason=null;item.FailureMessage="The browser engine stopped. Your tab is saved.";UpdateTabHeader(item);
                    CancelTabStartup(item);
                }
                foreach(var download in _downloads.Where(d=>d.IsActive||d.CanResume)){download.Operation=null;download.IsActive=false;download.CanResume=false;download.Status="Interrupted: browser engine stopped";}
                foreach(var retained in _retainedDownloadTabs.ToList())try{retained.View.Dispose();}catch{}
                _retainedDownloadTabs.Clear();_downloadOwners.Clear();_resumingDownloads.Clear();_permissionResetTasks.Clear();_environment=null;_environmentTask=null;
            }
            else tab.FailureMessage=kind==CoreWebView2ProcessFailedKind.RenderProcessUnresponsive?"This page is not responding.":"This page stopped unexpectedly.";
            SaveSession();if(SelectedTab==tab)ShowTabContent();
        }
        private FrameworkElement BuildTabRecoveryView(BrowserTab tab)
        {
            var panel=new StackPanel{MaxWidth=500,Margin=new Thickness(24),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            panel.Children.Add(new TextBlock{Text=tab.FailureMessage,FontSize=21,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});
            panel.Children.Add(new TextBlock{Text="Reload this tab to continue. Other tabs stay available.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)});
            var reload=new Button{Content="Reload tab",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(16,7,16,7)};reload.Click+=async(s,e)=>await RecoverTab(tab);panel.Children.Add(reload);return panel;
        }
        internal async Task RecoverTab(BrowserTab tab)
        {
            if(tab==null||tab.IsClosing)return;
            try
            {
                tab.FailureMessage=null;
                if(tab.NeedsNewView)
                {
                    CancelTabStartup(tab);
                    try{tab.View.Dispose();}catch{}tab.View=new WebView2();ConfigurePrivateView(tab);tab.Initialized=false;tab.Initializing=false;tab.IsSuspended=false;tab.SuspensionInProgress=false;tab.SleepReason=null;tab.MediaFrames.Clear();tab.ActiveVideoContext=null;tab.TrackerFilterRegistered=false;tab.NeedsNewView=false;
                    if(SelectedTab==tab)ShowTabContent();await EnsureTabInitializedAsync(tab);
                }
                else
                {
                    if(SelectedTab==tab)ShowTabContent();string destination=tab.PendingUrl;tab.PendingUrl=null;
                    if(destination!=null)tab.View.CoreWebView2.Navigate(destination);else tab.View.CoreWebView2.Reload();
                }
            }
            catch(Exception ex){tab.FailureMessage="This tab could not reload. Try again.";if(SelectedTab==tab)ShowTabContent();SetStatus(ex.Message);}
        }
        private async Task FinishPrivacyBeforeClose()
        {
            _torDebounce?.Stop();_browserVpn.PauseVerification();
            foreach(var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Where(t=>t!=null&&t.Initializing))CancelTabStartup(tab);
            CancelPermissions();CancelOnlineSuggestions();foreach(var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab))CancelHomeSuggestions(tab);
            foreach(var entry in _downloads.Where(d=>d.IsPrivate&&(d.IsActive||d.CanResume)).ToList())CancelDownload(entry);
            await WaitForPrivateCleanup();
            foreach(string session in _privatePermissions.Keys.Concat(_privateCleanupFailures).Distinct().ToList())await CleanupPrivateSession(session);
            if(_privateCleanupFailures.Count>0)throw new InvalidOperationException("Some private data could not be cleared. You can keep Harbor open and try again.");
            if(_settings.ClearCookiesOnExit||_settings.ClearCacheOnExit)
            {
                CoreWebView2BrowsingDataKinds kinds=0;if(_settings.ClearCookiesOnExit)kinds|=CoreWebView2BrowsingDataKinds.Cookies|CoreWebView2BrowsingDataKinds.AllDomStorage|CoreWebView2BrowsingDataKinds.ServiceWorkers;if(_settings.ClearCacheOnExit)kinds|=CoreWebView2BrowsingDataKinds.DiskCache;
                await WithProfile(null,profile=>profile.ClearBrowsingDataAsync(kinds));
            }
            _privatePermissions.Clear();
        }
    }
}
