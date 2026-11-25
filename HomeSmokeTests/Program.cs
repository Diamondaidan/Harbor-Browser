using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;
using HarborBrowser.Models;
using HarborBrowser.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static class HomeSmoke
{
    private static int checks;
    private static string destination;
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static void Check(string label, bool value)
    {
        if (!value) throw new Exception(label);
        checks++; Console.WriteLine("PASS " + label);
    }
    private static async Task CheckJs(CoreWebView2 core, string label, string js)
    {
        Check(label, await core.ExecuteScriptAsync(js) == "true");
    }
    [STAThread]
    private static int Main(string[] args)
    {
        int exit = 1;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var view = new WebView2();
        var window = new Window { Title="Harbor home page verification", Content=view, Width=1320, Height=850,
            Left=-10000, Top=-10000, ShowInTaskbar=false, WindowStyle=WindowStyle.None };
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(75) };
        timeout.Tick += (s,e) => { Console.WriteLine("FAIL: home page smoke test timed out"); view.Dispose(); app.Shutdown(1); };
        window.Loaded += async (s,e) =>
        {
            try
            {
                Directory.CreateDirectory(args[1]);
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(args[1], "TestProfile"));
                await view.EnsureCoreWebView2Async(environment);
                var core = view.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping(HomePageService.Host, args[0], CoreWebView2HostResourceAccessKind.DenyCors);
                // Test profiles only; no user cookies/history/settings are loaded.
                string engineId = "bing";
                string themeChoice = null;
                string state = HomePageService.CreateState(SearchEngineService.GetById(engineId), null, false);
                int externalRequests = 0;
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += (ss,ee) => {
                    Uri uri;
                    if (Uri.TryCreate(ee.Request.Uri,UriKind.Absolute,out uri) &&
                        (uri.Scheme=="https" || uri.Scheme=="http") && uri.Host!=HomePageService.Host)
                    {
                        externalRequests++;
                        ee.Response = environment.CreateWebResourceResponse(new MemoryStream(),403,"Blocked in test","");
                    }
                };
                core.WebMessageReceived += (ss,ee) => {
                    if (!HomePageService.IsHomeUrl(ee.Source) || !HomePageService.IsHomeUrl(core.Source)) return;
                    var message=HomePageService.ReadMessage(ee.Source,ee.WebMessageAsJson);object kind;
                    if(message!=null&&message.TryGetValue("type",out kind)&&((kind as string)=="home-suggest"||(kind as string)=="home-customize"))return;
                    string requestedTheme=HomePageService.ReadThemeMessage(ee.Source,ee.WebMessageAsJson);
                    if(requestedTheme!=null){themeChoice=requestedTheme;return;}
                    string input=HomePageService.ReadSearchMessage(ee.Source,ee.WebMessageAsJson);
                    if(input!=null) destination=SearchEngineService.ResolveInput(input,SearchEngineService.GetById(engineId));
                    else core.PostWebMessageAsJson(state);
                };
                await Navigate(core);
                await Task.Delay(400);
                await CheckJs(core,"actual HTML title", "document.title === 'Harbor Home'");
                await CheckJs(core,"selected engine delivered", "document.getElementById('engine').textContent === 'Bing'");
                await CheckJs(core,"Home version matches the running application", "document.getElementById('app-version').textContent === 'Harbor "+AppVersion.Number+"'");
                await CheckJs(core,"logo loaded locally", "document.querySelector('.brand img').complete && document.querySelector('.brand img').naturalWidth > 0");
                await core.ExecuteScriptAsync("document.querySelector('[data-theme=light]').click()");
                await CheckJs(core,"morning background applied", "getComputedStyle(document.body).backgroundImage.includes('morning.png')");
                await CheckJs(core,"search input accessible", "document.querySelector('label[for=query]') !== null");
                await CheckJs(core,"empty bookmarks explained", "!document.getElementById('empty-state').hidden && document.querySelectorAll('.favorite').length === 0");
                await CheckJs(core,"no horizontal overflow at desktop size", "document.documentElement.scrollWidth <= innerWidth");
                using(var file=File.Create(Path.Combine(args[1],"home-morning.png")))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
                await core.ExecuteScriptAsync("document.querySelector('[data-theme=dark]').click()");
                await Task.Delay(300);
                await CheckJs(core,"blue-hour background applied", "getComputedStyle(document.body).backgroundImage.includes('blue-hour.png')");
                await CheckJs(core,"normal theme choice saved", "localStorage.getItem('harbor.home.theme') === 'dark'");
                using(var file=File.Create(Path.Combine(args[1],"home-blue-hour.png")))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
                await Navigate(core);
                await Task.Delay(150);
                await CheckJs(core,"theme survives reload", "document.body.dataset.theme === 'dark'");
                engineId="google";
                state=HomePageService.CreateState(SearchEngineService.GetById(engineId),new [] {
                    new Bookmark {Title="<img src=x onerror=alert(1)>",Url="https://example.com/"},
                    new Bookmark {Title="Blocked",Url="javascript:alert(1)"}},true);
                core.PostWebMessageAsJson(state);
                await Task.Delay(150);
                await CheckJs(core,"engine change updates page", "document.getElementById('engine').textContent === 'Google'");
                await CheckJs(core,"private-tab indicator", "!document.querySelector('.private-note').hidden");
                await CheckJs(core,"unsafe bookmark excluded", "document.querySelectorAll('.favorite').length === 1");
                await CheckJs(core,"bookmark label treated as text", "document.querySelector('.favorite span:last-child').textContent === '<img src=x onerror=alert(1)>' && document.querySelector('.favorite img') === null");
                await CheckJs(core,"empty prompt hidden with bookmarks", "document.getElementById('empty-state').hidden");
                await core.ExecuteScriptAsync("document.querySelector('[data-theme=light]').click()");
                await CheckJs(core,"private theme change not saved", "localStorage.getItem('harbor.home.theme') === 'dark'");
                await core.ExecuteScriptAsync("document.getElementById('query').value='birds & sky'; document.getElementById('search').requestSubmit()");
                await Task.Delay(150);
                Check("search message uses selected engine encoding",destination=="https://www.google.com/search?q=birds%20%26%20sky");
                await core.ExecuteScriptAsync("document.getElementById('query').value='example.com'; document.getElementById('search').requestSubmit()");
                await Task.Delay(150);
                Check("address entry resolved",destination=="https://example.com");
                window.Width=800;
                await Task.Delay(150);
                await CheckJs(core,"no overflow at minimum Harbor width","document.documentElement.scrollWidth <= innerWidth");
                window.Width=390;
                await Task.Delay(150);
                await CheckJs(core,"responsive small-screen layout","document.documentElement.scrollWidth <= innerWidth");
                await CheckJs(core,"search remains usable at narrow width","document.getElementById('query').getBoundingClientRect().width > 150");

                var appearance=new BrowserSettings {ThemeMode="Dark"};
                appearance.DarkColors.Accent="#8844CC";
                appearance.DarkColors.Text="#DDEEFF";
                state=HomePageService.CreateState(SearchEngineService.GetById(engineId),null,false,appearance);
                core.PostWebMessageAsJson(state);
                window.Width=1320;
                await Task.Delay(200);
                await CheckJs(core,"app dark mode reaches home","document.body.dataset.theme === 'dark'");
                await CheckJs(core,"custom accent reaches home","getComputedStyle(document.body).getPropertyValue('--accent').trim() === '#8844CC'");
                await CheckJs(core,"custom text reaches home","getComputedStyle(document.body).getPropertyValue('--ink').trim() === '#DDEEFF'");
                await CheckJs(core,"custom accent paints search button","getComputedStyle(document.querySelector('#search button')).backgroundColor === 'rgb(136, 68, 204)'");
                using(var file=File.Create(Path.Combine(args[1],"home-custom-colors.png"))) await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
                themeChoice=null;
                await core.ExecuteScriptAsync("document.querySelector('[data-theme=light]').click()");
                await Task.Delay(150);
                Check("home theme choice requests app mode change",themeChoice=="Light");
                state=HomePageService.CreateState(SearchEngineService.GetById(engineId),null,true,appearance);
                core.PostWebMessageAsJson(state);
                await Task.Delay(100);
                themeChoice=null;
                await core.ExecuteScriptAsync("document.querySelector('[data-theme=light]').click()");
                await Task.Delay(150);
                Check("private theme change leaves app mode untouched",themeChoice==null);

                var custom=new BrowserSettings{ThemeMode="Dark",HomeScene="Morning",UseCustomHomeShortcuts=true};
                for(int i=0;i<14;i++)custom.HomeShortcuts.Add(new Bookmark{Title="Custom "+i,Url="https://shortcut.test/"+i});
                state=HomePageService.CreateState(SearchEngineService.GetById(engineId),null,false,custom);core.PostWebMessageAsJson(state);await Task.Delay(120);
                await CheckJs(core,"custom Home shortcuts use saved order and limit","document.querySelectorAll('.favorite').length===12&&document.querySelector('.favorite span:last-child').textContent==='Custom 0'");
                await CheckJs(core,"fixed scene independent of dark mode","document.body.dataset.theme==='dark'&&getComputedStyle(document.body).backgroundImage.includes('morning.png')");
                await CheckJs(core,"normal Home exposes customization action","!document.getElementById('customize-home').hidden");
                await core.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled","{\"enabled\":true}");
                int requestId=0;string requested=null;bool customizeRequested=false,searchOnly=false;
                core.WebMessageReceived+=(ss,ee)=>{
                    var message=HomePageService.ReadMessage(ee.Source,ee.WebMessageAsJson);object type,value,id,force;
                    if(message==null||!message.TryGetValue("type",out type))return;
                    if((type as string)=="home-suggest"&&message.TryGetValue("id",out id)&&message.TryGetValue("value",out value)&&Convert.ToString(value).Length>0){requestId=Convert.ToInt32(id);requested=Convert.ToString(value);}
                    if((type as string)=="home-customize")customizeRequested=true;
                    if((type as string)=="home-search"&&message.TryGetValue("searchOnly",out force))searchOnly=force is bool&&(bool)force;
                };
                await core.ExecuteScriptAsync("document.getElementById('customize-home').click();document.getElementById('query').focus();document.getElementById('query').value='weather';document.getElementById('query').dispatchEvent(new Event('input',{bubbles:true}));");await Task.Delay(100);
                Check("Home customization sends trusted action",customizeRequested);Check("Home input sends suggestion request",requested=="weather"&&requestId>0);
                core.PostWebMessageAsJson(Json.Serialize(new{type="home-suggestions",value="weather",id=requestId,items=new[]{new{title="<img src=x onerror=alert(1)>",query="weather tomorrow",url=(string)null,source="Test suggestion"}}}));await Task.Delay(100);
                await CheckJs(core,"suggestions render text without executing markup","document.querySelector('#home-suggestions button span').textContent==='<img src=x onerror=alert(1)>'&&document.querySelector('#home-suggestions img')===null");
                core.PostWebMessageAsJson(Json.Serialize(new{type="home-suggestions",value="weather",id=requestId-1,items=new[]{new{title="Stale",query="stale",source="Test"}}}));await Task.Delay(50);
                await CheckJs(core,"stale Home suggestion message ignored","document.querySelector('#home-suggestions button span').textContent.startsWith('<img')");
                string scroll=await core.ExecuteScriptAsync("window.scrollY");
                await core.ExecuteScriptAsync("document.getElementById('query').dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true}));");
                Check("Home keyboard navigation does not scroll page",await core.ExecuteScriptAsync("window.scrollY")==scroll);
                await CheckJs(core,"dark suggestion highlight stays translucent","(()=>{const color=getComputedStyle(document.querySelector('#home-suggestions button')).backgroundColor;const alpha=Number(color.split(',').pop().replace(')',''));return color.startsWith('rgba(')&&alpha>0&&alpha<.4;})()");
                await core.ExecuteScriptAsync("document.getElementById('query').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}));");await Task.Delay(80);Check("selected Home completion requests search only",searchOnly&&destination.Contains("weather%20tomorrow"));

                Check("home loaded without external network requests",externalRequests==0);
                Console.WriteLine("ALL PASSED: "+checks+" WebView2 browser checks. Captures are from an isolated test host, not the installed Harbor window.");
                exit=0;
            }
            catch(Exception ex) { Console.WriteLine("FAIL "+ex); }
            finally { timeout.Stop(); view.Dispose(); app.Shutdown(exit); }
        };
        timeout.Start(); window.Show(); app.Run();
        return exit;
    }
    private static async Task Navigate(CoreWebView2 core)
    {
        var done=new TaskCompletionSource<bool>();
        EventHandler<CoreWebView2NavigationCompletedEventArgs> handler=null;
        handler=(s,e)=>{core.NavigationCompleted-=handler; if(e.IsSuccess)done.TrySetResult(true);else done.TrySetException(new Exception("Navigation: "+e.WebErrorStatus));};
        core.NavigationCompleted+=handler; core.Navigate(HomePageService.Url); await done.Task;
    }
}
