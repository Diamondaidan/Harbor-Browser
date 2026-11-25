using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Models
{
    public sealed class DownloadEntry
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Status { get; set; }
        public CoreWebView2DownloadOperation Operation { get; set; }
    }
}
