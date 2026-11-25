using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Wpf;

namespace HarborBrowser.Views
{
    // Runtime state for a browser tab. Kept separate from MainWindow so tab lifecycle
    // changes can evolve without enlarging the main window's code-behind.
    internal sealed class BrowserTab
    {
        public readonly List<CoreWebView2Frame> MediaFrames = new List<CoreWebView2Frame>();
        public PictureInPictureWindow VideoPopout { get; set; }
        public Func<string,Task<string>> ActiveVideoContext { get; set; }
        public bool OpeningPopout { get; set; }
        public TabItem Item { get; set; }
        public WebView2 View { get; set; }
        public System.Windows.FrameworkElement NativeContent { get; set; }
        public System.Threading.CancellationTokenSource HomeSuggestionCancellation {get;set;}
        public TextBlock HeaderText { get; set; }
        public Image HeaderIcon { get; set; }
        public Border PrivateBadge {get;set;}
        public TextBlock FallbackIcon { get; set; }
        public StackPanel HeaderContainer { get; set; }
        public MenuItem PinCommand {get;set;}
        public int FaviconRevision {get;set;}
        public System.Windows.Controls.Grid HeaderLayout {get;set;}
        public Button CloseButton { get; set; }
        public string PendingUrl { get; set; }
        public string LastRecordedUrl { get; set; }
        public bool Initialized { get; set; }
        public bool Initializing { get; set; }
        public System.Threading.CancellationTokenSource StartupCancellation { get; set; }
        public bool IsSuspended { get; set; }
        public Guid ResourceId {get;}=Guid.NewGuid();
        public bool KeepAwake {get;set;}
        public bool SuspensionInProgress {get;set;}
        public DateTime? InactiveSinceUtc {get;set;}
        public DateTime RetrySleepAfterUtc {get;set;}
        public string SleepReason {get;set;}
        public bool IsClosing { get; set; }
        public bool IsPrivate { get; set; }
        public string PrivateSessionId {get;set;}
        public int NavigationGeneration {get;set;}
        public string LastKnownUrl {get;set;}
        public string TorBlockedUrl {get;set;}
        public string FailureMessage {get;set;}
        public bool NeedsNewView {get;set;}
        public bool IsPinned { get; set; }
        public bool TrackerFilterRegistered { get; set; }
        public string GroupName { get; set; }
        public string PageTitle { get; set; }
        public DispatcherTimer SuspendTimer { get; set; }
    }

    internal sealed class TabGroupAction
    {
        public BrowserTab Tab { get; set; }
        public string GroupName { get; set; }
    }
}
