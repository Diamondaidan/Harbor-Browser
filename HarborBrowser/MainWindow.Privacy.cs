using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private SitePermissionService _normalPermissions;
        private readonly Dictionary<string,SitePermissionService> _privatePermissions=new Dictionary<string,SitePermissionService>();
        private readonly Dictionary<string,Task> _permissionResetTasks=new Dictionary<string,Task>();
        private readonly List<Task> _privateCleanupTasks=new List<Task>();
        private readonly HashSet<string> _privateCleanupFailures=new HashSet<string>();
        private readonly HashSet<string> _initializedPrivateSessions=new HashSet<string>();
        private string _currentPrivateSession;
        private readonly Queue<PendingPermission> _permissionQueue=new Queue<PendingPermission>();
        private PendingPermission _activePermission;
        private DispatcherTimer _permissionTimer;
        private bool _shuttingDown;
        internal Func<bool> ConfirmPrivateDownloadClose;
        internal Func<bool> ConfirmPrivateDownload;
        internal string TestBrowserArguments;
        private int _browserGeneration;
        private TaskCompletionSource<bool> _environmentExited;
        private Task _pendingBrowserExit;
        private sealed class PendingPermission
        {
            public BrowserTab Tab;public string Origin,Top,Kind;public int Navigation;
            public CoreWebView2PermissionRequestedEventArgs Args;public CoreWebView2Deferral Deferral;public bool Completed;
        }
        private void InitializePrivacy()
        {
            _browserVpn=new BrowserVpnService(_store.RootFolder);
            Closed+=(s,e)=>_browserVpn.Dispose();
            _normalPermissions=new SitePermissionService(_store.LoadPermissions());
            ConfirmPrivateDownload=()=>MessageBox.Show(this,"Private browsing does not hide downloaded files from this computer. Continue downloading?","Private download",MessageBoxButton.YesNo,MessageBoxImage.Information)==MessageBoxResult.Yes;
            ConfirmPrivateDownloadClose=()=>MessageBox.Show(this,"Closing the last private tab cancels unfinished private downloads. Close and cancel them?","Private downloads",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
        }
        private void ConfigurePrivateView(BrowserTab tab)
        {
            if(!tab.IsPrivate)return;
            if(tab.PrivateSessionId==null)
            {
                if(_currentPrivateSession==null)_currentPrivateSession="p"+Guid.NewGuid().ToString("N");
                tab.PrivateSessionId=_currentPrivateSession;
            }
            if(!_privatePermissions.ContainsKey(tab.PrivateSessionId))_privatePermissions[tab.PrivateSessionId]=new SitePermissionService();
            tab.View.CreationProperties=new CoreWebView2CreationProperties{IsInPrivateModeEnabled=true,ProfileName=tab.PrivateSessionId};
        }
        private async Task EnsureEnvironmentAsync()
        {
            if(_pendingBrowserExit!=null)
            {
                var pending=_pendingBrowserExit;
                if(await Task.WhenAny(pending,Task.Delay(10000))!=pending)throw new InvalidOperationException("The previous browser engine is still shutting down. Try reloading again shortly.");
                if(_pendingBrowserExit==pending)_pendingBrowserExit=null;
            }
            if(_environment!=null)return;
            if(_environmentTask==null)
            {
                string folder=System.IO.Path.Combine(_store.RootFolder,"WebView2");System.IO.Directory.CreateDirectory(folder);
                var options=new CoreWebView2EnvironmentOptions{EnableTrackingPrevention=true,AreBrowserExtensionsEnabled=true};
                if(!string.IsNullOrEmpty(TestBrowserArguments))options.AdditionalBrowserArguments=TestBrowserArguments;
                _environmentTask=CoreWebView2Environment.CreateAsync(null,folder,options);
            }
            var creation=_environmentTask;var environment=await creation;
            if(_environmentTask!=creation)return;
            if(_environment==null)
            {
                _environment=environment;
                var exited=new TaskCompletionSource<bool>();_environmentExited=exited;
                environment.BrowserProcessExited+=(s,e)=>exited.TrySetResult(true);
            }
        }
        private Task InitializePermissionProfile(CoreWebView2Profile profile)
        {
            string key=profile.ProfileName+"|"+profile.IsInPrivateModeEnabled;Task task;
            if(!_permissionResetTasks.TryGetValue(key,out task)){task=ResetNativePermissions(profile);_permissionResetTasks[key]=task;}return task;
        }
        private static async Task ResetNativePermissions(CoreWebView2Profile profile)
        {
            var saved=await profile.GetNonDefaultPermissionSettingsAsync();
            foreach(var setting in saved)await profile.SetPermissionStateAsync(setting.PermissionKind,setting.PermissionOrigin,CoreWebView2PermissionState.Default);
        }
        private SitePermissionService PermissionPolicy(BrowserTab tab)
        {
            SitePermissionService policy;if(!tab.IsPrivate)return _normalPermissions;
            return tab.PrivateSessionId!=null&&_privatePermissions.TryGetValue(tab.PrivateSessionId,out policy)?policy:null;
        }
        private void CorePermissionRequested(BrowserTab tab,CoreWebView2PermissionRequestedEventArgs args)
        {
            args.SavesInProfile=false;args.Handled=true;args.State=CoreWebView2PermissionState.Deny;
            string origin=SitePermissionService.Origin(args.Uri),top=SitePermissionService.Origin(tab.LastKnownUrl??(tab.View.Source==null?null:tab.View.Source.AbsoluteUri)),kind=args.PermissionKind.ToString();
            var policy=PermissionPolicy(tab);
            if(_shuttingDown||tab.IsClosing||SelectedTab!=tab||origin==null||top==null||policy==null||!SitePermissionService.Supported(kind))return;
            string decision=policy.Get(origin,top,kind);
            if(decision=="Allow"){args.State=CoreWebView2PermissionState.Allow;return;}if(decision=="Block")return;
            if(_permissionQueue.Count>=4)return;
            var pending=new PendingPermission{Tab=tab,Origin=origin,Top=top,Kind=kind,Navigation=tab.NavigationGeneration,Args=args,Deferral=args.GetDeferral()};
            _permissionQueue.Enqueue(pending);Dispatcher.BeginInvoke(new Action(async()=>await ShowNextPermission()));
        }
        private bool ValidPermission(PendingPermission request)
        {return !_shuttingDown&&!request.Completed&&!request.Tab.IsClosing&&SelectedTab==request.Tab&&request.Navigation==request.Tab.NavigationGeneration&&SitePermissionService.Origin(request.Tab.LastKnownUrl)==request.Top&&PermissionPolicy(request.Tab)!=null;}
        private async Task ShowNextPermission()
        {
            if(_activePermission!=null)return;
            while(_permissionQueue.Count>0)
            {
                var request=_permissionQueue.Dequeue();if(!ValidPermission(request)){CompletePermission(request,false);continue;}
                _activePermission=request;
                if(_presentation!=null&&_presentation.IsFullScreen){await ExitVideoFullScreen();_presentation.SetBrowserFullScreen(false);}
                if(_activePermission!=request||!ValidPermission(request)){if(_activePermission==request)_activePermission=null;CompletePermission(request,false);continue;}
                PermissionRequestText.Text=request.Origin+" wants "+SitePermissionService.Label(request.Kind).ToLowerInvariant()+" access.";
                if(request.Top!=request.Origin)PermissionRequestText.Text+=" Opened in "+request.Top+".";
                PermissionRequestText.ToolTip=PermissionRequestText.Text;
                RememberPermissionBox.Content=request.Tab.IsPrivate?"Remember for this private session":"Remember for this site";RememberPermissionBox.IsChecked=false;RememberPermissionBox.IsEnabled=request.Tab.IsPrivate||!_store.IsReadOnlyForNewerDataVersion;
                PermissionRequestBar.Visibility=Visibility.Visible;
                _permissionTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(60)};_permissionTimer.Tick+=(s,e)=>ChoosePermission(false,false);_permissionTimer.Start();return;
            }
        }
        private void PermissionAllow_Click(object sender,RoutedEventArgs e){ChoosePermission(true,RememberPermissionBox.IsChecked==true);}
        private void PermissionBlock_Click(object sender,RoutedEventArgs e){ChoosePermission(false,RememberPermissionBox.IsChecked==true);}
        internal void ChoosePermission(bool allow,bool remember)
        {
            var request=_activePermission;if(request==null)return;bool valid=ValidPermission(request);
            if(valid&&remember)
            {
                var policy=PermissionPolicy(request.Tab);var before=policy.Snapshot();
                try{policy.Set(request.Origin,request.Top,request.Kind,allow?"Allow":"Block",DateTime.UtcNow);if(!request.Tab.IsPrivate)_store.SavePermissions(policy.Snapshot());}
                catch(Exception ex){if(!request.Tab.IsPrivate)_normalPermissions=new SitePermissionService(before);valid=false;SetStatus("Permission could not be saved; access was blocked. "+ex.Message);}
            }
            _activePermission=null;if(_permissionTimer!=null)_permissionTimer.Stop();PermissionRequestBar.Visibility=Visibility.Collapsed;CompletePermission(request,valid&&allow);Dispatcher.BeginInvoke(new Action(async()=>await ShowNextPermission()));
        }
        private static void CompletePermission(PendingPermission request,bool allow)
        {
            if(request.Completed)return;request.Completed=true;try{request.Args.State=allow?CoreWebView2PermissionState.Allow:CoreWebView2PermissionState.Deny;}catch{}finally{try{request.Deferral.Complete();}catch{}}
        }
        private void CancelPermissions(BrowserTab tab=null)
        {
            if(_activePermission!=null&&(tab==null||ReferenceEquals(_activePermission.Tab,tab))){var request=_activePermission;_activePermission=null;if(_permissionTimer!=null)_permissionTimer.Stop();PermissionRequestBar.Visibility=Visibility.Collapsed;CompletePermission(request,false);}
            var keep=_permissionQueue.Where(p=>tab!=null&&!ReferenceEquals(p.Tab,tab)).ToList();foreach(var request in _permissionQueue.Where(p=>tab==null||ReferenceEquals(p.Tab,tab)))CompletePermission(request,false);_permissionQueue.Clear();foreach(var request in keep)_permissionQueue.Enqueue(request);
            if(!_shuttingDown)Dispatcher.BeginInvoke(new Action(async()=>await ShowNextPermission()));
        }
        private async Task WithProfile(string privateSession,Func<CoreWebView2Profile,Task> action)
        {
            var source=BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Concat(_retainedDownloadTabs).FirstOrDefault(t=>t!=null&&t.Initialized&&!t.NeedsNewView&&((privateSession==null&&!t.IsPrivate)||(privateSession!=null&&t.IsPrivate&&t.PrivateSessionId==privateSession)));
            if(source!=null){await action(source.View.CoreWebView2.Profile);return;}
            await EnsureEnvironmentAsync();var options=_environment.CreateCoreWebView2ControllerOptions();options.ProfileName=privateSession??"Default";options.IsInPrivateModeEnabled=privateSession!=null;
            var creation=_environment.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(this).Handle,options);
            try{await StartupStep(creation,System.Threading.CancellationToken.None,BrowserStartupTimeout,"profile access");}
            catch{_ = CloseLateProfileController(creation);throw;}
            var controller=await creation;
            try{controller.IsVisible=false;await action(controller.CoreWebView2.Profile);}finally{controller.Close();}
        }
        private static async Task CloseLateProfileController(Task<CoreWebView2Controller> creation)
        {try{(await creation).Close();}catch{}}
        private async void SitePermissions_Click(object sender,RoutedEventArgs e)
        {
            var tab=SelectedTab;string session=tab!=null&&tab.IsPrivate?tab.PrivateSessionId:null;
            var policy=tab!=null?PermissionPolicy(tab):_normalPermissions;if(policy==null)return;
            if(_store.IsReadOnlyForNewerDataVersion&&session==null){SetStatus("Site settings cannot be saved in this read-only profile.");return;}
            var dialog=new SitePermissionsWindow(policy.Snapshot(),tab==null?null:tab.LastKnownUrl,session!=null){Owner=this};
            if(dialog.ShowDialog()!=true)return;
            try{await ApplyPermissionRules(dialog.ResultRules,session);SetStatus("Site permissions updated");}catch(Exception ex){MessageBox.Show(this,"Could not apply permissions. "+ex.Message,"Site permissions");}
        }
        internal async Task ApplyPermissionRules(List<SitePermissionRule> rules,string privateSession=null)
        {
            var next=new SitePermissionService(rules);var old=privateSession==null?_normalPermissions:_privatePermissions[privateSession];
            CancelPermissions();await WithProfile(privateSession,ResetNativePermissions);
            if(privateSession==null){_store.SavePermissions(next.Snapshot());_normalPermissions=next;}else _privatePermissions[privateSession]=next;
            var affected=new HashSet<string>(old.Snapshot().Concat(next.Snapshot()).Select(p=>p.TopLevelOrigin));
            foreach(var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Where(t=>t!=null&&t.Initialized&&((privateSession==null&&!t.IsPrivate)||(t.IsPrivate&&t.PrivateSessionId==privateSession))))
                if(affected.Contains(SitePermissionService.Origin(tab.LastKnownUrl)))try{tab.View.CoreWebView2.Reload();}catch{}
        }
        private async void ClearBrowsingData_Click(object sender,RoutedEventArgs e)
        {
            if(_presentation!=null&&_presentation.IsFullScreen){await ExitVideoFullScreen();_presentation.SetBrowserFullScreen(false);}
            string session=SelectedTab!=null&&SelectedTab.IsPrivate?SelectedTab.PrivateSessionId:null;
            new ClearBrowsingDataWindow(session!=null,request=>ClearBrowsingData(request,session)){Owner=this}.ShowDialog();
        }
        internal async Task ClearBrowsingData(BrowsingDataRequest request,string privateSession=null)
        {
            if(!request.HasSelection)return;if(privateSession==null&&_store.IsReadOnlyForNewerDataVersion)throw new InvalidOperationException("This profile is read-only.");
            if(request.DataKinds!=0)await WithProfile(privateSession,async profile=>{if(request.Range==BrowsingTimeRange.AllTime)await profile.ClearBrowsingDataAsync(request.DataKinds);else await profile.ClearBrowsingDataAsync(request.DataKinds,request.StartUtc,request.EndUtc);});
            if(request.History&&privateSession==null)
            {
                _store.ClearHistoryRange(request);_history.RemoveAll(h=>request.Contains(h.VisitedAt));_recentlyClosed.Clear();
                foreach(var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Where(t=>t!=null&&!t.IsPrivate))tab.LastRecordedUrl=null;
                if(_sessionRecovery!=null&&_sessionRecovery.Ready)_sessionRecovery.Clear();RecoveryBanner.Visibility=Visibility.Collapsed;SaveSession();_store.SanitizeRecoveryCopies();
            }
            if(request.Permissions)
            {
                var policy=privateSession==null?_normalPermissions:_privatePermissions[privateSession];var updated=new SitePermissionService(policy.Snapshot());updated.Clear(request);await ApplyPermissionRules(updated.Snapshot(),privateSession);if(privateSession==null)_store.SanitizePermissionCopies(request);
            }
            CancelOnlineSuggestions();foreach(var tab in BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab))CancelHomeSuggestions(tab);SetStatus("Selected browsing data cleared");
        }
        private bool PreparePrivateClose(BrowserTab tab)
        {
            if(!tab.IsPrivate||BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Any(t=>t!=tab&&t.IsPrivate&&t.PrivateSessionId==tab.PrivateSessionId))return true;
            var active=_downloads.Where(d=>d.IsPrivate&&d.PrivateSessionId==tab.PrivateSessionId&&(d.IsActive||d.CanResume)).ToList();
            if(active.Count>0&&!ConfirmPrivateDownloadClose())return false;
            foreach(var entry in active)CancelDownload(entry);return true;
        }
        private void PrivateTabClosed(BrowserTab tab)
        {
            string session=tab.PrivateSessionId;if(!tab.IsPrivate||session==null||BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Any(t=>t.IsPrivate&&t.PrivateSessionId==session))return;
            if(_currentPrivateSession==session)_currentPrivateSession=null;_privatePermissions.Remove(session);
            foreach(var entry in _downloads.Where(d=>d.IsPrivate&&d.PrivateSessionId==session).ToList()){_resumingDownloads.Remove(entry);_downloadOwners.Remove(entry);_downloads.Remove(entry);}
            foreach(var retained in _retainedDownloadTabs.Where(t=>t.IsPrivate&&t.PrivateSessionId==session).ToList()){_retainedDownloadTabs.Remove(retained);try{retained.View.Dispose();}catch{}}
            var cleanup=CleanupPrivateSession(session);_privateCleanupTasks.Add(cleanup);_ = TrackPrivateCleanup(cleanup);
        }
        private async Task CleanupPrivateSession(string session)
        {
            // A controller that never started cannot hold page data. Recreating
            // that failed controller during close can repeat its startup hang.
            if(!_initializedPrivateSessions.Contains(session)){_permissionResetTasks.Remove(session+"|True");return;}
            try{await WithProfile(session,profile=>StartupStep(profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile),System.Threading.CancellationToken.None,BrowserStartupTimeout,"private cleanup"));_privateCleanupFailures.Remove(session);_initializedPrivateSessions.Remove(session);}
            catch(Exception ex){_privateCleanupFailures.Add(session);SetStatus("Private cleanup could not finish. New private tabs use a separate session. "+ex.Message);}
            finally{_permissionResetTasks.Remove(session+"|True");}
        }
        private async Task TrackPrivateCleanup(Task task){try{await task;}finally{_privateCleanupTasks.Remove(task);}}
        internal async Task WaitForPrivateCleanup(){while(_privateCleanupTasks.Count>0)await Task.WhenAll(_privateCleanupTasks.ToArray());}
    }
}
