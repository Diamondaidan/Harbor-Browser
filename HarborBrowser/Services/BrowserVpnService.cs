using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Services
{
    // Browser-local proxy settings plus an owned user-level Tor process.
    // No Windows proxy, VPN profile, driver or installed service is changed.
    public sealed class BrowserVpnService:IDisposable
    {
        public const string ExtensionId="bhgkokoejhjhncbidhiemiemcachhehg";
        public const string ControlHost="harbor-tor-control.invalid";
        private readonly string _root,_setting,_assets;
        private readonly SemaphoreSlim _gate=new SemaphoreSlim(1);
        private Process _tor;
        private TaskCompletionSource<bool> _bootstrap;
        private bool _cancelRequested;
        private bool _disposed;
        private CoreWebView2Environment _configuredEnvironment;
        public bool Enabled {get;private set;}
        public bool Connected {get;private set;}
        public string Status {get;private set;}="Off · regular browser connection";
        public int SocksPort {get;private set;}
        public int HelperProcessId {get{return _tor!=null&&!_tor.HasExited?_tor.Id:0;}}
        public BrowserVpnService(string profile,string assets=null)
        {
            _root=profile;_setting=Path.Combine(profile,"tor-preview.txt");
            _assets=assets??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","BrowserVpn");
            Enabled=File.Exists(_setting)&&File.ReadAllText(_setting).Trim()!="off";
            if(Enabled)Status="Waiting to restore the Tor connection";
        }
        public async Task Enable(CoreWebView2Profile profile,CoreWebView2Environment environment,IntPtr owner)
        {
            if(_disposed)throw new ObjectDisposedException(nameof(BrowserVpnService));
            ValidateFiles();File.WriteAllText(_setting,"on");Enabled=true;
            await Prepare(profile,environment,owner);
        }
        private void ValidateFiles()
        {
            if(!File.Exists(Path.Combine(_assets,"Router","manifest.json"))||!File.Exists(Path.Combine(_assets,"TorRuntime","tor","tor.exe")))
                throw new FileNotFoundException("Extract the complete preview ZIP, including its Tor files.");
        }
        public async Task Prepare(CoreWebView2Profile profile,CoreWebView2Environment environment,IntPtr owner)
        {
            if(_disposed)throw new ObjectDisposedException(nameof(BrowserVpnService));
            if(!Enabled||profile.IsInPrivateModeEnabled)return;
            await _gate.WaitAsync();
            try
            {
                if(_disposed)throw new ObjectDisposedException(nameof(BrowserVpnService));
                if(!Enabled)return;
                _cancelRequested=false;
                if(Connected&&_configuredEnvironment==environment&&HelperProcessId!=0)return;
                ValidateFiles();StopHelper();
                var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();SocksPort=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
                Status="Configuring Harbor-only routing…";
                var extension=await profile.AddBrowserExtensionAsync(Path.Combine(_assets,"Router"));
                if(extension.Id!=ExtensionId){await extension.EnableAsync(false);await extension.RemoveAsync();throw new InvalidDataException("The bundled routing extension has an unexpected identity.");}
                if(!extension.IsEnabled)await extension.EnableAsync(true);
                // Configure a closed local port first: failed startup must not
                // silently send subsequent browser requests over the direct route.
                await Configure(environment,profile.ProfileName,owner,SocksPort);
                if(_cancelRequested||_disposed)throw new OperationCanceledException("Tor connection cancelled.");
                _configuredEnvironment=environment;
                await StartHelper(SocksPort);
                Connected=true;Status="Connected to Tor · Harbor normal tabs only";
            }
            catch(Exception ex){Connected=false;Status="Not connected · "+ex.Message+" Use Disconnect and restore to return to your regular connection.";throw;}
            finally{_gate.Release();}
        }
        private async Task Configure(CoreWebView2Environment environment,string name,IntPtr owner,int port)
        {
            var options=environment.CreateCoreWebView2ControllerOptions();options.ProfileName=name;options.IsInPrivateModeEnabled=false;
            var controller=await environment.CreateCoreWebView2ControllerAsync(owner,options);
            try
            {
                controller.IsVisible=false;var core=controller.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping(ControlHost,Path.Combine(_assets,"Control"),CoreWebView2HostResourceAccessKind.DenyCors);
                var done=new TaskCompletionSource<bool>();
                core.WebMessageReceived+=(s,e)=>{
                    Uri source;if(!Uri.TryCreate(e.Source,UriKind.Absolute,out source)||source.Scheme!="https"||source.Host!=ControlHost)return;
                    try
                    {
                        var result=new JavaScriptSerializer().Deserialize<RoutingReply>(e.WebMessageAsJson);
                        if(result!=null&&result.Port==port){if(result.Ok)done.TrySetResult(true);else done.TrySetException(new InvalidOperationException(result.Error??"The browser could not apply routing settings."));}
                    }
                    catch(Exception ex){done.TrySetException(ex);}
                };
                core.Navigate("https://"+ControlHost+"/index.html?port="+port);
                if(await Task.WhenAny(done.Task,Task.Delay(20000))!=done.Task)throw new TimeoutException("Browser routing setup timed out.");
                await done.Task;
            }
            finally{controller.Close();}
        }
        private async Task StartHelper(int port)
        {
            string runtime=Path.Combine(_assets,"TorRuntime"),data=Path.Combine(_root,"Tor");Directory.CreateDirectory(data);
            string config=Path.Combine(data,"torrc");
            Func<string,string> quote=value=>"\""+value.Replace("\\","/").Replace("\"","\\\"")+"\"";
            File.WriteAllText(config,"ClientOnly 1\nSocksPort 127.0.0.1:"+port+"\nSocksPolicy accept 127.0.0.1\nSocksPolicy reject *\nSafeSocks 1\nAvoidDiskWrites 1\nControlPort 0\nDataDirectory "+quote(data)+"\nGeoIPFile "+quote(Path.Combine(runtime,"data","geoip"))+"\nGeoIPv6File "+quote(Path.Combine(runtime,"data","geoip6"))+"\nLog notice stdout\n__OwningControllerProcess "+Process.GetCurrentProcess().Id+"\n");
            var ready=new TaskCompletionSource<bool>();_bootstrap=ready;
            var process=new Process{StartInfo=new ProcessStartInfo(Path.Combine(runtime,"tor","tor.exe"),"-f "+WindowsCommandRunner.Quote(config)){WorkingDirectory=runtime,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true},EnableRaisingEvents=true};
            _tor=process;Status="Connecting to Tor…";
            process.OutputDataReceived+=(s,e)=>{
                if(e.Data==null||!ReferenceEquals(_tor,process))return;
                int boot=e.Data.IndexOf("Bootstrapped ",StringComparison.Ordinal);
                if(boot>=0){Status="Tor: "+e.Data.Substring(boot);if(e.Data.Contains("Bootstrapped 100%"))ready.TrySetResult(true);}
                if(e.Data.Contains("[err]"))ready.TrySetException(new InvalidOperationException("Tor could not start. Check its bundled files and network access."));
            };
            process.ErrorDataReceived+=(s,e)=>{};
            process.Exited+=(s,e)=>{if(ReferenceEquals(_tor,process)){Connected=false;Status="Tor stopped · browser traffic remains blocked until you reconnect or disconnect.";}ready.TrySetException(new InvalidOperationException("The Tor helper stopped."));};
            if(!process.Start())throw new InvalidOperationException("Windows could not start Tor.");
            process.BeginOutputReadLine();process.BeginErrorReadLine();
            if(await Task.WhenAny(ready.Task,Task.Delay(120000))!=ready.Task){StopHelper();throw new TimeoutException("Tor could not connect within two minutes. This network may block Tor.");}
            await ready.Task;
        }
        public async Task Remove(CoreWebView2Profile profile)
        {
            // Stop first so Disconnect also interrupts a pending bootstrap.
            _cancelRequested=true;StopHelper();await _gate.WaitAsync();
            try
            {
                var extension=(await profile.GetBrowserExtensionsAsync()).FirstOrDefault(e=>e.Id==ExtensionId);
                if(extension!=null){await extension.EnableAsync(false);await extension.RemoveAsync();}
                File.WriteAllText(_setting,"off");Enabled=false;_configuredEnvironment=null;Status="Off · regular browser connection restored";
            }
            finally{_gate.Release();}
        }
        private void StopHelper()
        {
            var process=_tor;_tor=null;Connected=false;
            _bootstrap?.TrySetCanceled();
            if(process==null)return;
            try{if(!process.HasExited){process.Kill();process.WaitForExit(2000);}}catch(InvalidOperationException){}finally{process.Dispose();}
        }
        public void Dispose(){_disposed=true;_cancelRequested=true;StopHelper();}
        private sealed class RoutingReply {public bool Ok{get;set;}public int Port{get;set;}public string Error{get;set;}}
    }
}
