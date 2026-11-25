using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
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

internal static class Benchmark
{
    private static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static bool steady;
    private static async Task Wait(Func<bool> ready){for(int i=0;i<300;i++){if(ready())return;await Task.Delay(100);}throw new TimeoutException("Benchmark app did not become ready.");}
    private static object Field(MainWindow main,string name){return typeof(MainWindow).GetField(name,Hidden).GetValue(main);}
    private static void Activity(MainWindow main){typeof(MainWindow).GetMethod("UpdateTabActivity",Hidden).Invoke(main,null);}
    private static Task Sleep(MainWindow main,BrowserTab tab){return (Task)typeof(MainWindow).GetMethod("SuspendTabIfInactiveAsync",Hidden).Invoke(main,new object[]{tab});}
    private static async Task Navigate(BrowserTab tab,string uri)
    {
        var core=tab.View.CoreWebView2;ulong id=0;var done=new TaskCompletionSource<bool>();EventHandler<CoreWebView2NavigationStartingEventArgs> starting=(s,e)=>{if(e.Uri==uri)id=e.NavigationId;};
        EventHandler<CoreWebView2NavigationCompletedEventArgs> completed=(s,e)=>{if(id==0||id!=e.NavigationId)return;if(e.IsSuccess)done.TrySetResult(true);else done.TrySetException(new Exception("Benchmark fixture failed: "+e.WebErrorStatus));};
        core.NavigationStarting+=starting;core.NavigationCompleted+=completed;
        try{core.Navigate(uri);if(await Task.WhenAny(done.Task,Task.Delay(20000))!=done.Task)throw new TimeoutException("Fixture navigation timed out");await done.Task;}
        finally{core.NavigationStarting-=starting;core.NavigationCompleted-=completed;}
    }
    private sealed class Proc
    {public int Id;public long Started;public double Cpu;public long Working,Private;public bool App;}
    private sealed class Frame
    {
        public string Phase;public int Sleeping,Processes;public double AppWorkingMB,AppPrivateMB,WebWorkingMB,WebPrivateMB,CpuPercent;public bool ProtectedTabAwake;public long DownloadBytes;
    }
    private static List<Proc> Processes(MainWindow main)
    {
        var environment=(CoreWebView2Environment)Field(main,"_environment");int own=Process.GetCurrentProcess().Id;
        var ids=environment.GetProcessInfos().Select(p=>p.ProcessId).Concat(new[]{own}).Distinct();var result=new List<Proc>();
        foreach(int id in ids)try{using(var process=Process.GetProcessById(id)){result.Add(new Proc{Id=id,Started=process.StartTime.ToUniversalTime().Ticks,Cpu=process.TotalProcessorTime.TotalSeconds,Working=process.WorkingSet64,Private=process.PrivateMemorySize64,App=id==own});}}catch(ArgumentException){}catch(InvalidOperationException){}
        return result;
    }
    private static async Task<List<Frame>> Measure(MainWindow main,List<BrowserTab> tabs,string phase,BrowserTab protectedTab,DownloadEntry download)
    {
        var frames=new List<Frame>();var previous=Processes(main);long before=Stopwatch.GetTimestamp();
        for(int sample=0;sample<(steady?5:3);sample++)
        {
            await Task.Delay(1000);var current=Processes(main);long timestamp=Stopwatch.GetTimestamp();double elapsed=(timestamp-before)/(double)Stopwatch.Frequency;
            double cpu=current.Sum(p=>{var old=previous.FirstOrDefault(o=>o.Id==p.Id&&o.Started==p.Started);return old==null?0:Math.Max(0,p.Cpu-old.Cpu);});
            frames.Add(new Frame{Phase=phase,Sleeping=tabs.Count(t=>t.Initialized&&t.View.CoreWebView2.IsSuspended),Processes=current.Count,AppWorkingMB=current.Where(p=>p.App).Sum(p=>p.Working)/1048576.0,AppPrivateMB=current.Where(p=>p.App).Sum(p=>p.Private)/1048576.0,WebWorkingMB=current.Where(p=>!p.App).Sum(p=>p.Working)/1048576.0,WebPrivateMB=current.Where(p=>!p.App).Sum(p=>p.Private)/1048576.0,CpuPercent=Math.Max(0,Math.Min(100,cpu/elapsed/Environment.ProcessorCount*100)),ProtectedTabAwake=protectedTab==null||!protectedTab.View.CoreWebView2.IsSuspended,DownloadBytes=download==null?0:download.BytesReceived});
            previous=current;before=timestamp;
        }
        return frames;
    }
    [STAThread]
    private static int Main(string[] args)
    {
        string output=Path.GetFullPath(args[0]),fixtures=Path.GetFullPath(args[1]),expected=args[4],scene=args[5];int count=int.Parse(args[2]),trial=int.Parse(args[3]),exit=1;
        steady=args.Length>6&&args[6]=="--steady";
        Directory.CreateDirectory(output);string actual=typeof(MainWindow).Assembly.GetName().Version.ToString();if(actual!=expected){Console.WriteLine("FAIL wrong app assembly "+actual);return 1;}
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/HarborBrowser;component/Themes/Controls.xaml")});
        var store=new BrowserDataStore(Path.Combine(output,"Profile"),Path.Combine(output,"Downloads"));store.SaveSettings(new BrowserSettings{SuspendInactiveTabs=false,HarborHomeIntroduced=true,OnlineSuggestionsEnabled=false,RestorePreviousSession=false});
        var layout=new MonitorLayout(new PixelRect(-20000,0,-18720,720),new PixelRect(-20000,0,-18720,680),3);
        var main=new MainWindow(store,h=>layout){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=0,Width=1000,Height=600,ShowInTaskbar=false,ShowActivated=false};
        var timeout=new DispatcherTimer{Interval=TimeSpan.FromMinutes(3)};timeout.Tick+=(s,e)=>{Console.WriteLine("FAIL benchmark timeout");app.Shutdown(1);};
        main.Loaded+=async(s,e)=>
        {
            DownloadFixtureServer server=null;DownloadEntry download=null;var tabs=new List<BrowserTab>();var records=new List<Frame>();var settings=(BrowserSettings)Field(main,"_settings");DispatcherTimer churn=null;
            try
            {
                await Wait(()=>main.BrowserTabs.Items.Count>0&&((BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag).Initialized);
                for(int i=0;i<count;i++)
                {
                    BrowserTab tab;if(i==0)tab=(BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag;
                    else tab=(BrowserTab)typeof(MainWindow).GetMethod("CreateNewTab",Hidden,null,new[]{typeof(string),typeof(bool),typeof(bool),typeof(BrowserSessionTab)},null).Invoke(main,new object[]{HomePageService.Url,false,false,null});
                    tabs.Add(tab);main.BrowserTabs.SelectedItem=tab.Item;await Wait(()=>tab.Initialized);var core=tab.View.CoreWebView2;
                    core.SetVirtualHostNameToFolderMapping("performance.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);
                    await Navigate(tab,"https://performance.test.invalid/performance.html?tab="+i);
                }
                var last=tabs.Last();BrowserTab protectedTab=null;
                if(scene=="video")
                {
                    protectedTab=last;var core=last.View.CoreWebView2;core.SetVirtualHostNameToFolderMapping("media.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);core.SetVirtualHostNameToFolderMapping("media-frame.test.invalid",fixtures,CoreWebView2HostResourceAccessKind.DenyCors);await Navigate(last,"https://media.test.invalid/video.html");await Wait(()=>last.MediaFrames.Count>0);await Task.Delay(600);
                }
                if(scene=="download")
                {
                    protectedTab=last;server=new DownloadFixtureServer();var downloads=(ObservableCollection<DownloadEntry>)Field(main,"_downloads");
                    NotifyCollectionChangedEventHandler pause=(ss,ee)=>{if(ee.NewItems==null)return;foreach(DownloadEntry entry in ee.NewItems)if(entry.SourceUrl==server.Url+"/benchmark"){download=entry;entry.Operation.Pause();}};
                    downloads.CollectionChanged+=pause;try{last.View.CoreWebView2.Navigate(server.Url+"/benchmark");await Wait(()=>download!=null&&download.CanResume);}finally{downloads.CollectionChanged-=pause;}
                }
                main.BrowserTabs.SelectedItem=tabs[0].Item;await Task.Delay(steady?5000:1500);records.AddRange(await Measure(main,tabs,"Awake",protectedTab,download));
                settings.InactiveTabSuspendDelaySeconds=15;settings.SuspendInactiveTabs=true;
                if(scene=="automatic")
                {
                    var inactivity=typeof(BrowserTab).GetProperty("InactiveSinceUtc");if(inactivity!=null)foreach(var tab in tabs)inactivity.SetValue(tab,tab==tabs[0]?(DateTime?)null:DateTime.UtcNow);
                    Activity(main);churn=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};churn.Tick+=(ss,ee)=>Activity(main);churn.Start();await Task.Delay(17500);
                }
                else await Task.WhenAll(tabs.Skip(1).Select(t=>Sleep(main,t)));
                await Task.Delay(steady?5000:600);records.AddRange(await Measure(main,tabs,"Sleeping",protectedTab,download));if(churn!=null)churn.Stop();
                if(actual!="0.4.0.0")
                {
                    int expectedSleeping=count-(protectedTab==null?1:2);
                    if(records.Last().Sleeping!=expectedSleeping||!records.Last().ProtectedTabAwake)throw new Exception("Unexpected candidate suspension/protection: "+records.Last().Sleeping+" expected "+expectedSleeping);
                }
                settings.SuspendInactiveTabs=false;Activity(main);await Task.Delay(steady?5000:300);records.AddRange(await Measure(main,tabs,"Woken",protectedTab,download));
                if(tabs.Any(t=>t.View.CoreWebView2.IsSuspended))throw new Exception("A tab failed to wake");
                var environment=(CoreWebView2Environment)Field(main,"_environment");
                File.WriteAllText(Path.Combine(output,"measurements.json"),Json.Serialize(new{Version=actual,Scene=scene,Tabs=count,Trial=trial,MeasurementMode=steady?"Steady":"Short",ProcessBits=IntPtr.Size*8,LogicalProcessors=Environment.ProcessorCount,Runtime=environment.BrowserVersionString,Samples=records}));
                File.WriteAllLines(Path.Combine(output,"measurements.csv"),new[]{"version,scene,tabs,trial,phase,sleeping,processes,app_working_mb,app_private_mb,web_working_mb,web_private_mb,cpu_percent,protected_awake,download_bytes"}.Concat(records.Select(f=>string.Join(",",new[]{actual,scene,count.ToString(),trial.ToString(),f.Phase,f.Sleeping.ToString(),f.Processes.ToString(),f.AppWorkingMB.ToString("F3",CultureInfo.InvariantCulture),f.AppPrivateMB.ToString("F3",CultureInfo.InvariantCulture),f.WebWorkingMB.ToString("F3",CultureInfo.InvariantCulture),f.WebPrivateMB.ToString("F3",CultureInfo.InvariantCulture),f.CpuPercent.ToString("F3",CultureInfo.InvariantCulture),f.ProtectedTabAwake.ToString(),f.DownloadBytes.ToString()}))));
                Console.WriteLine("PASS benchmark "+actual+" "+scene+" tabs="+count+" trial="+trial+" sleeping="+records.First(f=>f.Phase=="Sleeping").Sleeping);exit=0;
            }
            catch(Exception ex){Console.WriteLine("FAIL "+ex);}
            finally
            {
                timeout.Stop();if(churn!=null)churn.Stop();if(download?.Operation!=null)try{download.Operation.Cancel();}catch{}server?.Dispose();
                typeof(MainWindow).GetField("_confirmedCloseWithActiveDownloads",Hidden).SetValue(main,true);try{main.Close();}catch{}app.Shutdown(exit);
            }
        };
        timeout.Start();main.Show();app.Run();return exit;
    }
}
