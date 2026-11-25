using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static partial class Program
{
    private static async Task BrowserVpnChecks(MainWindow main,BrowserDataStore store,string output)
    {
        var service=(BrowserVpnService)Field(main,"_browserVpn");var tab=(BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag;var core=tab.View.CoreWebView2;
        var progress=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};progress.Tick+=(s,e)=>Console.WriteLine("INFO "+service.Status);progress.Start();
        string proxyBefore=ProxySnapshot();
        try
        {
            Check("Tor preview defaults off",!service.Enabled);
            Task initial=main.ChangeBrowserVpn(true);await Wait(()=>service.Enabled);
            Task cancel=main.ChangeBrowserVpn(false);
            Check("connection startup can be cancelled",await Task.WhenAny(cancel,Task.Delay(20000))==cancel);
            await cancel;try{await initial;}catch(OperationCanceledException){}catch(InvalidOperationException){}
            Check("cancelled startup restores off state without an orphan helper",!service.Enabled&&service.HelperProcessId==0);
            await main.ChangeBrowserVpn(true);
            Check("free Tor client bootstraps without an account",service.Enabled&&service.Connected&&service.HelperProcessId!=0);
            Check("owned routing extension is enabled",(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==BrowserVpnService.ExtensionId&&e.IsEnabled));
            Check("Tor opt-in persists separately",new BrowserVpnService(store.RootFolder).Enabled);
            var settings=(BrowserSettings)Field(main,"_settings");settings.OnlineSuggestionsEnabled=true;
            var allow=typeof(MainWindow).GetProperty("AllowOnlineSuggestions",BindingFlags.NonPublic|BindingFlags.Instance);
            Check("native typing suggestions pause without changing preference",!(bool)allow.GetValue(main)&&settings.OnlineSuggestionsEnabled);
            var create=typeof(MainWindow).GetMethod("CreateNewTab",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string),typeof(bool),typeof(bool),typeof(BrowserSessionTab)},null);
            Check("private tab cannot bypass Tor mode",create.Invoke(main,new object[]{HomePageService.Url,true,true,null})==null);
            var protectedResult=await TorResult(core);
            Check("Tor Project confirms the actual Harbor page used Tor",Convert.ToBoolean(protectedResult["IsTor"]));
            var downloads=(System.Collections.ObjectModel.ObservableCollection<DownloadEntry>)Field(main,"_downloads");
            const string proof="tor-expert-bundle-windows-x86_64-16.0.1.tar.gz.asc";
            core.Navigate("https://dist.torproject.org/torbrowser/16.0.1/"+proof);
            for(int i=0;i<450&&!downloads.Any(d=>d.FileName==proof&&!d.IsActive);i++)await Task.Delay(100);
            var downloaded=downloads.FirstOrDefault(d=>d.FileName==proof&&!d.IsActive);
            Check("live Tor download keeps filename and extension",downloaded!=null&&File.Exists(downloaded.FilePath)&&Path.GetExtension(downloaded.FilePath)==".asc");
            Check("live Tor download contains the expected signature",File.ReadAllText(downloaded.FilePath).Contains("-----BEGIN PGP SIGNATURE-----"));
            var control=new WebView2();var window=new Window{Content=control,Width=300,Height=200,Left=-20000,ShowInTaskbar=false,ShowActivated=false};window.Show();
            try
            {
                await control.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(null,Path.Combine(output,"DirectControl")));
                var directResult=await TorResult(control.CoreWebView2);
                Check("separate browser retains its direct route",!Convert.ToBoolean(directResult["IsTor"]));
                Check("Harbor exit address differs from direct control",Convert.ToString(directResult["IP"])!=Convert.ToString(protectedResult["IP"]));
                using(var image=File.Create(Path.Combine(output,"tor-proof.png")))await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
                int helper=service.HelperProcessId;using(var process=System.Diagnostics.Process.GetProcessById(helper)){process.Kill();await Task.Run(()=>process.WaitForExit(10000));}
                await Task.Delay(300);Check("helper exit updates connection state",!service.Connected);
                Check("dead Tor helper blocks browser traffic without direct fallback",!await TorNavigate(core,"https://example.com/?harbor-tor-failure=1"));
                await main.ChangeBrowserVpn(false);
                Check("disconnect removes routing and stops owned helper",!service.Enabled&&service.HelperProcessId==0&&!(await core.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==BrowserVpnService.ExtensionId));
                var restored=await TorResult(core);
                Check("disconnect restores actual direct browser route",!Convert.ToBoolean(restored["IsTor"])&&Convert.ToString(restored["IP"])==Convert.ToString(directResult["IP"]));
                Check("typing suggestion preference resumes",(bool)allow.GetValue(main));
                Check("Windows proxy settings stay unchanged",proxyBefore==ProxySnapshot());
                Check("off state persists",!new BrowserVpnService(store.RootFolder).Enabled);
            }
            finally{control.Dispose();window.Close();}
        }
        finally{progress.Stop();if(service.Enabled)await main.ChangeBrowserVpn(false);}
    }
    private static async Task<System.Collections.Generic.Dictionary<string,object>> TorResult(CoreWebView2 core)
    {
        if(!await TorNavigate(core,"https://check.torproject.org/api/ip?harbor="+Guid.NewGuid().ToString("N")))throw new Exception("Tor verification endpoint did not load.");
        string text=Json.Deserialize<string>(await core.ExecuteScriptAsync("document.body.innerText"));
        return Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(text);
    }
    private static async Task<bool> TorNavigate(CoreWebView2 core,string url)
    {
        var done=new TaskCompletionSource<bool>();ulong? navigation=null;
        EventHandler<CoreWebView2NavigationStartingEventArgs> starting=(s,e)=>{if(e.Uri==url)navigation=e.NavigationId;};
        EventHandler<CoreWebView2NavigationCompletedEventArgs> handler=(s,e)=>{if(navigation==e.NavigationId)done.TrySetResult(e.IsSuccess);};core.NavigationStarting+=starting;core.NavigationCompleted+=handler;
        try{core.Navigate(url);if(await Task.WhenAny(done.Task,Task.Delay(45000))!=done.Task)throw new TimeoutException("Live Tor page timed out.");return await done.Task;}
        finally{core.NavigationStarting-=starting;core.NavigationCompleted-=handler;}
    }
    private static string ProxySnapshot()
    {
        using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))return string.Join("|",new[]{"ProxyEnable","ProxyServer","ProxyOverride","AutoConfigURL"}.Select(n=>n+"="+Convert.ToString(key?.GetValue(n))));
    }
}
