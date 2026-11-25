using System;
using System.Diagnostics;
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

internal static partial class Program
{
    private static int TorLifecycleHost(string[] args)
    {
        string root=args[0],phase=args[3];MainWindow window=null;bool closing=false,ready=false,verified=false,unsafeNavigation=false,restored=false,busy=false;string error=null;
        Directory.CreateDirectory(root);string stateFile=Path.Combine(root,"state-"+phase+".json"),command=Path.Combine(root,"command-"+phase);
        var app=new HarborBrowser.App(()=>{
            var store=new BrowserDataStore(Path.Combine(root,"Profile"),Path.Combine(root,"Downloads"));
            if(!File.Exists(Path.Combine(store.RootFolder,"settings.json")))store.SaveSettings(new BrowserSettings{RestorePreviousSession=false,HarborHomeIntroduced=true,OnlineSuggestionsEnabled=false});
            window=Offscreen(new MainWindow(store));var service=(BrowserVpnService)Field(window,"_browserVpn");
            var hooked=new System.Collections.Generic.HashSet<BrowserTab>();
            Action hook=()=>{
                foreach(var tab in window.BrowserTabs.Items.Cast<TabItem>().Select(i=>(BrowserTab)i.Tag))
                {
                    if(!hooked.Add(tab))continue;
                    tab.View.CoreWebView2InitializationCompleted+=(s,e)=>{if(e.IsSuccess)tab.View.CoreWebView2.NavigationStarting+=(ss,ee)=>{if(ee.Uri.StartsWith("https://check.torproject.org/")&&service.Enabled&&!service.Connected)unsafeNavigation=true;};};
                }
            };
            window.BrowserTabs.ItemContainerGenerator.ItemsChanged+=(s,e)=>hook();hook();
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};
            Action write=()=>BrowserDataStore.WriteTextAtomically(stateFile,Json.Serialize(new{ready,verified,unsafeNavigation,restored,error,enabled=service.Enabled,connected=service.Connected,pid=service.HelperProcessId,status=service.Status}));
            timer.Tick+=async(s,e)=>{
                write();if(closing||!File.Exists(command))return;string value=File.ReadAllText(command);
                if(value=="close"){closing=true;window.Close();return;}
                if(value=="disconnect"&&!busy)
                {
                    busy=true;File.Delete(command);
                    try{await window.ChangeBrowserVpn(false);var core=((BrowserTab)((TabItem)window.BrowserTabs.Items[0]).Tag).View.CoreWebView2;restored=!Convert.ToBoolean((await TorResult(core))["IsTor"]);}
                    catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;}
                    finally{busy=false;write();}
                }
            };
            window.Loaded+=async(s,e)=>{
                timer.Start();write();
                try
                {
                    for(int i=0;i<1800&&!closing;i++){if(window.BrowserTabs.Items.Count>0&&((BrowserTab)((TabItem)window.BrowserTabs.Items[0]).Tag).Initialized)break;await Task.Delay(100);}
                    if(closing)return;
                    if(phase=="connect")await window.ChangeBrowserVpn(true);
                    if(closing)return;
                    var core=((BrowserTab)((TabItem)window.BrowserTabs.Items[0]).Tag).View.CoreWebView2;
                    verified=Convert.ToBoolean((await TorResult(core))["IsTor"]);
                    var settings=store.LoadSettings();settings.HomePage="https://check.torproject.org/api/ip";store.SaveSettings(settings);
                    ready=true;write();
                }
                catch(Exception ex){if(!closing){error=ex.GetType().Name+": "+ex.Message;write();}}
            };
            window.Closed+=(s,e)=>timer.Stop();return window;
        },new string[0],args[1]);
        app.InitializeComponent();app.Run();return 0;
    }
    private static async Task TorLifecycle052(string output)
    {
        string root=Path.Combine(output,"TorLifecycle");Directory.CreateDirectory(root);string before=ProxySnapshot();
        Func<string,Process> launch=phase=>Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,string.Join(" ",new[]{root,".tor-lifecycle."+Guid.NewGuid().ToString("N"),"--tor-lifecycle-host",phase}.Select(WindowsCommandRunner.Quote))){UseShellExecute=false,CreateNoWindow=true});
        Func<string,System.Collections.Generic.Dictionary<string,object>> state=phase=>{try{return Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"state-"+phase+".json")));}catch(IOException){return null;}};
        Func<string,Task<System.Collections.Generic.Dictionary<string,object>>> waitReady=async phase=>{
            string last=null;for(int i=0;i<2100;i++)
            {
                var current=state(phase);
                if(current!=null)
                {
                    if(current["error"]!=null)throw new Exception("Lifecycle "+phase+": "+current["error"]);
                    if(Convert.ToBoolean(current["ready"]))return current;
                    string status=Convert.ToString(current["status"]);if(status!=last){Console.WriteLine("INFO "+phase+": "+status);last=status;}
                }
                await Task.Delay(100);
            }
            throw new TimeoutException("Tor lifecycle phase "+phase);
        };
        foreach(string phase in new[]{"connect","restore","after-crash"})
        {
            using(var host=launch(phase))
            {
                try
                {
                    var info=await waitReady(phase);Check(phase+" verifies Tor from an actual browser process",Convert.ToBoolean(info["verified"]));
                    Check(phase+" starts no test page before Tor routing is ready",!Convert.ToBoolean(info["unsafeNavigation"]));
                    int pid=Convert.ToInt32(info["pid"]);
                    if(phase=="restore")
                    {
                        using(var helper=Process.GetProcessById(pid))
                        {
                            Check("restarted browser owns a live Tor helper",!helper.HasExited);
                            host.Kill();await Task.Run(()=>host.WaitForExit(10000));
                            Check("forced browser termination also ends its Tor helper",await Task.Run(()=>helper.WaitForExit(30000)));
                        }
                        continue;
                    }
                    if(phase=="after-crash")
                    {
                        File.WriteAllText(Path.Combine(root,"command-"+phase),"disconnect");
                        for(int i=0;i<600;i++){var current=state(phase);if(current?["error"]!=null)throw new Exception(Convert.ToString(current["error"]));if(current!=null&&Convert.ToBoolean(current["restored"]))break;await Task.Delay(100);}
                        var restored=state(phase);Check("disconnect after crash recovery restores the direct route",Convert.ToBoolean(restored["restored"])&&!Convert.ToBoolean(restored["enabled"])&&Convert.ToInt32(restored["pid"])==0);
                    }
                    using(var helper=phase=="connect"?Process.GetProcessById(pid):null)
                    {
                        File.WriteAllText(Path.Combine(root,"command-"+phase),"close");
                        Check(phase+" closes normally",await Task.Run(()=>host.WaitForExit(30000))&&host.ExitCode==0);
                        if(helper!=null)Check("normal browser close ends its Tor helper",await Task.Run(()=>helper.WaitForExit(10000)));
                    }
                }
                finally{if(!host.HasExited){File.WriteAllText(Path.Combine(root,"command-"+phase),"close");if(!await Task.Run(()=>host.WaitForExit(10000)))host.Kill();}}
            }
        }
        Check("restart and crash tests leave Windows proxy settings unchanged",before==ProxySnapshot());
        Check("disconnect remains off in the next service instance",!new BrowserVpnService(Path.Combine(root,"Profile")).Enabled);
    }
}
