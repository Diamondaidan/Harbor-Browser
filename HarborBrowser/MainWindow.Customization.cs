using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private void ManageBookmarks_Click(object sender,RoutedEventArgs e)
        {
            var dialog=new BookmarksWindow(_bookmarks,_settings.BookmarkFolders){Owner=this};
            if(dialog.ShowDialog()!=true)return;
            _bookmarks.Clear();_bookmarks.AddRange(dialog.ResultBookmarks);_settings.BookmarkFolders=dialog.ResultFolders;
            SaveBookmarks();SaveSettings();RefreshBookmarksBar();UpdateBookmarkButtonState();SetStatus("Bookmarks saved");
        }
        private void FillBookmarkFolder(ItemsControl parent,string folder)
        {
            foreach(var bookmark in _bookmarks.Where(b=>string.Equals(b.Folder??string.Empty,folder,StringComparison.OrdinalIgnoreCase)))
            {
                var entry=CreateGearSubmenu(string.IsNullOrWhiteSpace(bookmark.Title)?bookmark.Url:bookmark.Title);
                AddGearCommand(entry,"Open",(s,e)=>NavigateCurrent(bookmark.Url));AddGearCommand(entry,"Open in new tab",(s,e)=>CreateNewTab(bookmark.Url,true));
                AddGearCommand(entry,"Rename…",(s,e)=>RenameBookmark(bookmark));AddGearCommand(entry,"Remove bookmark",(s,e)=>RemoveBookmark(bookmark));parent.Items.Add(entry);
            }
            foreach(string path in BookmarkLibraryService.Folders(_bookmarks,_settings.BookmarkFolders).Where(p=>ParentBookmarkFolder(p).Equals(folder,StringComparison.OrdinalIgnoreCase)))
            {
                var child=CreateGearSubmenu("▸ "+path.Substring(path.LastIndexOf('/')+1));FillBookmarkFolder(child,path);
                if(child.Items.Count==0)AddGearCommand(child,"Empty folder",null,false);parent.Items.Add(child);
            }
        }
        private static string ParentBookmarkFolder(string path){int slash=path.LastIndexOf('/');return slash<0?string.Empty:path.Substring(0,slash);}
    }
}
