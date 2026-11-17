using System;
using System.Collections.Generic;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Models
{
    public sealed class SitePermissionRule
    {
        public string Origin {get;set;}
        public string TopLevelOrigin {get;set;}
        public string Kind {get;set;}
        public string Decision {get;set;}
        public DateTime UpdatedUtc {get;set;}
        public SitePermissionRule Copy(){return (SitePermissionRule)MemberwiseClone();}
    }
    public enum BrowsingTimeRange { LastHour, LastDay, LastWeek, LastFourWeeks, AllTime }
    public sealed class BrowsingDataRequest
    {
        public BrowsingTimeRange Range {get;set;}
        public bool History {get;set;}
        public bool CookiesAndSiteData {get;set;}
        public bool Cache {get;set;}
        public bool Permissions {get;set;}
        public DateTime StartUtc {get;set;}
        public DateTime EndUtc {get;set;}
        public bool HasSelection {get{return History||CookiesAndSiteData||Cache||Permissions;}}
        public CoreWebView2BrowsingDataKinds DataKinds
        {
            get
            {
                CoreWebView2BrowsingDataKinds kinds=0;
                if(History)kinds|=CoreWebView2BrowsingDataKinds.BrowsingHistory;
                if(CookiesAndSiteData)kinds|=CoreWebView2BrowsingDataKinds.Cookies|CoreWebView2BrowsingDataKinds.AllDomStorage|CoreWebView2BrowsingDataKinds.ServiceWorkers;
                if(Cache)kinds|=CoreWebView2BrowsingDataKinds.DiskCache;
                return kinds;
            }
        }
        public void SetBounds(DateTime now)
        {
            EndUtc=now.ToUniversalTime();
            StartUtc=Range==BrowsingTimeRange.LastHour?EndUtc.AddHours(-1):Range==BrowsingTimeRange.LastDay?EndUtc.AddDays(-1):Range==BrowsingTimeRange.LastWeek?EndUtc.AddDays(-7):Range==BrowsingTimeRange.LastFourWeeks?EndUtc.AddDays(-28):DateTime.SpecifyKind(DateTime.MinValue,DateTimeKind.Utc);
        }
        public bool Contains(DateTime value){if(Range==BrowsingTimeRange.AllTime)return true;DateTime utc=value.Kind==DateTimeKind.Utc?value:value.ToUniversalTime();return utc>=StartUtc&&utc<=EndUtc;}
    }
    public sealed class SessionRunState
    {
        public bool Running {get;set;}
        public DateTime StartedUtc {get;set;}
    }
    public sealed class SessionRecoverySnapshot
    {
        public BrowserSession Session {get;set;}
        public bool Pending {get;set;}
        public DateTime CapturedUtc {get;set;}
    }
}
