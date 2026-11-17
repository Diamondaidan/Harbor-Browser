using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Services
{
    internal sealed class ProtonProxyProof
    {
        internal bool Owned { get; set; }
        internal bool Supported { get; set; }
    }
    internal sealed class ProtonRouteCheckResult
    {
        internal bool Verified { get; set; }
        internal string Message { get; set; }
    }
    // Typed, bounded evidence for the browser request gate. No address or proxy
    // configuration is returned to the UI, logged, or persisted.
    internal static class ProtonRouteProbe
    {
        internal static async Task<string> Compare(CoreWebView2Environment environment, CoreWebView2 provider, IntPtr owner, string controlFolder, CancellationToken cancellation=default(CancellationToken))
        { return (await CompareDetailed(environment,provider,owner,controlFolder,cancellation)).Message; }

        internal static async Task<ProtonRouteCheckResult> CompareDetailed(CoreWebView2Environment environment, CoreWebView2 provider, IntPtr owner, string controlFolder, CancellationToken cancellation=default(CancellationToken))
        {
            using(var session=new ProtonRouteSession(environment,owner))return await session.Compare(provider,cancellation);
        }
        internal static ProtonRouteCheckResult Result(bool verified,string message)
        { return new ProtonRouteCheckResult{Verified=verified,Message=message}; }

        internal static async Task<bool> HasOwnedProxy(CoreWebView2 core,CancellationToken cancellation=default(CancellationToken))
        { var proof=await ReadProxyProof(core,cancellation);return proof.Owned&&proof.Supported; }

        internal static async Task<ProtonProxyProof> ReadProxyProof(CoreWebView2 core,CancellationToken cancellation=default(CancellationToken))
        {
            if(!ProtonPreviewService.IsProviderPage(core.Source))return new ProtonProxyProof();
            string nonce=Guid.NewGuid().ToString("N");var done=new TaskCompletionSource<ProtonProxyProof>();
            EventHandler<CoreWebView2WebMessageReceivedEventArgs> handler=(s,e)=>{
                if(!ProtonPreviewService.IsProviderPage(e.Source))return;
                try{
                    string json=e.WebMessageAsJson;if(json.Length>2048||!json.Contains(nonce))return;
                    var value=new JavaScriptSerializer{MaxJsonLength=2048,RecursionLimit=4}.Deserialize<Dictionary<string,object>>(json);
                    object key,owned,supported;
                    if(value!=null&&value.TryGetValue("harborProxyProbe",out key)&&Convert.ToString(key)==nonce&&
                       value.TryGetValue("owned",out owned)&&owned is bool&&value.TryGetValue("supported",out supported)&&supported is bool)
                        done.TrySetResult(new ProtonProxyProof{Owned=(bool)owned,Supported=(bool)supported});
                }catch{}
            };
            core.WebMessageReceived+=handler;
            try{
                string script=@"(()=>{
                    if(location.protocol!=='chrome-extension:'||location.hostname!=='jplgfhpmjnbigmhklmmbgecoobifkmpa')return;
                    const reply=(owned,supported)=>chrome.webview.postMessage({harborProxyProbe:'"+nonce+@"',owned:!!owned,supported:!!supported});
                    try{chrome.proxy.settings.get({},s=>{
                        if(chrome.runtime.lastError){reply(false,false);return;}
                        const owned=s.levelOfControl==='controlled_by_this_extension'&&['fixed_servers','pac_script'].includes(s.value?.mode);
                        const r=s.value?.rules,p=r?.singleProxy;
                        const expected=new Set(['account.proton.me','localhost','127.0.0.1','127.0.0.0/8','127.0.0.1:80','10.0.0.0/8','172.16.0.0/12','192.168.0.0/16','[::1]','<local>']);
                        const host=typeof p?.host==='string'?p.host.toLowerCase():'';
                        const safeHost=host&& !['localhost','127.0.0.1','::1','[::1]','0.0.0.0'].includes(host);
                        const supported=owned&&s.value.mode==='fixed_servers'&&p?.scheme==='https'&&safeHost&&Number.isInteger(p.port)&&p.port>0&&p.port<=65535&&
                            Object.keys(r).every(k=>['singleProxy','bypassList'].includes(k))&&Array.isArray(r.bypassList)&&r.bypassList.every(x=>typeof x==='string'&&expected.has(x.toLowerCase()));
                        if(!supported){reply(owned,false);return;}
                        try{chrome.privacy.network.webRTCIPHandlingPolicy.get({},p=>reply(owned,!chrome.runtime.lastError&&p.value==='disable_non_proxied_udp'));}
                        catch{reply(owned,false);}
                    });}catch{reply(false,false);}
                })()";
                await Bounded(core.ExecuteScriptAsync(script),5000,cancellation);
                return await Bounded(done.Task,5000,cancellation);
            }finally{core.WebMessageReceived-=handler;}
        }

        internal static async Task<IPAddress> Address(CoreWebView2 core,CancellationToken cancellation=default(CancellationToken))
        {
            string target=AddressTarget();
            var done=new TaskCompletionSource<IPAddress>();var loaded=new TaskCompletionSource<bool>();ulong? id=null;
            EventHandler<CoreWebView2NavigationStartingEventArgs> starting=(s,e)=>{if(e.Uri==target)id=e.NavigationId;else{e.Cancel=true;var error=new InvalidOperationException("The verifier redirected.");done.TrySetException(error);loaded.TrySetException(error);}};
            EventHandler<CoreWebView2NavigationCompletedEventArgs> completed=(s,e)=>{
                if(e.NavigationId!=id)return;
                if(e.IsSuccess)loaded.TrySetResult(true);
                else{var error=new InvalidOperationException("The verifier did not load.");done.TrySetException(error);loaded.TrySetException(error);}
            };
            EventHandler<CoreWebView2WebResourceResponseReceivedEventArgs> response=async(s,e)=>{
                if(e.Request.Uri!=target)return;
                try{
                    if(e.Response.StatusCode!=200)throw new InvalidOperationException("The verifier returned an unsuccessful response.");
                    using(var stream=await e.Response.GetContentAsync())done.TrySetResult(await ReadAddress(stream,cancellation));
                }catch(Exception error){done.TrySetException(error);}
            };
            core.NavigationStarting+=starting;core.NavigationCompleted+=completed;core.WebResourceResponseReceived+=response;
            try{core.NavigateWithWebResourceRequest(core.Environment.CreateWebResourceRequest(target,"GET",null,"Cache-Control: no-store\r\nPragma: no-cache"));return await Bounded(LoadedAddress(done.Task,loaded.Task),20000,cancellation);}
            finally{core.NavigationStarting-=starting;core.NavigationCompleted-=completed;core.WebResourceResponseReceived-=response;}
        }
        private static async Task<IPAddress> LoadedAddress(Task<IPAddress> content,Task loaded)
        {await Task.WhenAll(content,loaded);return await content;}
        internal static string AddressTarget()
        {return "https://api.ipify.org/?format=json&harbor_beta="+Guid.NewGuid().ToString("N");}

        internal static async Task<IPAddress> DirectAddress(Uri target,CancellationToken cancellation)
        {
            // A credential-free direct HTTPS control replaces the separate
            // browser engine. Only the fixed ipify URL is used by production.
            var request=(HttpWebRequest)WebRequest.Create(target);
            request.Proxy=null;request.AllowAutoRedirect=false;request.UseDefaultCredentials=false;
            request.Credentials=null;request.CookieContainer=null;request.KeepAlive=false;
            request.Timeout=15000;request.ReadWriteTimeout=15000;
            request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            request.Headers[HttpRequestHeader.CacheControl]="no-store";
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                deadline.CancelAfter(15000);
                using(deadline.Token.Register(()=>request.Abort()))
                using(var response=(HttpWebResponse)await request.GetResponseAsync())
                {
                    if(response.StatusCode!=HttpStatusCode.OK)throw new InvalidDataException("The direct verifier returned an unsuccessful response.");
                    using(var stream=response.GetResponseStream())return await ReadAddress(stream,deadline.Token);
                }
            }
        }
        private static async Task<IPAddress> ReadAddress(Stream stream,CancellationToken cancellation)
        {
            if(stream==null)throw new InvalidDataException("The verifier returned no content.");
            using(var memory=new MemoryStream())
            {
                var buffer=new byte[2048];int count;
                while((count=await stream.ReadAsync(buffer,0,buffer.Length,cancellation))>0)
                {if(memory.Length+count>16384)throw new InvalidDataException("The verifier response was too large.");memory.Write(buffer,0,count);}
                cancellation.ThrowIfCancellationRequested();
                var value=new JavaScriptSerializer{MaxJsonLength=16384,RecursionLimit=4}.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(memory.ToArray()));
                object address;IPAddress ip;
                if(value==null||!value.TryGetValue("ip",out address)||!IPAddress.TryParse(Convert.ToString(address),out ip)||ip.AddressFamily!=AddressFamily.InterNetwork)
                    throw new InvalidDataException("The verifier returned no valid IPv4 address.");
                return ip;
            }
        }
        internal static async Task<CoreWebView2Controller> CreateController(Task<CoreWebView2Controller> creation,CancellationToken cancellation)
        {try{return await Bounded(creation,20000,cancellation);}catch{_ = CloseLate(creation);throw;}}
        private static async Task CloseLate(Task<CoreWebView2Controller> creation){try{(await creation).Close();}catch{}}
        internal static async Task<T> Bounded<T>(Task<T> task,int timeout,CancellationToken cancellation)
        {
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation))try{
                if(await Task.WhenAny(task,Task.Delay(timeout,deadline.Token))!=task){Observe(task);cancellation.ThrowIfCancellationRequested();throw new TimeoutException("The connection check timed out.");}
                var result=await task;cancellation.ThrowIfCancellationRequested();return result;
            }finally{deadline.Cancel();}
        }
        private static void Observe(Task task){task.ContinueWith(t=>{var ignored=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);}
    }
}

