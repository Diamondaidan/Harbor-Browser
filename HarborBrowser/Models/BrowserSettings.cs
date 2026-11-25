namespace HarborBrowser.Models
{
    public sealed class BrowserSettings
    {
        public string SearchEngineId { get; set; }
        public string HomePage { get; set; }
        public bool HarborHomeIntroduced { get; set; }
        public bool ShowBookmarksBar { get; set; }
        public bool ShowStatusBar { get; set; }
        public bool EnableDeveloperTools { get; set; }
        public bool SuspendInactiveTabs { get; set; }
        public int InactiveTabSuspendDelaySeconds { get; set; }
        public bool RestorePreviousSession { get; set; }
        public bool EnableTrackerBlocking { get; set; }
        public string TrackingPreventionLevel { get; set; }
        public bool ClearCookiesOnExit { get; set; }
        public bool ClearCacheOnExit { get; set; }

        public BrowserSettings()
        {
            SearchEngineId = "duckduckgo";
            HomePage = HarborBrowser.Services.HomePageService.Url;
            ShowBookmarksBar = true;
            ShowStatusBar = true;
            EnableDeveloperTools = false;
            SuspendInactiveTabs = true;
            InactiveTabSuspendDelaySeconds = 60;
            RestorePreviousSession = true;
            EnableTrackerBlocking = true;
            TrackingPreventionLevel = "Balanced";
            ClearCookiesOnExit = false;
            ClearCacheOnExit = false;
        }

        public BrowserSettings Copy()
        {
            return new BrowserSettings
            {
                SearchEngineId = SearchEngineId,
                HomePage = HomePage,
                HarborHomeIntroduced = HarborHomeIntroduced,
                ShowBookmarksBar = ShowBookmarksBar,
                ShowStatusBar = ShowStatusBar,
                EnableDeveloperTools = EnableDeveloperTools,
                SuspendInactiveTabs = SuspendInactiveTabs,
                InactiveTabSuspendDelaySeconds = InactiveTabSuspendDelaySeconds,
                RestorePreviousSession = RestorePreviousSession,
                EnableTrackerBlocking = EnableTrackerBlocking,
                TrackingPreventionLevel = TrackingPreventionLevel,
                ClearCookiesOnExit = ClearCookiesOnExit,
                ClearCacheOnExit = ClearCacheOnExit
            };
        }
    }
}
