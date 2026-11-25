using System.Collections.Generic;
using System.Linq;
namespace HarborBrowser.Models
{
    public sealed class BrowserSettings
    {
        public string ThemeMode { get; set; }
        public AppearanceColors LightColors { get; set; }
        public AppearanceColors DarkColors { get; set; }
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
        public bool OnlineSuggestionsEnabled {get;set;}
        public List<string> BookmarkFolders {get;set;}
        public List<string> BookmarkBarOrder {get;set;}=new List<string>();
        public List<AppearancePreset> AppearancePresets {get;set;}
        public bool UseCustomHomeShortcuts {get;set;}
        public List<Bookmark> HomeShortcuts {get;set;}
        public string HomeScene {get;set;}
        public string HomeBackgroundFile {get;set;}

        public BrowserSettings()
        {
            ThemeMode = "Light"; LightColors = new AppearanceColors(); DarkColors = new AppearanceColors();
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
            OnlineSuggestionsEnabled=true;
            BookmarkFolders=new List<string>();AppearancePresets=new List<AppearancePreset>();HomeShortcuts=new List<Bookmark>();HomeScene="Auto";
        }

        public BrowserSettings Copy()
        {
            return new BrowserSettings
            {
                ThemeMode = ThemeMode,
                LightColors = LightColors == null ? new AppearanceColors() : LightColors.Copy(),
                DarkColors = DarkColors == null ? new AppearanceColors() : DarkColors.Copy(),
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
                ClearCacheOnExit = ClearCacheOnExit,
                OnlineSuggestionsEnabled=OnlineSuggestionsEnabled,
                BookmarkFolders=(BookmarkFolders??new List<string>()).ToList(),
                BookmarkBarOrder=(BookmarkBarOrder??new List<string>()).ToList(),
                AppearancePresets=(AppearancePresets??new List<AppearancePreset>()).Where(p=>p!=null&&!string.IsNullOrWhiteSpace(p.Name)).Take(20).Select(p=>p.Copy()).ToList(),
                UseCustomHomeShortcuts=UseCustomHomeShortcuts,
                HomeShortcuts=(HomeShortcuts??new List<Bookmark>()).Where(b=>b!=null).Select(b=>b.Copy()).ToList(),
                HomeScene=HomeScene,HomeBackgroundFile=HomeBackgroundFile
            };
        }
    }
}
