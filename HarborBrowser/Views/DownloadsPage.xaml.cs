using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HarborBrowser.Models;

namespace HarborBrowser.Views
{
    public partial class DownloadsPage : UserControl, IDisposable
    {
        private readonly ListCollectionView _view;
        private readonly Action<DownloadEntry,string> _action;
        private readonly Action _folder,_clear;
        public bool IsPrivate {get;private set;}
        public DownloadsPage(ObservableCollection<DownloadEntry> entries,bool isPrivate,Action<DownloadEntry,string> action,Action folder,Action clear)
        {
            InitializeComponent();IsPrivate=isPrivate;_action=action;_folder=folder;_clear=clear;
            _view=new ListCollectionView(entries);
            _view.Filter=value=>{var entry=value as DownloadEntry;return entry!=null&&entry.IsPrivate==IsPrivate&&(string.IsNullOrWhiteSpace(DownloadSearch.Text)||(entry.FileName??string.Empty).IndexOf(DownloadSearch.Text.Trim(),StringComparison.OrdinalIgnoreCase)>=0);};
            DownloadList.ItemsSource=_view;
            if(isPrivate){PageHeading.Text="Private downloads";PageDescription.Text="Files downloaded in private tabs still remain on this computer. This list lasts for the current browser session.";}
        }
        private void Search_Changed(object sender,TextChangedEventArgs e){if(_view!=null)_view.Refresh();}
        private void Action_Click(object sender,RoutedEventArgs e){var button=sender as Button;if(button!=null&&button.DataContext is DownloadEntry)_action((DownloadEntry)button.DataContext,Convert.ToString(button.Tag));}
        private void Folder_Click(object sender,RoutedEventArgs e){_folder();}
        private void Clear_Click(object sender,RoutedEventArgs e){_clear();}
        public void Dispose(){DownloadList.ItemsSource=null;_view.DetachFromSourceCollection();}
    }
}
