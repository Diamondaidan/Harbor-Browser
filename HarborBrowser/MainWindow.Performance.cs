using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private readonly ProcessCpuSampler _processCpu=new ProcessCpuSampler();
        private IEnumerable<BrowserTab> OpenTabs {get{return BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).Where(t=>t!=null);}}
        private bool HasTabDownload(BrowserTab tab)
        {return _downloadOwners.Any(pair=>ReferenceEquals(pair.Value,tab)&&(pair.Key.IsActive||pair.Key.CanResume));}
        private string SleepProtection(BrowserTab tab)
        {
            if(tab.IsClosing||_shuttingDown)return "Closing";
            if(tab.FailureMessage!=null||tab.NeedsNewView)return "Needs reload";
            if(tab.NativeContent!=null)return "Browser page";
            if(tab.IsPopupPending)return "Opening requested window";
            if(tab.IsProviderAuthentication || ProtonPreviewService.IsProviderPage(tab.PendingUrl) || ProtonPreviewService.IsProviderPage(tab.LastKnownUrl))return "Proton sign-in or controls";
            if(!tab.Initialized)return tab.Initializing?"Starting":"Not loaded";
            if(tab==SelectedTab)return "Active";
            if(tab.KeepAwake)return "Kept awake";
            if(tab.VideoPopout!=null||tab.OpeningPopout)return "Picture in picture";
            if(HasTabDownload(tab))return "Download in progress or paused";
            if((_activePermission!=null&&_activePermission.Tab==tab)||_permissionQueue.Any(p=>p.Tab==tab))return "Permission request";
            try{if(tab.View.CoreWebView2.IsDocumentPlayingAudio)return "Playing audio";}catch{return "Unavailable";}
            return null;
        }
        private void UpdateTabActivity()
        {
            foreach(var tab in OpenTabs)
            {
                if(tab.IsClosing)continue;
                if(tab==SelectedTab)
                {
                    tab.UnloadTimer?.Stop(); tab.RetryUnloadAfterUtc=default(DateTime); tab.UnloadReason=null;
                    tab.InactiveSinceUtc=null;tab.RetrySleepAfterUtc=default(DateTime);
                    if(tab.SuspendTimer!=null)tab.SuspendTimer.Stop();ResumeTabIfNeeded(tab);
                }
                else
                {
                    if(!tab.InactiveSinceUtc.HasValue)tab.InactiveSinceUtc=DateTime.UtcNow;
                    if(!_settings.SuspendInactiveTabs){if(tab.SuspendTimer!=null)tab.SuspendTimer.Stop();ResumeTabIfNeeded(tab);}
                    else if(!tab.IsSuspended)StartTabSuspendTimer(tab);
                    StartTabUnloadTimer(tab);
                }
            }
        }
        private void StartTabSuspendTimer(BrowserTab tab)
        {
            if(tab==null||tab.SuspendTimer==null)return;
            tab.SuspendTimer.Stop();
            if(!_settings.SuspendInactiveTabs||tab.IsClosing||tab==SelectedTab||!tab.Initialized||tab.NativeContent!=null||tab.KeepAwake||tab.IsSuspended||tab.FailureMessage!=null||_shuttingDown||tab.IsProviderAuthentication||ProtonPreviewService.IsProviderPage(tab.PendingUrl)||ProtonPreviewService.IsProviderPage(tab.LastKnownUrl))return;
            if(!tab.InactiveSinceUtc.HasValue)tab.InactiveSinceUtc=DateTime.UtcNow;
            tab.SuspendTimer.Interval=TabSleepSchedule.Delay(tab.InactiveSinceUtc.Value,tab.RetrySleepAfterUtc,DateTime.UtcNow,_settings.InactiveTabSuspendDelaySeconds);
            tab.SuspendTimer.Start();
        }
        private void ResumeTabIfNeeded(BrowserTab tab)
        {
            if(tab==null||!tab.Initialized||tab.IsClosing||tab.NeedsNewView)return;
            try
            {
                var core=tab.View.CoreWebView2;if(core.IsSuspended)core.Resume();tab.IsSuspended=false;tab.SleepReason=null;UpdateTabHeader(tab);
            }
            catch(Exception ex){Debug.WriteLine("Could not wake a tab: "+ex.Message);}
        }
        private const string PlayingMediaProbe="(()=>{const visit=r=>{for(const e of r.querySelectorAll('*')){if((e.tagName==='VIDEO'||e.tagName==='AUDIO')&&!e.paused&&!e.ended&&e.readyState>=2)return true;if(e.shadowRoot&&visit(e.shadowRoot))return true;}return false;};return visit(document);})()";
        private static async Task<bool> MediaContextPlaying(Func<string,Task<string>> context)
        {try{return await context(PlayingMediaProbe)=="true";}catch{return true;}}
        private async Task<bool> HasPlayingMedia(BrowserTab tab)
        {
            var probes=Task.WhenAll(VideoContexts(tab).Select(MediaContextPlaying));
            if(await Task.WhenAny(probes,Task.Delay(1500))!=probes)return true;
            return (await probes).Any(value=>value);
        }
        private async Task SuspendTabIfInactiveAsync(BrowserTab tab)
        {
            if(tab==null||tab.SuspensionInProgress||tab.UnloadingInProgress||tab.IsUnloaded||!_settings.SuspendInactiveTabs)return;
            string reason=SleepProtection(tab);
            if(reason!=null){tab.SleepReason=reason;RetryTabSleep(tab);return;}
            var view=tab.View;int navigation=tab.NavigationGeneration;
            tab.SuspensionInProgress=true;
            try
            {
                var core=view.CoreWebView2;
                if(core.IsSuspended){tab.IsSuspended=true;return;}
                bool media=await HasPlayingMedia(tab);
                if(tab.IsClosing||!ReferenceEquals(tab.View,view)||tab.NavigationGeneration!=navigation||tab.NeedsNewView)return;
                reason=SleepProtection(tab);
                if(reason!=null||!_settings.SuspendInactiveTabs){tab.SleepReason=reason??"Sleep disabled";return;}
                if(media){tab.SleepReason="Playing media or page busy";return;}
                bool accepted=await core.TrySuspendAsync();
                if(tab.IsClosing||!ReferenceEquals(tab.View,view)||tab.NeedsNewView)return;
                reason=SleepProtection(tab);
                if(reason!=null||!_settings.SuspendInactiveTabs||tab.NavigationGeneration!=navigation)
                {if(core.IsSuspended)core.Resume();tab.IsSuspended=false;tab.SleepReason=reason;return;}
                tab.IsSuspended=accepted&&core.IsSuspended;
                tab.SleepReason=tab.IsSuspended?null:"Page kept running by browser";
            }
            catch(Exception ex){tab.IsSuspended=false;tab.SleepReason="Sleep unavailable";Debug.WriteLine(ex.Message);}
            finally
            {
                if(ReferenceEquals(tab.View,view))
                {
                    tab.SuspensionInProgress=false;
                    if(!tab.IsClosing){UpdateTabHeader(tab);if(!tab.IsSuspended)RetryTabSleep(tab);}
                }
            }
        }
        private void RetryTabSleep(BrowserTab tab)
        {tab.RetrySleepAfterUtc=DateTime.UtcNow.AddSeconds(30);StartTabSuspendTimer(tab);}
        private void ResetTabSleepOnNavigation(BrowserTab tab)
        {
            tab.IsSuspended=false;tab.SleepReason=null;tab.RetrySleepAfterUtc=default(DateTime);tab.InactiveSinceUtc=tab==SelectedTab?(DateTime?)null:DateTime.UtcNow;StartTabSuspendTimer(tab);UpdateTabHeader(tab);
        }
        internal async Task SleepInactiveTabs()
        {
            var tabs=OpenTabs.Where(t=>t!=SelectedTab).ToList();foreach(var tab in tabs)if(tab.SuspendTimer!=null)tab.SuspendTimer.Stop();
            await Task.WhenAll(tabs.Select(SuspendTabIfInactiveAsync));
            SetStatus(tabs.Count(t=>t.IsSuspended)+" tabs sleeping. Active media, downloads and kept-awake tabs stay running.");
        }
        private async void SuspendInactiveTabsNow_Click(object sender,RoutedEventArgs e){await SleepInactiveTabs();}
        private string TabResourceState(BrowserTab tab)
        {
            if(tab.IsUnloaded)return "Unloaded — reloads on selection";
            if(tab.FailureMessage!=null||tab.NeedsNewView){tab.IsSuspended=false;return "Needs reload";}
            if(tab.Initialized&&!tab.NeedsNewView&&tab.FailureMessage==null)try{tab.IsSuspended=tab.View.CoreWebView2.IsSuspended;}catch{}
            if(tab.IsSuspended)return "Sleeping";
            var protection=SleepProtection(tab);if(protection!=null)return protection;
            if(!_settings.SuspendInactiveTabs)return "Sleep disabled";
            if(tab.SuspensionInProgress)return "Checking activity";
            if(tab.SleepReason!=null)return tab.SleepReason;
            if(!tab.InactiveSinceUtc.HasValue)return "Awake";
            int seconds=(int)Math.Ceiling(TabSleepSchedule.Delay(tab.InactiveSinceUtc.Value,tab.RetrySleepAfterUtc,DateTime.UtcNow,_settings.InactiveTabSuspendDelaySeconds).TotalSeconds);
            return "Sleeps in "+seconds+"s";
        }
        internal IList<TabResourceRow> BuildTabResourceRows()
        {
            return OpenTabs.Select((tab,index)=>new TabResourceRow{Id=tab.ResourceId,Title=tab.IsPrivate?"Private tab "+(index+1):tab.PageTitle??"New tab",Scope=tab.IsPrivate?"Private":"Normal",State=TabResourceState(tab),UnloadState=TabUnloadState(tab),KeepAwake=tab.KeepAwake,CanSleep=_settings.SuspendInactiveTabs&&!tab.IsSuspended&&!tab.IsUnloaded&&!tab.UnloadingInProgress&&SleepProtection(tab)==null,CanWake=tab.IsSuspended||tab.IsUnloaded,CanUnload=_settings.UnloadInactiveTabs&&!tab.UnloadingInProgress&&UnloadProtection(tab)==null,IsUnloaded=tab.IsUnloaded}).ToList();
        }
        internal void SetTabKeepAwake(BrowserTab tab,bool value)
        {
            if(tab==null||tab.IsClosing)return;tab.KeepAwake=value;WakeTab(tab);if(value&&tab.SuspendTimer!=null)tab.SuspendTimer.Stop();
        }
        private void WakeTab(BrowserTab tab)
        {if(tab==null)return;if(tab.IsUnloaded){BrowserTabs.SelectedItem=tab.Item;_=EnsureTabInitializedAsync(tab);}else ResumeTabIfNeeded(tab);tab.InactiveSinceUtc=tab==SelectedTab?(DateTime?)null:DateTime.UtcNow;tab.RetrySleepAfterUtc=default(DateTime);tab.RetryUnloadAfterUtc=default(DateTime);StartTabSuspendTimer(tab);StartTabUnloadTimer(tab);}
        private ResourceMonitorActions MonitorActions()
        {
            return new ResourceMonitorActions{Tabs=BuildTabResourceRows,Activate=id=>{var tab=OpenTabs.FirstOrDefault(t=>t.ResourceId==id);if(tab!=null){BrowserTabs.SelectedItem=tab.Item;Activate();}},Sleep=async id=>{var tab=OpenTabs.FirstOrDefault(t=>t.ResourceId==id);if(tab!=null)await SuspendTabIfInactiveAsync(tab);},Unload=async id=>await UnloadTabIfInactiveAsync(OpenTabs.FirstOrDefault(t=>t.ResourceId==id)),Wake=id=>WakeTab(OpenTabs.FirstOrDefault(t=>t.ResourceId==id)),KeepAwake=(id,value)=>SetTabKeepAwake(OpenTabs.FirstOrDefault(t=>t.ResourceId==id),value),SleepInactive=SleepInactiveTabs};
        }
        private string SampleProcessCpu(Process process,long timestamp)
        {
            try{var value=_processCpu.SamplePercent(process.Id,process.StartTime.ToUniversalTime().Ticks,process.TotalProcessorTime,timestamp,Stopwatch.Frequency,Environment.ProcessorCount);return value.HasValue?value.Value.ToString("N1")+"%":"Sampling…";}catch{return "Unavailable";}
        }
    }
}
