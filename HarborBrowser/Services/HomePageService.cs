using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using HarborBrowser.Models;

namespace HarborBrowser.Services
{
    public static class HomePageService
    {
        public const string Host = "harbor.invalid";
        public const string Url = "https://harbor.invalid/index.html";
        public const string FriendlyUrl = "harbor://home";


        public static bool UpgradeDefaults(BrowserSettings settings)
        {
            if (settings == null || settings.HarborHomeIntroduced) return false;
            if (string.Equals(settings.HomePage, "https://duckduckgo.com/", StringComparison.OrdinalIgnoreCase))
                settings.HomePage = Url;
            settings.HarborHomeIntroduced = true;
            return true;
        }


        public static bool IsHomeUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort &&
                string.IsNullOrEmpty(uri.UserInfo) &&
                string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase) &&
                (uri.AbsolutePath == "/" || uri.AbsolutePath == "/index.html") &&
                string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
        }

        public static bool IsHomeAlias(string value)
        {
            return string.Equals((value ?? "").Trim().TrimEnd('/'), FriendlyUrl, StringComparison.OrdinalIgnoreCase) ||
                IsHomeUrl(value);
        }

        public static string DisplayUrl(string value)
        {
            return IsHomeUrl(value) ? FriendlyUrl : value;
        }

        public static string CreateState(SearchEngine engine, IEnumerable<Bookmark> bookmarks, bool isPrivate, BrowserSettings appearance = null, string backgroundData = null)
        {
            engine = engine ?? SearchEngineService.GetById(null);
            var source = appearance != null && appearance.UseCustomHomeShortcuts ? appearance.HomeShortcuts : bookmarks;
            var links = (source ?? Enumerable.Empty<Bookmark>())
                .Where(b => b != null && IsWebLink(b.Url) && !IsHomeUrl(b.Url))
                .Take(12).Select(b => new { title = b.Title ?? "", url = b.Url }).ToArray();
            return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }.Serialize(new {
                type = "home-state", engineName = engine.Name, searchTemplate = engine.SearchUrl,
                isPrivate = isPrivate, bookmarks = links, appearance = appearance == null ? null : AppearanceService.HomeAppearance(appearance),
                scene = appearance == null ? "Auto" : appearance.HomeScene,
                customShortcuts = appearance != null && appearance.UseCustomHomeShortcuts,
                backgroundData = backgroundData != null && backgroundData.StartsWith("data:image/png;base64,", StringComparison.Ordinal) ? backgroundData : null
            });
        }

        public static bool IsWebLink(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && string.IsNullOrEmpty(uri.UserInfo);
        }

        public static Dictionary<string,object> ReadMessage(string source,string json)
        {
            if(!IsHomeUrl(source)||string.IsNullOrEmpty(json)||json.Length>12000)return null;
            try{return new JavaScriptSerializer{MaxJsonLength=12000,RecursionLimit=8}.Deserialize<Dictionary<string,object>>(json);}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}
        }


        public static string ReadThemeMessage(string source,string json)
        {
            if(!IsHomeUrl(source)||string.IsNullOrEmpty(json)||json.Length>500)return null;
            try
            {
                var message=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
                object type,value;
                if(message!=null&&message.TryGetValue("type",out type)&&(type as string)=="home-theme"&&
                    message.TryGetValue("value",out value)&&((value as string)=="Light"||(value as string)=="Dark"))
                    return (string)value;
            }
            catch(ArgumentException){}catch(InvalidOperationException){}
            return null;
        }

        public static string ReadSearchMessage(string source, string json)
        {
            if (!IsHomeUrl(source) || string.IsNullOrEmpty(json) || json.Length > 12000) return null;
            try
            {
                var message = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                object type, value;
                if (message != null && message.TryGetValue("type", out type) &&
                    string.Equals(type as string, "home-search", StringComparison.Ordinal) &&
                    message.TryGetValue("value", out value) && value is string)
                {
                    string input = ((string)value).Trim();
                    if (input.Length > 0 && input.Length <= 4096) return input;
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            return null;
        }
    }
}
