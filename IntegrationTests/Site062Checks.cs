using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static void SiteAction(MainWindow main, string name)
    { typeof(MainWindow).GetMethod(name, BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{null,new RoutedEventArgs()}); }
    private static async Task Site062Checks(MainWindow main, string output, BetaGuardFixtureServer server)
    {
        PageDiagnosticsChecks();
        var original = (BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var core = original.View.CoreWebView2;
        Check("062 tests use an owned profile",core.Environment.UserDataFolder.StartsWith(Path.GetFullPath(output),StringComparison.OrdinalIgnoreCase));
        await AuthNavigate(core,server.Url("diagnostics/page"));
        await Wait(()=>original.PageDiagnostics.Challenges>0&&main.PageIssueBar.IsVisible);
        Check("native HTTPS stylesheet challenge is detected",original.PageDiagnostics.Challenges==1&&original.PageDiagnostics.Refused==0);
        Check("challenge notice keeps the original document",await core.ExecuteScriptAsync("document.querySelector('#owned').textContent") == "\"Original page stays visible\"");
        Check("challenge offers a user-controlled helper",main.CompleteSiteCheckButton.IsVisible);
        Check("helper excludes the original query token",original.PageDiagnostics.VerificationTarget==server.Url("diagnostics/style.css"));
        string report=main.CurrentPageLoadingReport();
        Check("native report has no host IP token or document title",!report.Contains("127.0.0.1")&&!report.Contains("private-token")&&!report.Contains("canary")&&!report.Contains("Original page"));
        File.WriteAllText(Path.Combine(output,"sanitized-report.txt"),report);
        int pageRequests=server.Count("diagnostics/page");
        main.CompleteSiteCheckButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Wait(()=>main.BrowserTabs.SelectedItem!=original.Item);
        var helper=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        await Wait(()=>helper.Initialized&&helper.View.CoreWebView2.Source==server.Url("diagnostics/style.css"));
        await Wait(()=>main.ReturnFromSiteCheckButton.IsVisible);
        Check("helper is a separate regular tab in the same browser profile",helper.VerificationReturnTab==original&&!helper.IsPrivate&&helper.View.CoreWebView2.Profile.ProfilePath==core.Profile.ProfilePath);
        Check("opening verification does not reload or replay the original page",server.Count("diagnostics/page")==pageRequests);
        main.ReturnFromSiteCheckButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("return selects original without automatic reload",main.BrowserTabs.SelectedItem==original.Item&&server.Count("diagnostics/page")==pageRequests);
        Check("return gives an explicit next step",main.PageIssueText.Text.Contains("reload"));
        foreach(double width in new[]{1366d,1093d,911d,600d})
        {
            main.Width=width;main.Height=540;main.UpdateResponsiveLayout();main.UpdateLayout();await Task.Delay(60);
            Check("notice text fits width "+width,main.PageIssueText.ActualWidth<=main.ActualWidth&&main.PageIssueBar.ActualHeight<180);
            foreach(var button in Visuals<Button>(main.PageIssueBar).Where(b=>b.IsVisible))
            {
                double right=button.TranslatePoint(new Point(button.ActualWidth,0),main.PageIssueBar).X;
                Check("notice action fits "+width+" "+button.Content,right<=main.PageIssueBar.ActualWidth+1);
            }
        }
        Capture(main.PageIssueBar,Path.Combine(output,"site-challenge-compact.png"));
        // Simulate fixture-side completion only. No CAPTCHA or account is automated.
        server.DiagnosticsVerified=true;
        SiteAction(main,"Reload_Click");
        await Wait(()=>server.Count("diagnostics/page")==pageRequests+1&&original.PageDiagnostics.Challenges==0);
        await Wait(()=>main.PageIssueBar.Visibility==Visibility.Collapsed);
        for(int i=0;i<100&&await core.ExecuteScriptAsync("document.readyState")!="\"complete\"";i++)await Task.Delay(50);
        Check("explicit reload restores the actual stylesheet",await core.ExecuteScriptAsync("getComputedStyle(document.querySelector('#owned')).color")=="\"rgb(20, 80, 140)\"");
        await AuthNavigate(core,server.Url("diagnostics/refused"));
        await Wait(()=>original.PageDiagnostics.Refused==1&&main.PageIssueBar.IsVisible);
        Check("generic 403 is labeled separately",main.PageIssueText.Text.Contains("refused")&&main.CompleteSiteCheckButton.Visibility==Visibility.Collapsed);
        SiteAction(main,"DismissPageIssue_Click");
        Check("dismiss hides notice without clearing report counters",main.PageIssueBar.Visibility==Visibility.Collapsed&&main.CurrentPageLoadingReport().Contains("(401/403): 1"));
        var help=main.BuildGearMenu().Items.OfType<MenuItem>().Single(i=>Label(i)=="Help");
        Check("local report is available from Help",help.Items.OfType<MenuItem>().Any(i=>Label(i)=="Page loading details…"));
        var details=new PageLoadingDetailsWindow(main.CurrentPageLoadingReport());Offscreen(details);details.Show();await Task.Delay(50);
        foreach(string mode in new[]{"Dark","Light"})
        {
            AppearanceService.Apply(details.Resources,new BrowserSettings{ThemeMode=mode});details.UpdateLayout();
            Check("diagnostics caption follows "+mode,details.Template.FindName("HarborCaptionBar",details)!=null);
            Check("diagnostics report remains readable and read-only in "+mode,details.ReportBox.IsReadOnly&&details.ReportBox.ActualWidth>200);
            Capture(details,Path.Combine(output,"page-details-"+mode+".png"));
        }
        details.Close();
        var tor=(BrowserVpnService)Field(main,"_browserVpn");
        var enabledField=typeof(BrowserVpnService).GetField("_enabled",BindingFlags.NonPublic|BindingFlags.Instance);
        enabledField.SetValue(tor,true); // Inject a closed gate; no Tor process or network change.
        try
        {
            int beforeBlock=server.Count("diagnostics/blocked.png");
            await core.ExecuteScriptAsync("window.ownedBlockFinished=false;var blockedImage=new Image();blockedImage.onerror=()=>window.ownedBlockFinished=true;blockedImage.src='"+server.Url("diagnostics/blocked.png")+"';document.body.append(blockedImage)");
            await Wait(()=>original.PageDiagnostics.ProtectionBlocks>0);
            await WaitJs(core,"window.ownedBlockFinished===true");
            Check("native Tor denial is counted as a protection block",original.PageDiagnostics.ProtectionBlocks>0&&original.PageDiagnostics.Unavailable==0);
            Check("closed Tor gate never sends the test image to the server",server.Count("diagnostics/blocked.png")==beforeBlock);
        }
        finally { enabledField.SetValue(tor,false); }
        main.AutomaticProtonMonitoringEnabled=false;
        main.BeginProtonProtection(true);
        int countBefore=main.BrowserTabs.Items.Count;
        var blocked=AddTab(main,HomePageService.Url,true);
        Check("private tab remains blocked while browser protection is enabled",blocked==null&&main.BrowserTabs.Items.Count==countBefore);
        Check("private restriction has an actionable visible explanation",main.PageIssueBar.IsVisible&&main.PrivateTabConnectionButton.IsVisible&&main.PageIssueText.Text.Contains("keep sign-in")&&main.PageIssueText.Text.Contains("regular connection"));
        Check("private notice does not disable browser protection",main.ProtonProtection.Enabled);
        main.UpdateLayout();await Task.Delay(100);
        Capture(main.PageIssueBar,Path.Combine(output,"private-vpn-limit.png"));
        main.BeginProtonProtection(false);SiteAction(main,"DismissPageIssue_Click");
        var privateTab=AddTab(main,HomePageService.Url,true);
        Check("private tab can be created after protection is off",privateTab!=null&&privateTab.IsPrivate);
        main.BrowserTabs.SelectedItem=privateTab.Item;
        Check("private report is accurate before its first navigation",main.CurrentPageLoadingReport().Contains("Tab: Private"));
        await Wait(()=>privateTab.Initialized);
        Check("off-mode private tab is native InPrivate",privateTab.View.CoreWebView2.Profile.IsInPrivateModeEnabled);
        Check("private report identifies the tab without its URL",main.CurrentPageLoadingReport().Contains("Tab: Private")&&!main.CurrentPageLoadingReport().Contains(HomePageService.Host));
        main.BrowserTabs.SelectedItem=original.Item;
    }
}
