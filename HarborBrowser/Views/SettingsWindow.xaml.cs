using System.Windows;
using HarborBrowser.Models;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    public partial class SettingsWindow : Window
    {
        public BrowserSettings ResultSettings { get; private set; }

        public SettingsWindow(BrowserSettings current)
        {
            InitializeComponent();
            SearchEngineBox.ItemsSource = SearchEngineService.Engines;

            BrowserSettings settings = current == null ? new BrowserSettings() : current.Copy();
            SearchEngineBox.SelectedValue = settings.SearchEngineId;
            HomePageBox.Text = HomePageService.DisplayUrl(settings.HomePage);
            ShowBookmarksBarBox.IsChecked = settings.ShowBookmarksBar;
            ShowStatusBarBox.IsChecked = settings.ShowStatusBar;
            EnableDevToolsBox.IsChecked = settings.EnableDeveloperTools;
            SuspendInactiveTabsBox.IsChecked = settings.SuspendInactiveTabs;
            SuspendDelayBox.SelectedValue = settings.InactiveTabSuspendDelaySeconds.ToString();
            if (SuspendDelayBox.SelectedItem == null)
                SuspendDelayBox.SelectedValue = "60";
            RestorePreviousSessionBox.IsChecked = settings.RestorePreviousSession;
            EnableTrackerBlockingBox.IsChecked = settings.EnableTrackerBlocking;
            TrackingPreventionLevelBox.SelectedValue = string.IsNullOrWhiteSpace(settings.TrackingPreventionLevel) ? "Balanced" : settings.TrackingPreventionLevel;
            if (TrackingPreventionLevelBox.SelectedItem == null) TrackingPreventionLevelBox.SelectedValue = "Balanced";
            ClearCookiesOnExitBox.IsChecked = settings.ClearCookiesOnExit;
            ClearCacheOnExitBox.IsChecked = settings.ClearCacheOnExit;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
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

            ResultSettings = new BrowserSettings
            {
                SearchEngineId = selected == null ? "duckduckgo" : selected.Id,
                HomePage = homePage,
                HarborHomeIntroduced = true,
                ShowBookmarksBar = ShowBookmarksBarBox.IsChecked == true,
                ShowStatusBar = ShowStatusBarBox.IsChecked == true,
                EnableDeveloperTools = EnableDevToolsBox.IsChecked == true,
                SuspendInactiveTabs = SuspendInactiveTabsBox.IsChecked == true,
                InactiveTabSuspendDelaySeconds = suspendDelay,
                RestorePreviousSession = RestorePreviousSessionBox.IsChecked == true,
                EnableTrackerBlocking = EnableTrackerBlockingBox.IsChecked == true,
                TrackingPreventionLevel = System.Convert.ToString(TrackingPreventionLevelBox.SelectedValue),
                ClearCookiesOnExit = ClearCookiesOnExitBox.IsChecked == true,
                ClearCacheOnExit = ClearCacheOnExitBox.IsChecked == true
            };

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
