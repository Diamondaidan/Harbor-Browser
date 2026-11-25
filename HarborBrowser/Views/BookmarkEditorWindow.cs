using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Models;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    public sealed class BookmarkEditorWindow : Window
    {
        public readonly TextBox TitleBox=new TextBox(),UrlBox=new TextBox();
        public readonly ComboBox FolderBox=new ComboBox{IsEditable=true};
        public Bookmark Result {get;private set;}
        public BookmarkEditorWindow(Bookmark bookmark,IEnumerable<string> folders,bool shortcut=false)
        {
            Title=shortcut?"Edit home shortcut":"Edit bookmark";Width=480;Height=shortcut?250:310;MinWidth=350;MinHeight=220;WindowStartupLocation=WindowStartupLocation.CenterOwner;FontFamily=new System.Windows.Media.FontFamily("Segoe UI");FontSize=13;
            WindowFitService.Attach(this);
            var root=new DockPanel{Margin=new Thickness(16)};Content=root;
            var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
            var cancel=new Button{Content="Cancel",Width=85,IsCancel=true};var save=new Button{Content="Save",Width=85,IsDefault=true};buttons.Children.Add(cancel);buttons.Children.Add(save);DockPanel.SetDock(buttons,Dock.Bottom);root.Children.Add(buttons);
            var form=new StackPanel();var scroll=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(scroll);
            form.Children.Add(new TextBlock{Text="Name"});form.Children.Add(TitleBox);form.Children.Add(new TextBlock{Text="Web address",Margin=new Thickness(0,10,0,0)});form.Children.Add(UrlBox);
            if(!shortcut){form.Children.Add(new TextBlock{Text="Folder (use / for subfolders)",Margin=new Thickness(0,10,0,0)});form.Children.Add(FolderBox);}
            FolderBox.Items.Add(string.Empty);foreach(string folder in folders)FolderBox.Items.Add(folder);
            TitleBox.Text=bookmark==null?string.Empty:bookmark.Title;UrlBox.Text=bookmark==null?string.Empty:bookmark.Url;FolderBox.Text=bookmark==null?string.Empty:bookmark.Folder;
            save.Click+=(s,e)=>{Bookmark result;if(!BookmarkLibraryService.TryBookmark(TitleBox.Text,UrlBox.Text,shortcut?string.Empty:FolderBox.Text,out result)){MessageBox.Show(this,"Enter a valid HTTP or HTTPS address and folder name.",Title,MessageBoxButton.OK,MessageBoxImage.Information);return;}Result=result;DialogResult=true;};
            Loaded+=(s,e)=>{TitleBox.Focus();TitleBox.SelectAll();};
        }
    }
}
