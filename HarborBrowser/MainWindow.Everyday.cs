using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        internal const string DownloadsUrl="harbor://downloads";
        private readonly RecentlyClosedTabsService _recentlyClosed=new RecentlyClosedTabsService();
        private readonly System.Collections.Generic.HashSet<DownloadEntry> _resumingDownloads=new System.Collections.Generic.HashSet<DownloadEntry>();
        private readonly System.Collections.Generic.Dictionary<DownloadEntry,BrowserTab> _downloadOwners=new System.Collections.Generic.Dictionary<DownloadEntry,BrowserTab>();
        private readonly System.Collections.Generic.HashSet<BrowserTab> _retainedDownloadTabs=new System.Collections.Generic.HashSet<BrowserTab>();
        private bool _settingAddress,_addressEditing;
        private System.Threading.CancellationTokenSource _suggestionCancellation;
        private int _suggestionRevision;
        private AddressSuggestion _pressedAddressSuggestion;
        internal OnlineSuggestionService OnlineSuggestions=new OnlineSuggestionService();
        private static bool IsDownloadsUrl(string value){return string.Equals((value??string.Empty).Trim().TrimEnd('/'),DownloadsUrl,StringComparison.OrdinalIgnoreCase);}
        private void InitializeEverydayFeatures()
        {
            AddressBox.TextChanged+=(s,e)=>RefreshAddressSuggestions();
            AddressBox.LostKeyboardFocus+=(s,e)=>{if(!AddressSuggestions.IsMouseOver){AddressSuggestions.IsOpen=false;_addressEditing=false;}};
            Deactivated+=(s,e)=>AddressSuggestions.IsOpen=false;
            SizeChanged+=(s,e)=>AddressSuggestions.IsOpen=false;
            AddressSuggestions.Closed+=(s,e)=>{_pressedAddressSuggestion=null;CancelOnlineSuggestions();};
            Closed+=(s,e)=>CancelOnlineSuggestions();
        }
        private void SetAddressText(string value)
        {
            if(_addressEditing&&AddressBox.IsKeyboardFocusWithin)return;
            _settingAddress=true;
            try{AddressSuggestions.IsOpen=false;AddressBox.Text=value??string.Empty;}
            finally{_settingAddress=false;}
        }
        internal void RefreshAddressSuggestions()
        {
            CancelOnlineSuggestions();
            if(_settingAddress||!AddressBox.IsKeyboardFocusWithin){AddressSuggestions.IsOpen=false;return;}
            _addressEditing=true;
            var suggestions=AddressSuggestionService.Find(AddressBox.Text,_bookmarks,_history,SelectedTab!=null&&SelectedTab.IsPrivate);
            if(suggestions.Count==0&&OnlineSuggestionService.IsSearchQuery(AddressBox.Text))suggestions.Add(new AddressSuggestion{Title="Search "+SearchEngineService.GetById(_settings.SearchEngineId).Name+" for “"+AddressBox.Text.Trim()+"”",Query=AddressBox.Text.Trim(),SourceLabel="Search the web"});
            AddressSuggestionList.ItemsSource=suggestions;AddressSuggestionList.SelectedIndex=-1;
            AddressSuggestions.IsOpen=suggestions.Count>0;
            if(AllowOnlineSuggestions&&SelectedTab!=null&&!SelectedTab.IsPrivate&&OnlineSuggestionService.IsSearchQuery(AddressBox.Text))
            {
                _suggestionCancellation=new System.Threading.CancellationTokenSource();
                _ = FillOnlineSuggestions(AddressBox.Text.Trim(),_settings.SearchEngineId,SelectedTab,suggestions,_suggestionRevision,_suggestionCancellation.Token);
            }
        }
        private void CancelOnlineSuggestions(){_suggestionRevision++;if(_suggestionCancellation!=null){_suggestionCancellation.Cancel();_suggestionCancellation.Dispose();_suggestionCancellation=null;}}
        private async System.Threading.Tasks.Task FillOnlineSuggestions(string query,string engine,BrowserTab tab,System.Collections.Generic.List<AddressSuggestion> local,int revision,System.Threading.CancellationToken cancellation)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(280,cancellation);
                var terms=await OnlineSuggestions.GetAsync(query,engine,AllowOnlineSuggestions,tab.IsPrivate,cancellation);
                if(cancellation.IsCancellationRequested||revision!=_suggestionRevision||SelectedTab!=tab||tab.IsPrivate||!AllowOnlineSuggestions||_settings.SearchEngineId!=engine||!AddressBox.IsKeyboardFocusWithin||AddressBox.Text.Trim()!=query||!AddressSuggestions.IsOpen||AddressSuggestionList.SelectedIndex>=0)return;
                if(terms.Count==0)return;
                var remote=terms.Select(term=>new AddressSuggestion{Title=term,Query=term,SourceLabel=SearchEngineService.GetById(engine).Name+" suggestion"});
                AddressSuggestionList.ItemsSource=remote.Take(5).Concat(local.Where(s=>s.Query==null)).Take(8).ToList();
            }
            catch(OperationCanceledException){}
        }
        private AddressSuggestion AddressSuggestionFromSource(object source)
        {
            var element=source as DependencyObject;
            var item=element==null?null:ItemsControl.ContainerFromElement(AddressSuggestionList,element) as ListBoxItem;
            return item==null?null:AddressSuggestionList.ItemContainerGenerator.ItemFromContainer(item) as AddressSuggestion;
        }
        private void AddressSuggestion_MouseDown(object sender,MouseButtonEventArgs e)
        {
            _pressedAddressSuggestion=AddressSuggestionFromSource(e.OriginalSource);
            if(_pressedAddressSuggestion==null)return;
            CancelOnlineSuggestions();AddressSuggestionList.SelectedItem=_pressedAddressSuggestion;e.Handled=true;
        }
        private void AddressSuggestion_Click(object sender,MouseButtonEventArgs e)
        {
            var suggestion=AddressSuggestionFromSource(e.OriginalSource);
            bool activate=suggestion!=null&&ReferenceEquals(suggestion,_pressedAddressSuggestion);_pressedAddressSuggestion=null;
            if(activate){AddressSuggestions.IsOpen=false;NavigateCurrent(suggestion.Query==null?suggestion.Url:SearchSuggestionTarget(suggestion.Query));e.Handled=true;}
        }
        private bool HandleSuggestionKey(KeyEventArgs e)
        {
            if(!AddressSuggestions.IsOpen)return false;
            if(e.Key==Key.Down||e.Key==Key.Up)
            {
                int count=AddressSuggestionList.Items.Count;
                int index=AddressSuggestionList.SelectedIndex;
                AddressSuggestionList.SelectedIndex=e.Key==Key.Down?(index+1)%count:(index<=0?count-1:index-1);
                AddressSuggestionList.ScrollIntoView(AddressSuggestionList.SelectedItem);e.Handled=true;return true;
            }
            if(e.Key==Key.Escape){AddressSuggestions.IsOpen=false;e.Handled=true;return true;}
            if(e.Key==Key.Enter)
            {
                var suggestion=AddressSuggestionList.SelectedItem as AddressSuggestion;
                AddressSuggestions.IsOpen=false;
                NavigateCurrent(suggestion==null?AddressBox.Text:suggestion.Query==null?suggestion.Url:SearchSuggestionTarget(suggestion.Query));e.Handled=true;return true;
            }
            return false;
        }
        private void RememberClosedTab(BrowserTab tab)
        {
            string url=tab.NativeContent!=null?DownloadsUrl:tab.PendingUrl??(tab.View.Source==null?null:tab.View.Source.AbsoluteUri);
            _recentlyClosed.Remember(new ClosedTabSnapshot{Url=url,Title=tab.PageTitle,GroupName=tab.GroupName,IsPinned=tab.IsPinned,Index=BrowserTabs.Items.IndexOf(tab.Item)},tab.IsPrivate);
        }
        internal BrowserTab ReopenClosedTab()
        {
            var saved=_recentlyClosed.TakeLast();
            if(saved==null){SetStatus("No recently closed tabs");return null;}
            string group=_tabGroups.FirstOrDefault(g=>string.Equals(g,saved.GroupName,StringComparison.OrdinalIgnoreCase));
            var tab=CreateNewTab(saved.Url,false,false,new BrowserSessionTab{Url=saved.Url,IsPinned=saved.IsPinned,GroupName=group});
            tab.PageTitle=saved.Title;UpdateTabHeader(tab);
            _reorderingTabs=true;
            try
            {
                BrowserTabs.Items.Remove(tab.Item);
                int pinned=GetPinnedCount();
                int index=tab.IsPinned?Math.Min(Math.Max(0,saved.Index),pinned):Math.Min(BrowserTabs.Items.Count,Math.Max(pinned,saved.Index));
                BrowserTabs.Items.Insert(index,tab.Item);
            }
            finally{_reorderingTabs=false;}
            _activeGroupFilter="*";ApplyTabGroupFilter();BrowserTabs.SelectedItem=tab.Item;
            SaveSession();SetStatus("Reopened "+(saved.Title??"closed tab"));return tab;
        }
        private void ReopenClosedTab_Click(object sender,RoutedEventArgs e){ReopenClosedTab();}
        private void AddReopenCommand(ContextMenu menu)
        {
            var item=new MenuItem{Header="Reopen closed tab (Ctrl+Shift+T)"};item.Click+=ReopenClosedTab_Click;
            menu.Items.Add(item);menu.Opened+=(s,e)=>item.IsEnabled=_recentlyClosed.Count>0;
        }
        internal BrowserTab OpenDownloadsPage()
        {
            _addressEditing=false;
            bool isPrivate=SelectedTab!=null&&SelectedTab.IsPrivate;
            string session=isPrivate?SelectedTab.PrivateSessionId:null;
            var existing=BrowserTabs.Items.Cast<TabItem>().Select(i=>i.Tag as BrowserTab).FirstOrDefault(t=>t!=null&&t.NativeContent is DownloadsPage&&t.IsPrivate==isPrivate&&(!isPrivate||t.PrivateSessionId==session));
            var tab=existing??CreateNewTab(DownloadsUrl,false,isPrivate,null);
            _activeGroupFilter="*";ApplyTabGroupFilter();BrowserTabs.SelectedItem=tab.Item;SetAddressText(DownloadsUrl);return tab;
        }
        private void ShowDownloads_Click(object sender,RoutedEventArgs e){OpenDownloadsPage();}
        private void ConfigureDownloadsTab(BrowserTab tab)
        {
            tab.PendingUrl=DownloadsUrl;tab.PageTitle=tab.IsPrivate?"Private downloads":"Downloads";
            tab.NativeContent=new DownloadsPage(_downloads,tab.IsPrivate,DownloadAction,OpenDownloadsFolder,()=>ClearCompletedDownloads(tab.IsPrivate,tab.PrivateSessionId),tab.PrivateSessionId);
        }
        private bool RebindResumedDownload(BrowserTab tab,CoreWebView2DownloadStartingEventArgs args)
        {
            if(args.DownloadOperation==null)return false;
            var candidates=_resumingDownloads.Where(d=>d.IsPrivate==(tab!=null&&tab.IsPrivate)).ToList();
            var entry=candidates.FirstOrDefault(d=>string.Equals(d.FilePath,args.ResultFilePath,StringComparison.OrdinalIgnoreCase));
            var matchingSource=candidates.Where(d=>d.SourceUrl==args.DownloadOperation.Uri).ToList();
            if(entry==null&&matchingSource.Count==1)entry=matchingSource[0];
            if(entry==null)return false;
            args.ResultFilePath=entry.FilePath;args.Handled=true;
            _resumingDownloads.Remove(entry);
            entry.Operation=args.DownloadOperation;entry.SourceUrl=entry.Operation.Uri;entry.Status="Resuming";
            var operation=entry.Operation;
            operation.BytesReceivedChanged+=(s,e)=>QueueDownloadUpdate(entry,operation);
            operation.StateChanged+=(s,e)=>QueueDownloadUpdate(entry,operation);
            QueueDownloadUpdate(entry,operation);return true;
        }
        private bool RetainDownloadSource(BrowserTab tab)
        {
            if(!_downloadOwners.Any(p=>ReferenceEquals(p.Value,tab)&&(p.Key.IsActive||p.Key.CanResume||_resumingDownloads.Contains(p.Key))))return false;
            _retainedDownloadTabs.Add(tab);
            try{if(tab.Initialized)tab.View.CoreWebView2.IsMuted=true;}catch{}
            return true;
        }
        private void ReleaseDownloadSource(DownloadEntry entry)
        {
            BrowserTab owner;
            if(entry.IsActive||entry.CanResume||_resumingDownloads.Contains(entry)||!_downloadOwners.TryGetValue(entry,out owner))return;
            _downloadOwners.Remove(entry);
            if(owner!=null&&_retainedDownloadTabs.Contains(owner)&&!_downloadOwners.Values.Any(t=>ReferenceEquals(t,owner)))
            {
                _retainedDownloadTabs.Remove(owner);
                try{owner.View.Dispose();}catch{}
            }
        }
        internal void DownloadAction(DownloadEntry entry,string action)
        {
            if(entry==null||!_downloads.Contains(entry))return;
            try
            {
            switch(action)
            {
                case "open":if(entry.CanOpen)OpenDownloadedFile(entry);break;
                case "reveal":ShowDownloadedFile(entry);break;
                case "pause":
                    try{if(entry.IsActive&&entry.Operation!=null)entry.Operation.Pause();}catch(Exception ex){SetStatus("Could not pause download: "+ex.Message);}break;
                case "resume":ResumeDownload(entry);break;
                case "cancel":CancelDownload(entry);break;
                case "remove":if(entry.CanRemove){_resumingDownloads.Remove(entry);ReleaseDownloadSource(entry);_downloads.Remove(entry);}break;
            }
            }
            catch(Exception ex){SetStatus("Could not complete download action: "+ex.Message);}
        }
        private void ClearCompletedDownloads(bool isPrivate,string session=null)
        {
            foreach(var entry in _downloads.Where(d=>d.IsPrivate==isPrivate&&(!isPrivate||d.PrivateSessionId==session)&&d.Status=="Complete").ToList())_downloads.Remove(entry);
            SetStatus("Completed downloads removed from the list; files are kept");
        }
    }
}
