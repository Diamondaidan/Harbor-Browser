using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using HarborBrowser.Models;

namespace HarborBrowser.Services
{
    public sealed class BookmarkImport
    {
        public List<Bookmark> Bookmarks {get;set;}=new List<Bookmark>();
        public List<string> Folders {get;set;}=new List<string>();
    }
    public static class BookmarkLibraryService
    {
        public const int MaximumBookmarks=10000;
        public static string Folder(string value)
        {
            var parts=(value??string.Empty).Split(new[]{'/','\\'},StringSplitOptions.RemoveEmptyEntries).Select(p=>p.Trim()).Where(p=>p.Length>0).ToArray();
            if(parts.Length>12||parts.Any(p=>p.Length>80||p=="."||p==".."||p.Any(char.IsControl)))throw new ArgumentException("Use a folder path with up to 12 short names, separated by /.");
            return string.Join("/",parts);
        }
        public static bool TryBookmark(string title,string url,string folder,out Bookmark result)
        {
            result=null;Uri uri;
            url=(url??string.Empty).Trim();
            if(url.Contains(":")&&!url.StartsWith("https://",StringComparison.OrdinalIgnoreCase)&&!url.StartsWith("http://",StringComparison.OrdinalIgnoreCase))return false;
            if(!SearchEngineService.TryNormalizeHomePage(url,out url)||!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.UserInfo.Length>0||HomePageService.IsHomeUrl(url))return false;
            try{result=new Bookmark{Title=string.IsNullOrWhiteSpace(title)?uri.Host:title.Trim().Substring(0,Math.Min(500,title.Trim().Length)),Url=uri.AbsoluteUri,Folder=Folder(folder)};return true;}catch(ArgumentException){return false;}
        }
        public static List<string> Folders(IEnumerable<Bookmark> bookmarks,IEnumerable<string> folders)
        {
            var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string raw in (folders??Enumerable.Empty<string>()).Concat((bookmarks??Enumerable.Empty<Bookmark>()).Where(b=>b!=null).Select(b=>b.Folder)))
            {
                string path;try{path=Folder(raw);}catch(ArgumentException){continue;}
                while(path.Length>0){result.Add(path);int slash=path.LastIndexOf('/');path=slash<0?string.Empty:path.Substring(0,slash);}
            }
            return result.OrderBy(p=>p,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static BookmarkImport Import(string text,bool json)
        {
            if(text==null||text.Length>8*1024*1024)throw new ArgumentException("Choose a bookmark file smaller than 8 MB.");
            BookmarkImport result;
            if(json)
            {
                var serializer=new JavaScriptSerializer{MaxJsonLength=8*1024*1024,RecursionLimit=40};
                result=text.TrimStart().StartsWith("[")?new BookmarkImport{Bookmarks=serializer.Deserialize<List<Bookmark>>(text)}:serializer.Deserialize<BookmarkImport>(text);
                if(result==null)throw new ArgumentException("The bookmark file is empty.");
            }
            else
            {
                result=new BookmarkImport();var stack=new Stack<string>();string current=string.Empty,pending=null;
                var tokens=Regex.Matches(text,@"<H3\b[^>]*>(?<folder>.*?)</H3\s*>|<A\b(?<attrs>[^>]*)>(?<title>.*?)</A\s*>|(?<open><DL\b[^>]*>)|(?<close></DL\s*>)",RegexOptions.IgnoreCase|RegexOptions.Singleline,TimeSpan.FromSeconds(2));
                foreach(Match token in tokens)
                {
                    if(token.Groups["folder"].Success){pending=Clean(token.Groups["folder"].Value).Replace('/',' ').Replace('\\',' ');continue;}
                    if(token.Groups["open"].Success){stack.Push(current);if(pending!=null){current=Folder(current+"/"+pending);result.Folders.Add(current);pending=null;}continue;}
                    if(token.Groups["close"].Success){if(stack.Count>0)current=stack.Pop();pending=null;continue;}
                    var href=Regex.Match(token.Groups["attrs"].Value,@"\bhref\s*=\s*(?:""(?<u>[^""]*)""|'(?<u>[^']*)'|(?<u>[^\s>]+))",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
                    Bookmark bookmark;if(href.Success&&TryBookmark(Clean(token.Groups["title"].Value),WebUtility.HtmlDecode(href.Groups["u"].Value),current,out bookmark))result.Bookmarks.Add(bookmark);
                    if(result.Bookmarks.Count>MaximumBookmarks)throw new ArgumentException("Import up to 10,000 bookmarks at a time.");
                }
            }
            if(result.Bookmarks==null)result.Bookmarks=new List<Bookmark>();
            if(result.Bookmarks.Count>MaximumBookmarks)throw new ArgumentException("Import up to 10,000 bookmarks at a time.");
            var valid=new List<Bookmark>();foreach(var entry in result.Bookmarks){Bookmark normalized;if(entry!=null&&TryBookmark(entry.Title,entry.Url,entry.Folder,out normalized))valid.Add(normalized);}
            result.Bookmarks=Merge(new List<Bookmark>(),valid);result.Folders=Folders(valid,result.Folders);return result;
        }
        public static List<Bookmark> Merge(IEnumerable<Bookmark> existing,IEnumerable<Bookmark> imported)
        {
            var result=new List<Bookmark>();var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in (existing??Enumerable.Empty<Bookmark>()).Concat(imported??Enumerable.Empty<Bookmark>()))
            {
                Bookmark valid;if(entry==null||!TryBookmark(entry.Title,entry.Url,entry.Folder,out valid))continue;
                if(keys.Add((valid.Folder??string.Empty).ToUpperInvariant()+"\n"+valid.Url))result.Add(valid);
                if(result.Count>MaximumBookmarks)throw new ArgumentException("The library supports up to 10,000 bookmarks.");
            }
            return result;
        }
        public static string Export(IEnumerable<Bookmark> bookmarks,IEnumerable<string> folders,bool json)
        {
            var valid=Merge(new List<Bookmark>(),bookmarks);var paths=Folders(valid,folders);
            if(json)return new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Serialize(new BookmarkImport{Bookmarks=valid,Folders=paths});
            var output=new StringBuilder("<!DOCTYPE NETSCAPE-Bookmark-file-1>\n<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">\n<TITLE>Harbor bookmarks</TITLE>\n<H1>Harbor bookmarks</H1>\n<DL><p>\n");
            WriteFolder(output,string.Empty,valid,paths,0);output.Append("</DL><p>\n");return output.ToString();
        }
        private static void WriteFolder(StringBuilder output,string folder,List<Bookmark> bookmarks,List<string> paths,int depth)
        {
            string pad=new string(' ',depth*2);
            foreach(var entry in bookmarks.Where(b=>string.Equals(b.Folder??string.Empty,folder,StringComparison.OrdinalIgnoreCase)))output.Append(pad+"<DT><A HREF=\""+WebUtility.HtmlEncode(entry.Url)+"\">"+WebUtility.HtmlEncode(entry.Title)+"</A>\n");
            foreach(string child in paths.Where(p=>Parent(p).Equals(folder,StringComparison.OrdinalIgnoreCase)))
            {output.Append(pad+"<DT><H3>"+WebUtility.HtmlEncode(child.Substring(child.LastIndexOf('/')+1))+"</H3>\n"+pad+"<DL><p>\n");WriteFolder(output,child,bookmarks,paths,depth+1);output.Append(pad+"</DL><p>\n");}
        }
        private static string Parent(string path){int slash=path.LastIndexOf('/');return slash<0?string.Empty:path.Substring(0,slash);}
        private static string Clean(string value){return WebUtility.HtmlDecode(Regex.Replace(value,@"<[^>]*>",string.Empty,RegexOptions.None,TimeSpan.FromSeconds(1))).Trim();}
    }
}
