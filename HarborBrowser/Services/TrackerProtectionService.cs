using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Services
{
    /// <summary>
    /// Owns Harbor's supplemental tracker host list and WebView2 filter registration.
    /// Native WebView2 tracking prevention remains separately configurable.
    /// </summary>
    internal static class TrackerProtectionService
    {
        private static readonly string[] Hosts =
        {
            "doubleclick.net", "google-analytics.com", "googletagmanager.com", "googlesyndication.com",
            "adservice.google.com", "connect.facebook.net", "pixel.facebook.com", "analytics.tiktok.com",
            "ads-twitter.com", "static.ads-twitter.com", "ads.linkedin.com", "snap.licdn.com",
            "hotjar.com", "mixpanel.com", "segment.io", "amplitude.com", "scorecardresearch.com",
            "quantserve.com", "taboola.com", "outbrain.com", "criteo.com", "adnxs.com",
            "pubmatic.com", "openx.net", "rubiconproject.com", "adsrvr.org", "matomo.cloud",
            "clarity.ms", "newrelic.com", "nr-data.net"
        };

        public static void RegisterFilters(CoreWebView2 core, System.EventHandler<CoreWebView2WebResourceRequestedEventArgs> handler)
        {
            if (core == null || handler == null) return;
            foreach (string host in Hosts)
            {
                core.AddWebResourceRequestedFilter("*://" + host + "/*", CoreWebView2WebResourceContext.All,
                    CoreWebView2WebResourceRequestSourceKinds.Document);
                core.AddWebResourceRequestedFilter("*://*." + host + "/*", CoreWebView2WebResourceContext.All,
                    CoreWebView2WebResourceRequestSourceKinds.Document);
            }
            core.WebResourceRequested += handler;
        }

        public static bool IsBlockedHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            foreach (string domain in Hosts)
            {
                if (host.Equals(domain, System.StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + domain, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
