using System;
using System.Linq;
using System.Threading.Tasks;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        internal Func<BrowserTab, Task<string>> UnloadActivityReader { get; set; }
        // Only a boolean activity result crosses the page boundary; field values never do.
        private const string UnloadActivityScript = @"(()=>{
let dirty=false;
for(const name of ['input','change','beforeinput','submit'])addEventListener(name,()=>{dirty=true},true);
let visited=0;
const inspect=root=>{
 const walker=document.createTreeWalker(root,NodeFilter.SHOW_ELEMENT);let e;
 while(e=walker.nextNode()){
  if(++visited>10000)return 'unknown';
  if((e.tagName==='VIDEO'||e.tagName==='AUDIO')&&!e.paused&&!e.ended&&e.readyState>=2)return 'media';
  if(e.isContentEditable)return 'input';
  if(e.tagName==='INPUT'&&(e.value!==e.defaultValue||e.checked!==e.defaultChecked||(e.files&&e.files.length)))return 'input';
  if(e.tagName==='TEXTAREA'&&e.value!==e.defaultValue)return 'input';
  if(e.tagName==='SELECT'){
   const options=Array.from(e.options);
   if(e.multiple?options.some(o=>o.selected!==o.defaultSelected):e.selectedIndex!==Math.max(0,options.findIndex(o=>o.defaultSelected)))return 'input';
  }
  if(e.shadowRoot){const state=inspect(e.shadowRoot);if(state!=='clear')return state;}
 }
 return 'clear';
};
Object.defineProperty(window,'__harborUnloadActivity070',{value:()=>{visited=0;return dirty?'input':inspect(document)}});
})()";
        private const string UnloadActivityProbe = "typeof window.__harborUnloadActivity070==='function'?window.__harborUnloadActivity070():'unknown'";

        private string UnloadProtection(BrowserTab tab)
        {
            if (tab.IsUnloaded) return "Unloaded";
            if (tab.SuspensionInProgress) return "Checking sleep";
            if (tab.IsPrivate) return "Private session";
            if (tab.IsPinned) return "Pinned tab";
            if (tab.IsPopupWindow) return "Requested window";
            if (tab.ProtonPaused || tab.ProtonBlockedUrl != null || tab.TorBlockedUrl != null) return "Browsing paused";
            if (tab.IsLoading) return "Page loading";
            if (tab.NonGetDocument) return "Submitted page";
            return SleepProtection(tab);
        }

        private void StartTabUnloadTimer(BrowserTab tab)
        {
            if (tab?.UnloadTimer == null) return;
            tab.UnloadTimer.Stop();
            if (!_settings.UnloadInactiveTabs || tab.IsUnloaded || tab.IsClosing || _shuttingDown ||
                tab == SelectedTab || !tab.Initialized || tab.UnloadingInProgress || tab.NativeContent != null ||
                tab.KeepAwake || tab.IsPrivate || tab.IsPinned || tab.IsProviderAuthentication) return;
            if (!tab.InactiveSinceUtc.HasValue) tab.InactiveSinceUtc = DateTime.UtcNow;
            tab.UnloadTimer.Interval = TabUnloadSchedule.Delay(tab.InactiveSinceUtc.Value,
                tab.RetryUnloadAfterUtc, DateTime.UtcNow, _settings.InactiveTabUnloadDelaySeconds);
            tab.UnloadTimer.Start();
        }

        private async Task<string> ReadUnloadActivity(BrowserTab tab)
        {
            try
            {
                var reads = Task.WhenAll(VideoContexts(tab).Select(async context => await context(UnloadActivityProbe)));
                if (await Task.WhenAny(reads, Task.Delay(1500)) != reads)
                { ObserveAbandonedTask(reads); return "Page activity unavailable"; }
                var results = await reads;
                if (results.Any(r => r == "\"media\"")) return "Playing media";
                if (results.Any(r => r == "\"input\"")) return "Unsaved input";
                return results.All(r => r == "\"clear\"") ? null : "Page activity unavailable";
            }
            catch { return "Page activity unavailable"; }
        }

        internal async Task UnloadTabIfInactiveAsync(BrowserTab tab)
        {
            if (tab == null || tab.UnloadingInProgress || !_settings.UnloadInactiveTabs) return;
            tab.UnloadTimer?.Stop();
            string reason = UnloadProtection(tab);
            if (reason != null) { tab.UnloadReason = reason; RetryTabUnload(tab); return; }
            var view = tab.View;
            int navigation = tab.NavigationGeneration;
            bool wasSuspended = tab.IsSuspended;
            tab.UnloadingInProgress = true;
            try
            {
                var core = view.CoreWebView2;
                Uri target;
                if (!Uri.TryCreate(core.Source, UriKind.Absolute, out target) ||
                    (target.Scheme != "http" && target.Scheme != "https"))
                { tab.UnloadReason = "Browser page"; return; }
                if (core.IsSuspended) core.Resume();
                reason = await (UnloadActivityReader == null ? ReadUnloadActivity(tab) : UnloadActivityReader(tab));
                // Selection, navigation, settings, downloads and permissions can change during the probe.
                if (tab.IsClosing || _shuttingDown || !ReferenceEquals(tab.View, view) ||
                    tab.NavigationGeneration != navigation || !_settings.UnloadInactiveTabs) return;
                tab.UnloadReason = reason ?? UnloadProtection(tab);
                if (tab.UnloadReason != null) return;

                string url = core.Source;
                CancelTabStartup(tab);
                CancelHomeSuggestions(tab);
                tab.View = new WebView2();
                tab.PendingUrl = url;
                tab.LastKnownUrl = url;
                tab.Initialized = false;
                tab.Initializing = false;
                tab.InitializationTask = null;
                tab.IsSuspended = false;
                tab.IsUnloaded = true;
                tab.IsLoading = false;
                tab.SuspensionInProgress = false;
                tab.NavigationGeneration++;
                tab.MediaFrames.Clear();
                tab.ActiveVideoContext = null;
                tab.TrackerFilterRegistered = false;
                tab.SleepReason = null;
                tab.UnloadReason = null;
                tab.SuspendTimer?.Stop();
                view.Dispose();
                UpdateTabHeader(tab);
                SaveSession();
            }
            catch { tab.UnloadReason = "Unload unavailable"; }
            finally
            {
                tab.UnloadingInProgress = false;
                if (!tab.IsClosing && !tab.IsUnloaded && ReferenceEquals(tab.View, view))
                {
                    // A rejected check must not leave a formerly sleeping page running.
                    if (wasSuspended && tab != SelectedTab && _settings.SuspendInactiveTabs)
                    {
                        try
                        {
                            tab.IsSuspended = await view.CoreWebView2.TrySuspendAsync();
                            if (tab == SelectedTab || !_settings.SuspendInactiveTabs)
                            { if (view.CoreWebView2.IsSuspended) view.CoreWebView2.Resume(); tab.IsSuspended = false; }
                        }
                        catch { tab.IsSuspended = false; }
                    }
                    UpdateTabHeader(tab);
                    RetryTabUnload(tab);
                }
            }
        }

        private void RetryTabUnload(BrowserTab tab)
        {
            tab.RetryUnloadAfterUtc = DateTime.UtcNow.AddSeconds(60);
            StartTabUnloadTimer(tab);
        }

        private string TabUnloadState(BrowserTab tab)
        {
            if (tab.IsUnloaded) return "Reloads when selected";
            if (!_settings.UnloadInactiveTabs) return "Off in Settings";
            if (tab.UnloadingInProgress) return "Checking page activity";
            var reason = UnloadProtection(tab) ?? tab.UnloadReason;
            if (reason != null) return reason;
            if (!tab.InactiveSinceUtc.HasValue) return "Waiting";
            var remaining = TabUnloadSchedule.Delay(tab.InactiveSinceUtc.Value, tab.RetryUnloadAfterUtc,
                DateTime.UtcNow, _settings.InactiveTabUnloadDelaySeconds);
            return "Unloads in " + (int)Math.Ceiling(remaining.TotalSeconds) + "s";
        }
    }
}
