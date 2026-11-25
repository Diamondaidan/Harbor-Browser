using System;
using System.Collections.Generic;
using System.Linq;
using HarborBrowser.Models;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Services
{
    public sealed class SitePermissionService
    {
        private readonly List<SitePermissionRule> rules;
        public SitePermissionService(IEnumerable<SitePermissionRule> saved=null)
        {
            rules=new List<SitePermissionRule>();
            foreach(var item in saved??Enumerable.Empty<SitePermissionRule>())
                if(item!=null&&Origin(item.Origin)!=null&&Origin(item.TopLevelOrigin)!=null&&Supported(item.Kind)&&(item.Decision=="Allow"||item.Decision=="Block"))Set(item.Origin,item.TopLevelOrigin,item.Kind,item.Decision,item.UpdatedUtc);
        }
        public static string Origin(string address)
        {
            Uri uri;if(!Uri.TryCreate(address,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http")||uri.UserInfo.Length>0)return null;
            var builder=new UriBuilder(uri.Scheme,uri.IdnHost,uri.IsDefaultPort?-1:uri.Port);return builder.Uri.GetLeftPart(UriPartial.Authority);
        }
        public static bool Supported(string kind)
        {
            return new[]{"Camera","Microphone","Geolocation","Notifications","ClipboardRead","OtherSensors","MultipleAutomaticDownloads","FileReadWrite","LocalFonts","WindowManagement","Autoplay"}.Contains(kind,StringComparer.Ordinal);
        }
        public static string Label(string kind)
        {
            switch(kind){case "Camera":return "Camera";case "Microphone":return "Microphone";case "Geolocation":return "Location";case "Notifications":return "Notifications";case "ClipboardRead":return "Clipboard reading";case "OtherSensors":return "Sensors";case "MultipleAutomaticDownloads":return "Automatic downloads";case "FileReadWrite":return "File access";case "LocalFonts":return "Local fonts";case "WindowManagement":return "Window management";case "Autoplay":return "Autoplay";default:return "Other access";}
        }
        public string Get(string origin,string top,string kind)
        {
            origin=Origin(origin);top=Origin(top);if(origin==null||top==null||!Supported(kind))return "Block";
            var rule=rules.LastOrDefault(r=>r.Origin==origin&&r.TopLevelOrigin==top&&r.Kind==kind);return rule==null?"Ask":rule.Decision;
        }
        public void Set(string origin,string top,string kind,string decision,DateTime now)
        {
            origin=Origin(origin);top=Origin(top);
            if(origin==null||top==null||!Supported(kind)||!new[]{"Ask","Allow","Block"}.Contains(decision))throw new ArgumentException("Choose a valid website, permission and setting.");
            rules.RemoveAll(r=>r.Origin==origin&&r.TopLevelOrigin==top&&r.Kind==kind);
            if(decision!="Ask")rules.Add(new SitePermissionRule{Origin=origin,TopLevelOrigin=top,Kind=kind,Decision=decision,UpdatedUtc=now.ToUniversalTime()});
        }
        public List<SitePermissionRule> Snapshot(){return rules.Select(r=>r.Copy()).ToList();}
        public void Clear(){rules.Clear();}
        public void Clear(BrowsingDataRequest request){rules.RemoveAll(r=>request.Contains(r.UpdatedUtc));}
    }
}
