using System;
using System.Collections.Generic;
using System.Linq;
using HarborBrowser.Models;

namespace HarborBrowser.Services
{
    public sealed class AddressSuggestion
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string SourceLabel { get; set; }
        public string Query {get;set;}
        public string TargetInput {get{return Query??Url;}}
        internal DateTime Visited { get; set; }
        internal int Rank { get; set; }
    }
    public static class AddressSuggestionService
    {
        public static List<AddressSuggestion> Find(string query, IEnumerable<Bookmark> bookmarks, IEnumerable<HistoryEntry> history, bool isPrivate, int limit=8)
        {
            query=(query??string.Empty).Trim();
            if(query.Length==0 || limit<=0) return new List<AddressSuggestion>();
            var matches=new Dictionary<string,AddressSuggestion>(StringComparer.Ordinal);
            foreach(var bookmark in bookmarks??Enumerable.Empty<Bookmark>())
                if(bookmark!=null) Add(matches,query,bookmark.Title,bookmark.Url,"Bookmark",DateTime.MaxValue);
            if(!isPrivate)
                foreach(var entry in (history??Enumerable.Empty<HistoryEntry>()).Where(h=>h!=null).OrderByDescending(h=>h.VisitedAt))
                    Add(matches,query,entry.Title,entry.Url,"History",entry.VisitedAt);
            return matches.Values.OrderBy(m=>m.Rank).ThenBy(m=>m.SourceLabel=="Bookmark"?0:1).ThenByDescending(m=>m.Visited).ThenBy(m=>m.Url,StringComparer.Ordinal).Take(Math.Min(20,limit)).ToList();
        }
        private static void Add(Dictionary<string,AddressSuggestion> matches,string query,string title,string url,string source,DateTime visited)
        {
            Uri uri;
            if(!Uri.TryCreate(url,UriKind.Absolute,out uri) || (uri.Scheme!="http"&&uri.Scheme!="https") || uri.UserInfo.Length>0 || HomePageService.IsHomeUrl(url))return;
            string key=uri.AbsoluteUri;
            if(matches.ContainsKey(key))return;
            string label=string.IsNullOrWhiteSpace(title)?uri.Host:title;
            string haystack=label+" "+key;
            if(!query.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).All(word=>haystack.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0))return;
            int rank=uri.Host.StartsWith(query,StringComparison.OrdinalIgnoreCase)||label.StartsWith(query,StringComparison.OrdinalIgnoreCase)?0:1;
            matches.Add(key,new AddressSuggestion{Title=label,Url=key,SourceLabel=source,Visited=visited,Rank=rank});
        }
    }
}
