using System;
using HarborBrowser.Services;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static void PageDiagnosticsChecks()
    {
        var d = new PageLoadDiagnostics();
        const string page = "https://owned.invalid/form?email=private@example.invalid#secret";
        const string css = "https://owned.invalid/style.css?token=secret#fragment";
        Action begin = () => d.Begin(1, page, false, false);
        Action request = () => d.Requested(css, "GET", CoreWebView2WebResourceContext.Stylesheet);
        begin(); request(); d.Response(css,403,"challenge","text/html; charset=utf-8");
        Check("challenge header and HTML show verification notice",d.Visible&&d.Challenges==1&&d.Refused==0);
        Check("helper target strips query and fragment",d.VerificationTarget=="https://owned.invalid/style.css");
        string report=d.Report("0.6.2.0","157.0.4341.46",19045,ProtonProtectionPhase.Protected,false,true);
        Check("report omits URL host query email and path",!report.Contains("owned.invalid")&&!report.Contains("private@")&&!report.Contains("secret")&&!report.Contains("style.css"));
        Check("report shows protection category",report.Contains("Proton: Protected"));
        d.Dismiss();request();d.Response(css,403,"challenge","text/html");
        Check("repeated same-priority issue stays dismissed",!d.Visible);
        d.Complete(1,false,CoreWebView2WebErrorStatus.HostNameNotResolved);
        Check("higher-priority navigation failure reopens notice",d.Visible&&d.Message.Contains("could not be found"));
        begin();request();d.Response(css,403,null,"text/html");
        Check("generic 403 is not labeled a challenge",d.Refused==1&&d.Challenges==0&&d.VerificationTarget==null);
        begin();request();d.Response(css,403,"challenge","text/css");
        Check("challenge header with non-HTML is not actionable",d.Challenges==0&&d.VerificationTarget==null);
        begin();request();d.Blocked(css,true);d.Response(css,403,"challenge","text/html");
        Check("app tracker block is distinct from website response",d.TrackerBlocks==1&&d.Challenges==0&&!d.Visible);
        begin();request();d.Blocked(css,false);d.Response(css,503,null,"text/plain");
        Check("app protection block is distinct from server failure",d.ProtectionBlocks==1&&d.Unavailable==0);
        begin();request();d.Begin(2,"https://other.invalid/",false,false);d.Response(css,403,"challenge","text/html");
        Check("old response cannot change a new origin",d.Challenges==0&&!d.Visible);
        begin();request();d.Begin(2,page,false,false);request();d.Response(css,403,"challenge","text/html");d.Response(css,403,"challenge","text/html");
        Check("overlapping cross-navigation URLs are conservatively ignored",d.Challenges==0);
        begin();d.Requested("https://third.invalid/a.css","GET",CoreWebView2WebResourceContext.Stylesheet);d.Response("https://third.invalid/a.css",403,"challenge","text/html");
        Check("third-party response cannot open a helper tab",!d.Visible&&d.VerificationTarget==null);
        begin();d.Requested(css,"POST",CoreWebView2WebResourceContext.Stylesheet);d.Response(css,403,"challenge","text/html");
        Check("POST cannot become a verification action",d.VerificationTarget==null);
        d.Begin(3,"http://owned.invalid/",false,false);d.Requested("http://owned.invalid/a.css","GET",CoreWebView2WebResourceContext.Stylesheet);d.Response("http://owned.invalid/a.css",403,"challenge","text/html");
        Check("HTTP challenge offers no helper navigation",d.Challenges==1&&d.VerificationTarget==null);
        d.Begin(4,page,true,false);request();d.Response(css,403,"challenge","text/html");
        report=d.Report(page,page,19045,ProtonProtectionPhase.Off,false,false);
        Check("private diagnostics inspect no resource URLs",d.Challenges==0&&d.VerificationTarget==null);
        Check("private report redacts arbitrary environment strings",report.Contains("Tab: Private")&&!report.Contains("owned.invalid")&&report.Contains("unavailable"));
        d.Begin(5,page,false,true);request();d.Response(css,403,"challenge","text/html");d.Complete(5,false,CoreWebView2WebErrorStatus.CannotConnect);
        Check("account-page diagnostics remain suppressed",!d.Visible&&d.VerificationTarget==null);
        begin();d.Complete(999,false,CoreWebView2WebErrorStatus.CannotConnect);
        Check("stale navigation completion is ignored",!d.Visible);
        d.Complete(1,false,CoreWebView2WebErrorStatus.OperationCanceled);
        Check("canceled downloads do not show failed page",!d.Visible);
        d.Complete(1,false,CoreWebView2WebErrorStatus.Unknown);
        Check("unknown navigation failure still shows a notice",d.Visible);
        begin();request();d.Response(css,503,null,"text/plain");
        Check("server 503 is counted without claiming a VPN block",d.Unavailable==1&&d.ProtectionBlocks==0);
        d.Complete(1,false,CoreWebView2WebErrorStatus.CertificateExpired);
        Check("TLS notice keeps certificate checks enabled",d.Message.Contains("kept certificate checks enabled"));
        d.Begin(8,page,false,false);d.Redirect(8,"https://redirect.invalid/",false);d.Requested("https://redirect.invalid/a.css","GET",CoreWebView2WebResourceContext.Stylesheet);d.Response("https://redirect.invalid/a.css",403,"challenge","text/html");
        Check("redirected document tracks its final origin",d.Challenges==1);
        d.Begin(9,page,false,false);for(int i=0;i<300;i++)d.Requested("https://owned.invalid/a"+i+".css","GET",CoreWebView2WebResourceContext.Stylesheet);
        Check("resource observation is bounded and discloses its limit",d.Report("0.6.2","157.0",19045,ProtonProtectionPhase.Off,false,true).Contains("limit was reached"));
    }
}
