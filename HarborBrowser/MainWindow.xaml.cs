using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;

namespace HarborBrowser
{
    public partial class MainWindow : Window
    {
        private readonly BrowserDataStore _store;
        private BrowserSettings _settings;
        private readonly List<Bookmark> _bookmarks;
        private readonly List<HistoryEntry> _history;
        private readonly List<string> _tabGroups = new List<string> { "Work", "Personal", "Research", "Shopping" };
        private readonly ObservableCollection<DownloadEntry> _downloads;
        private CoreWebView2Environment _environment;
        private Task<CoreWebView2Environment> _environmentTask;
        private bool _ready;
        private readonly bool _homeDefaultsUpdated;
        private bool _reorderingTabs;
        private bool _restoringSession;
        private bool _closingAfterPrivacyCleanup;
        private bool _confirmedCloseWithActiveDownloads;
        private DispatcherTimer _downloadNotificationTimer;
        private long _blockedTrackerRequests;




        public MainWindow() : this(new BrowserDataStore(), null) { }

        internal MainWindow(BrowserDataStore store, Func<IntPtr, MonitorLayout> monitorProvider = null)
        {
            InitializeComponent();
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _monitorProvider = monitorProvider;
            InitializePresentation();
            InitializeResponsiveLayout();
            _settings = _store.LoadSettings();
            AppearanceService.Normalize(_settings); AppearanceService.ApplyApplication(_settings);
            _homeDefaultsUpdated = HomePageService.UpgradeDefaults(_settings);
            string safeHomePage;
            if (!SearchEngineService.TryNormalizeHomePage(_settings.HomePage, out safeHomePage))
                safeHomePage = HomePageService.Url;
            _settings.HomePage = safeHomePage;
            _settings.SearchEngineId = SearchEngineService.GetById(_settings.SearchEngineId).Id;
            _bookmarks = _store.LoadBookmarks();
            _history = _store.LoadHistory();
            _downloads = new ObservableCollection<DownloadEntry>();
            InitializePrivacy();
            InitializeRecovery();
            InitializeEverydayFeatures();
            BrowserTabs.ContextMenu = BuildTabStripContextMenu();
            BookmarksBar.ContextMenu = BuildBookmarksBarContextMenu();
        }


        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (MaximizeWindowButton == null || WindowFrame == null) return;
            bool maximized = WindowState == WindowState.Maximized;
            MaximizeWindowButton.Content = maximized ? "\uE923" : "\uE922";
            MaximizeWindowButton.ToolTip = maximized ? "Restore down" : "Maximize";
            System.Windows.Automation.AutomationProperties.SetName(
                MaximizeWindowButton, maximized ? "Restore down" : "Maximize");
            // The native window hook constrains maximized bounds to the monitor work area.
            WindowFrame.Padding = new Thickness(0);
        }

        private string RepairCompletedDownload(DownloadEntry entry)
        {
            try
            {
                string repairedPath = DownloadFileNameService.RepairCompletedFile(entry.FilePath,
                    _downloads.Where(d => d != entry).Select(d => d.FilePath));
                entry.FilePath = repairedPath;
                entry.FileName = Path.GetFileName(repairedPath);
                return "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Download filename recovery failed: " + ex.Message);
                // The completed bytes remain available under the original name.
                return " (filename recovery unavailable)";
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!BeginSessionRun()) { Close(); return; }
            if (_homeDefaultsUpdated && _sessionWritesAllowed) SaveSettings();
            SearchEngineBox.ItemsSource = SearchEngineService.Engines;
            SearchEngineBox.SelectedValue = _settings.SearchEngineId;
            ApplyInterfaceSettings();
            RefreshBookmarksBar();
            _ready = true;
            ApplyTabGroupFilter();

            if (_store.IsReadOnlyForNewerDataVersion)
            {
                MessageBox.Show(this,
                    "This Harbor profile was last used by a newer Harbor version. The profile has been opened in read-only mode to protect your bookmarks, settings, and history. Install the newer Harbor version to resume saving changes.",
                    "Newer Harbor profile detected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            try
            {
                CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch (Exception)
            {
                MessageBox.Show(this,
                    "Harbor needs the Microsoft Edge WebView2 Runtime to display websites.\n\nInstall the free Evergreen Runtime, then start Harbor again:\nhttps://developer.microsoft.com/microsoft-edge/webview2/",
                    "WebView2 Runtime Required", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
                return;
            }

            bool shouldRestorePreviousSession = _settings.RestorePreviousSession;
            if (_sessionRecovery.HasPending) { shouldRestorePreviousSession = false; RecoveryBanner.Visibility = Visibility.Visible; }
            BrowserSession session = _store.LoadSession();
            LoadTabGroups(session);
            RefreshGroupFilterBox("*");

            if (BrowserTabs.Items.Count == 0)
            {
                bool restored = false;
                if (shouldRestorePreviousSession && session != null && session.Tabs != null && session.Tabs.Count > 0)
                {
                    _restoringSession = true;
                    try
                    {
                        var restoredTabs = new List<BrowserTab>();
                        foreach (BrowserSessionTab savedTab in session.Tabs)
                        {
                            if (savedTab == null || !IsRestorableUrl(savedTab.Url)) continue;
                            restoredTabs.Add(CreateNewTab(savedTab.Url, false, false, savedTab));
                        }
                        restored = restoredTabs.Count > 0;
                        if (restored)
                        {
                            int selectedIndex = Math.Max(0, Math.Min(session.SelectedTabIndex, restoredTabs.Count - 1));
                            BrowserTabs.SelectedItem = restoredTabs[selectedIndex].Item;
                            SetStatus("Previous browsing session restored");
                        }
                    }
                    finally { _restoringSession = false; }
                }
                if (!restored)
                    CreateNewTab(_settings.HomePage, true);
                SaveSession();
            }

            await Task.CompletedTask;
        }

        private void ApplyInterfaceSettings()
        {
            AppearanceService.ApplyApplication(_settings);
            foreach (TabItem item in BrowserTabs.Items)
            {
                BrowserTab tab = item.Tag as BrowserTab;
                if (tab != null && tab.Initialized && tab.View.CoreWebView2 != null)
                    tab.View.CoreWebView2.Profile.PreferredColorScheme = _settings.ThemeMode == "Dark" ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
            }
            BookmarksBar.Visibility = _settings.ShowBookmarksBar ? Visibility.Visible : Visibility.Collapsed;
            BottomStatus.Visibility = _settings.ShowStatusBar && (_presentation == null || !_presentation.IsFullScreen) ? Visibility.Visible : Visibility.Collapsed;
            SearchEngineBox.SelectedValue = _settings.SearchEngineId;
            UpdateBookmarkButtonState();
        }

        private void LoadTabGroups(BrowserSession session)
        {
            if (session == null) return;
            if (session.GroupsConfigured || (session.Groups != null && session.Groups.Count > 0))
            {
                _tabGroups.Clear();
                if (session.Groups != null)
                    foreach (string name in session.Groups)
                        AddGroupNameIfMissing(name);
            }
            if (session.Tabs != null)
                foreach (BrowserSessionTab saved in session.Tabs)
                    if (saved != null) AddGroupNameIfMissing(saved.GroupName);
        }

        private void AddGroupNameIfMissing(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            string clean = name.Trim();
            if (!_tabGroups.Any(g => string.Equals(g, clean, StringComparison.OrdinalIgnoreCase)))
                _tabGroups.Add(clean);
        }

        private void RefreshGroupFilterBox(string preferredValue)
        {
            string value=preferredValue??_activeGroupFilter;
            _activeGroupFilter=value=="*"||value==""?value:_tabGroups.FirstOrDefault(g=>string.Equals(g,value,StringComparison.OrdinalIgnoreCase))??"*";
        }
        private BrowserTab SelectedTab
        {
            get
            {
                TabItem selected = BrowserTabs.SelectedItem as TabItem;
                return selected == null ? null : selected.Tag as BrowserTab;
            }
        }

        private BrowserTab CreateNewTab(string initialUrl, bool select)
        {
            return CreateNewTab(initialUrl, select, false, null);
        }

        private BrowserTab CreateNewTab(string initialUrl, bool select, bool isPrivate, BrowserSessionTab restoredTab)
        {
            var tab = new BrowserTab
            {
                View = new WebView2(),
                PendingUrl = string.IsNullOrWhiteSpace(initialUrl) ? _settings.HomePage : initialUrl,
                PageTitle = isPrivate ? "Private tab" : "New tab",
                IsPrivate = isPrivate,
                IsPinned = restoredTab != null && restoredTab.IsPinned,
                GroupName = restoredTab == null ? (isPrivate ? null : GetSelectedGroupForNewTab()) : restoredTab.GroupName
            };
            if (tab.IsPrivate)
            {
                ConfigurePrivateView(tab);
            }

            if (IsDownloadsUrl(initialUrl)) ConfigureDownloadsTab(tab);

            tab.HeaderText = new TextBlock
            {
                Text = "New tab",
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 150,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(5, 0, 5, 0)
            };
            tab.HeaderIcon = new Image { Width = 16, Height = 16, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
            tab.FallbackIcon = new TextBlock { Text = "🌐", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
            tab.HeaderContainer = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            tab.HeaderContainer.Children.Add(tab.HeaderIcon);
            tab.HeaderContainer.Children.Add(tab.FallbackIcon);
            tab.HeaderContainer.Children.Add(tab.HeaderText);
            tab.CloseButton = new Button
            {
                Content = "×", Width = 18, Height = 18, MinHeight = 18, Padding = new Thickness(0),
                Margin = new Thickness(1, 0, 0, 0), FontSize = 12, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Tag = tab, ToolTip = "Close tab"
            };
            tab.CloseButton.Click += CloseTabButton_Click;
            tab.HeaderContainer.Children.Add(tab.CloseButton);

            tab.Item = new TabItem
            {
                Header = tab.HeaderContainer,
                Tag = tab,
                ToolTip = "New tab",
                Padding = new Thickness(7, 3, 7, 3),
                MinWidth = 42
            };
            tab.SuspendTimer = new DispatcherTimer();
            tab.SuspendTimer.Tick += async (s, e) =>
            {
                tab.SuspendTimer.Stop();
                await SuspendTabIfInactiveAsync(tab);
            };

            tab.Item.ContextMenu = BuildTabContextMenu(tab);
            UpdateTabHeader(tab);
            tab.Item.PreviewMouseLeftButtonDown += Tab_PreviewMouseDown;
            tab.Item.PreviewMouseMove += Tab_PreviewMouseMove;
            int insertIndex = TabOrderService.NewTabIndex(BrowserTabs.Items.Cast<TabItem>().Select(i => ((BrowserTab)i.Tag).IsPinned).ToList(), tab.IsPinned);
            BrowserTabs.Items.Insert(insertIndex, tab.Item);
            ApplyTabGroupFilter();
            if (select && tab.Item.Visibility == Visibility.Visible) { BrowserTabs.SelectedItem = tab.Item; Dispatcher.BeginInvoke(new Action(() => tab.Item.BringIntoView()), DispatcherPriority.Loaded); }
            SaveSession();
            return tab;
        }

        private ContextMenu BuildTabContextMenu(BrowserTab tab)
        {
            var menu = new ContextMenu();
            AddReopenCommand(menu);
            AddContextItem(menu, "New tab", (s, e) => CreateNewTab(_settings.HomePage, true));
            var pin = new MenuItem { Header = tab.IsPinned ? "Unpin tab" : "Pin tab", Tag = tab };
            pin.Click += TogglePinTab_Click;
            menu.Items.Add(pin);
            var group = new MenuItem { Header = "Move to group" };
            foreach (string name in _tabGroups.OrderBy(g => g, StringComparer.CurrentCultureIgnoreCase))
            {
                var item = new MenuItem { Header = name, Tag = new TabGroupAction { Tab = tab, GroupName = name } };
                item.Click += TabGroupContext_Click;
                group.Items.Add(item);
            }
            var clearGroup = new MenuItem { Header = "Ungroup tab", Tag = new TabGroupAction { Tab = tab, GroupName = null } };
            clearGroup.Click += TabGroupContext_Click;
            group.Items.Add(new Separator());
            group.Items.Add(clearGroup);
            menu.Items.Add(group);
            menu.Items.Add(new Separator());
            AddContextItem(menu, "Move left (Ctrl+Shift+Page Up)", (s, e) => MoveTabRelative(tab, -1));
            AddContextItem(menu, "Keep this tab awake / allow sleep", (s,e) => { SetTabKeepAwake(tab, !tab.KeepAwake); SetStatus(tab.KeepAwake ? "Tab kept awake for this session" : "Automatic sleep allowed"); });
            AddContextItem(menu, "Move right (Ctrl+Shift+Page Down)", (s, e) => MoveTabRelative(tab, 1));
            AddContextItem(menu, "Close tab", (s, e) => CloseTab(tab));
            AddContextItem(menu, "Close other tabs", (s, e) => CloseOtherTabs(tab));
            AddContextItem(menu, "Manage groups…", ManageTabGroups_Click);
            return menu;
        }

        private ContextMenu BuildTabStripContextMenu()
        {
            var menu = new ContextMenu();
            AddReopenCommand(menu);
            AddContextItem(menu, "New tab", (s, e) => CreateNewTab(_settings.HomePage, true));
            AddContextItem(menu, "New private tab", NewPrivateTab_Click);
            AddContextItem(menu, "Restore previous session", RestoreSession_Click);
            menu.Items.Add(new Separator());
            AddContextItem(menu, "Manage groups…", ManageTabGroups_Click);
            AddContextItem(menu, "Put inactive tabs to sleep now", SuspendInactiveTabsNow_Click);
            return menu;
        }

        private ContextMenu BuildBookmarksBarContextMenu()
        {
            var menu = new ContextMenu();
            AddContextItem(menu, "Bookmark current page", (s, e) => AddBookmark());
            return menu;
        }

        private static void AddContextItem(ItemsControl menu, string header, RoutedEventHandler handler)
        {
            var item = new MenuItem { Header = header };
            item.Click += handler;
            menu.Items.Add(item);
        }

        private void CloseOtherTabs(BrowserTab keep)
        {
            foreach (BrowserTab tab in BrowserTabs.Items.Cast<TabItem>().Select(i => i.Tag as BrowserTab).ToList())
                if (tab != null && tab != keep && !tab.IsPinned) CloseTab(tab);
        }

        private int GetPinnedCount()
        {
            int count = 0;
            foreach (TabItem item in BrowserTabs.Items)
            {
                BrowserTab tab = item.Tag as BrowserTab;
                if (tab != null && tab.IsPinned) count++;
            }
            return count;
        }

        private async Task EnsureTabInitializedAsync(BrowserTab tab)
        {
            if (tab == null || tab.NativeContent != null || tab.Initialized || tab.Initializing || tab.IsClosing || tab.FailureMessage != null || tab.NeedsNewView)
                return;

            tab.Initializing = true;
            int generation = _browserGeneration;
            var view = tab.View;
            Func<bool> current = () => !tab.IsClosing && generation == _browserGeneration && ReferenceEquals(view, tab.View);
            SetStatus("Starting browser engine…");
            try
            {
                await EnsureEnvironmentAsync();

                if (!current()) return;
                await view.EnsureCoreWebView2Async(_environment);
                if (!current()) return;
                CoreWebView2 core = view.CoreWebView2;
                await InitializePermissionProfile(core.Profile);
                if (!current()) return;
                core.PermissionRequested += (s, args) => { if (current()) CorePermissionRequested(tab, args); else { args.Handled = true; args.State = CoreWebView2PermissionState.Deny; args.SavesInProfile = false; } };
                await InitializeVideoFeatures(tab, core);
                if (!current()) return;

                string homeAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Home");
                core.SetVirtualHostNameToFolderMapping(HomePageService.Host, homeAssets,
                    CoreWebView2HostResourceAccessKind.DenyCors);
                core.WebMessageReceived += (s, args) =>
                {
                    if (!current()) return;
                    if (!HomePageService.IsHomeUrl(args.Source) || !HomePageService.IsHomeUrl(core.Source)) return;
                    if (HandleHomeCustomization(tab, args.Source, args.WebMessageAsJson)) return;
                    string mode = HomePageService.ReadThemeMessage(args.Source, args.WebMessageAsJson);
                    if (mode != null) { if (!tab.IsPrivate) { _settings.ThemeMode = mode; ApplyInterfaceSettings(); SaveSettings(); } return; }
                    string query = HomePageService.ReadSearchMessage(args.Source, args.WebMessageAsJson);
                    if (query != null)
                        core.Navigate(HomeSearchOnly(args.Source, args.WebMessageAsJson) ? SearchSuggestionTarget(query) : SearchEngineService.ResolveInput(query, SearchEngineService.GetById(_settings.SearchEngineId)));
                    else
                        SendHomePageState(tab);
                };

                core.Profile.PreferredColorScheme = _settings.ThemeMode == "Dark" ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
                core.Profile.PreferredTrackingPreventionLevel = GetTrackingPreventionLevel();
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.AreDevToolsEnabled = _settings.EnableDeveloperTools;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = true;
                ConfigureTrackerProtection(tab);
                core.FaviconChanged += (s, args) => { if (current()) UpdateTabFavicon(tab); };
                UpdateTabFavicon(tab);

                core.NavigationStarting += (s, args) =>
                {
                    if (!current()) return;
                    tab.NavigationGeneration++;
                    ResetTabSleepOnNavigation(tab);
                    bool wasFailed = tab.FailureMessage != null;
                    tab.FailureMessage = null;
                    if (wasFailed && SelectedTab == tab) ShowTabContent();
                    CancelPermissions(tab);
                    CancelHomeSuggestions(tab);
                    if (tab.VideoPopout != null) tab.VideoPopout.Close();
                    if (SelectedTab == tab)
                    {
                        SetAddressText(HomePageService.DisplayUrl(args.Uri));
                        UpdateBookmarkButtonState();
                        SetStatus("Loading " + args.Uri);
                    }
                };

                core.SourceChanged += (s, args) =>
                {
                    if (!current()) return;
                    tab.LastKnownUrl = core.Source;
                    if (SelectedTab == tab && tab.View.Source != null)
                    {
                        SetAddressText(HomePageService.DisplayUrl(tab.View.Source.AbsoluteUri));
                        UpdateBookmarkButtonState();
                    }
                };

                core.DocumentTitleChanged += (s, args) =>
                {
                    if (!current()) return;
                    string title = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "Untitled" : core.DocumentTitle;
                    tab.PageTitle = title;
                    UpdateTabHeader(tab);
                    if (SelectedTab == tab)
                        Title = title + " - Harbor Browser";
                };

                core.NavigationCompleted += (s, args) =>
                {
                    if (!current()) return;
                    if (SelectedTab == tab)
                    {
                        SetStatus(args.IsSuccess ? "Done" : "Page load finished with an error");
                        if (tab.View.Source != null)
                            SetAddressText(HomePageService.DisplayUrl(tab.View.Source.AbsoluteUri));
                        UpdateBookmarkButtonState();
                    }

                    if (args.IsSuccess && tab.View.Source != null)
                    {
                        RecordHistory(tab, tab.View.Source.AbsoluteUri, core.DocumentTitle);
                        SaveSession();
                    }
                    SendHomePageState(tab);
                };

                core.NewWindowRequested += (s, args) =>
                {
                    args.Handled = true;
                    if (!current()) return;
                    if (!string.IsNullOrWhiteSpace(args.Uri))
                        CreateNewTab(args.Uri, true, tab.IsPrivate, null);
                };

                core.DownloadStarting += (sender, args) => { if (current()) Core_DownloadStarting(tab, sender, args); else args.Cancel = true; };
                core.ProcessFailed += (s, args) =>
                {
                    if (current()) HandleProcessFailure(tab, args.ProcessFailedKind);
                };

                tab.Initialized = true;
                string firstUrl = tab.PendingUrl;
                tab.PendingUrl = null;
                if (!string.IsNullOrWhiteSpace(firstUrl))
                    core.Navigate(firstUrl);
                else
                    core.Navigate(_settings.HomePage);

                UpdateTabActivity();
            }
            catch (Exception ex)
            {
                if (!current()) return;
                SetStatus("Browser engine could not start");
                MessageBox.Show(this,
                    "Harbor could not initialize the WebView2 browser engine.\n\n" + ex.Message +
                    "\n\nCheck that the Microsoft Edge WebView2 Runtime is installed and up to date.",
                    "Browser startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (ReferenceEquals(view, tab.View)) tab.Initializing = false;
            }
        }

        private void Core_DownloadStarting(BrowserTab tab, object sender, CoreWebView2DownloadStartingEventArgs e)
        {
            if (RebindResumedDownload(tab, e)) return;
            if (tab != null && tab.IsClosing) { e.Cancel = true; return; }
            if (tab != null && tab.IsPrivate)
            {
                if (!ConfirmPrivateDownload())
                {
                    e.Cancel = true;
                    SetStatus("Private download cancelled");
                    return;
                }
            }

            CoreWebView2DownloadOperation operation = e.DownloadOperation;
            if (operation == null)
            {
                SetStatus("WebView2 could not create the download operation");
                return;
            }

            DownloadEntry entry = null;
            try
            {
                Directory.CreateDirectory(_store.DownloadsFolder);
                string suggestedName = DownloadFileNameService.Suggest(operation.ContentDisposition, e.ResultFilePath, operation.Uri, operation.MimeType);
                IEnumerable<string> reservedPaths = _downloads.Select(d => d.FilePath);
                string safeName = DownloadFileNameService.MakeUnique(_store.DownloadsFolder, suggestedName, reservedPaths);
                string fullPath = Path.Combine(_store.DownloadsFolder, safeName);

                // WebView2 requires an absolute path that does not already exist.
                e.ResultFilePath = fullPath;

                entry = new DownloadEntry
                {
                    FileName = safeName,
                    FilePath = fullPath,
                    Status = "Starting",
                    IsActive = true,
                    IsPrivate = tab != null && tab.IsPrivate,
                    PrivateSessionId = tab == null ? null : tab.PrivateSessionId,
                    Operation = operation
                };
                entry.SourceUrl = operation.Uri;
                _downloadOwners[entry] = tab;
                _downloads.Insert(0, entry);

                operation.BytesReceivedChanged += (s, args) => QueueDownloadUpdate(entry, operation);
                operation.StateChanged += (s, args) => QueueDownloadUpdate(entry, operation);

                // Handled only hides WebView2's default dialog. The DownloadOperation
                // continues normally and Harbor supplies its own progress feedback.
                e.Handled = true;
                SetStatus("Download started: " + entry.FileName);
                ShowDownloadNotification("Download started: " + entry.FileName, 0, true);
                QueueDownloadUpdate(entry, operation);
            }
            catch (Exception ex)
            {
                if (entry != null)
                {
                    _downloads.Remove(entry);
                    _downloadOwners.Remove(entry);
                }

                // Fall back to WebView2's built-in download UI if Harbor cannot
                // prepare its custom download tracking for any reason.
                e.Handled = false;
                SetStatus("Harbor could not manage this download: " + ex.Message);
            }
        }

        private void QueueDownloadUpdate(DownloadEntry entry, CoreWebView2DownloadOperation operation)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateDownloadStatus(entry, operation)));
            }
            catch (InvalidOperationException)
            {
                // The window dispatcher may already be shutting down.
            }
        }

        private void UpdateDownloadStatus(DownloadEntry entry, CoreWebView2DownloadOperation operation)
        {
            if (entry == null || operation == null || !ReferenceEquals(entry.Operation, operation) || !_downloads.Contains(entry))
                return;

            try
            {
                entry.IsActive = operation.State == CoreWebView2DownloadState.InProgress;
                entry.CanResume = operation.State == CoreWebView2DownloadState.Interrupted && operation.CanResume;
                entry.BytesReceived = operation.BytesReceived;
                ulong? expected = operation.TotalBytesToReceive;
                entry.TotalBytes = expected.HasValue ? (long?)Math.Min(expected.Value, (ulong)long.MaxValue) : null;
                switch (operation.State)
                {
                    case CoreWebView2DownloadState.Completed:
                        _resumingDownloads.Remove(entry);
                        string filenameNote = RepairCompletedDownload(entry);
                        entry.Status = "Complete";
                        SetStatus("Download complete: " + entry.FileName + filenameNote);
                        ShowDownloadNotification("Download complete: " + entry.FileName + filenameNote, 100, false);
                        break;

                    case CoreWebView2DownloadState.Interrupted:
                        string reason = operation.InterruptReason.ToString();
                        entry.Status = reason == "UserPaused" ? "Paused" : reason == "UserCanceled" ? "Cancelled" : "Interrupted: " + SplitEnumWords(reason);
                        SetStatus("Download interrupted: " + entry.FileName + " (" + SplitEnumWords(reason) + ")");
                        ShowDownloadNotification("Download interrupted: " + entry.FileName + " · " + SplitEnumWords(reason), 0, false);
                        break;

                    default:
                        long received = operation.BytesReceived;
                        ulong? total = operation.TotalBytesToReceive;
                        if (total.HasValue && total.Value > 0)
                        {
                            // WebView2 exposes TotalBytesToReceive as ulong?. Clamp it
                            // before passing it to Harbor's signed-size formatter.
                            long totalBytes = total.Value > (ulong)long.MaxValue
                                ? long.MaxValue
                                : (long)total.Value;
                            double percent = Math.Min(100.0, (received * 100.0) / totalBytes);
                            entry.Status = "Downloading " + percent.ToString("0") + "%";
                            string progress = FormatDownloadSize(received) + " of " + FormatDownloadSize(totalBytes) + " (" + percent.ToString("0") + "%)";
                            SetStatus("Downloading " + entry.FileName + " · " + progress);
                            ShowDownloadNotification("Downloading " + entry.FileName + " · " + progress, percent, true);
                        }
                        else
                        {
                            entry.Status = "Downloading · " + FormatDownloadSize(received);
                            SetStatus("Downloading " + entry.FileName + " · " + FormatDownloadSize(received));
                            ShowDownloadNotification("Downloading " + entry.FileName + " · " + FormatDownloadSize(received), 0, true);
                        }
                        break;
                }
                ReleaseDownloadSource(entry);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Download status update failed: " + ex.Message);
                entry.Status = "Download status unavailable";
            }
        }

        private void ShowDownloadNotification(string message, double progress, bool inProgress)
        {
            if (DownloadNotificationText == null || DownloadNotificationBar == null)
                return;

            DownloadNotificationText.Text = message;
            DownloadNotificationProgress.IsIndeterminate = inProgress && progress <= 0;
            DownloadNotificationProgress.Value = Math.Max(0, Math.Min(100, progress));
            DownloadNotificationBar.Visibility = _presentation != null && _presentation.IsFullScreen ? Visibility.Collapsed : Visibility.Visible;

            if (_downloadNotificationTimer == null)
            {
                _downloadNotificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                _downloadNotificationTimer.Tick += (s, e) =>
                {
                    _downloadNotificationTimer.Stop();
                    DownloadEntry active = _downloads.FirstOrDefault(d => d.Operation != null &&
                        d.Operation.State == CoreWebView2DownloadState.InProgress);
                    if (active != null)
                    {
                        UpdateDownloadStatus(active, active.Operation);
                        return;
                    }
                    DownloadNotificationBar.Visibility = Visibility.Collapsed;
                };
            }

            _downloadNotificationTimer.Stop();
            if (!inProgress)
                _downloadNotificationTimer.Start();
        }

        private static string FormatDownloadSize(long bytes)
        {
            if (bytes < 0) bytes = 0;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return size.ToString(unit == 0 ? "0" : "0.0") + " " + units[unit];
        }

        private static string SplitEnumWords(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unknown reason";
            var result = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1])))
                    result.Append(' ');
                result.Append(value[i]);
            }
            return result.ToString();
        }

        private void RecordHistory(BrowserTab tab, string url, string title)
        {
            if (tab == null || tab.IsClosing || tab.IsPrivate || string.IsNullOrWhiteSpace(url) || url == "about:blank" || HomePageService.IsHomeUrl(url) || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return;
            if (string.Equals(tab.LastRecordedUrl, url, StringComparison.OrdinalIgnoreCase))
                return;

            tab.LastRecordedUrl = url;
            _history.Insert(0, new HistoryEntry
            {
                Title = string.IsNullOrWhiteSpace(title) ? url : title,
                Url = url,
                VisitedAt = DateTime.Now
            });
            if (_history.Count > 2000)
                _history.RemoveRange(2000, _history.Count - 2000);
            SaveHistory();
        }

        private void NavigateCurrent(string input)
        {
            _addressEditing = false;

            AddressSuggestions.IsOpen = false;
            if (IsDownloadsUrl(input)) { OpenDownloadsPage(); return; }
            BrowserTab tab = SelectedTab;
            if (tab == null)
                tab = CreateNewTab(_settings.HomePage, true);

            SearchEngine selectedEngine = SearchEngineBox.SelectedItem as SearchEngine;
            string destination = SearchEngineService.ResolveInput(input, selectedEngine);
            if (tab.NativeContent != null)
            {
                ((DownloadsPage)tab.NativeContent).Dispose();
                tab.NativeContent = null;
                tab.PageTitle = "Loading…";
                UpdateTabHeader(tab);
                ShowTabContent();
            }
            tab.PendingUrl = destination;
            SaveSession();
            if (tab.FailureMessage != null || tab.NeedsNewView) { _ = RecoverTab(tab); return; }

            if (tab.Initialized && tab.View.CoreWebView2 != null)
            {
                tab.PendingUrl = null;
                tab.View.CoreWebView2.Navigate(destination);
            }
            else
            {
                _ = EnsureTabInitializedAsync(tab);
            }
        }

        private void BrowserTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != BrowserTabs || _reorderingTabs)
                return;

            BrowserTab tab = SelectedTab;
            if (tab == null)
                return;

            _addressEditing = false;
            foreach (TabItem item in BrowserTabs.Items) if (!ReferenceEquals(item.Tag, tab)) CancelHomeSuggestions(item.Tag as BrowserTab);
            foreach (TabItem item in BrowserTabs.Items) if (!ReferenceEquals(item.Tag, tab)) CancelPermissions(item.Tag as BrowserTab);
            if (tab.NativeContent != null)
                SetAddressText(DownloadsUrl);
            else if (tab.View.Source != null)
                SetAddressText(HomePageService.DisplayUrl(tab.View.Source.AbsoluteUri));
            else
                SetAddressText(tab.PendingUrl == null ? string.Empty : HomePageService.DisplayUrl(tab.PendingUrl));
            ShowTabContent();
            UpdateBookmarkButtonState();

            UpdateTabActivity();
            SaveSession();
            string title = string.IsNullOrWhiteSpace(tab.PageTitle) ? "New tab" : tab.PageTitle;
            Title = title + " - Harbor Browser";
            if (!tab.Initialized)
                _ = EnsureTabInitializedAsync(tab);
        }

        private void UpdateTabHeader(BrowserTab tab)
        {
            if (tab == null || tab.HeaderText == null)
                return;

            string title = string.IsNullOrWhiteSpace(tab.PageTitle) ? "New tab" : tab.PageTitle;
            tab.HeaderText.Text = (tab.IsSuspended ? "☾ " : "") + title;
            tab.Item.Opacity = tab.IsSuspended ? 0.65 : 1.0;
            tab.HeaderText.Visibility = tab.IsPinned ? Visibility.Collapsed : Visibility.Visible;
            tab.CloseButton.Visibility = tab.IsPinned ? Visibility.Collapsed : Visibility.Visible;
            tab.Item.Width = tab.IsPinned ? 43 : double.NaN;
            tab.HeaderText.MaxWidth = ResponsiveLayout.TabTitleWidth(ActualWidth > 0 ? ActualWidth : Width);
            string url = tab.View != null && tab.View.Source != null ? tab.View.Source.AbsoluteUri : title;
            string description = tab.IsPrivate ? "Private tab" : "Normal tab";
            if (tab.IsPinned) description += " • Pinned";
            if (!string.IsNullOrWhiteSpace(tab.GroupName)) description += " • " + tab.GroupName;
            if (tab.IsSuspended) description += " • Sleeping";
            if (tab.KeepAwake) description += " • Kept awake for this session";
            tab.Item.ToolTip = description + " • " + url;
            if (tab.Item.ContextMenu != null && tab.Item.ContextMenu.Items.Count > 1)
            {
                MenuItem pinItem = tab.Item.ContextMenu.Items[1] as MenuItem;
                if (pinItem != null) pinItem.Header = tab.IsPinned ? "Unpin tab" : "Pin tab";
            }
        }

        private void UpdateTabFavicon(BrowserTab tab)
        {
            if (tab == null || tab.HeaderIcon == null || tab.View == null || tab.View.CoreWebView2 == null) return;
            try
            {
                string favicon = tab.View.CoreWebView2.FaviconUri;
                if (string.IsNullOrWhiteSpace(favicon)) return;
                Uri uri;
                if (!Uri.TryCreate(favicon, UriKind.Absolute, out uri)) return;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 32;
                bitmap.EndInit();
                bitmap.Freeze();
                tab.HeaderIcon.Source = bitmap;
                tab.HeaderIcon.Visibility = Visibility.Visible;
                tab.FallbackIcon.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex) { Debug.WriteLine("Could not load tab favicon: " + ex.Message); }
        }

        private void AddressBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (HandleSuggestionKey(e)) return;
            if (e.Key == Key.Enter)
            {
                NavigateCurrent(AddressBox.Text);
                e.Handled = true;
            }
        }

        private void Go_Click(object sender, RoutedEventArgs e) { NavigateCurrent(AddressBox.Text); }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            BrowserTab tab = SelectedTab;
            if (tab != null && tab.Initialized && tab.View.CoreWebView2.CanGoBack)
                tab.View.CoreWebView2.GoBack();
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            BrowserTab tab = SelectedTab;
            if (tab != null && tab.Initialized && tab.View.CoreWebView2.CanGoForward)
                tab.View.CoreWebView2.GoForward();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            BrowserTab tab = SelectedTab;
            if (tab != null && tab.FailureMessage != null) { _ = RecoverTab(tab); return; }
            if (tab != null && tab.Initialized)
                tab.View.CoreWebView2.Reload();
        }

        private void Home_Click(object sender, RoutedEventArgs e) { NavigateCurrent(_settings.HomePage); }

        private void NewTab_Click(object sender, RoutedEventArgs e) { CreateNewTab(_settings.HomePage, true); }

        private void CloseTab_Click(object sender, RoutedEventArgs e) { CloseSelectedTab(); }

        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            BrowserTab tab = button == null ? null : button.Tag as BrowserTab;
            if (tab != null)
                CloseTab(tab);
        }

        private void CloseSelectedTab()
        {
            BrowserTab tab = SelectedTab;
            if (tab != null)
                CloseTab(tab);
        }

        private void CloseTab(BrowserTab tab)
        {
            if (tab == null || tab.IsClosing)
                return;
            if (!PreparePrivateClose(tab)) return;
            CancelPermissions(tab);
            RememberClosedTab(tab);
            CancelHomeSuggestions(tab);
            tab.IsClosing = true;
            if (tab.NativeContent is DownloadsPage) ((DownloadsPage)tab.NativeContent).Dispose();
            if (tab.VideoPopout != null) tab.VideoPopout.Close();
            if (tab.SuspendTimer != null)
                tab.SuspendTimer.Stop();
            BrowserTabs.Items.Remove(tab.Item);
            PrivateTabClosed(tab);
            ApplyTabGroupFilter();
            SaveSession();
            try
            {
                IDisposable disposable = tab.View as IDisposable;
                if (disposable != null && !RetainDownloadSource(tab))
                    disposable.Dispose();
            }
            catch { }
            if (BrowserTabs.Items.Count == 0)
                CreateNewTab(_settings.HomePage, true);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { ClearBrowsingData_Click(sender, e); e.Handled = true; return; }
            if (AddressSuggestions.IsOpen && e.Key == Key.Escape) { AddressSuggestions.IsOpen = false; e.Handled = true; return; }
            if (e.Key == Key.T && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { ReopenClosedTab(); e.Handled = true; return; }
            if (e.Key == Key.J && Keyboard.Modifiers == ModifierKeys.Control) { OpenDownloadsPage(); e.Handled = true; return; }
            if (e.Key == Key.F11) { FullScreenShortcut(); e.Handled = true; return; }
            if (e.Key == Key.Escape && _presentation != null && _presentation.IsFullScreen) { EscapeFullScreen(); e.Handled = true; return; }
            if (e.Key == Key.P && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift)) { PictureInPicture_Click(sender,e); e.Handled = true; return; }
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift) && (e.Key == Key.PageUp || e.Key == Key.PageDown))
            { MoveTabRelative(SelectedTab, e.Key == Key.PageUp ? -1 : 1); e.Handled = true; return; }
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.N)
            {
                CreateNewTab(_settings.HomePage, true, true, null);
                e.Handled = true;
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.T) { CreateNewTab(_settings.HomePage, true); e.Handled = true; }
                else if (e.Key == Key.W) { CloseSelectedTab(); e.Handled = true; }
                else if (e.Key == Key.L) { AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; }
                else if (e.Key == Key.R) { Reload_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.D) { AddBookmark(); e.Handled = true; }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left)
            {
                Back_Click(this, new RoutedEventArgs()); e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Right)
            {
                Forward_Click(this, new RoutedEventArgs()); e.Handled = true;
            }
        }

        private void AddBookmark_Click(object sender, RoutedEventArgs e) { AddBookmark(); }

        private Bookmark FindBookmark(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            return _bookmarks.FirstOrDefault(b => b != null && string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));
        }

        private void ToggleCurrentBookmark_Click(object sender, RoutedEventArgs e)
        {
            BrowserTab tab = SelectedTab;
            if (tab == null || tab.View == null || tab.View.Source == null)
            {
                SetStatus("Open a web page before adding a bookmark");
                return;
            }
            Bookmark existing = FindBookmark(tab.View.Source.AbsoluteUri);
            if (existing != null)
                RemoveBookmark(existing);
            else
                AddBookmark();
        }

        private void UpdateBookmarkButtonState()
        {
            if (BookmarkToggleButton == null) return;
            BrowserTab tab = SelectedTab;
            string url = tab != null && tab.View != null && tab.View.Source != null ? tab.View.Source.AbsoluteUri : null;
            bool saved = FindBookmark(url) != null;
            BookmarkToggleButton.Content = saved ? "★" : "☆";
            BookmarkToggleButton.SetResourceReference(Control.ForegroundProperty, saved ? "HarborAccent" : "HarborMuted");
            BookmarkToggleButton.ToolTip = saved ? "Remove bookmark" : "Bookmark this page";
            BookmarkToggleButton.IsEnabled = !string.IsNullOrWhiteSpace(url) && IsRestorableUrl(url) && !HomePageService.IsHomeUrl(url);
        }

        private void AddBookmark()
        {
            BrowserTab tab = SelectedTab;
            if (tab == null || !tab.Initialized || tab.View.Source == null || HomePageService.IsHomeUrl(tab.View.Source.AbsoluteUri))
            {
                SetStatus("Open a page before bookmarking it");
                return;
            }

            if (tab.IsPrivate && MessageBox.Show(this,
                "Bookmarks are saved to your normal browser profile and will remain after this private tab closes. Add this bookmark anyway?",
                "Private browsing", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            string url = tab.View.Source.AbsoluteUri;
            if (FindBookmark(url) != null)
            {
                SetStatus("That page is already bookmarked");
                UpdateBookmarkButtonState();
                return;
            }

            _bookmarks.Insert(0, new Bookmark
            {
                Title = string.IsNullOrWhiteSpace(tab.View.CoreWebView2.DocumentTitle) ? url : tab.View.CoreWebView2.DocumentTitle,
                Url = url
            });
            SaveBookmarks();
            RefreshBookmarksBar();
            UpdateBookmarkButtonState();
            SetStatus("Bookmark added");
        }

        private void RemoveBookmark(Bookmark bookmark)
        {
            if (bookmark == null) return;
            _bookmarks.Remove(bookmark);
            SaveBookmarks();
            RefreshBookmarksBar();
            UpdateBookmarkButtonState();
            SetStatus("Bookmark removed");
        }

        private string PromptForText(string title, string prompt, string initialValue)
        {
            var dialog = new Window
            {
                Title = title, Owner = this, Width = 390, Height = 158, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, 
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };
            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 7) });
            var text = new TextBox { Text = initialValue ?? string.Empty, Height = 28, Padding = new Thickness(5, 3, 5, 3) };
            panel.Children.Add(text);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = "OK", Width = 76, IsDefault = true, Margin = new Thickness(0, 0, 7, 0) };
            var cancel = new Button { Content = "Cancel", Width = 76, IsCancel = true };
            ok.Click += (s, e) => { dialog.DialogResult = true; };
            buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons);
            dialog.Content = panel;
            dialog.Loaded += (s, e) => { text.Focus(); text.SelectAll(); };
            WindowFitService.Attach(dialog);
            return dialog.ShowDialog() == true ? text.Text.Trim() : null;
        }

        private void RenameBookmark(Bookmark bookmark)
        {
            if (bookmark == null) return;
            string name = PromptForText("Rename bookmark", "Bookmark name:", bookmark.Title);
            if (string.IsNullOrWhiteSpace(name)) return;
            bookmark.Title = name;
            SaveBookmarks();
            RefreshBookmarksBar();
            SetStatus("Bookmark renamed");
        }

        private ContextMenu BuildBookmarkContextMenu(Bookmark bookmark)
        {
            var menu = new ContextMenu();
            AddContextItem(menu, "Open", (s, e) => NavigateCurrent(bookmark.Url));
            AddContextItem(menu, "Open in new tab", (s, e) => CreateNewTab(bookmark.Url, true));
            menu.Items.Add(new Separator());
            AddContextItem(menu, "Rename…", (s, e) => RenameBookmark(bookmark));
            AddContextItem(menu, "Remove bookmark", (s, e) => RemoveBookmark(bookmark));
            return menu;
        }

        private void RefreshBookmarksBar()
        {
            BookmarksBarPanel.Children.Clear();
            foreach (Bookmark bookmark in _bookmarks.Where(b => string.IsNullOrEmpty(b.Folder)))
            {
                var button = new Button
                {
                    Content = string.IsNullOrWhiteSpace(bookmark.Title) ? bookmark.Url : bookmark.Title,
                    Tag = bookmark,
                    Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(2, 1, 2, 1),
                    MaxWidth = 190, ToolTip = bookmark.Url, Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0), ContextMenu = BuildBookmarkContextMenu(bookmark)
                };
                button.Click += BookmarkBarButton_Click;
                BookmarksBarPanel.Children.Add(button);
            }
            foreach (string folder in BookmarkLibraryService.Folders(_bookmarks, _settings.BookmarkFolders).Where(p => !p.Contains("/")))
            {
                var button = new Button { Content = folder + " ▾", MaxWidth = 190, ToolTip = folder, Margin = new Thickness(2, 1, 2, 1), Padding = new Thickness(7, 2, 7, 2), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
                button.Click += (s, e) => { var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom }; FillBookmarkFolder(menu, folder); if (menu.Items.Count == 0) AddGearCommand(menu, "Empty folder", null, false); menu.IsOpen = true; };
                BookmarksBarPanel.Children.Add(button);
            }
        }

        private void BookmarkBarButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Bookmark bookmark = button == null ? null : button.Tag as Bookmark;
            if (bookmark != null) NavigateCurrent(bookmark.Url);
        }


        private void SendHomePageState(BrowserTab tab)
        {
            if (tab == null || !tab.Initialized || tab.View.CoreWebView2 == null ||
                !HomePageService.IsHomeUrl(tab.View.CoreWebView2.Source)) return;
            tab.View.CoreWebView2.PostWebMessageAsJson(HomePageService.CreateState(
                SearchEngineService.GetById(_settings.SearchEngineId), _bookmarks, tab.IsPrivate, _settings,
                _settings.HomeScene == "Custom" ? BackgroundImageService.ReadDataUri(_store.RootFolder, _settings.HomeBackgroundFile) : null));
        }

        private void RefreshHomePages()
        {
            foreach (TabItem item in BrowserTabs.Items)
            {
                BrowserTab tab = item.Tag as BrowserTab;
                try { SendHomePageState(tab); }
                catch (InvalidOperationException) { }
            }
        }


        private void SearchEngineBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready)
                return;
            SearchEngine engine = SearchEngineBox.SelectedItem as SearchEngine;
            if (engine != null)
            {
                CancelOnlineSuggestions();
                foreach (TabItem item in BrowserTabs.Items) CancelHomeSuggestions(item.Tag as BrowserTab);
                _settings.SearchEngineId = engine.Id;
                SaveSettings();
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e) { OpenSettings(false); }
        private void OpenSettings(bool customizeHome)
        {
            var dialog = new SettingsWindow(_settings, _bookmarks) { Owner = this };
            if (customizeHome) dialog.Loaded += (s, e) => dialog.HomeCustomizationGroup.BringIntoView();
            WindowFitService.Attach(dialog);
            if (dialog.ShowDialog() == true && dialog.ResultSettings != null)
            {
                if (dialog.SelectedBackgroundPath != null)
                {
                    try { dialog.ResultSettings.HomeBackgroundFile = BackgroundImageService.Import(_store.RootFolder, dialog.SelectedBackgroundPath); }
                    catch (Exception ex) { MessageBox.Show(this, "The settings were not applied because the background could not be saved. " + ex.Message, "Home background"); return; }
                }
                CancelOnlineSuggestions();
                foreach (TabItem item in BrowserTabs.Items) CancelHomeSuggestions(item.Tag as BrowserTab);
                _settings = dialog.ResultSettings;
                ApplyInterfaceSettings();
                SaveSettings();
                foreach (TabItem item in BrowserTabs.Items)
                {
                    BrowserTab openTab = item.Tag as BrowserTab;
                    if (openTab != null && openTab.Initialized)
                    {
                        openTab.View.CoreWebView2.Settings.AreDevToolsEnabled = _settings.EnableDeveloperTools;
                        openTab.View.CoreWebView2.Profile.PreferredTrackingPreventionLevel = GetTrackingPreventionLevel();
                        ConfigureTrackerProtection(openTab);
                    }
                }
                UpdateTabActivity();
                SaveSession();
                SetStatus("Settings saved");
            }
        }

        private void NewPrivateTab_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab(_settings.HomePage, true, true, null);
            SetStatus("Private tab opened. Its history and cookies are isolated from your normal profile.");
        }

        private void TogglePinTab_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            BrowserTab tab = item == null ? null : item.Tag as BrowserTab;
            if (tab != null) TogglePin(tab);
        }

        private void TogglePin(BrowserTab tab)
        {
            tab.IsPinned = !tab.IsPinned;
            ReorderPinnedTabs();
            foreach (TabItem item in BrowserTabs.Items)
                UpdateTabHeader(item.Tag as BrowserTab);
            ApplyTabGroupFilter();
            SaveSession();
        }

        private void TabGroupMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            if (item != null && SelectedTab != null)
                SetTabGroup(SelectedTab, Convert.ToString(item.Tag));
        }

        private void TabGroupContext_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            TabGroupAction action = item == null ? null : item.Tag as TabGroupAction;
            if (action != null) SetTabGroup(action.Tab, action.GroupName);
        }

        private void SetTabGroup(BrowserTab tab, string groupName)
        {
            if (tab == null) return;
            tab.GroupName = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
            AddGroupNameIfMissing(tab.GroupName);
            tab.Item.ContextMenu = BuildTabContextMenu(tab);
            UpdateTabHeader(tab);
            RefreshGroupFilterBox(_activeGroupFilter);
            ApplyTabGroupFilter();
            SaveSession();
        }

        private string GetSelectedGroupForNewTab()
        {

            string filter = _activeGroupFilter;
            if (string.IsNullOrEmpty(filter) || filter == "*" || !_tabGroups.Contains(filter)) return null;
            return filter;
        }

        private bool IsTabVisibleForCurrentGroup(BrowserTab tab)
        {
            if (tab == null) return false;
            string filter = _activeGroupFilter;
            if (filter == null || filter == "*") return true;
            if (tab.IsPinned) return true;
            if (filter.Length == 0) return string.IsNullOrWhiteSpace(tab.GroupName);
            return string.Equals(tab.GroupName, filter, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyTabGroupFilter()
        {
            if (BrowserTabs == null) return;
            int visible = 0;
            TabItem firstVisible = null;
            foreach (TabItem item in BrowserTabs.Items)
            {
                BrowserTab tab = item.Tag as BrowserTab;
                bool show = IsTabVisibleForCurrentGroup(tab);
                item.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (show)
                {
                    visible++;
                    if (firstVisible == null) firstVisible = item;
                }
            }

            BrowserTab selected = SelectedTab;
            if (selected != null && selected.Item.Visibility != Visibility.Visible)
                BrowserTabs.SelectedItem = firstVisible;
            else if (selected == null && firstVisible != null)
                BrowserTabs.SelectedItem = firstVisible;
            if (BrowserContentHost != null) ShowTabContent();


            if (visible == 0 && _activeGroupFilter != "*")
                SetStatus("No tabs in this group. New tabs will be added to the selected group.");
        }

        private void ManageTabGroups_Click(object sender, RoutedEventArgs e)
        {
            var menu=new ContextMenu();
            menu.Items.Add(CreateTabGroupsMenu());menu.PlacementTarget=GearMenuButton;menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;
        }
        private UIElement ManageGroupsButtonFallback()
        {
            return GearMenuButton;
        }

        private static bool IsReservedGroupName(string name)
        {
            return string.Equals(name, "*", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "All tabs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Ungrouped", StringComparison.OrdinalIgnoreCase);
        }

        private void AddTabGroup()
        {
            string name = PromptForText("Add tab group", "New group name:", string.Empty);
            if (string.IsNullOrWhiteSpace(name)) return;
            if (IsReservedGroupName(name))
            {
                MessageBox.Show(this, "That name is reserved for a built-in tab filter. Choose another name.", "Tab groups", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_tabGroups.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "A group with that name already exists.", "Tab groups", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _tabGroups.Add(name);
            RefreshGroupFilterBox(name);
            RefreshTabContextMenus();
            ApplyTabGroupFilter();
            SaveSession();
            SetStatus("Tab group created: " + name);
        }

        private void RenameTabGroup(string oldName)
        {
            if (string.IsNullOrWhiteSpace(oldName)) return;
            string newName = PromptForText("Rename tab group", "New group name:", oldName);
            if (string.IsNullOrWhiteSpace(newName) || string.Equals(oldName, newName, StringComparison.Ordinal)) return;
            if (IsReservedGroupName(newName))
            {
                MessageBox.Show(this, "That name is reserved for a built-in tab filter. Choose another name.", "Tab groups", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_tabGroups.Any(g => !string.Equals(g, oldName, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(g, newName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "A group with that name already exists.", "Tab groups", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int index = _tabGroups.FindIndex(g => string.Equals(g, oldName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _tabGroups[index] = newName;
            foreach (TabItem item in BrowserTabs.Items)
            {
                BrowserTab tab = item.Tag as BrowserTab;
                if (tab != null && string.Equals(tab.GroupName, oldName, StringComparison.OrdinalIgnoreCase))
                {
                    tab.GroupName = newName;
                    UpdateTabHeader(tab);
                }
            }
            RefreshTabContextMenus();
            RefreshGroupFilterBox(newName);
            ApplyTabGroupFilter();
            SaveSession();
            SetStatus("Tab group renamed");
        }

        private void RemoveTabGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (MessageBox.Show(this, "Remove the '" + name + "' group? Tabs in this group will become ungrouped.",
                "Remove tab group", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _tabGroups.RemoveAll(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));
            foreach (TabItem item in BrowserTabs.Items)
            {
                BrowserTab tab = item.Tag as BrowserTab;
                if (tab != null && string.Equals(tab.GroupName, name, StringComparison.OrdinalIgnoreCase))
                {
                    tab.GroupName = null;
                    UpdateTabHeader(tab);
                }
            }
            RefreshTabContextMenus();
            RefreshGroupFilterBox("*");
            ApplyTabGroupFilter();
            SaveSession();
            SetStatus("Tab group removed");
        }

        private void RefreshTabContextMenus()
        {
            foreach (TabItem item in BrowserTabs.Items)
            {
                BrowserTab tab = item.Tag as BrowserTab;
                if (tab != null) tab.Item.ContextMenu = BuildTabContextMenu(tab);
            }
        }

        private CoreWebView2TrackingPreventionLevel GetTrackingPreventionLevel()
        {
            CoreWebView2TrackingPreventionLevel level;
            string savedLevel = _settings == null ? "Balanced" : _settings.TrackingPreventionLevel;
            if (Enum.TryParse(savedLevel ?? "Balanced", true, out level))
                return level;
            return CoreWebView2TrackingPreventionLevel.Balanced;
        }

        private void ReorderPinnedTabs()
        {
            BrowserTab selected = SelectedTab;
            var ordered = BrowserTabs.Items.Cast<TabItem>()
                .Select(i => i.Tag as BrowserTab)
                .Where(t => t != null)
                .OrderByDescending(t => t.IsPinned)
                .ToList();
            _reorderingTabs = true;
            try
            {
                BrowserTabs.Items.Clear();
                foreach (BrowserTab tab in ordered) BrowserTabs.Items.Add(tab.Item);
                if (selected != null) BrowserTabs.SelectedItem = selected.Item;
            }
            finally { _reorderingTabs = false; }
            UpdateTabActivity();
        }

        private static bool IsRestorableUrl(string value)
        {
            if (IsDownloadsUrl(value)) return true;
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private void SaveSession()
        {
            if (_store == null || !_sessionWritesAllowed || _store.IsReadOnlyForNewerDataVersion || _restoringSession) return;
            _lastSessionSaveSucceeded = false;
            try
            {
                var session = new BrowserSession { SavedAt = DateTime.Now, SelectedTabIndex = 0, Groups = new List<string>(_tabGroups), GroupsConfigured = true };
                int selectedSavedIndex = -1;
                foreach (TabItem item in BrowserTabs.Items)
                {
                    BrowserTab tab = item.Tag as BrowserTab;
                    if (tab == null || tab.IsPrivate || tab.IsClosing) continue;
                    string url = SafeTabUrl(tab);
                    if (!IsRestorableUrl(url)) continue;
                    if (tab == SelectedTab) selectedSavedIndex = session.Tabs.Count;
                    session.Tabs.Add(new BrowserSessionTab { Url = url, IsPinned = tab.IsPinned, GroupName = tab.GroupName });
                }
                if (selectedSavedIndex >= 0) session.SelectedTabIndex = selectedSavedIndex;
                _store.SaveSession(session);
                _lastSessionSaveSucceeded = true;
            }
            catch (Exception ex) { Debug.WriteLine("Could not save the tab session: " + ex.Message); }
        }

        private void RestoreSession_Click(object sender, RoutedEventArgs e)
        {
            BrowserSession session = _store.LoadSession();
            if (session == null || session.Tabs == null || session.Tabs.Count == 0)
            {
                SetStatus("No saved browsing session was found");
                return;
            }
            bool hasTabs = BrowserTabs.Items.Count > 0;
            if (hasTabs && MessageBox.Show(this, "Open the saved session in addition to the tabs already open?", "Restore session",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var restoredTabs = new List<BrowserTab>();
            _restoringSession = true;
            try
            {
                foreach (BrowserSessionTab savedTab in session.Tabs)
                {
                    if (savedTab == null || !IsRestorableUrl(savedTab.Url)) continue;
                    restoredTabs.Add(CreateNewTab(savedTab.Url, false, false, savedTab));
                }
                ApplyTabGroupFilter();
                if (restoredTabs.Count > 0)
                {
                    int selected = Math.Max(0, Math.Min(session.SelectedTabIndex, restoredTabs.Count - 1));
                    BrowserTab selectedTab = restoredTabs[selected];
                    if (selectedTab.Item.Visibility == Visibility.Visible)
                        BrowserTabs.SelectedItem = selectedTab.Item;
                }
            }
            finally { _restoringSession = false; }
            SaveSession();
        }

        private void ResourceMonitor_Click(object sender, RoutedEventArgs e)
        {
            var monitor = new ResourceMonitorWindow(BuildResourceReport, MonitorActions()) { Owner = this };
            monitor.Show();
        }

        private string BuildResourceReport()
        {
            var lines = new List<string>();
            var sampledIds = new List<int>();
            long sampleTime = Stopwatch.GetTimestamp();
            using (Process self = Process.GetCurrentProcess())
            {
                sampledIds.Add(self.Id);
                lines.Add("HARBOR APPLICATION");
                lines.Add("  PID:             " + self.Id);
                lines.Add("  Working set:     " + FormatMemory(self.WorkingSet64));
                lines.Add("  Private memory:  " + FormatMemory(self.PrivateMemorySize64));
                lines.Add("  CPU (sample):    " + SampleHarborCpu(self));
            }
            int total = BrowserTabs.Items.Count;
            int sleeping = BrowserTabs.Items.Cast<TabItem>().Select(i => i.Tag as BrowserTab).Count(t => t != null && t.IsSuspended);
            int pinned = BrowserTabs.Items.Cast<TabItem>().Select(i => i.Tag as BrowserTab).Count(t => t != null && t.IsPinned);
            int privateTabs = BrowserTabs.Items.Cast<TabItem>().Select(i => i.Tag as BrowserTab).Count(t => t != null && t.IsPrivate);
            lines.Add(string.Empty);
            lines.Add("TABS");
            lines.Add("  Open: " + total + "   Sleeping: " + sleeping + "   Pinned: " + pinned + "   Private: " + privateTabs);
            lines.Add("  Tracker requests blocked: " + System.Threading.Interlocked.Read(ref _blockedTrackerRequests));
            lines.Add(string.Empty);
            lines.Add("WEBVIEW2 PROCESSES (Harbor profile)");
            if (_environment == null)
            {
                lines.Add("  Browser engine has not initialized yet.");
            }
            else
            {
                long working = 0, privateBytes = 0;
                int found = 0;
                try
                {
                    var processes = _environment.GetProcessInfos();
                    foreach (CoreWebView2ProcessInfo info in processes)
                    {
                        try
                        {
                            using (Process process = Process.GetProcessById(info.ProcessId))
                            {
                                sampledIds.Add(process.Id);
                                working += process.WorkingSet64;
                                privateBytes += process.PrivateMemorySize64;
                                found++;
                                lines.Add("  PID " + info.ProcessId.ToString().PadRight(7) + " " + info.Kind.ToString().PadRight(12) +
                                    " WS " + FormatMemory(process.WorkingSet64).PadLeft(10) + "  Private " + FormatMemory(process.PrivateMemorySize64) + "  CPU " + SampleProcessCpu(process, sampleTime));
                            }
                        }
                        catch (ArgumentException) { }
                        catch (InvalidOperationException) { }
                    }
                }
                catch (Exception ex) { lines.Add("  Process snapshot unavailable: " + ex.Message); }
                lines.Add("  Processes: " + found + "   Combined working set: " + FormatMemory(working) +
                    "   Combined private: " + FormatMemory(privateBytes));
            }
            lines.Add(string.Empty);
            lines.Add("Note: WebView2 can share renderer processes between tabs. Memory is therefore shown by process, not guessed per tab.");
            _processCpu.Retain(sampledIds);
            lines.Add("CPU is a sample of total machine capacity; shared working-set pages may be counted more than once.");
            lines.Add("Auto-refresh: every 2 seconds.");
            lines.Add("Snapshot: " + DateTime.Now.ToString("G"));
            return string.Join(Environment.NewLine, lines);
        }

        private string SampleHarborCpu(Process process) { return SampleProcessCpu(process, Stopwatch.GetTimestamp()); }

        private static string FormatMemory(long bytes)
        {
            return (bytes / (1024.0 * 1024.0)).ToString("N1") + " MB";
        }

        private void ManageCookies_Click(object sender, RoutedEventArgs e)
        {
            BrowserTab tab = SelectedTab;
            if (tab == null || !tab.Initialized || tab.View.CoreWebView2 == null)
            {
                MessageBox.Show(this, "Open a page first so Harbor knows which site's cookies to show.", "Cookie Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string uri = tab.View.Source == null ? string.Empty : tab.View.Source.AbsoluteUri;
            Uri parsed;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                uri = string.Empty;
            var dialog = new CookieManagerWindow(tab.View.CoreWebView2.CookieManager, uri) { Owner = this };
            dialog.ShowDialog();
        }

        private void ConfigureTrackerProtection(BrowserTab tab)
        {
            if (tab == null || !_settings.EnableTrackerBlocking || tab.View == null ||
                tab.View.CoreWebView2 == null || tab.TrackerFilterRegistered)
                return;
            CoreWebView2 core = tab.View.CoreWebView2;
            try
            {
                TrackerProtectionService.RegisterFilters(core, Core_WebResourceRequested);
                tab.TrackerFilterRegistered = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Harbor could not initialize tracker protection: " + ex.Message);
            }
        }

        private void Core_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (!_settings.EnableTrackerBlocking || e.Request == null) return;
            Uri uri;
            if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out uri)) return;
            if (!TrackerProtectionService.IsBlockedHost(uri.Host)) return;
            try
            {
                CoreWebView2 core = sender as CoreWebView2;
                if (core != null)
                {
                    e.Response = core.Environment.CreateWebResourceResponse(null, 204, "Blocked by Harbor", "Content-Type: text/plain");
                    System.Threading.Interlocked.Increment(ref _blockedTrackerRequests);
                }
            }
            catch (Exception ex) { Debug.WriteLine("Tracker request could not be blocked: " + ex.Message); }
        }

        private void GearMenuButton_Click(object sender, RoutedEventArgs e)
        {
            BuildGearMenu().IsOpen = true;
        }

        internal ContextMenu BuildGearMenu()
        {
            // The gear sits at the far right of the window. RTL menu flow makes flyouts
            // open toward the left so they stay inside the browser window.
            var menu = new ContextMenu { FlowDirection = FlowDirection.RightToLeft };

            MenuItem bookmarks = CreateGearSubmenu("Bookmarks (" + _bookmarks.Count + ")");
            AddGearCommand(bookmarks, "Manage bookmarks…", ManageBookmarks_Click);
            AddGearCommand(bookmarks, "Bookmark current page", (s, args) => AddBookmark());
            if (_bookmarks.Count > 0 || (_settings.BookmarkFolders != null && _settings.BookmarkFolders.Count > 0))
            {
                AddGearSeparator(bookmarks);
                FillBookmarkFolder(bookmarks, string.Empty);
            }
            else
            {
                AddGearCommand(bookmarks, "No bookmarks yet", null, false);
            }
            menu.Items.Add(bookmarks);

            MenuItem history = CreateGearSubmenu("History (" + _history.Count + ")");
            AddGearCommand(history, "Reopen closed tab (Ctrl+Shift+T)", ReopenClosedTab_Click, _recentlyClosed.Count > 0);
            AddGearCommand(history, "Show all history…", ShowHistoryManager_Click);
            AddGearCommand(history, "Clear browsing history…", ClearHistory_Click);
            if (_history.Count > 0)
            {
                AddGearSeparator(history);
                foreach (HistoryEntry entry in _history.OrderByDescending(h => h.VisitedAt).Take(30).ToList())
                {
                    string title = string.IsNullOrWhiteSpace(entry.Title) ? entry.Url : entry.Title;
                    MenuItem historyEntry = CreateGearSubmenu(title + "  ·  " + entry.VisitedAt.ToString("g"));
                    AddGearCommand(historyEntry, "Open", (s, args) => NavigateCurrent(entry.Url));
                    AddGearCommand(historyEntry, "Open in new tab", (s, args) => CreateNewTab(entry.Url, true));
                    AddGearSeparator(historyEntry);
                    AddGearCommand(historyEntry, "Remove from history", (s, args) =>
                    {
                        _history.Remove(entry);
                        SaveHistory();
                        SetStatus("History entry removed");
                    });
                    history.Items.Add(historyEntry);
                }
            }
            else
            {
                AddGearCommand(history, "No history yet", null, false);
            }
            menu.Items.Add(history);

            var visibleDownloads = _downloads.Where(d => d.IsPrivate == (SelectedTab != null && SelectedTab.IsPrivate) && (!d.IsPrivate || d.PrivateSessionId == SelectedTab.PrivateSessionId)).ToList();
            MenuItem downloads = CreateGearSubmenu("Downloads (" + visibleDownloads.Count + ")");
            AddGearCommand(downloads, "Show all downloads (Ctrl+J)", ShowDownloads_Click);
            AddGearCommand(downloads, "Open downloads folder", OpenDownloads_Click);
            AddGearCommand(downloads, "Clear completed downloads", (s, args) => ClearCompletedDownloads());
            if (visibleDownloads.Count > 0)
            {
                AddGearSeparator(downloads);
                foreach (DownloadEntry download in visibleDownloads)
                {
                    MenuItem downloadEntry = CreateGearSubmenu(download.FileName + "  ·  " + download.Status);
                    if (download.CanCancel)
                        AddGearCommand(downloadEntry, "Cancel download", (s, args) => CancelDownload(download));
                    if (download.CanResume)
                        AddGearCommand(downloadEntry, "Resume download", (s, args) => ResumeDownload(download));
                    AddGearCommand(downloadEntry, "Open file", (s, args) => OpenDownloadedFile(download), download.Status == "Complete");
                    AddGearCommand(downloadEntry, "Show in folder", (s, args) => ShowDownloadedFile(download), File.Exists(download.FilePath));
                    AddGearSeparator(downloadEntry);
                    bool canRemoveFromList = download.CanRemove;
                    AddGearCommand(downloadEntry, "Remove from list", (s, args) =>
                    {
                        DownloadAction(download, "remove");
                        SetStatus("Download removed from list");
                    }, canRemoveFromList);
                    downloads.Items.Add(downloadEntry);
                }
            }
            else
            {
                AddGearCommand(downloads, "No downloads yet", null, false);
            }
            menu.Items.Add(downloads);

            menu.Items.Add(CreateTabGroupsMenu());
            MenuItem vpn = CreateGearSubmenu("VPN");
            List<string> profiles = VpnService.GetConfiguredProfiles();
            if (profiles.Count > 0)
            {
                foreach (string profile in profiles)
                {
                    AddGearCommand(vpn, "Connect: " + profile, async (s, args) =>
                    {
                        SetStatus("Connecting to " + profile + "…");
                        string result = await Task.Run(() => VpnService.Connect(profile));
                        MessageBox.Show(this, result, "VPN connection", MessageBoxButton.OK,
                            result.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) ? MessageBoxImage.Warning : MessageBoxImage.Information);
                        SetStatus("VPN command finished");
                    });
                }
            }
            else
            {
                AddGearCommand(vpn, "No VPN profiles found", null, false);
            }
            AddGearSeparator(vpn);
            AddGearCommand(vpn, "Disconnect VPN", async (s, args) =>
            {
                SetStatus("Disconnecting VPN…");
                string result = await Task.Run(() => VpnService.Disconnect());
                MessageBox.Show(this, result, "VPN disconnect", MessageBoxButton.OK, MessageBoxImage.Information);
                SetStatus("VPN disconnect command finished");
            });
            AddGearCommand(vpn, "Windows VPN settings…", VpnSettings_Click);
            menu.Items.Add(vpn);

            MenuItem tools = CreateGearSubmenu("Tools");
            AddGearCommand(tools, "Site permissions…", SitePermissions_Click);
            AddGearCommand(tools, "Clear browsing data… (Ctrl+Shift+Delete)", ClearBrowsingData_Click);
            AddGearCommand(tools, "Restore recovery snapshot", RestoreRecovery_Click, _sessionRecovery != null && _sessionRecovery.Snapshot != null && _sessionRecovery.Snapshot.Session != null);
            AddGearCommand(tools, "Browser full screen (F11)", FullScreen_Click);
            AddGearCommand(tools, "Picture in picture (Ctrl+Shift+P)", PictureInPicture_Click);
            AddGearCommand(tools, "Resource monitor", ResourceMonitor_Click);
            AddGearCommand(tools, "Manage cookies for this site", ManageCookies_Click);
            menu.Items.Add(tools);

            MenuItem help = CreateGearSubmenu("Help");
            AddGearCommand(help, "About Harbor Browser", About_Click);
            menu.Items.Add(help);

            // Settings is intentionally the final item in the gear menu.
            MenuItem settings = CreateGearMenuItem("Settings…");
            settings.Click += Settings_Click;
            menu.Items.Add(settings);

            menu.PlacementTarget = GearMenuButton;
            menu.Placement = PlacementMode.Bottom;
            return menu;
        }

        private MenuItem CreateGearMenuItem(string header)
        {
            return new MenuItem
            {
                Header = new TextBlock { Text = header, FlowDirection = FlowDirection.LeftToRight, TextTrimming = TextTrimming.CharacterEllipsis },
                FlowDirection = FlowDirection.RightToLeft,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
        }

        private MenuItem CreateGearSubmenu(string header)
        {
            return CreateGearMenuItem(header);
        }

        private void AddGearCommand(ItemsControl parent, string header, RoutedEventHandler handler, bool enabled = true)
        {
            MenuItem item = CreateGearMenuItem(header);
            item.IsEnabled = enabled;
            if (handler != null) item.Click += handler;
            parent.Items.Add(item);
        }

        private void AddGearSeparator(ItemsControl parent)
        {
            parent.Items.Add(new Separator { Style = (Style)Application.Current.FindResource("HarborMenuSeparator") });
        }

        private void ClearCompletedDownloads()
        {
            ClearCompletedDownloads(SelectedTab != null && SelectedTab.IsPrivate, SelectedTab == null ? null : SelectedTab.PrivateSessionId);
        }

        private void CancelDownload(DownloadEntry download)
        {
            try
            {
                if (download != null) _resumingDownloads.Remove(download);
                if (download != null && download.Operation != null && download.CanCancel)
                    download.Operation.Cancel();
            }
            catch (Exception ex)
            {
                SetStatus("Could not cancel download: " + ex.Message);
            }
        }

        private void ResumeDownload(DownloadEntry download)
        {
            try
            {
                if (download != null && download.Operation != null && download.Operation.State == CoreWebView2DownloadState.Interrupted && download.Operation.CanResume)
                {
                    _resumingDownloads.Remove(download);
                    if (_resumingDownloads.Any(d => d.SourceUrl == download.SourceUrl)) { SetStatus("A download is resuming. Try again in a moment."); return; }
                    _resumingDownloads.Add(download);
                    download.Operation.Resume();
                }
                else if (download != null && download.Operation != null)
                {
                    UpdateDownloadStatus(download, download.Operation);
                    SetStatus("This download cannot currently be resumed. Download it again from the website.");
                }
            }
            catch (Exception ex)
            {
                if (download != null) _resumingDownloads.Remove(download);
                SetStatus("Could not resume download: " + ex.Message);
            }
        }

        private void OpenDownloadedFile(DownloadEntry download)
        {
            if (download == null || !File.Exists(download.FilePath))
            {
                MessageBox.Show(this, "That download is not available on disk.", "Downloads", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Process.Start(new ProcessStartInfo(download.FilePath) { UseShellExecute = true });
        }

        private void ShowDownloadedFile(DownloadEntry download)
        {
            if (download == null || !File.Exists(download.FilePath))
            {
                MessageBox.Show(this, "That download is not available on disk.", "Downloads", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + download.FilePath + "\"") { UseShellExecute = true });
        }

        private void VpnSettings_Click(object sender, RoutedEventArgs e) { VpnService.OpenWindowsVpnSettings(); }
        private void OpenDownloads_Click(object sender, RoutedEventArgs e) { OpenDownloadsFolder(); }

        private void OpenDownloadsFolder()
        {
            try
            {
                Directory.CreateDirectory(_store.DownloadsFolder);
                Process.Start(new ProcessStartInfo(_store.DownloadsFolder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not open downloads folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e) { ClearHistory(); }

        private void ShowHistoryManager_Click(object sender, RoutedEventArgs e)
        {
            var window = new Window
            {
                Title = "Browsing history",
                Owner = this,
                Width = 900,
                Height = 570,
                MinWidth = 650,
                MinHeight = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };

            var layout = new DockPanel { Margin = new Thickness(12) };
            var searchRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            searchRow.Children.Add(new TextBlock { Text = "Filter history", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            var searchBox = new TextBox { Width = 420, Height = 29, VerticalContentAlignment = VerticalAlignment.Center };
            searchRow.Children.Add(searchBox);
            DockPanel.SetDock(searchRow, Dock.Top);
            layout.Children.Add(searchRow);

            var actions = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(actions, Dock.Bottom);
            layout.Children.Add(actions);

            var list = new ListView();
            var columns = new GridView();
            columns.Columns.Add(new GridViewColumn { Header = "Page", Width = 220, DisplayMemberBinding = new System.Windows.Data.Binding("Title") });
            columns.Columns.Add(new GridViewColumn { Header = "Address", Width = 440, DisplayMemberBinding = new System.Windows.Data.Binding("Url") });
            columns.Columns.Add(new GridViewColumn { Header = "Visited", Width = 145, DisplayMemberBinding = new System.Windows.Data.Binding("VisitedAt") { StringFormat = "g" } });
            list.View = columns;
            layout.Children.Add(list);

            Action refresh = () =>
            {
                string query = searchBox.Text.Trim();
                IEnumerable<HistoryEntry> matches = _history.OrderByDescending(h => h.VisitedAt);
                if (query.Length > 0)
                    matches = matches.Where(h => (h.Title ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                 (h.Url ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
                list.ItemsSource = matches.ToList();
            };
            searchBox.TextChanged += (s, args) => refresh();

            Func<HistoryEntry> selected = () => list.SelectedItem as HistoryEntry;
            Action openSelected = () =>
            {
                HistoryEntry entry = selected();
                if (entry != null) NavigateCurrent(entry.Url);
            };
            Action newTabSelected = () =>
            {
                HistoryEntry entry = selected();
                if (entry != null) CreateNewTab(entry.Url, true);
            };

            list.MouseDoubleClick += (s, args) => openSelected();
            Button openButton = new Button { Content = "Open", MinWidth = 78, Margin = new Thickness(3, 0, 3, 0) };
            openButton.Click += (s, args) => openSelected();
            Button newTabButton = new Button { Content = "Open in new tab", MinWidth = 120, Margin = new Thickness(3, 0, 3, 0) };
            newTabButton.Click += (s, args) => newTabSelected();
            Button removeButton = new Button { Content = "Remove selected", MinWidth = 120, Margin = new Thickness(3, 0, 3, 0) };
            removeButton.Click += (s, args) =>
            {
                HistoryEntry entry = selected();
                if (entry != null)
                {
                    _history.Remove(entry);
                    SaveHistory();
                    refresh();
                }
            };
            Button clearButton = new Button { Content = "Clear history", MinWidth = 100, Margin = new Thickness(3, 0, 3, 0) };
            clearButton.Click += (s, args) => { ClearHistory(); refresh(); };
            Button closeButton = new Button { Content = "Close", MinWidth = 78, Margin = new Thickness(3, 0, 0, 0) };
            closeButton.Click += (s, args) => window.Close();
            actions.Children.Add(openButton);
            actions.Children.Add(newTabButton);
            actions.Children.Add(removeButton);
            actions.Children.Add(clearButton);
            actions.Children.Add(closeButton);

            window.Content = layout;
            refresh();
            WindowFitService.Attach(window);
            window.ShowDialog();
        }

        private void ClearHistory()
        {
            ClearBrowsingData_Click(this, new RoutedEventArgs());
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this,
                "Harbor Browser 0.4.1 — Hotfix 1\nSuggestion click fix (build 0.4.1.1).\nA compact, modular browser shell for Windows.\n\nRendering engine: Microsoft Edge WebView2\nSearch engines: DuckDuckGo, Google, Bing, Brave Search, Startpage\n\nVPN: connects to VPN profiles configured in Windows. Harbor does not provide VPN servers or a VPN subscription.",
                "About Harbor Browser", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e) { Close(); }

        private void SaveSettings()
        {
            try { _store.SaveSettings(_settings); RefreshHomePages(); }
            catch (Exception ex) { SetStatus("Could not save settings: " + ex.Message); }
        }

        private void SaveBookmarks()
        {
            try { _store.SaveBookmarks(_bookmarks); RefreshHomePages(); }
            catch (Exception ex) { SetStatus("Could not save bookmarks: " + ex.Message); }
        }

        private void SaveHistory()
        {
            try { _store.SaveHistory(_history); }
            catch (Exception ex) { SetStatus("Could not save history: " + ex.Message); }
        }

        private void SetStatus(string message)
        {
            if (StatusText != null)
                StatusText.Text = message ?? string.Empty;
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_confirmedCloseWithActiveDownloads)
            {
                List<DownloadEntry> activeDownloads = _downloads.Where(d => d.Operation != null &&
                    (d.IsActive || d.CanResume)).ToList();
                if (activeDownloads.Count > 0)
                {
                    string detail = activeDownloads.Count == 1
                        ? "1 download is still in progress."
                        : activeDownloads.Count + " downloads are still in progress.";
                    MessageBoxResult choice = MessageBox.Show(this,
                        detail + " Closing Harbor may interrupt them. Close anyway?",
                        "Downloads in progress", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (choice != MessageBoxResult.Yes)
                    {
                        e.Cancel = true;
                        return;
                    }
                    _confirmedCloseWithActiveDownloads = true;
                }
            }

            if (!_closingAfterPrivacyCleanup && (_privatePermissions.Count > 0 || _privateCleanupTasks.Count > 0 || _privateCleanupFailures.Count > 0 || _settings.ClearCookiesOnExit || _settings.ClearCacheOnExit))
            {
                e.Cancel = true;
                if (_shuttingDown) return;
                _shuttingDown = true;
                try { await FinishPrivacyBeforeClose(); }
                catch (Exception ex)
                {
                    if (MessageBox.Show(this, "Cleanup did not finish. Close anyway?\n\n" + ex.Message, "Privacy cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) { _shuttingDown = false; return; }
                }
                _closingAfterPrivacyCleanup = true;
                Close();
                return;
            }
            _shuttingDown = true;
            CancelPermissions();
            if (_downloadNotificationTimer != null)
                _downloadNotificationTimer.Stop();

            if (_sessionWritesAllowed)
            {
                SaveSession(); SaveSettings(); SaveBookmarks(); SaveHistory();
                try { if (_lastSessionSaveSucceeded) _sessionRecovery.MarkClean(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            }
            foreach (TabItem item in BrowserTabs.Items)
            {
                BrowserTab tab = item.Tag as BrowserTab;
                if (tab == null || tab.View == null) continue;
                tab.IsClosing = true;
                CancelHomeSuggestions(tab);
                if (tab.NativeContent is DownloadsPage) ((DownloadsPage)tab.NativeContent).Dispose();
            if (tab.VideoPopout != null) tab.VideoPopout.Close();
                if (tab.SuspendTimer != null) tab.SuspendTimer.Stop();
                try
                {
                    IDisposable disposable = tab.View as IDisposable;
                    if (disposable != null) disposable.Dispose();
                }
                catch { }
            }
            foreach (var retained in _retainedDownloadTabs.ToList()) { try { retained.View.Dispose(); } catch { } }
            _retainedDownloadTabs.Clear();
            _downloadOwners.Clear();
        }
    }
}
