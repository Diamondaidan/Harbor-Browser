using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static int StartupHost(string[] args)
    {
        string root=args[0],suffix=args[1];Directory.CreateDirectory(root);MainWindow window=null;var json=new JavaScriptSerializer();
        var app=new HarborBrowser.App(()=>
        {
            string data=Path.Combine(Path.GetTempPath(),"HbStartup_"+suffix.Substring(suffix.Length-8));
            var store=new BrowserDataStore(Path.Combine(data,"Profile"),Path.Combine(data,"Downloads"));
            store.SaveSettings(new BrowserSettings{RestorePreviousSession=false,HarborHomeIntroduced=true,OnlineSuggestionsEnabled=false});
            window=Offscreen(new MainWindow(store));
            string fixtures=Path.Combine(root,"Pages");Directory.CreateDirectory(fixtures);File.WriteAllText(Path.Combine(fixtures,"first.html"),"<title>Startup first</title><body>First</body>");File.WriteAllText(Path.Combine(fixtures,"second.html"),"<title>Startup second</title><body>Second</body>");
            var configured=new System.Collections.Generic.HashSet<BrowserTab>();
            Action mappings=()=>{foreach(var t in window.BrowserTabs.Items.Cast<TabItem>().Select(i=>(BrowserTab)i.Tag)){if(!configured.Add(t))continue;t.View.CoreWebView2InitializationCompleted+=(s,e)=>{if(e.IsSuccess)t.View.CoreWebView2.SetVirtualHostNameToFolderMapping("startup.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);};}};
            window.BrowserTabs.ItemContainerGenerator.ItemsChanged+=(s,e)=>mappings();mappings();
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};var age=Stopwatch.StartNew();bool closing=false;
            timer.Tick+=(s,e)=>
            {
                if(File.Exists(Path.Combine(root,"private"))){File.Delete(Path.Combine(root,"private"));var t=AddTab(window,HomePageService.Url,true);window.BrowserTabs.SelectedItem=t.Item;}
                var tabs=window.BrowserTabs.Items.Cast<TabItem>().Select(i=>(BrowserTab)i.Tag).ToList();
                File.WriteAllText(Path.Combine(root,"state.json"),json.Serialize(new{tabs=tabs.Select(t=>new{url=t.PendingUrl??t.View?.Source?.AbsoluteUri,privateTab=t.IsPrivate}).ToArray(),selectedPrivate=((window.BrowserTabs.SelectedItem as TabItem)?.Tag as BrowserTab)?.IsPrivate??false,status=window.StatusText.Text,shuttingDown=Field(window,"_shuttingDown"),privateFailures=((System.Collections.Generic.HashSet<string>)Field(window,"_privateCleanupFailures")).Count}));
                if(!closing&&(File.Exists(Path.Combine(root,"close"))||age.Elapsed.TotalSeconds>90)){closing=true;window.Close();}
            };window.Loaded+=(s,e)=>timer.Start();window.Closed+=(s,e)=>timer.Stop();return window;
        },args.Skip(3).ToArray(),suffix);
        app.InitializeComponent();app.Run();return 0;
    }
    private static async Task StartupChecks(string output)
    {
        string root=Path.Combine(output,"StartupProcess"),suffix=".startup."+Guid.NewGuid().ToString("N");Directory.CreateDirectory(root);
        string executable=Assembly.GetExecutingAssembly().Location;
        Func<string[],Process> launch=targets=>Process.Start(new ProcessStartInfo(executable,string.Join(" ",new[]{root,suffix,"--startup-host"}.Concat(targets).Select(WindowsCommandRunner.Quote))){UseShellExecute=false,CreateNoWindow=true});
        Func<string> state=()=>{try{return File.ReadAllText(Path.Combine(root,"state.json"));}catch(IOException){return "";}};
        using(var primary=launch(new[]{"--","https://startup.test.invalid/first.html?q=hello%20world"}))
        {
            try
            {
                await Wait(()=>state().Contains("startup.test.invalid/first"));
                var first=(System.Collections.Generic.Dictionary<string,object>)Json.DeserializeObject(state());
                Check("real App startup opens shell URL without an extra Home tab",((object[])first["tabs"]).Length==1);
                File.WriteAllText(Path.Combine(root,"private"),"");await Wait(()=>state().Contains("\"selectedPrivate\":true"));
                using(var secondary=launch(new[]{"--","https://startup.test.invalid/second.html#part"})){if(!await Task.Run(()=>secondary.WaitForExit(10000)))throw new Exception("Secondary app did not exit after forwarding.");Check("secondary App exits after forwarding shell activation",secondary.ExitCode==0);}
                await Wait(()=>state().Contains("startup.test.invalid/second"));var second=(System.Collections.Generic.Dictionary<string,object>)Json.DeserializeObject(state());
                Check("real App forwards external link into a normal tab",Convert.ToBoolean(second["selectedPrivate"])==false&&((object[])second["tabs"]).Length==3);
                string file=Path.Combine(root,"local page.html");File.WriteAllText(file,"<title>Local file activation</title>");
                using(var secondary=launch(new[]{"--",file})){await Task.Run(()=>secondary.WaitForExit(10000));}
                await Wait(()=>state().Contains("local%20page.html")||state().Contains("local page.html"));
                Check("real App forwards local HTML file activation with spaces",true);
            }
            finally
            {
                File.WriteAllText(Path.Combine(root,"close"),"");if(!await Task.Run(()=>primary.WaitForExit(45000))){try{primary.Kill();}catch{}throw new Exception("Startup test app failed to close cleanly: "+state());}
            }
        }
    }
}
