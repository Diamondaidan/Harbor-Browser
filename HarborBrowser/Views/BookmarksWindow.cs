using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using HarborBrowser.Models;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    public sealed class BookmarksWindow : Window
    {
        private readonly List<Bookmark> entries;
        private List<string> folders;
        private readonly bool shortcuts;
        public readonly ComboBox FolderFilter=new ComboBox{Width=190,DisplayMemberPath="Label",SelectedValuePath="Value"};
        public readonly TextBox SearchBox=new TextBox{Width=230};
        public readonly DataGrid BookmarkGrid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Single,HeadersVisibility=DataGridHeadersVisibility.Column};
        private readonly TextBlock status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
        public List<Bookmark> ResultBookmarks {get;private set;}
        public List<string> ResultFolders {get;private set;}
        public BookmarksWindow(IEnumerable<Bookmark> bookmarks,IEnumerable<string> folderPaths,bool homeShortcuts=false)
        {
            shortcuts=homeShortcuts;entries=(bookmarks??Enumerable.Empty<Bookmark>()).Where(b=>b!=null).Select(b=>b.Copy()).ToList();folders=BookmarkLibraryService.Folders(entries,folderPaths);
            Title=shortcuts?"Home shortcuts":"Bookmarks";Width=900;Height=620;MinWidth=480;MinHeight=300;WindowStartupLocation=WindowStartupLocation.CenterOwner;FontFamily=new System.Windows.Media.FontFamily("Segoe UI");FontSize=13;WindowFitService.Attach(this);
            var root=new DockPanel{Margin=new Thickness(16)};Content=root;
            var footer=new DockPanel{Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(buttons,Dock.Right);footer.Children.Add(buttons);
            AddButton(buttons,"Cancel",()=>DialogResult=false).IsCancel=true;AddButton(buttons,"Save",()=>{ResultBookmarks=entries.Select(b=>b.Copy()).ToList();ResultFolders=BookmarkLibraryService.Folders(entries,folders);DialogResult=true;}).IsDefault=true;
            footer.Children.Add(new TextBlock{Text="Save applies your changes.",VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,12,0)});
            var header=new StackPanel{Margin=new Thickness(0,0,0,10)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            header.Children.Add(new TextBlock{Text=Title,FontSize=23,FontWeight=FontWeights.SemiBold});
            var filters=new WrapPanel{Margin=new Thickness(0,10,0,8)};header.Children.Add(filters);if(!shortcuts)filters.Children.Add(FolderFilter);
            var searchGroup=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(shortcuts?0:8,0,0,0)};searchGroup.Children.Add(new TextBlock{Text="Search",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)});SearchBox.ToolTip="Search names, addresses or folders";System.Windows.Automation.AutomationProperties.SetName(SearchBox,"Search bookmarks");searchGroup.Children.Add(SearchBox);filters.Children.Add(searchGroup);
            var actions=new WrapPanel();header.Children.Add(actions);
            AddButton(actions,"Add…",()=>Edit(null));AddButton(actions,"Edit…",()=>{if(BookmarkGrid.SelectedItem!=null)Edit(BookmarkGrid.SelectedItem as Bookmark);});AddButton(actions,"Remove",()=>{var selected=BookmarkGrid.SelectedItem as Bookmark;if(selected!=null){entries.Remove(selected);Refresh();}});
            AddButton(actions,"↑",()=>MoveSelected(-1));AddButton(actions,"↓",()=>MoveSelected(1));
            if(!shortcuts)
            {
                var folderButton=AddButton(actions,"Folders…",()=>{});folderButton.Click+=(s,e)=>OpenFolderMenu(folderButton);
                AddButton(actions,"Import…",ImportFile);AddButton(actions,"Export…",ExportFile);
            }
            header.Children.Add(status);
            BookmarkGrid.Columns.Add(new DataGridTextColumn{Header="Name",Binding=new Binding("Title"),Width=new DataGridLength(2,DataGridLengthUnitType.Star)});
            BookmarkGrid.Columns.Add(new DataGridTextColumn{Header="Address",Binding=new Binding("Url"),Width=new DataGridLength(3,DataGridLengthUnitType.Star)});
            if(!shortcuts)BookmarkGrid.Columns.Add(new DataGridTextColumn{Header="Folder",Binding=new Binding("Folder"),Width=new DataGridLength(1.5,DataGridLengthUnitType.Star)});
            BookmarkGrid.MouseDoubleClick+=(s,e)=>Edit(BookmarkGrid.SelectedItem as Bookmark);root.Children.Add(BookmarkGrid);
            FolderFilter.SelectionChanged+=(s,e)=>Refresh();SearchBox.TextChanged+=(s,e)=>Refresh();RefreshFolders();
        }
        private Button AddButton(Panel panel,string text,Action action){var button=new Button{Content=text,Padding=new Thickness(10,5,10,5)};button.Click+=(s,e)=>action();panel.Children.Add(button);return button;}
        private void RefreshFolders(string select="*")
        {
            folders=BookmarkLibraryService.Folders(entries,folders);
            FolderFilter.Items.Clear();FolderFilter.Items.Add(new Choice{Label="All bookmarks",Value="*"});FolderFilter.Items.Add(new Choice{Label="Unfiled",Value=string.Empty});foreach(string folder in folders)FolderFilter.Items.Add(new Choice{Label=folder,Value=folder});FolderFilter.SelectedValue=select;Refresh();
        }
        private void Refresh()
        {
            string filter=Convert.ToString(FolderFilter.SelectedValue),query=(SearchBox.Text??string.Empty).Trim();
            BookmarkGrid.ItemsSource=entries.Where(b=>(shortcuts||filter=="*"||string.Equals(b.Folder??string.Empty,filter,StringComparison.OrdinalIgnoreCase))&&(query.Length==0||((b.Title??string.Empty)+" "+b.Url+" "+b.Folder).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
            status.Text=entries.Count+(shortcuts?" of 12 home shortcuts. Use ↑ and ↓ to arrange them.":" bookmarks. Folders use / for subfolders.");
        }
        private void Edit(Bookmark bookmark)
        {
            var editor=new BookmarkEditorWindow(bookmark,folders,shortcuts){Owner=this};
            if(editor.ShowDialog()!=true)return;
            int index=bookmark==null?-1:entries.IndexOf(bookmark);
            try{ApplyEdit(index,editor.Result.Title,editor.Result.Url,editor.Result.Folder);}catch(ArgumentException ex){MessageBox.Show(this,ex.Message,Title);}
        }
        internal void ApplyEdit(int index,string title,string url,string folder)
        {
            Bookmark bookmark;if(!BookmarkLibraryService.TryBookmark(title,url,shortcuts?string.Empty:folder,out bookmark))throw new ArgumentException("Enter a valid HTTP or HTTPS address and folder.");
            if(index<0){if(entries.Count>=(shortcuts?12:BookmarkLibraryService.MaximumBookmarks))throw new ArgumentException(shortcuts?"Use up to 12 home shortcuts.":"The library is full.");entries.Add(bookmark);}else entries[index]=bookmark;
            RefreshFolders(shortcuts?"*":(FolderFilter.SelectedValue as string??"*"));BookmarkGrid.SelectedItem=bookmark;
        }
        internal void MoveSelected(int direction)
        {
            var selected=BookmarkGrid.SelectedItem as Bookmark;var visible=BookmarkGrid.Items.Cast<Bookmark>().ToList();int at=visible.IndexOf(selected),next=at+direction;
            if(at<0||next<0||next>=visible.Count)return;int target=entries.IndexOf(visible[next]);entries.Remove(selected);entries.Insert(target,selected);Refresh();BookmarkGrid.SelectedItem=selected;
        }
        private void OpenFolderMenu(Button button)
        {
            var menu=new ContextMenu{PlacementTarget=button};string selected=FolderFilter.SelectedValue as string;
            var add=new MenuItem{Header="New folder…"};add.Click+=(s,e)=>ChangeFolder(null);menu.Items.Add(add);
            var rename=new MenuItem{Header="Rename / move selected folder…",IsEnabled=!string.IsNullOrEmpty(selected)&&selected!="*"};rename.Click+=(s,e)=>ChangeFolder(selected);menu.Items.Add(rename);
            var remove=new MenuItem{Header="Remove selected folder (keep bookmarks)",IsEnabled=rename.IsEnabled};remove.Click+=(s,e)=>{foreach(var entry in entries.Where(b=>Within(b.Folder,selected)))entry.Folder=string.Empty;folders.RemoveAll(p=>Within(p,selected));RefreshFolders();};menu.Items.Add(remove);menu.IsOpen=true;
        }
        private void ChangeFolder(string old)
        {
            var prompt=new Window{Title=old==null?"New folder":"Rename folder",Owner=this,Width=420,Height=165,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};WindowFitService.Attach(prompt);
            var panel=new StackPanel{Margin=new Thickness(14)};var text=new TextBox{Text=old??string.Empty};panel.Children.Add(new TextBlock{Text="Folder path (for example Work/Projects)",Margin=new Thickness(0,0,0,6)});panel.Children.Add(text);var save=new Button{Content="Save",IsDefault=true,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,10,0,0)};panel.Children.Add(save);prompt.Content=panel;
            save.Click+=(s,e)=>{try{RenameFolder(old,text.Text);prompt.DialogResult=true;}catch(ArgumentException ex){MessageBox.Show(prompt,ex.Message,Title);}};prompt.ShowDialog();
        }
        internal void RenameFolder(string old,string target)
        {
            target=BookmarkLibraryService.Folder(target);if(target.Length==0)throw new ArgumentException("Enter a folder name.");
            if(old!=null&&target.StartsWith(old+"/",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Choose a location outside this folder.");
            if(folders.Any(p=>p.Equals(target,StringComparison.OrdinalIgnoreCase)&&!p.Equals(old,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("That folder already exists.");
            if(old!=null){foreach(var entry in entries.Where(b=>Within(b.Folder,old)))entry.Folder=target+(entry.Folder??string.Empty).Substring(old.Length);folders=folders.Select(p=>Within(p,old)?target+p.Substring(old.Length):p).ToList();}
            folders.Add(target);RefreshFolders(target);
        }
        internal int ImportText(string text,bool json)
        {
            var imported=BookmarkLibraryService.Import(text,json);int before=entries.Count;var merged=BookmarkLibraryService.Merge(entries,imported.Bookmarks);entries.Clear();entries.AddRange(merged);folders=BookmarkLibraryService.Folders(entries,folders.Concat(imported.Folders));RefreshFolders();return entries.Count-before;
        }
        private void ImportFile()
        {
            var picker=new OpenFileDialog{Filter="Bookmark files|*.html;*.htm;*.json|All files|*.*"};if(picker.ShowDialog(this)!=true)return;
            try{if(new FileInfo(picker.FileName).Length>8*1024*1024)throw new ArgumentException("Choose a file smaller than 8 MB.");int added=ImportText(File.ReadAllText(picker.FileName),Path.GetExtension(picker.FileName).Equals(".json",StringComparison.OrdinalIgnoreCase));status.Text="Imported "+added+" new bookmarks. Save to keep them.";}catch(Exception ex){MessageBox.Show(this,"Could not import bookmarks. "+ex.Message,Title);}
        }
        private void ExportFile()
        {
            var picker=new SaveFileDialog{Filter="Browser bookmarks (HTML)|*.html|Harbor bookmarks (JSON)|*.json",FileName="Harbor-bookmarks.html",AddExtension=true};if(picker.ShowDialog(this)!=true)return;
            try{File.WriteAllText(picker.FileName,BookmarkLibraryService.Export(entries,folders,Path.GetExtension(picker.FileName).Equals(".json",StringComparison.OrdinalIgnoreCase)),new UTF8Encoding(false));status.Text="Bookmarks exported.";}catch(Exception ex){MessageBox.Show(this,"Could not export bookmarks. "+ex.Message,Title);}
        }
        private static bool Within(string path,string folder){return string.Equals(path??string.Empty,folder,StringComparison.OrdinalIgnoreCase)||(path??string.Empty).StartsWith(folder+"/",StringComparison.OrdinalIgnoreCase);}
        private sealed class Choice{public string Label{get;set;}public string Value{get;set;}public override string ToString(){return Label??string.Empty;}}
    }
}
