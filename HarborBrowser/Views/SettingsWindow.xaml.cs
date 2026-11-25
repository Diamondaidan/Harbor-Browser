using System.Windows;
using HarborBrowser.Models;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    public partial class SettingsWindow : Window
    {
        public BrowserSettings ResultSettings { get; private set; }

        public SettingsWindow(BrowserSettings current, System.Collections.Generic.IEnumerable<Bookmark> bookmarks = null)
        {
            InitializeComponent();
            HarborBrowser.Services.WindowFitService.Attach(this);
            SearchEngineBox.ItemsSource = SearchEngineService.Engines;

            BrowserSettings settings = current == null ? new BrowserSettings() : current.Copy();
            InitializeAppearance(settings);
            InitializeCustomization(bookmarks);
            SearchEngineBox.SelectedValue = settings.SearchEngineId;
            HomePageBox.Text = HomePageService.DisplayUrl(settings.HomePage);
            ShowBookmarksBarBox.IsChecked = settings.ShowBookmarksBar;
            ShowStatusBarBox.IsChecked = settings.ShowStatusBar;
            EnableDevToolsBox.IsChecked = settings.EnableDeveloperTools;
            SuspendInactiveTabsBox.IsChecked = settings.SuspendInactiveTabs;
            SuspendDelayBox.SelectedValue = settings.InactiveTabSuspendDelaySeconds.ToString();
            if (SuspendDelayBox.SelectedItem == null)
                SuspendDelayBox.SelectedValue = "60";
            UnloadInactiveTabsBox.IsChecked = settings.UnloadInactiveTabs;
            UnloadDelayBox.SelectedValue = settings.InactiveTabUnloadDelaySeconds.ToString();
            if (UnloadDelayBox.SelectedItem == null) UnloadDelayBox.SelectedValue = "900";
            RestorePreviousSessionBox.IsChecked = settings.RestorePreviousSession;
            EnableTrackerBlockingBox.IsChecked = settings.EnableTrackerBlocking;
            TrackingPreventionLevelBox.SelectedValue = string.IsNullOrWhiteSpace(settings.TrackingPreventionLevel) ? "Balanced" : settings.TrackingPreventionLevel;
            if (TrackingPreventionLevelBox.SelectedItem == null) TrackingPreventionLevelBox.SelectedValue = "Balanced";
            ClearCookiesOnExitBox.IsChecked = settings.ClearCookiesOnExit;
            ClearCacheOnExitBox.IsChecked = settings.ClearCacheOnExit;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!CommitHex()) { AppearanceHexBox.Focus(); return; }
            if (System.Convert.ToString(HomeSceneBox.SelectedValue) == "Custom" && SelectedBackgroundPath == null && string.IsNullOrEmpty(_appearanceDraft.HomeBackgroundFile))
            { MessageBox.Show(this, "Choose a custom background image first.", "Home background"); return; }
            string homePage;
            if (!SearchEngineService.TryNormalizeHomePage(HomePageBox.Text, out homePage))
            {
                MessageBox.Show(this, "Enter harbor://home for Harbor Home, or a valid HTTP or HTTPS home page.", "Harbor Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                HomePageBox.Focus();
                return;
            }

            SearchEngine selected = SearchEngineBox.SelectedItem as SearchEngine;
            int suspendDelay;
            if (!int.TryParse(System.Convert.ToString(SuspendDelayBox.SelectedValue), out suspendDelay))
                suspendDelay = 60;
            int unloadDelay;
            if (!int.TryParse(System.Convert.ToString(UnloadDelayBox.SelectedValue), out unloadDelay)) unloadDelay = 900;

            ResultSettings = new BrowserSettings
            {
                ThemeMode = _appearanceDraft.ThemeMode,
                LightColors = _appearanceDraft.LightColors.Copy(),
                DarkColors = _appearanceDraft.DarkColors.Copy(),
                SearchEngineId = selected == null ? "duckduckgo" : selected.Id,
                HomePage = homePage,
                HarborHomeIntroduced = true,
                ShowBookmarksBar = ShowBookmarksBarBox.IsChecked == true,
                ShowStatusBar = ShowStatusBarBox.IsChecked == true,
                EnableDeveloperTools = EnableDevToolsBox.IsChecked == true,
                SuspendInactiveTabs = SuspendInactiveTabsBox.IsChecked == true,
                InactiveTabSuspendDelaySeconds = suspendDelay,
                UnloadInactiveTabs = UnloadInactiveTabsBox.IsChecked == true,
                InactiveTabUnloadDelaySeconds = unloadDelay,
                RestorePreviousSession = RestorePreviousSessionBox.IsChecked == true,
                EnableTrackerBlocking = EnableTrackerBlockingBox.IsChecked == true,
                TrackingPreventionLevel = System.Convert.ToString(TrackingPreventionLevelBox.SelectedValue),
                ClearCookiesOnExit = ClearCookiesOnExitBox.IsChecked == true,
                ClearCacheOnExit = ClearCacheOnExitBox.IsChecked == true,
                OnlineSuggestionsEnabled = OnlineSuggestionsBox.IsChecked == true,
                BookmarkFolders = _appearanceDraft.Copy().BookmarkFolders,
                BookmarkBarOrder = _appearanceDraft.Copy().BookmarkBarOrder,
                AppearancePresets = _appearanceDraft.Copy().AppearancePresets,
                HomeShortcuts = _appearanceDraft.Copy().HomeShortcuts,
                UseCustomHomeShortcuts = UseBookmarkShortcutsBox.IsChecked == false,
                HomeScene = System.Convert.ToString(HomeSceneBox.SelectedValue),
                HomeBackgroundFile = _appearanceDraft.HomeBackgroundFile
            };

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
