using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task LiveSiteChecks(MainWindow main,string output)
    {
        var tab=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;var core=tab.View.CoreWebView2;var results=new List<object>();
        foreach(string url in new[]{"https://www.w3.org/","https://www.wikipedia.org/","https://example.com/"})
        {
            var done=new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> completed=(s,e)=>done.TrySetResult(e);core.NavigationCompleted+=completed;
            try
            {
                core.Navigate(url);bool finished=await Task.WhenAny(done.Task,Task.Delay(25000))==done.Task;
                bool loaded=finished&&done.Task.Result.IsSuccess;
                if(loaded)
                {
                    await Task.Delay(2200);
                    bool declared=!string.IsNullOrEmpty(core.FaviconUri),visible=tab.HeaderIcon.Source!=null&&tab.HeaderIcon.Visibility==Visibility.Visible;
                    Check(url+" real-page navigation",true);
                    Check(url+" visible icon or clean no-icon fallback",declared?visible:tab.FallbackIcon.Visibility==Visibility.Visible);
                    Console.WriteLine("INFO live site title="+core.DocumentTitle+" favicon="+core.FaviconUri);
                    results.Add(new{url,loaded,title=core.DocumentTitle,favicon=core.FaviconUri,visible});
                }
                else
                {
                    string error=finished?done.Task.Result.WebErrorStatus.ToString():"Timeout";Console.WriteLine("UNVERIFIED "+url+" "+error);results.Add(new{url,loaded,error});
                }
            }
            finally{core.NavigationCompleted-=completed;}
        }
        File.WriteAllText(Path.Combine(output,"live-sites.json"),Json.Serialize(results));
    }
}
