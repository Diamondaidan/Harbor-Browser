using System;
using System.Diagnostics;
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
    public enum BrowserConnectionState { Off, Connecting, Checking, Connected, Failed, Disconnecting }

    // Profile-local routing and an owned Tor process. No Windows proxy/service changes.
    public sealed class BrowserVpnService:IDisposable
    {
        public const string ExtensionId="bhgkokoejhjhncbidhiemiemcachhehg";
        public const string ControlHost="harbor-tor-control.invalid";
        private readonly string _root,_setting,_assets;
        private readonly SemaphoreSlim _gate=new SemaphoreSlim(1);
        private readonly CancellationTokenSource _lifetime=new CancellationTokenSource();
        private CancellationTokenSource _operation;
        private Process _tor;
        private TaskCompletionSource<bool> _bootstrap;
        private volatile bool _enabled,_desiredEnabled,_connected,_verified,_bootstrapped,_disposed,_explicitRemoval,_networkAvailable=true;
        private int _intent;
        private CoreWebView2Environment _configuredEnvironment;
        public bool Enabled {get{return _enabled;}}
        public bool Connected {get{return _connected;}}
        public bool RouteVerified {get{return _verified;}}
        public bool Bootstrapped {get{return _bootstrapped;}}
        public BrowserConnectionState State {get;private set;}=BrowserConnectionState.Off;
        public string Status {get;private set;}="Off · regular browser connection";
        public DateTime? LastVerifiedUtc {get;private set;}
        public int SocksPort {get;private set;}
        public event EventHandler StatusChanged;
        internal Func<CoreWebView2, CancellationToken, Task<bool>> VerificationOverride;
        public int HelperProcessId
        {
            get{var process=_tor;try{return process!=null&&!process.HasExited?process.Id:0;}catch(InvalidOperationException){return 0;}}
        }
        public BrowserVpnService(string profile,string assets=null)
        {
            _root=profile;_setting=Path.Combine(profile,"tor-preview.txt");
            _assets=assets??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","BrowserVpn");
            _desiredEnabled=_enabled=File.Exists(_setting)&&File.ReadAllText(_setting).Trim()!="off";
            if(Enabled)SetState(BrowserConnectionState.Connecting,"Waiting to restore the Tor connection");
        }
        private void SetState(BrowserConnectionState state,string status)
        {State=state;Status=status;StatusChanged?.Invoke(this,EventArgs.Empty);}
        private void CancelOperation(){try{_operation?.Cancel();}catch(ObjectDisposedException){}}
        private bool Current(int intent){return !_disposed&&_desiredEnabled&&Enabled&&intent==Volatile.Read(ref _intent);}
        private void RequireCurrent(int intent,CancellationToken cancellation)
        {cancellation.ThrowIfCancellationRequested();if(!Current(intent))throw new OperationCanceledException("The connection request was cancelled.");}
        public async Task Enable(CoreWebView2Profile profile,CoreWebView2Environment environment,IntPtr owner)
        {
            if(_disposed)throw new ObjectDisposedException(nameof(BrowserVpnService));
            ValidateFiles();int intent=Interlocked.Increment(ref _intent);
            CancelOperation();_explicitRemoval=false;_desiredEnabled=_enabled=true;_connected=_verified=false;
            File.WriteAllText(_setting,"on");SetState(BrowserConnectionState.Connecting,"Connecting to Tor…");
            await PrepareCore(profile,environment,owner,intent,true);
        }
        public Task Prepare(CoreWebView2Profile profile,CoreWebView2Environment environment,IntPtr owner)
        {
            if(_disposed)throw new ObjectDisposedException(nameof(BrowserVpnService));
            if(!Enabled||profile.IsInPrivateModeEnabled)return Task.CompletedTask;
            return PrepareCore(profile,environment,owner,Volatile.Read(ref _intent),false);
        }
        private void ValidateFiles()
        {
            if(!File.Exists(Path.Combine(_assets,"Router","manifest.json"))||!File.Exists(Path.Combine(_assets,"TorRuntime","tor","tor.exe")))
                throw new FileNotFoundException("Harbor's bundled Tor files are missing. Rerun Harbor Setup to repair the installation.");
        }
        private async Task PrepareCore(CoreWebView2Profile profile,CoreWebView2Environment environment,IntPtr owner,int intent,bool reconnect)
        {
            await _gate.WaitAsync(_lifetime.Token);
            using(var operation=CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
            {
                _operation=operation;
                try
                {
                    RequireCurrent(intent,operation.Token);
                    if(!reconnect&&_configuredEnvironment==environment&&RouteVerified&&Connected&&HelperProcessId!=0)return;
                    if(!reconnect&&_configuredEnvironment==environment&&State==BrowserConnectionState.Failed)
                        throw new InvalidOperationException("Tor needs a connection check or reconnect. Open the Tor controls.");
                    if(!_networkAvailable)throw new InvalidOperationException("The network is unavailable. Reconnect when it returns.");
                    ValidateFiles();StopHelper();_connected=_verified=false;
                    var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();SocksPort=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
                    SetState(BrowserConnectionState.Connecting,"Configuring Harbor-only routing…");
                    var extension=(await Deadline(profile.GetBrowserExtensionsAsync(),20000,operation.Token)).FirstOrDefault(e=>e.Id==ExtensionId);
                    if(extension==null){
                        var adding=profile.AddBrowserExtensionAsync(Path.Combine(_assets,"Router"));
                        try{extension=await Deadline(adding,20000,operation.Token);}catch{_ = RemoveLateExtension(adding);throw;}
                    }
                    RequireCurrent(intent,operation.Token);
                    if(extension.Id!=ExtensionId){await extension.EnableAsync(false);await extension.RemoveAsync();throw new InvalidDataException("The routing extension has an unexpected identity.");}
                    if(!extension.IsEnabled)await WaitOperation(extension.EnableAsync(true),operation.Token);
                    // A closed local port is installed before the helper starts.
                    await Routing(environment,profile.ProfileName,owner,SocksPort,false,operation.Token);
                    RequireCurrent(intent,operation.Token);_configuredEnvironment=environment;
                    await StartHelper(SocksPort,operation.Token);
                    RequireCurrent(intent,operation.Token);
                    SetState(BrowserConnectionState.Checking,"Checking the browser's Tor route…");
                    await VerifyCore(profile,environment,owner,operation.Token);
                    RequireCurrent(intent,operation.Token);
                    _connected=_verified=true;LastVerifiedUtc=DateTime.UtcNow;
                    SetState(BrowserConnectionState.Connected,"Tor route verified · Harbor normal tabs only");
                }
                catch(Exception ex)
                {
                    if(Current(intent))Fail(_networkAvailable?"Tor is not verified. "+Friendly(ex):"Network unavailable. Browsing is paused until Tor is checked again.");
                    throw;
                }
                finally{if(ReferenceEquals(_operation,operation))_operation=null;_gate.Release();}
            }
        }
        public void NotifyConnectionChange(bool available,string reason)
        {
            if(_disposed)return;_networkAvailable=available;
            if(!Enabled)return;
            _connected=_verified=false;CancelOperation();
            SetState(available?BrowserConnectionState.Checking:BrowserConnectionState.Failed,
                reason+" Browsing is paused until the Tor route is verified.");
        }
        public void PauseVerification()
        {if(!Enabled||_disposed)return;_connected=_verified=false;CancelOperation();SetState(BrowserConnectionState.Checking,"Waiting for a new Tor connection check.");}
        private void Fail(string reason)
        {_connected=_verified=false;SetState(BrowserConnectionState.Failed,reason+" Reconnect, or use Disconnect and restore for regular browsing.");}
        private static string Friendly(Exception error)
        {return error is OperationCanceledException?"The connection check was cancelled.":error is TimeoutException?"The connection check timed out.":error.Message;}
        public async Task<bool> CheckHealth(CoreWebView2Profile profile,CoreWebView2Environment environment,IntPtr owner)
        {
            if(_disposed||!Enabled||!_desiredEnabled)return false;
            int intent=Volatile.Read(ref _intent);await _gate.WaitAsync(_lifetime.Token);
            using(var operation=CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
            {
                _operation=operation;
                try
                {
                    RequireCurrent(intent,operation.Token);
                    if(!_networkAvailable||!Bootstrapped||HelperProcessId==0)throw new InvalidOperationException("The Tor helper is not ready. Reconnect when the network is available.");
                    SetState(BrowserConnectionState.Checking,"Checking the browser's Tor route…");
                    await VerifyCore(profile,environment,owner,operation.Token);RequireCurrent(intent,operation.Token);
                    _configuredEnvironment=environment;_connected=_verified=true;LastVerifiedUtc=DateTime.UtcNow;
                    SetState(BrowserConnectionState.Connected,"Tor route verified · Harbor normal tabs only");return true;
                }
                catch(Exception ex){if(Current(intent))Fail(_networkAvailable?Friendly(ex):"Network unavailable.");return false;}
                finally{if(ReferenceEquals(_operation,operation))_operation=null;_gate.Release();}
            }
        }
        private async Task VerifyCore(CoreWebView2Profile profile,CoreWebView2Environment environment,IntPtr owner,CancellationToken cancellation)
        {
            var extension=(await Deadline(profile.GetBrowserExtensionsAsync(),20000,cancellation)).FirstOrDefault(e=>e.Id==ExtensionId);
            if(extension==null||!extension.IsEnabled)throw new InvalidOperationException("Harbor's routing extension is unavailable; browsing is paused.");
            var controller=await Routing(environment,profile.ProfileName,owner,SocksPort,true,cancellation,true);
            try
            {
                bool isTor=VerificationOverride==null?await Probe(controller.CoreWebView2,cancellation):await Deadline(VerificationOverride(controller.CoreWebView2,cancellation),25000,cancellation);
                if(!isTor)throw new InvalidOperationException("The verification response did not confirm Tor.");
                if(HelperProcessId==0)throw new InvalidOperationException("The Tor helper stopped during verification.");
            }
            finally{controller.Close();}
        }
        internal async Task<CoreWebView2Controller> Routing(CoreWebView2Environment environment,string name,IntPtr owner,int port,bool inspect,CancellationToken cancellation,bool retain=false)
        {
            var options=environment.CreateCoreWebView2ControllerOptions();options.ProfileName=name;options.IsInPrivateModeEnabled=false;
            var creation=environment.CreateCoreWebView2ControllerAsync(owner,options);
            CoreWebView2Controller controller;
            try{controller=await Deadline(creation,20000,cancellation);}
            catch{_ = CloseLate(creation);throw;}
            try
            {
                controller.IsVisible=false;var core=controller.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping(ControlHost,Path.Combine(_assets,"Control"),CoreWebView2HostResourceAccessKind.DenyCors);
                string nonce=Guid.NewGuid().ToString("N");var done=new TaskCompletionSource<bool>();
                core.NavigationCompleted+=(s,e)=>{if(!e.IsSuccess)done.TrySetException(new InvalidOperationException("The local routing control page failed: "+e.WebErrorStatus));};
                core.WebMessageReceived+=(s,e)=>{
                    if(!Uri.TryCreate(e.Source,UriKind.Absolute,out var source)||source.Scheme!="https"||source.Host!=ControlHost)return;
                    try{
                        var result=new JavaScriptSerializer().Deserialize<RoutingReply>(e.WebMessageAsJson);
                        if(result!=null&&result.Port==port&&result.Nonce==nonce){if(result.Ok)done.TrySetResult(true);else done.TrySetException(new InvalidOperationException(result.Error??"Browser routing settings could not be verified."));}
                    }catch(Exception ex){done.TrySetException(ex);}
                };
                core.Navigate("https://"+ControlHost+"/index.html?port="+port+"&nonce="+nonce+"&action="+(inspect?"inspect":"configure"));
                await Deadline(done.Task,20000,cancellation);
                if(retain)return controller;
            }
            catch{controller.Close();throw;}
            controller.Close();return null;
        }
        private static async Task CloseLate(Task<CoreWebView2Controller> creation){try{(await creation).Close();}catch{}}
        private async Task RemoveLateExtension(Task<CoreWebView2BrowserExtension> adding)
        {
            try{var extension=await adding;await _gate.WaitAsync();try{if(_explicitRemoval&&!_desiredEnabled&&extension.Id==ExtensionId)await WaitOperation(extension.RemoveAsync(),CancellationToken.None);}finally{_gate.Release();}}catch{}
        }
        private static async Task<bool> Completion(Task operation){await operation;return true;}
        private static async Task WaitOperation(Task operation,CancellationToken cancellation){await Deadline(Completion(operation),20000,cancellation);}
        private static async Task<T> Deadline<T>(Task<T> task,int milliseconds,CancellationToken cancellation)
        {
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                try{
                    if(await Task.WhenAny(task,Task.Delay(milliseconds,deadline.Token))!=task){Observe(task);cancellation.ThrowIfCancellationRequested();throw new TimeoutException();}
                    var result=await task;cancellation.ThrowIfCancellationRequested();return result;
                }finally{deadline.Cancel();}
            }
        }
        private static void Observe(Task task){task.ContinueWith(t=>{var ignored=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);}
        private static async Task<bool> Probe(CoreWebView2 core,CancellationToken cancellation)
        {
            string target="https://check.torproject.org/api/ip?harbor-health="+Guid.NewGuid().ToString("N");
            var done=new TaskCompletionSource<bool>();ulong? navigation=null;
            EventHandler<CoreWebView2NavigationStartingEventArgs> starting=(s,e)=>{if(e.Uri==target)navigation=e.NavigationId;else{e.Cancel=true;done.TrySetException(new InvalidOperationException("The verification page tried to redirect."));}};
            EventHandler<CoreWebView2NavigationCompletedEventArgs> completed=(s,e)=>{if(navigation==e.NavigationId&&!e.IsSuccess)done.TrySetException(new InvalidOperationException("The Tor verification request failed."));};
            EventHandler<CoreWebView2WebResourceResponseReceivedEventArgs> response=async(s,e)=>{
                if(e.Request.Uri!=target)return;
                try{
                    if(e.Response.StatusCode!=200)throw new InvalidOperationException("The Tor verification server did not return a successful response.");
                    using(var stream=await e.Response.GetContentAsync()){
                        if(stream==null)throw new InvalidDataException("The verification response was empty.");
                        using(var memory=new MemoryStream()){
                            var buffer=new byte[4096];int count;
                            while((count=await stream.ReadAsync(buffer,0,buffer.Length))>0){if(memory.Length+count>16384)throw new InvalidDataException("The verification response was too large.");memory.Write(buffer,0,count);}
                            var result=new JavaScriptSerializer{MaxJsonLength=16384,RecursionLimit=8}.Deserialize<System.Collections.Generic.Dictionary<string,object>>(Encoding.UTF8.GetString(memory.ToArray()));
                            object value;done.TrySetResult(result!=null&&result.TryGetValue("IsTor",out value)&&value is bool&&(bool)value);
                        }
                    }
                }catch(Exception ex){done.TrySetException(ex);}
            };
            core.NavigationStarting+=starting;core.NavigationCompleted+=completed;core.WebResourceResponseReceived+=response;
            try{
                var request=core.Environment.CreateWebResourceRequest(target,"GET",null,"Cache-Control: no-cache\r\nPragma: no-cache");
                core.NavigateWithWebResourceRequest(request);
                return await Deadline(done.Task,25000,cancellation);
            }finally{core.NavigationStarting-=starting;core.NavigationCompleted-=completed;core.WebResourceResponseReceived-=response;}
        }
        private async Task StartHelper(int port,CancellationToken cancellation)
        {
            string runtime=Path.Combine(_assets,"TorRuntime"),data=Path.Combine(_root,"Tor");Directory.CreateDirectory(data);
            string config=Path.Combine(data,"torrc");Func<string,string> quote=value=>"\""+value.Replace("\\","/").Replace("\"","\\\"")+"\"";
            File.WriteAllText(config,"ClientOnly 1\nSocksPort 127.0.0.1:"+port+"\nSocksPolicy accept 127.0.0.1\nSocksPolicy reject *\nSafeSocks 1\nAvoidDiskWrites 1\nControlPort 0\nDataDirectory "+quote(data)+"\nGeoIPFile "+quote(Path.Combine(runtime,"data","geoip"))+"\nGeoIPv6File "+quote(Path.Combine(runtime,"data","geoip6"))+"\nLog notice stdout\n__OwningControllerProcess "+Process.GetCurrentProcess().Id+"\n");
            var ready=new TaskCompletionSource<bool>();_bootstrap=ready;
            var process=new Process{StartInfo=new ProcessStartInfo(Path.Combine(runtime,"tor","tor.exe"),"-f "+WindowsCommandRunner.Quote(config)){WorkingDirectory=runtime,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true},EnableRaisingEvents=true};
            _tor=process;_bootstrapped=false;SetState(BrowserConnectionState.Connecting,"Connecting to Tor…");
            process.OutputDataReceived+=(s,e)=>{
                if(e.Data==null||!ReferenceEquals(_tor,process))return;
                int boot=e.Data.IndexOf("Bootstrapped ",StringComparison.Ordinal);
                if(boot>=0){if(_networkAvailable&&State==BrowserConnectionState.Connecting)SetState(BrowserConnectionState.Connecting,"Tor: "+e.Data.Substring(boot));if(e.Data.Contains("Bootstrapped 100%")){_bootstrapped=true;ready.TrySetResult(true);}}
                if(e.Data.Contains("[err]"))ready.TrySetException(new InvalidOperationException("Tor could not start. Check its bundled files and network access."));
            };
            process.ErrorDataReceived+=(s,e)=>{};
            process.Exited+=(s,e)=>{if(ReferenceEquals(_tor,process)){_bootstrapped=false;if(Enabled&&!_disposed)Fail("Tor stopped. Browsing is paused.");}ready.TrySetException(new InvalidOperationException("The Tor helper stopped."));};
            if(!process.Start())throw new InvalidOperationException("Windows could not start Tor.");
            process.BeginOutputReadLine();process.BeginErrorReadLine();
            try{await Deadline(ready.Task,120000,cancellation);}catch(TimeoutException){StopHelper();throw new TimeoutException("Tor could not connect within two minutes. This network may block Tor.");}
        }
        public async Task Remove(CoreWebView2Profile profile)
        {
            int intent=Interlocked.Increment(ref _intent);_explicitRemoval=true;_desiredEnabled=false;
            CancelOperation();StopHelper();SetState(BrowserConnectionState.Disconnecting,"Disconnecting Tor and restoring the regular route…");
            await _gate.WaitAsync();
            try{
                // Explicit removal remains available after cancellation/disposal.
                // Its bounded cleanup must not inherit the cancelled lifetime.
                var extension=(await Deadline(profile.GetBrowserExtensionsAsync(),20000,CancellationToken.None)).FirstOrDefault(e=>e.Id==ExtensionId);
                if(extension!=null){await WaitOperation(extension.EnableAsync(false),CancellationToken.None);await WaitOperation(extension.RemoveAsync(),CancellationToken.None);}
                _configuredEnvironment=null;
                if(!_desiredEnabled&&(intent==Volatile.Read(ref _intent)||_disposed)){File.WriteAllText(_setting,"off");_enabled=false;SetState(BrowserConnectionState.Off,"Off · regular browser connection restored");}
            }catch(Exception ex){if(!_disposed)Fail("The regular route could not be restored. "+ex.Message);throw;}
            finally{_gate.Release();}
        }
        private void StopHelper()
        {
            var process=_tor;_tor=null;_connected=_verified=_bootstrapped=false;_bootstrap?.TrySetCanceled();
            if(process==null)return;
            try{if(!process.HasExited){process.Kill();process.WaitForExit(2000);}}catch(InvalidOperationException){}finally{process.Dispose();}
        }
        public void Dispose(){if(_disposed)return;_disposed=true;_desiredEnabled=false;Interlocked.Increment(ref _intent);CancelOperation();_lifetime.Cancel();StopHelper();StatusChanged=null;}
        private sealed class RoutingReply{public bool Ok{get;set;}public int Port{get;set;}public string Nonce{get;set;}public string Error{get;set;}}
    }
}
