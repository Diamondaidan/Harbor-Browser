using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Services
{
    // Per-tab, memory-only observations. No response bodies, cookies or request headers.
    internal sealed class PageLoadDiagnostics
    {
        private sealed class Request
        {
            internal int Generation, Count;
            internal bool Ambiguous, Blocked;
            internal CoreWebView2WebResourceContext Kind;
        }
        private readonly Dictionary<string, Request> _pending = new Dictionary<string, Request>(StringComparer.Ordinal);
        private Uri _page;
        private int _generation, _dismissed;
        private bool _private, _suppressed, _failed, _limited;
        internal ulong NavigationId { get; private set; }
        internal int Challenges { get; private set; }
        internal int Refused { get; private set; }
        internal int Unavailable { get; private set; }
        internal int TrackerBlocks { get; private set; }
        internal int ProtectionBlocks { get; private set; }
        internal CoreWebView2WebErrorStatus Error { get; private set; }
        internal string VerificationTarget { get; private set; }
        internal bool ReturningFromCheck { get; set; }
        private int Priority { get { return _failed ? 3 : Challenges > 0 ? 2 : Refused + Unavailable > 0 ? 1 : 0; } }
        internal bool Visible { get { return !_suppressed && Priority > _dismissed; } }

        internal void Begin(ulong id, string url, bool isPrivate, bool sensitive)
        {
            NavigationId = id; _generation++; _private = isPrivate; _suppressed = sensitive;
            Uri parsed;
            _page = !_private && !sensitive && WebUri(url, out parsed) ? parsed : null;
            Challenges = Refused = Unavailable = TrackerBlocks = ProtectionBlocks = _dismissed = 0;
            Error = CoreWebView2WebErrorStatus.Unknown; VerificationTarget = null; ReturningFromCheck = false;
            _failed = _limited = false;
            // Keep outstanding older requests until their response arrives, so an
            // overlapping URL is conservatively ignored instead of misattributed.
            if (_private || sensitive) _pending.Clear();
        }
        internal void Redirect(ulong id, string url, bool sensitive)
        {
            if (id != NavigationId) return;
            _suppressed |= sensitive;
            Uri parsed; _page = !_private && !_suppressed && WebUri(url, out parsed) ? parsed : null;
            if (_page == null) { _pending.Clear(); VerificationTarget = null; }
        }
        private static bool WebUri(string value, out Uri uri)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == "https" || uri.Scheme == "http") && uri.UserInfo.Length == 0 && uri.Host != HomePageService.Host;
        }
        private bool SameOrigin(Uri uri)
        {
            return _page != null && uri.Scheme == _page.Scheme && uri.Host == _page.Host && uri.Port == _page.Port;
        }
        internal void Requested(string url, string method, CoreWebView2WebResourceContext kind)
        {
            Uri uri;
            if (_page == null || method != "GET" || !WebUri(url, out uri) || !SameOrigin(uri) ||
                (kind != CoreWebView2WebResourceContext.Document && kind != CoreWebView2WebResourceContext.Stylesheet &&
                 kind != CoreWebView2WebResourceContext.Script && kind != CoreWebView2WebResourceContext.Image && kind != CoreWebView2WebResourceContext.Font)) return;
            Request request;
            if (_pending.TryGetValue(url, out request))
            {
                request.Ambiguous |= request.Generation != _generation;
                request.Count = Math.Min(10000, request.Count + 1); return;
            }
            // A busy/hostile page cannot grow the report or retain unlimited URLs.
            if (_pending.Count >= 256 || url.Length > 8192) { _limited = true; return; }
            _pending[url] = new Request { Generation = _generation, Count = 1, Kind = kind };
        }
        internal void Blocked(string url, bool tracker)
        {
            Request request;
            if (url != null && _pending.TryGetValue(url, out request)) request.Blocked = true;
            if (_suppressed) return;
            if (tracker) TrackerBlocks = Math.Min(9999, TrackerBlocks + 1);
            else ProtectionBlocks = Math.Min(9999, ProtectionBlocks + 1);
        }
        internal bool Response(string url, int status, string mitigated, string contentType)
        {
            Request request;
            if (url == null || !_pending.TryGetValue(url, out request)) return false;
            if (--request.Count == 0) _pending.Remove(url);
            if (request.Ambiguous || request.Generation != _generation || request.Blocked || _page == null) return false;
            Uri uri;
            if (!WebUri(url, out uri) || !SameOrigin(uri)) return false;
            bool html = (contentType ?? "").Split(';')[0].Trim().Equals("text/html", StringComparison.OrdinalIgnoreCase);
            if (html && string.Equals((mitigated ?? "").Trim(), "challenge", StringComparison.OrdinalIgnoreCase))
            {
                Challenges = Math.Min(9999, Challenges + 1);
                if (VerificationTarget == null && uri.Scheme == "https" && request.Kind != CoreWebView2WebResourceContext.Document)
                    VerificationTarget = uri.GetLeftPart(UriPartial.Path); // No account/query/fragment tokens in the helper tab.
                return true;
            }
            if (status == 401 || status == 403) { Refused = Math.Min(9999, Refused + 1); return true; }
            if (status == 404 || status == 410 || status >= 500 && status <= 599) { Unavailable = Math.Min(9999, Unavailable + 1); return true; }
            return false;
        }
        internal void Complete(ulong id, bool success, CoreWebView2WebErrorStatus error)
        {
            if (id != NavigationId || _suppressed) return;
            // OperationCanceled includes downloads and canceled redirects, not a failed page.
            Error = success || error == CoreWebView2WebErrorStatus.OperationCanceled ? CoreWebView2WebErrorStatus.Unknown : error;
            _failed = !success && error != CoreWebView2WebErrorStatus.OperationCanceled;
        }
        internal void Dismiss() { _dismissed = Priority; ReturningFromCheck = false; }
        internal string Message
        {
            get
            {
                if (_failed) return ErrorMessage(Error);
                if (ReturningFromCheck) return "Site check opened. After completing it yourself, return here and reload this page.";
                if (Challenges > 0) return "This website requested a verification check. Some page files may be missing until you complete it.";
                if (Refused > 0) return "This website refused some page files. A site check, sign-in or website restriction may be involved.";
                if (Unavailable > 0) return "Some page files are missing or the website returned a server error. The page may look incomplete.";
                return "No page-loading issue was detected by Harbor. This does not verify every website feature.";
            }
        }
        private static string ErrorMessage(CoreWebView2WebErrorStatus error)
        {
            switch (error)
            {
                case CoreWebView2WebErrorStatus.HostNameNotResolved: return "The website address could not be found. Check the address and your connection.";
                case CoreWebView2WebErrorStatus.CannotConnect: return "Harbor could not connect to this website. Check your connection and Proton status, then try again.";
                case CoreWebView2WebErrorStatus.Timeout: return "The website took too long to respond. Check your connection, then try again.";
                case CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect:
                case CoreWebView2WebErrorStatus.CertificateExpired:
                case CoreWebView2WebErrorStatus.ClientCertificateContainsErrors:
                case CoreWebView2WebErrorStatus.CertificateRevoked:
                case CoreWebView2WebErrorStatus.CertificateIsInvalid: return "The website's secure connection could not be verified. Harbor has kept certificate checks enabled.";
                case CoreWebView2WebErrorStatus.ValidProxyAuthenticationRequired: return "The browser proxy needs authentication. Check Proton's controls and connection status.";
                case CoreWebView2WebErrorStatus.Disconnected:
                case CoreWebView2WebErrorStatus.ConnectionAborted:
                case CoreWebView2WebErrorStatus.ConnectionReset: return "The connection was interrupted. Check your connection and Proton status, then try again.";
                default: return "The page could not finish loading. See Page loading details for the error category.";
            }
        }
        private static string VersionText(string value)
        {
            return Regex.IsMatch(value ?? "", @"^\d{1,5}(\.\d{1,5}){1,3}$") ? value : "unavailable";
        }
        internal string Report(string appVersion, string runtimeVersion, int osBuild, ProtonProtectionPhase protection, bool tor, bool tracker, bool? networkAvailable=null)
        {
            var text = new StringBuilder();
            text.AppendLine("Harbor page loading details");
            text.AppendLine("Harbor: " + VersionText(appVersion)); text.AppendLine("WebView2: " + VersionText(runtimeVersion));
            text.AppendLine("Windows build: " + Math.Max(0, osBuild)); text.AppendLine("Tab: " + (_private ? "Private" : "Regular"));
            text.AppendLine("Network adapter: " + (networkAvailable.HasValue ? networkAvailable.Value ? "Available" : "Unavailable" : "Unknown"));
            text.AppendLine("Proton: " + (Enum.IsDefined(typeof(ProtonProtectionPhase), protection) ? protection.ToString() : "Unknown"));
            if (protection == ProtonProtectionPhase.Connecting || protection == ProtonProtectionPhase.Disconnected)
                text.AppendLine("Browser protection is pausing supported browsing. Connect and verify in Proton's controls, or turn protection off to use the regular connection. Paused pages are not retried automatically.");
            text.AppendLine("Tor: " + (tor ? "Enabled" : "Off")); text.AppendLine("Harbor tracker blocking: " + (tracker ? "On" : "Off"));
            text.AppendLine("Observation: " + Message);
            text.AppendLine("Navigation error: " + (Enum.IsDefined(typeof(CoreWebView2WebErrorStatus), Error) ? Error.ToString() : "Unknown"));
            text.AppendLine("Verification challenges: " + Challenges); text.AppendLine("Refused page files (401/403): " + Refused);
            text.AppendLine("Missing/server-error page files: " + Unavailable); text.AppendLine("Harbor tracker blocks: " + TrackerBlocks);
            text.AppendLine("Harbor protection blocks: " + ProtectionBlocks);
            text.AppendLine("Counts cover observed first-party GET page files; third-party failures and native tracking prevention are not fully counted.");
            if (_private || _suppressed) text.AppendLine("Resource inspection is limited for private and account tabs.");
            if (_limited) text.AppendLine("The bounded resource-inspection limit was reached; additional page files were not inspected. Reopen the tab to reset inspection.");
            text.AppendLine("No URLs, IP addresses, page titles, account details, cookies, passwords or response contents are included. Nothing is uploaded.");
            return text.ToString();
        }
    }
}
