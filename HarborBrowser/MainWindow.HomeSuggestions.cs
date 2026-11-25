using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HarborBrowser.Services;
using HarborBrowser.Views;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private void CancelHomeSuggestions(BrowserTab tab)
        {
            if(tab==null||tab.HomeSuggestionCancellation==null)return;
            tab.HomeSuggestionCancellation.Cancel();tab.HomeSuggestionCancellation.Dispose();tab.HomeSuggestionCancellation=null;
        }
        private bool HandleHomeCustomization(BrowserTab tab,string source,string json)
        {
            var message=HomePageService.ReadMessage(source,json);object type;
            if(message==null||!message.TryGetValue("type",out type))return false;
            if((type as string)=="home-customize")
            {
                if(!tab.IsPrivate&&SelectedTab==tab)Dispatcher.BeginInvoke(new Action(()=>OpenSettings(true)));return true;
            }
            if((type as string)!="home-suggest")return false;
            CancelHomeSuggestions(tab);object value,id;
            if(SelectedTab!=tab||!message.TryGetValue("value",out value)||!(value is string)||((string)value).Length>160||!message.TryGetValue("id",out id)||!(id is int))return true;
            string query=((string)value).Trim();if(query.Length==0)return true;
            var cancellation=new CancellationTokenSource();tab.HomeSuggestionCancellation=cancellation;
            _ = SendHomeSuggestions(tab,query,(int)id,_settings.SearchEngineId,cancellation.Token);return true;
        }
        private async Task SendHomeSuggestions(BrowserTab tab,string query,int id,string engine,CancellationToken cancellation)
        {
            try
            {
                var local=AddressSuggestionService.Find(query,_bookmarks,_history,tab.IsPrivate,3);
                var terms=new List<string>();
                if(AllowOnlineSuggestions&&!tab.IsPrivate&&OnlineSuggestionService.IsSearchQuery(query))
                {await Task.Delay(280,cancellation);terms=await OnlineSuggestions.GetAsync(query,engine,true,false,cancellation);}
                if(cancellation.IsCancellationRequested||tab.IsClosing||SelectedTab!=tab||!tab.Initialized||_settings.SearchEngineId!=engine||!HomePageService.IsHomeUrl(tab.View.CoreWebView2.Source))return;
                var remote=terms.Take(5).Select(term=>new AddressSuggestion{Title=term,Query=term,SourceLabel=SearchEngineService.GetById(engine).Name+" suggestion"});
                var items=remote.Concat(local).Select(item=>new{title=item.Title,url=item.Url,query=item.Query,source=item.SourceLabel}).ToArray();
                tab.View.CoreWebView2.PostWebMessageAsJson(new JavaScriptSerializer().Serialize(new{type="home-suggestions",value=query,id=id,items=items}));
            }
            catch(OperationCanceledException){}catch(InvalidOperationException){}
        }
        private static bool HomeSearchOnly(string source,string json)
        {
            var message=HomePageService.ReadMessage(source,json);object value;return message!=null&&message.TryGetValue("searchOnly",out value)&&value is bool&&(bool)value;
        }
        private string SearchSuggestionTarget(string query){return string.Format(SearchEngineService.GetById(_settings.SearchEngineId).SearchUrl,Uri.EscapeDataString(query));}
    }
}
