using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using HarborBrowser.Models;
using HarborBrowser.Services;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private Point _bookmarkDragStart;
        private Button _bookmarkDragButton,_bookmarkDropMarker;
        private bool _bookmarkDragging;
        private sealed class BookmarkDrag {public MainWindow Owner;public string Key;}
        private void RefreshBookmarksBar()
        {
            ClearBookmarkDropMarker();BookmarksBarPanel.Children.Clear();
            BookmarksBarPanel.Background=Brushes.Transparent;
            BookmarksBarPanel.MinWidth=Math.Max(0,BookmarksBar.ActualWidth-2);
            BookmarksBarPanel.AllowDrop=true;
            BookmarksBarPanel.DragOver-=BookmarkBarDragOver;BookmarksBarPanel.DragOver+=BookmarkBarDragOver;
            BookmarksBarPanel.Drop-=BookmarkBarDrop;BookmarksBarPanel.Drop+=BookmarkBarDrop;
            BookmarksBarPanel.DragLeave-=BookmarkBarDragLeave;BookmarksBarPanel.DragLeave+=BookmarkBarDragLeave;
            foreach(var entry in BookmarkBarService.Entries(_bookmarks,_settings))
            {
                var button=new Button{Content=entry.Bookmark==null?entry.Folder+" ▾":string.IsNullOrWhiteSpace(entry.Bookmark.Title)?entry.Bookmark.Url:entry.Bookmark.Title,
                    Tag=(object)entry.Bookmark??entry.Folder,MaxWidth=190,ToolTip=entry.Bookmark==null?entry.Folder:entry.Bookmark.Url,
                    Padding=new Thickness(7,2,7,2),Margin=new Thickness(2,1,2,1),Background=Brushes.Transparent,BorderThickness=new Thickness(2,0,2,0)};
                button.ContextMenu=entry.Bookmark==null?new ContextMenu():BuildBookmarkContextMenu(entry.Bookmark);
                AddBookmarkMoveCommands(button.ContextMenu,entry.Key);
                button.Click+=(s,e)=>
                {
                    if(_bookmarkDragging)return;
                    if(entry.Bookmark!=null){NavigateCurrent(entry.Bookmark.Url);return;}
                    var menu=new ContextMenu{PlacementTarget=button,Placement=PlacementMode.Bottom};FillBookmarkFolder(menu,entry.Folder);
                    if(menu.Items.Count==0)AddGearCommand(menu,"Empty folder",null,false);menu.IsOpen=true;
                };
                button.PreviewMouseLeftButtonDown+=(s,e)=>{_bookmarkDragStart=e.GetPosition(BookmarksBarPanel);_bookmarkDragButton=button;};
                button.PreviewMouseMove+=(s,e)=>
                {
                    if(e.LeftButton!=MouseButtonState.Pressed||_bookmarkDragging||_bookmarkDragButton!=button)return;
                    var p=e.GetPosition(BookmarksBarPanel);
                    if(Math.Abs(p.X-_bookmarkDragStart.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(p.Y-_bookmarkDragStart.Y)<SystemParameters.MinimumVerticalDragDistance)return;
                    _bookmarkDragging=true;
                    try{DragDrop.DoDragDrop(button,new DataObject(typeof(BookmarkDrag),new BookmarkDrag{Owner=this,Key=entry.Key}),DragDropEffects.Move);}
                    finally{_bookmarkDragging=false;_bookmarkDragButton=null;button.ReleaseMouseCapture();ClearBookmarkDropMarker();}
                    e.Handled=true;
                };
                BookmarksBarPanel.Children.Add(button);
            }
        }
        private void AddBookmarkMoveCommands(ContextMenu menu,string key)
        {
            var keys=BookmarkBarService.Entries(_bookmarks,_settings).Select(e=>e.Key).ToList();int index=keys.IndexOf(key);
            if(menu.Items.Count>0)menu.Items.Add(new Separator());
            var left=new MenuItem{Header="Move left",IsEnabled=index>0};left.Click+=(s,e)=>MoveBookmark(key,keys[index-1],false);menu.Items.Add(left);
            var right=new MenuItem{Header="Move right",IsEnabled=index>=0&&index<keys.Count-1};right.Click+=(s,e)=>MoveBookmark(key,keys[index+1],true);menu.Items.Add(right);
        }
        internal void MoveBookmark(string source,string target,bool after)
        {
            var keys=BookmarkBarService.Entries(_bookmarks,_settings).Select(e=>e.Key).ToList();
            _settings.BookmarkBarOrder=BookmarkBarService.Move(keys,source,target,after);SaveBookmarks();SaveSettings();RefreshBookmarksBar();SetStatus("Bookmark bar order saved");
        }
        private BookmarkDrag BookmarkPayload(DragEventArgs e){return e.Data.GetDataPresent(typeof(BookmarkDrag))?e.Data.GetData(typeof(BookmarkDrag)) as BookmarkDrag:null;}
        private Button BookmarkDropTarget(Point p){return BookmarksBarPanel.Children.OfType<Button>().FirstOrDefault(b=>{var x=b.TranslatePoint(new Point(),BookmarksBarPanel).X;return p.X>=x&&p.X<x+b.ActualWidth;});}
        private string BookmarkButtonKey(Button b){return b.Tag is Bookmark?BookmarkBarService.Key((Bookmark)b.Tag):BookmarkBarService.FolderKey((string)b.Tag);}
        private void BookmarkBarDragOver(object sender,DragEventArgs e)
        {
            var data=BookmarkPayload(e);if(data==null||data.Owner!=this){e.Effects=DragDropEffects.None;e.Handled=true;return;}
            e.Effects=DragDropEffects.Move;e.Handled=true;ClearBookmarkDropMarker();
            var p=e.GetPosition(BookmarksBarPanel);var target=BookmarkDropTarget(p);
            if(target!=null){bool after=p.X>target.TranslatePoint(new Point(),BookmarksBarPanel).X+target.ActualWidth/2;target.SetResourceReference(Control.BorderBrushProperty,"HarborAccent");target.BorderThickness=after?new Thickness(0,0,3,0):new Thickness(3,0,0,0);_bookmarkDropMarker=target;}
            var scroller=BookmarksBarPanel.Parent as ScrollViewer;
            if(scroller!=null){double x=e.GetPosition(scroller).X;if(x<28)scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset-18);else if(x>scroller.ActualWidth-28)scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset+18);}
        }
        private void BookmarkBarDrop(object sender,DragEventArgs e)
        {
            var data=BookmarkPayload(e);ClearBookmarkDropMarker();if(data==null||data.Owner!=this)return;
            var p=e.GetPosition(BookmarksBarPanel);var target=BookmarkDropTarget(p);
            MoveBookmark(data.Key,target==null?null:BookmarkButtonKey(target),target!=null&&p.X>target.TranslatePoint(new Point(),BookmarksBarPanel).X+target.ActualWidth/2);e.Handled=true;
        }
        private void BookmarkBarDragLeave(object sender,DragEventArgs e){ClearBookmarkDropMarker();}
        private void ClearBookmarkDropMarker(){if(_bookmarkDropMarker==null)return;_bookmarkDropMarker.ClearValue(Control.BorderBrushProperty);_bookmarkDropMarker.BorderThickness=new Thickness(2,0,2,0);_bookmarkDropMarker=null;}
    }
}
