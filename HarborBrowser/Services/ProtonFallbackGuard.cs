using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Services
{
    // A lower-priority browser proxy preference. Proton is installed afterward
    // and controls the active route. Clearing its preference exposes this reject
    // route instead of the system/direct route. It never forwards any traffic.
    internal sealed class ProtonFallbackGuard : IDisposable
    {
        internal const string ExtensionId="pdpnihohpkanplhhcbalplbfmephfhek";
        private const string Page="chrome-extension://"+ExtensionId+"/guard.html";
        private readonly string _folder,_intent;
        private readonly SemaphoreSlim _gate=new SemaphoreSlim(1);
        private readonly CancellationTokenSource _lifetime=new CancellationTokenSource();
        private TcpListener _reject;
        private int _port;
        private CoreWebView2Controller _controller;
        private CoreWebView2Environment _controllerEnvironment;
        private bool _disposed;
        internal bool Armed {get;private set;}
        internal ProtonFallbackGuard(string root)
        {
            _folder=Path.Combine(root,"BrowserProtection");_intent=Path.Combine(root,"browser-protection.txt");
            Armed=File.Exists(_intent)&&File.ReadAllText(_intent).Trim()=="on";
        }
        private int RejectPort
        {
            get
            {
                if(_disposed)throw new ObjectDisposedException(nameof(ProtonFallbackGuard));
                if(_reject==null)
                {
                    _reject=new TcpListener(IPAddress.Loopback,0);_reject.Start(32);
                    _port=((IPEndPoint)_reject.LocalEndpoint).Port;_=RejectConnections(_reject);
                }
                return _port;
            }
        }
        internal string BrowserArguments
        {get{return "--proxy-server=http://127.0.0.1:"+RejectPort+" --proxy-bypass-list=https://account.proton.me:443;https://account.protonvpn.com:443;https://account-api.proton.me:443;<-loopback>";}}
        private async Task RejectConnections(TcpListener listener)
        {
            try
            {
                while(!_disposed)
                {
                    var client=await listener.AcceptTcpClientAsync();
                    // No request headers, destinations or credentials are read.
                    using(client)
                    {
                        try
                        {
                            client.SendTimeout=1000;
                            byte[] response=Encoding.ASCII.GetBytes("HTTP/1.1 503 Harbor connection paused\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                            client.GetStream().Write(response,0,response.Length);
                        }
                        catch(IOException){}catch(SocketException){}
                    }
                }
            }
            catch(ObjectDisposedException){}catch(SocketException){}catch(InvalidOperationException){}
        }
        internal Task Prepare(CoreWebView2Environment environment,CoreWebView2Profile profile,IntPtr owner,bool armed)
        {return Apply(environment,profile,owner,armed,false);}
        internal Task SetArmed(CoreWebView2Environment environment,CoreWebView2Profile profile,IntPtr owner,bool armed)
        {return Apply(environment,profile,owner,armed,true);}
        private async Task Apply(CoreWebView2Environment environment,CoreWebView2Profile profile,IntPtr owner,bool armed,bool persist)
        {
            if(profile.IsInPrivateModeEnabled)return;
            await _gate.WaitAsync(_lifetime.Token);
            bool before=Armed;
            try
            {
                if(!persist)armed=armed||Armed;
                await EnsureController(environment,profile,owner);
                await Configure(armed);
                if(persist)File.WriteAllText(_intent,armed?"on":"off");
                Armed=armed;
            }
            catch
            {
                // A failed explicit off must not become regular browsing on
                // this run or the next. Roll back a failed first enable to off.
                Armed=before||!armed;
                if(Armed)try{File.WriteAllText(_intent,"on");}catch(IOException){}
                try{if(_controller!=null)await Configure(Armed);}catch(Exception){}
                throw;
            }
            finally{ResetController();_gate.Release();}
        }
        private async Task EnsureController(CoreWebView2Environment environment,CoreWebView2Profile profile,IntPtr owner)
        {
            if(_controller!=null&&ReferenceEquals(_controllerEnvironment,environment))return;
            ResetController();
            var extensions=await ProtonRouteProbe.Bounded(profile.GetBrowserExtensionsAsync(),15000,_lifetime.Token);
            var guard=extensions.FirstOrDefault(e=>e.Id==ExtensionId);
            if(guard==null)
            {
                if(extensions.Any(e=>e.Id==ProtonPreviewService.ExtensionId))
                    throw new InvalidOperationException("This profile has an older Proton installation without the required browser protection guard. Existing Proton sign-in data has been preserved.");
                string source=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","BrowserVpn","HarborGuard");
                Directory.CreateDirectory(_folder);
                foreach(string name in new[]{"manifest.json","guard.html"})File.Copy(Path.Combine(source,name),Path.Combine(_folder,name),true);
                guard=await ProtonRouteProbe.Bounded(profile.AddBrowserExtensionAsync(_folder),15000,_lifetime.Token);
                if(guard.Id!=ExtensionId)throw new InvalidDataException("The browser protection identity did not match.");
            }
            if(!guard.IsEnabled)await guard.EnableAsync(true);
            var options=environment.CreateCoreWebView2ControllerOptions();options.ProfileName=profile.ProfileName;
            var controller=await ProtonRouteProbe.CreateController(environment.CreateCoreWebView2ControllerAsync(owner,options),_lifetime.Token);
            _controller=controller;_controllerEnvironment=environment;controller.IsVisible=false;
            var core=controller.CoreWebView2;core.Settings.AreDevToolsEnabled=false;
            core.NavigationStarting+=(s,e)=>{if(e.Uri!=Page&&e.Uri!="about:blank")e.Cancel=true;};
            core.PermissionRequested+=(s,e)=>{e.Handled=true;e.SavesInProfile=false;e.State=CoreWebView2PermissionState.Deny;};
            core.NewWindowRequested+=(s,e)=>e.Handled=true;core.DownloadStarting+=(s,e)=>e.Cancel=true;
            var loaded=new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> complete=(s,e)=>loaded.TrySetResult(e.IsSuccess);
            core.NavigationCompleted+=complete;
            try
            {
                core.Navigate(Page);
                if(!await ProtonRouteProbe.Bounded(loaded.Task,15000,_lifetime.Token))throw new InvalidOperationException("The browser connection fallback did not load.");
            }
            catch{ResetController();throw;}
            finally{core.NavigationCompleted-=complete;}
        }
        private async Task Configure(bool armed)
        {
            var core=_controller.CoreWebView2;
            string nonce=Guid.NewGuid().ToString("N");var done=new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2WebMessageReceivedEventArgs> reply=(s,e)=>
            {
                if(e.Source!=Page)return;
                try
                {
                    string json=e.WebMessageAsJson;if(json.Length>1024)return;
                    var data=new JavaScriptSerializer{MaxJsonLength=1024,RecursionLimit=4}.Deserialize<Dictionary<string,object>>(json);
                    object channel,ok;
                    if(data!=null&&data.TryGetValue("nonce",out channel)&&Equals(channel,nonce)&&data.TryGetValue("ok",out ok)&&ok is bool)done.TrySetResult((bool)ok);
                }
                catch(Exception){}
            };
            core.WebMessageReceived+=reply;
            try
            {
                object config=armed?(object)new{mode="fixed_servers",rules=new{singleProxy=new{scheme="http",host="127.0.0.1",port=RejectPort},bypassList=new[]{"https://account.proton.me:443","https://account.protonvpn.com:443","https://account-api.proton.me:443","<-loopback>"}}}:new{mode="system"};
                string value=new JavaScriptSerializer().Serialize(config);
                string script="(()=>{if(location.href!=="+new JavaScriptSerializer().Serialize(Page)+")return;const finish=ok=>chrome.webview.postMessage({nonce:'"+nonce+"',ok:!!ok});try{chrome.proxy.settings.set({scope:'regular',value:"+value+"},()=>finish(!chrome.runtime.lastError));}catch{finish(false);}})()";
                await ProtonRouteProbe.Bounded(core.ExecuteScriptAsync(script),5000,_lifetime.Token);
                if(!await ProtonRouteProbe.Bounded(done.Task,5000,_lifetime.Token))throw new InvalidOperationException("The browser connection fallback could not be applied.");
            }
            finally{core.WebMessageReceived-=reply;}
        }
        internal void ResetController()
        {
            var controller=_controller;_controller=null;_controllerEnvironment=null;
            try{controller?.Close();}catch(Exception){}
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;_lifetime.Cancel();ResetController();_reject?.Stop();
        }
    }
}
