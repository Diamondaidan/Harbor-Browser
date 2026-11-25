using System;
using System.Collections.Generic;
using System.Net;
using HarborBrowser.Models;

namespace HarborBrowser.Services
{
    public static class SearchEngineService
    {
        private static readonly IList<SearchEngine> EngineList = new List<SearchEngine>
        {
            new SearchEngine("duckduckgo", "DuckDuckGo", "https://duckduckgo.com/?q={0}"),
            new SearchEngine("google", "Google", "https://www.google.com/search?q={0}"),
            new SearchEngine("bing", "Bing", "https://www.bing.com/search?q={0}"),
            new SearchEngine("brave", "Brave Search", "https://search.brave.com/search?q={0}"),
            new SearchEngine("startpage", "Startpage", "https://www.startpage.com/sp/search?query={0}")
        };

        public static IList<SearchEngine> Engines
        {
            get { return EngineList; }
        }

        public static SearchEngine GetById(string id)
        {
            foreach (SearchEngine engine in EngineList)
            {
                if (string.Equals(engine.Id, id, StringComparison.OrdinalIgnoreCase))
                    return engine;
            }
            return EngineList[0];
        }

        public static string ResolveInput(string input, SearchEngine engine)
        {
            string value = (input ?? string.Empty).Trim();
            if (HomePageService.IsHomeAlias(value))
            {
                return HomePageService.Url;
            }
            if (value.Length == 0)
                return "about:blank";

            if (value.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                return value;

            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                return uri.AbsoluteUri;

            // Treat host-like strings as addresses. Everything else becomes a search.
            IPAddress ip;
            bool looksLikeHost = value.IndexOf(' ') < 0 &&
                (value.IndexOf('.') >= 0 || value.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
                 IPAddress.TryParse(value, out ip));

            if (looksLikeHost)
                return "https://" + value;

            SearchEngine selected = engine ?? EngineList[0];
            return string.Format(selected.SearchUrl, Uri.EscapeDataString(value));
        }

        public static bool TryNormalizeHomePage(string input, out string normalized)
        {
            normalized = null;
            string value = (input ?? string.Empty).Trim();
            if (HomePageService.IsHomeAlias(value))
            {
                normalized = HomePageService.Url; return true;
            }
            if (value.Length == 0)
                return false;

            if (!value.Contains("://"))
                value = "https://" + value;

            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            normalized = uri.AbsoluteUri;
            return true;
        }
    }
}
