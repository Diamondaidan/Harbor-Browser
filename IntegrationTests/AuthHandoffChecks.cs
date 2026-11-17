using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static partial class Program
{
    private static async Task AuthHandoffChecks(MainWindow main,BrowserDataStore store,string output,string fixtures)
    {
        var source=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var core=source.View.CoreWebView2;var environment=(CoreWebView2Environment)Field(main,"_environment");
        var extensionsBefore=(await core.Profile.GetBrowserExtensionsAsync()).ToDictionary(e=>e.Id,e=>e.IsEnabled);
        string fixtureId=null;BrowserTab extensionParent=null,reopened=null;
        bool sourceWasAuth=source.IsProviderAuthentication;
        using(var server=new AuthProofServer(fixtures))
        {
            try
            {
                await AuthOldCopyBaseline(environment,server);
                await AuthNavigate(core,server.ParentUrl);
                await WaitJs(core,"harborPopupProof.ready===true");
                // The flag models a provider-origin flow without visiting any
                // provider/account page or reading real provider state.
                source.IsProviderAuthentication=true;
                string getCanary="harbor-synthetic-"+Guid.NewGuid().ToString("N");
                var get=await AuthBoundPopup(main,source,server.ChildUrl(getCanary),getCanary,false);
                try
                {
                    Check("native popup retains a usable opener",get.Child.hasOpener&&get.SourceReturnedWindow);
                    Check("native popup copies the opener sessionStorage canary",get.Child.sessionCanary==getCanary);
                    Check("native popup preserves its requested window name",get.Child.name==get.ExpectedName);
                    Check("native popup completes a two-way opener message exchange",get.RoundTrip&&get.Child.parentConfirmed);
                    Check("native popup stays in the source profile",get.Target.View.CoreWebView2.Profile.ProfileName==core.Profile.ProfileName);
                    Check("popup target navigates once without a preliminary Home page",get.Requests==1&&get.TargetNavigations.Count(u=>u==get.TargetUrl)==1&&!get.TargetNavigations.Any(HomePageService.IsHomeUrl));
                    Check("auth classification and navigation suppression exist before popup initialization",get.AuthAtCreation&&get.PendingAtCreation&&get.SuppressedAtCreation);
                    Check("auth popup remains transient after native navigation",get.Target.IsProviderAuthentication&&!store.LoadSession().Tabs.Any(t=>t.Url==get.TargetUrl));
                    Check("the local GET target was requested only once",server.Count(get.TargetUrl)==1&&get.Child.serverMethod=="GET");
                }
                finally{CloseTab(main,get.Target);main.BrowserTabs.SelectedItem=source.Item;}

                string postCanary="harbor-synthetic-"+Guid.NewGuid().ToString("N");
                var post=await AuthBoundPopup(main,source,server.ChildUrl(postCanary),postCanary,true);
                try
                {
                    Check("native form popup preserves the POST method",post.Child.serverMethod=="POST");
                    Check("native form popup preserves the POST body",post.Child.serverBody=="canary="+postCanary);
                    Check("native form popup retains opener, sessionStorage and window name",post.Child.hasOpener&&post.Child.sessionCanary==postCanary&&post.Child.name==post.ExpectedName);
                    Check("native form popup navigates and reaches its target only once",post.Requests==1&&post.TargetNavigations.Count(u=>u==post.TargetUrl)==1&&server.Count(post.TargetUrl)==1);
                    Check("native form popup completes the message round trip",post.RoundTrip&&post.Child.parentConfirmed);
                }
                finally{CloseTab(main,post.Target);main.BrowserTabs.SelectedItem=source.Item;}

                var fixture=await core.Profile.AddBrowserExtensionAsync(Path.Combine(Path.GetFullPath(fixtures),"AuthHandoffExtension"));fixtureId=fixture.Id;
                Check("storage fixture is its own native extension",fixtureId!=ProtonPreviewService.ExtensionId&&fixtureId!=BrowserVpnService.ExtensionId&&!extensionsBefore.ContainsKey(fixtureId));
                string parentUrl="chrome-extension://"+fixtureId+"/popup.html";
                extensionParent=AddTab(main,parentUrl);main.BrowserTabs.SelectedItem=extensionParent.Item;await Wait(()=>extensionParent.Initialized);
                await WaitJs(extensionParent.View.CoreWebView2,"harborPopupProof.ready===true");extensionParent.IsProviderAuthentication=true;
                string nativeCanary="harbor-synthetic-"+Guid.NewGuid().ToString("N");
                await extensionParent.View.CoreWebView2.ExecuteScriptAsync("prepareNativeCanary("+Json.Serialize(nativeCanary)+")");
                await WaitJs(extensionParent.View.CoreWebView2,"harborNativePrepared===true");
                var native=await AuthBoundPopup(main,extensionParent,"chrome-extension://"+fixtureId+"/child.html?canary="+nativeCanary,nativeCanary,false);
                try
                {
                    Check("owned extension popup retains opener and session state",native.Child.hasOpener&&native.Child.sessionCanary==nativeCanary&&native.Child.name==native.ExpectedName);
                    Check("native extension pending canary survives the handoff",native.Child.nativeCanary==nativeCanary);
                    Check("owned extension popup completes two-way messages",native.RoundTrip&&native.Child.parentConfirmed);
                    Check("owned extension target navigates once",native.Requests==1&&native.TargetNavigations.Count(u=>u==native.TargetUrl)==1);
                }
                finally{CloseTab(main,native.Target);}
                CloseTab(main,extensionParent);extensionParent=null;
                reopened=AddTab(main,parentUrl);main.BrowserTabs.SelectedItem=reopened.Item;await Wait(()=>reopened.Initialized);
                await WaitJs(reopened.View.CoreWebView2,"harborPopupProof.ready===true");
                await reopened.View.CoreWebView2.ExecuteScriptAsync("readNativeCanary()");await WaitJs(reopened.View.CoreWebView2,"harborNativeRead===true");
                Check("pending native storage survives closing and reopening controls",Json.Deserialize<string>(await reopened.View.CoreWebView2.ExecuteScriptAsync("harborNativeValue"))==nativeCanary);
                Check("reopening uses the same enabled extension registration",(await core.Profile.GetBrowserExtensionsAsync()).Count(e=>e.Id==fixtureId&&e.IsEnabled)==1);
                AuthSleepPolicyChecks(main);
                File.WriteAllText(Path.Combine(output,"auth-handoff-summary.json"),Json.Serialize(new{OldCopyBaselineReproduced=true,NativeGetOpenerAndStorage=true,NativePostPreserved=true,OwnExtensionStoragePersists=true,Scope="Synthetic local HTTP and an owned MV3 canary only. No account login, Proton provider state, credentials or live profile was used."}));
            }
            finally
            {
                if(reopened!=null)CloseTab(main,reopened);if(extensionParent!=null)CloseTab(main,extensionParent);
                source.IsProviderAuthentication=sourceWasAuth;
                main.BrowserTabs.SelectedItem=source.Item;
                if(fixtureId!=null){var fixture=(await core.Profile.GetBrowserExtensionsAsync()).FirstOrDefault(e=>e.Id==fixtureId);if(fixture!=null)await fixture.RemoveAsync();}
            }
        }
        var after=await core.Profile.GetBrowserExtensionsAsync();
        Check("handoff fixture cleanup preserves every original native component",after.Count==extensionsBefore.Count&&after.All(e=>extensionsBefore.ContainsKey(e.Id)&&extensionsBefore[e.Id]==e.IsEnabled));
    }

    private static async Task AuthOldCopyBaseline(CoreWebView2Environment environment,AuthProofServer server)
    {
        var parent=new WebView2();var child=new WebView2();var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());grid.Children.Add(parent);Grid.SetColumn(child,1);grid.Children.Add(child);
        var window=Offscreen(new Window{Content=grid,Width=800,Height=500});window.Show();
        try
        {
            await parent.EnsureCoreWebView2Async(environment);await child.EnsureCoreWebView2Async(environment);
            parent.CoreWebView2.NewWindowRequested+=(s,e)=>{e.Handled=true;child.CoreWebView2.Navigate(e.Uri);};
            await AuthNavigate(parent.CoreWebView2,server.ParentUrl);await WaitJs(parent.CoreWebView2,"harborPopupProof.ready===true");
            string canary="harbor-synthetic-"+Guid.NewGuid().ToString("N");string target=server.ChildUrl(canary);
            await Gesture(parent.CoreWebView2,"beginPopupProof("+Json.Serialize(target)+","+Json.Serialize("old-copy-"+canary)+","+Json.Serialize(canary)+")");
            await WaitJs(child.CoreWebView2,"globalThis.harborChildProof?.ready===true");
            var state=Json.Deserialize<AuthChildState>(await child.CoreWebView2.ExecuteScriptAsync("harborChildProof"));
            Check("old handled-and-URL-copy baseline loses window.opener",!state.hasOpener);
            Check("old handled-and-URL-copy baseline loses sessionStorage handoff",state.sessionCanary==null);
            Check("old handled-and-URL-copy baseline loses the requested name",state.name!="old-copy-"+canary);
        }
        finally{parent.Dispose();child.Dispose();window.Close();}
    }

    private sealed class AuthPopupObservation
    {
        internal BrowserTab Target;internal string TargetUrl,ExpectedName;internal AuthChildState Child;
        internal bool SourceReturnedWindow,RoundTrip,AuthAtCreation,PendingAtCreation,SuppressedAtCreation;
        internal int Requests;internal readonly List<string> TargetNavigations=new List<string>();
    }
    private sealed class AuthChildState
    {
        public bool ready{get;set;}public bool hasOpener{get;set;}public bool parentConfirmed{get;set;}
        public string name{get;set;}public string sessionCanary{get;set;}public string nativeCanary{get;set;}public string serverMethod{get;set;}public string serverBody{get;set;}
    }
    private static async Task<AuthPopupObservation> AuthBoundPopup(MainWindow main,BrowserTab source,string target,string canary,bool post)
    {
        var result=new AuthPopupObservation{TargetUrl=target,ExpectedName="harbor-proof-"+Guid.NewGuid().ToString("N")};var core=source.View.CoreWebView2;
        var original=main.BrowserTabs.Items.Cast<TabItem>().Select(i=>(BrowserTab)i.Tag).ToArray();
        NotifyCollectionChangedEventHandler added=(s,e)=>{
            if(e.NewItems==null)return;foreach(TabItem item in e.NewItems){var tab=item.Tag as BrowserTab;if(tab==null||original.Contains(tab)||result.Target!=null)continue;
                result.Target=tab;result.AuthAtCreation=tab.IsProviderAuthentication;result.PendingAtCreation=tab.IsPopupPending;result.SuppressedAtCreation=tab.SuppressInitialNavigation;
                tab.View.CoreWebView2InitializationCompleted+=(sender,args)=>{if(args.IsSuccess)tab.View.CoreWebView2.NavigationStarting+=(ss,nav)=>result.TargetNavigations.Add(nav.Uri);};
            }
        };
        EventHandler<CoreWebView2NewWindowRequestedEventArgs> requested=(s,e)=>{if(e.Uri==target)result.Requests++;};
        ((INotifyCollectionChanged)main.BrowserTabs.Items).CollectionChanged+=added;core.NewWindowRequested+=requested;
        try
        {
            main.BrowserTabs.SelectedItem=source.Item;
            await Gesture(core,(post?"beginPostProof(":"beginPopupProof(")+Json.Serialize(target)+","+Json.Serialize(result.ExpectedName)+","+Json.Serialize(canary)+")");
            await Wait(()=>result.Target!=null&&result.Target.Initialized);
            await WaitJs(result.Target.View.CoreWebView2,"globalThis.harborChildProof?.ready===true");
            await WaitJs(core,"harborPopupProof.roundTrip===true");
            await WaitJs(result.Target.View.CoreWebView2,"harborChildProof.parentConfirmed===true");
            result.Child=Json.Deserialize<AuthChildState>(await result.Target.View.CoreWebView2.ExecuteScriptAsync("harborChildProof"));
            result.SourceReturnedWindow=await core.ExecuteScriptAsync("harborPopupProof.returnedWindow===true")=="true";
            result.RoundTrip=await core.ExecuteScriptAsync("harborPopupProof.roundTrip===true")=="true";
            return result;
        }
        catch{if(result.Target!=null)CloseTab(main,result.Target);throw;}
        finally{((INotifyCollectionChanged)main.BrowserTabs.Items).CollectionChanged-=added;core.NewWindowRequested-=requested;}
    }

    private static void AuthSleepPolicyChecks(MainWindow main)
    {
        var policy=typeof(MainWindow).GetMethod("SleepProtection",BindingFlags.Instance|BindingFlags.NonPublic);
        var auth=new BrowserTab{Initialized=true,IsProviderAuthentication=true,LastKnownUrl="https://local-fixture.invalid/redirect",InactiveSinceUtc=DateTime.UtcNow.AddHours(-1)};
        var controls=new BrowserTab{PendingUrl=ProtonPreviewService.PopupUrl};
        Check("sticky authentication state prevents background sleep",Convert.ToString(policy.Invoke(main,new object[]{auth}))=="Proton sign-in or controls");
        Check("provider controls are sleep-protected even before initialization",Convert.ToString(policy.Invoke(main,new object[]{controls}))=="Proton sign-in or controls");
    }
    private static async Task AuthNavigate(CoreWebView2 core,string target)
    {
        ulong? navigation=null;var done=new TaskCompletionSource<bool>();
        EventHandler<CoreWebView2NavigationStartingEventArgs> starting=(s,e)=>{if(e.Uri==target)navigation=e.NavigationId;};
        EventHandler<CoreWebView2NavigationCompletedEventArgs> completed=(s,e)=>{if(e.NavigationId==navigation){if(e.IsSuccess)done.TrySetResult(true);else done.TrySetException(new Exception("Synthetic navigation failed: "+e.WebErrorStatus));}};
        core.NavigationStarting+=starting;core.NavigationCompleted+=completed;
        try{core.Navigate(target);if(await Task.WhenAny(done.Task,Task.Delay(20000))!=done.Task)throw new TimeoutException("Synthetic navigation timed out.");await done.Task;}
        finally{core.NavigationStarting-=starting;core.NavigationCompleted-=completed;}
    }

    private sealed class AuthProofServer:IDisposable
    {
        private readonly TcpListener _listener=new TcpListener(IPAddress.Loopback,0);private readonly string _fixtures;
        private readonly ConcurrentQueue<string> _requests=new ConcurrentQueue<string>();
        internal string Root{get;private set;}internal string ParentUrl{get{return Root+"/harbor-popup-proof.html";}}
        internal string ChildUrl(string canary){return Root+"/harbor-popup-child.html?canary="+canary;}
        internal int Count(string url){return _requests.Count(r=>r==url);}
        internal AuthProofServer(string fixtures){_fixtures=Path.GetFullPath(fixtures);_listener.Start();Root="http://127.0.0.1:"+((IPEndPoint)_listener.LocalEndpoint).Port;_ = Accept();}
        private async Task Accept(){try{while(true){var client=await _listener.AcceptTcpClientAsync();_ = Task.Run(()=>Serve(client));}}catch(ObjectDisposedException){}catch(SocketException){}catch(InvalidOperationException){}}
        private void Serve(TcpClient client)
        {
            using(client)try{
                client.ReceiveTimeout=5000;client.SendTimeout=5000;var stream=client.GetStream();var header=new StringBuilder();
                while(header.Length<16384&&!header.ToString().EndsWith("\r\n\r\n",StringComparison.Ordinal)){int next=stream.ReadByte();if(next<0)return;header.Append((char)next);}
                var lines=header.ToString().Split(new[]{"\r\n"},StringSplitOptions.None);var request=lines[0].Split(' ');if(request.Length<2)return;
                int length=0;foreach(string line in lines.Skip(1))if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))int.TryParse(line.Substring(15).Trim(),out length);
                if(length<0||length>16384)return;var body=new byte[length];int at=0;while(at<length){int read=stream.Read(body,at,length-at);if(read==0)return;at+=read;}
                var uri=new Uri(Root+request[1]);_requests.Enqueue(uri.AbsoluteUri);
                string file=uri.AbsolutePath=="/harbor-popup-proof.html"?"harbor-popup-proof.html":uri.AbsolutePath=="/harbor-popup-child.html"?"harbor-popup-child.html":null;
                string text=file==null?"Not found":File.ReadAllText(Path.Combine(_fixtures,file));
                if(file=="harbor-popup-child.html")text=text.Replace("__SERVER_REQUEST_JSON__",new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{method=request[0],body=Encoding.UTF8.GetString(body)}));
                byte[] payload=Encoding.UTF8.GetBytes(text);byte[] response=Encoding.ASCII.GetBytes("HTTP/1.1 "+(file==null?"404 Not Found":"200 OK")+"\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: "+payload.Length+"\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
                stream.Write(response,0,response.Length);stream.Write(payload,0,payload.Length);
            }catch(IOException){}catch(SocketException){}catch(ObjectDisposedException){}
        }
        public void Dispose(){_listener.Stop();}
    }
}
