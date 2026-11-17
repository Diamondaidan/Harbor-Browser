using System;
using System.Collections.Generic;
using System.Linq;
using HarborBrowser.Models;

namespace HarborBrowser.Services
{
    public sealed class BookmarkBarEntry
    {
        public string Key,Folder;
        public Bookmark Bookmark;
    }
    public static class BookmarkBarService
    {
        public static bool EnsureIds(IEnumerable<Bookmark> bookmarks)
        {
            bool changed=false;var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var b in bookmarks.Where(b=>b!=null))
            {
                Guid id;
                if(!Guid.TryParseExact(b.Id,"N",out id)||!seen.Add(b.Id))
                {b.Id=Guid.NewGuid().ToString("N");seen.Add(b.Id);changed=true;}
            }
            return changed;
        }
        public static string Key(Bookmark bookmark){return "bookmark:"+bookmark.Id;}
        public static string FolderKey(string folder){return "folder:"+folder.ToUpperInvariant();}
        public static List<BookmarkBarEntry> Entries(List<Bookmark> bookmarks,BrowserSettings settings)
        {
            EnsureIds(bookmarks);
            var entries=bookmarks.Where(b=>b!=null&&string.IsNullOrEmpty(b.Folder)).Select(b=>new BookmarkBarEntry{Key=Key(b),Bookmark=b}).ToList();
            entries.AddRange(BookmarkLibraryService.Folders(bookmarks,settings.BookmarkFolders).Where(f=>!f.Contains("/")).Select(f=>new BookmarkBarEntry{Key=FolderKey(f),Folder=f}));
            var order=settings.BookmarkBarOrder??new List<string>();
            var result=new List<BookmarkBarEntry>();
            foreach(string key in order){var entry=entries.FirstOrDefault(e=>e.Key==key);if(entry!=null){result.Add(entry);entries.Remove(entry);}}
            result.AddRange(entries);return result;
        }
        public static List<string> Move(IList<string> order,string source,string target,bool after)
        {
            var result=order.Distinct().ToList();if(source==target||!result.Contains(source)||target!=null&&!result.Contains(target))return result;
            result.Remove(source);int index=target==null?result.Count:result.IndexOf(target)+(after?1:0);result.Insert(index,source);return result;
        }
    }
}
