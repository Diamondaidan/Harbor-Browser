using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private bool _pageIssueUpdateQueued;
        private bool _privateTabLimitVisible;
        private void ShowPrivateTabLimit()
        {
            _privateTabLimitVisible = true;
            UpdatePageIssueBar();
        }
        private void ConfigurePageDiagnostics(BrowserTab tab, CoreWebView2 core, Func<bool> current)
        {
            core.WebResourceRequested += (s, e) =>
            {
                if (!current() || e.RequestedSourceKind != CoreWebView2WebResourceRequestSourceKinds.Document) return;
                tab.PageDiagnostics.Requested(e.Request.Uri, e.Request.Method, e.ResourceContext);
            };
            core.WebResourceResponseReceived += (s, e) =>
            {
                if (!current() || tab.IsPrivate || tab.IsProviderAuthentication) return;
                try
                {
                    var headers = e.Response.Headers;
                    string challenge = headers.Contains("cf-mitigated") ? headers.GetHeader("cf-mitigated") : null;
                    string type = headers.Contains("Content-Type") ? headers.GetHeader("Content-Type") : null;
                    if (tab.PageDiagnostics.Response(e.Request.Uri, e.Response.StatusCode, challenge, type)) QueuePageIssueUpdate();
                }
                catch { /* A disappearing view must not affect navigation. */ }
            };
        }
        private void QueuePageIssueUpdate()
        {
            if (_pageIssueUpdateQueued || _shuttingDown) return;
            _pageIssueUpdateQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { _pageIssueUpdateQueued = false; UpdatePageIssueBar(); }));
        }
        private void UpdatePageIssueBar()
        {
            if (PageIssueBar == null) return;
            if (_protonChanges == 0 && !(_protonPreview?.Enabled ?? false) && !(ProtonProtection?.Enabled ?? false) && !(_browserVpn?.Enabled ?? false))
                _privateTabLimitVisible = false;
            var tab = SelectedTab;
            bool usable = tab != null && !tab.IsClosing && tab.FailureMessage == null && !tab.ProtonPaused && tab.NativeContent == null;
            bool returning = usable && tab.VerificationReturnTab != null && !tab.VerificationReturnTab.IsClosing;
            bool limit = _privateTabLimitVisible && tab != null && !tab.IsClosing;
            PageIssueBar.Visibility = limit || usable && (tab.PageDiagnostics.Visible || returning) ? Visibility.Visible : Visibility.Collapsed;
            if (PageIssueBar.Visibility != Visibility.Visible) return;
            PrivateTabConnectionButton.Visibility = limit ? Visibility.Visible : Visibility.Collapsed;
            if (limit)
            {
                PageIssueText.Text = "Private tabs cannot use this browser-only VPN. Turn protection off in its controls to use private tabs on your regular connection. Proton's ‘Turn off protection — keep sign-in’ saves your login.";
                CompleteSiteCheckButton.Visibility = ReturnFromSiteCheckButton.Visibility = PageIssueReloadButton.Visibility = Visibility.Collapsed;
                return;
            }
            PageIssueText.Text = returning ? "Complete this website's check yourself, then return to your original page and reload it." : tab.PageDiagnostics.Message;
            CompleteSiteCheckButton.Visibility = !returning && tab.PageDiagnostics.VerificationTarget != null ? Visibility.Visible : Visibility.Collapsed;
            ReturnFromSiteCheckButton.Visibility = returning ? Visibility.Visible : Visibility.Collapsed;
            PageIssueReloadButton.Visibility = returning ? Visibility.Collapsed : Visibility.Visible;
        }
        private void CompleteSiteCheck_Click(object sender, RoutedEventArgs e)
        {
            var original = SelectedTab;
            string target = original?.PageDiagnostics.VerificationTarget;
            if (target == null || original.IsPrivate || original.ProtonPaused || ProtonProtection.ShouldBlock(target, false)) return;
            var check = CreateNewTab(target, true);
            if (check == null) return;
            check.VerificationReturnTab = original;
            original.PageDiagnostics.ReturningFromCheck = true;
            UpdatePageIssueBar();
        }
        private void ReturnFromSiteCheck_Click(object sender, RoutedEventArgs e)
        {
            var check = SelectedTab; var original = check?.VerificationReturnTab;
            if (original == null || original.IsClosing) return;
            check.VerificationReturnTab = null;
            BrowserTabs.SelectedItem = original.Item;
            SetStatus("Returned to the original page. Use Reload when the site check is complete; review any form-resubmission prompt.");
            UpdatePageIssueBar();
        }
        private void DismissPageIssue_Click(object sender, RoutedEventArgs e)
        {
            if (_privateTabLimitVisible) { _privateTabLimitVisible = false; UpdatePageIssueBar(); return; }
            var tab = SelectedTab; if (tab == null) return;
            tab.PageDiagnostics.Dismiss(); tab.VerificationReturnTab = null; UpdatePageIssueBar();
        }
        internal string CurrentPageLoadingReport()
        {
            var diagnostics = SelectedTab?.PageDiagnostics ?? new PageLoadDiagnostics();
            return diagnostics.Report(typeof(MainWindow).Assembly.GetName().Version.ToString(), _environment?.BrowserVersionString,
                Environment.OSVersion.Version.Build, ProtonProtection.State, _browserVpn?.Enabled ?? false, _settings.EnableTrackerBlocking, _protonNetworkAvailable);
        }
        private void PageLoadingDetails_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PageLoadingDetailsWindow(CurrentPageLoadingReport()) { Owner = this };
            dialog.ShowDialog();
        }
        private void RecordPageRequestBlock(CoreWebView2 core, string url, bool tracker)
        {
            var tab = BrowserTabs.Items.Cast<TabItem>().Select(i => i.Tag as BrowserTab)
                .FirstOrDefault(t => t != null && !t.IsClosing && t.View?.CoreWebView2 == core);
            tab?.PageDiagnostics.Blocked(url, tracker);
        }
    }
}
