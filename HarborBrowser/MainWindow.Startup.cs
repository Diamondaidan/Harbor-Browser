using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        internal TimeSpan BrowserStartupTimeout = TimeSpan.FromSeconds(45);
        internal Func<WebView2, CoreWebView2Environment, Task> BrowserViewInitializer;

        private static void ObserveAbandonedTask(Task task)
        {
            task.ContinueWith(t => { var error = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private async Task StartupStep(Task operation, CancellationToken cancellation, TimeSpan timeout, string step)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try
                {
                    if (await Task.WhenAny(operation, Task.Delay(timeout, deadline.Token)) != operation)
                    {
                        ObserveAbandonedTask(operation);
                        cancellation.ThrowIfCancellationRequested();
                        throw new TimeoutException("Browser startup timed out during " + step + ".");
                    }
                    await operation;
                    cancellation.ThrowIfCancellationRequested();
                }
                finally { deadline.Cancel(); }
            }
        }

        private void CancelTabStartup(BrowserTab tab)
        {
            if (tab?.StartupCancellation != null) tab.StartupCancellation.Cancel();
        }

        private void ShowStartupFailure(BrowserTab tab, Exception error)
        {
            tab.PendingUrl = SafeTabUrl(tab) ?? _settings.HomePage;
            tab.Initialized = false;
            tab.Initializing = false;
            tab.NeedsNewView = true;
            tab.FailureMessage = error is TimeoutException
                ? "This tab took too long to start."
                : "This tab could not start.";
            if (_browserVpn.Enabled) tab.FailureMessage += " The Tor connection is still enabled.";
            CancelTabStartup(tab);
            try { tab.View.Dispose(); } catch { }
            // Keep toolbar/resource-monitor queries safe while the failed
            // controller is gone. No new controller starts until Reload.
            tab.View = new WebView2();
            ConfigurePrivateView(tab);
            tab.MediaFrames.Clear();
            tab.ActiveVideoContext = null;
            if (_environmentTask != null && (_environmentTask.IsFaulted || _environmentTask.IsCanceled)) _environmentTask = null;
            if (_browserVpnPreparation != null && (_browserVpnPreparation.IsFaulted || _browserVpnPreparation.IsCanceled)) _browserVpnPreparation = null;
            foreach(var entry in _permissionResetTasks.Where(p=>p.Value.IsFaulted||p.Value.IsCanceled).ToList()) _permissionResetTasks.Remove(entry.Key);
            if(_moduleValidation!=null&&(_moduleValidation.IsFaulted||_moduleValidation.IsCanceled)) _moduleValidationGeneration=-1;
            UpdateTabHeader(tab);
            if (SelectedTab == tab) ShowTabContent();
            if(SelectedTab==tab)SetStatus("Browser startup did not finish. Use Reload tab to try again.");
            System.Diagnostics.Debug.WriteLine(error);
        }
    }
}
