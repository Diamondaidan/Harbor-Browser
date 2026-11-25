using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using HarborBrowser;
using HarborBrowser.Services;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task Privacy054Checks(MainWindow main,string output)
    {
        string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","BrowserVpn");
        var options=new CoreWebView2EnvironmentOptions{AreBrowserExtensionsEnabled=true};
        var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(output,"RoutingWebView"),options);
        var owner=new WindowInteropHelper(main).Handle;
        var bootstrap=await environment.CreateCoreWebView2ControllerAsync(owner);
        bootstrap.IsVisible=false;var profile=bootstrap.CoreWebView2.Profile;
        CoreWebView2Controller controller=null;
        using(var routing=new BrowserVpnService(output,assets))
        using(var socks=new ProofSocksServer())
        using(var stun=new StunCounter())
        {
            try{
                var extension=await profile.AddBrowserExtensionAsync(Path.Combine(assets,"Router"));
                Check("privacy fixture loads Harbor's actual routing extension",extension.Id==BrowserVpnService.ExtensionId&&extension.IsEnabled);
                controller=await routing.Routing(environment,profile.ProfileName,owner,socks.Port,false,CancellationToken.None,true);
                var core=controller.CoreWebView2;
                string host="harbor-dns-proof-"+Guid.NewGuid().ToString("N")+".invalid";
                Check("browser receives the SOCKS fixture response",await TorNavigate(core,"http://"+host+"/proof"));
                Check("tested hostname reaches SOCKS as a domain rather than a resolved IP",socks.Domains.Contains(host));
                Check("SOCKS proof response is displayed",(await core.ExecuteScriptAsync("document.body.textContent")).Contains("Harbor SOCKS proof"));
                await TorNavigate(core,"https://"+BrowserVpnService.ControlHost+"/index.html");
                Check("WebRTC fixture uses a secure browser context",await core.ExecuteScriptAsync("isSecureContext")=="true");
                var protectedResult=await GatherIce(core,stun.Port);int protectedPackets=stun.Packets;
                Check("protected WebRTC gathering completes",protectedResult!=null);
                Check("protected gathering sends no UDP to the local STUN fixture",protectedPackets==0);
                await extension.RemoveAsync();
                var baseline=await GatherIce(core,stun.Port);await Task.Delay(200);
                Check("direct WebRTC positive control reaches the STUN fixture",stun.Packets>protectedPackets);
                Check("positive control completes without camera or microphone permission",baseline!=null);
                File.WriteAllText(Path.Combine(output,"privacy054-summary.json"),Json.Serialize(new{domainForwarded=socks.Domains.Contains(host),protectedStunPackets=protectedPackets,directStunPackets=stun.Packets-protectedPackets,protectedCandidateTypes=protectedResult,directCandidateTypes=baseline,scope="One HTTP SOCKS-domain request and one local STUN positive control; not a packet-level DNS, background traffic or anonymity audit."}));
            }finally{
                if(controller!=null)controller.Close();
                try{foreach(var extension in await profile.GetBrowserExtensionsAsync())if(extension.Id==BrowserVpnService.ExtensionId)await extension.RemoveAsync();}finally{bootstrap.Close();}
            }
        }
    }
    private static async Task<string> GatherIce(CoreWebView2 core,int port)
    {
        string nonce=Guid.NewGuid().ToString("N");var done=new TaskCompletionSource<string>();
        EventHandler<CoreWebView2WebMessageReceivedEventArgs> handler=(s,e)=>{
            try{var value=Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(e.WebMessageAsJson);if(value.ContainsKey("privacyProof")&&Convert.ToString(value["privacyProof"])==nonce){if(value.ContainsKey("error"))done.TrySetException(new Exception(Convert.ToString(value["error"])));else done.TrySetResult(Convert.ToString(value["types"]));}}catch(Exception ex){done.TrySetException(ex);}
        };
        core.WebMessageReceived+=handler;
        try{
            await core.ExecuteScriptAsync("(async()=>{let p;try{p=new RTCPeerConnection({iceServers:[{urls:'stun:127.0.0.1:"+port+"'}]});const types=new Set();p.onicecandidate=e=>{if(e.candidate){const t=e.candidate.candidate.match(/ typ (\\w+)/);if(t)types.add(t[1]);}};p.createDataChannel('proof');await p.setLocalDescription(await p.createOffer());await new Promise(r=>{const timer=setTimeout(r,3500);p.onicegatheringstatechange=()=>{if(p.iceGatheringState==='complete'){clearTimeout(timer);r();}};});chrome.webview.postMessage({privacyProof:'"+nonce+"',types:Array.from(types).sort().join(',')});}catch(e){chrome.webview.postMessage({privacyProof:'"+nonce+"',error:String(e)});}finally{if(p)p.close();}})()");
            if(await Task.WhenAny(done.Task,Task.Delay(10000))!=done.Task)throw new TimeoutException("WebRTC fixture timed out.");
            return await done.Task;
        }finally{core.WebMessageReceived-=handler;}
    }
    private sealed class StunCounter:IDisposable
    {
        private readonly UdpClient socket=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
        private int packets;public int Packets{get{return Volatile.Read(ref packets);}}public int Port{get{return ((IPEndPoint)socket.Client.LocalEndPoint).Port;}}
        public StunCounter(){_ = Receive();}
        private async Task Receive(){try{while(true){await socket.ReceiveAsync();Interlocked.Increment(ref packets);}}catch(ObjectDisposedException){}catch(SocketException){}}
        public void Dispose(){socket.Close();}
    }
    private sealed class ProofSocksServer:IDisposable
    {
        private readonly TcpListener listener=new TcpListener(IPAddress.Loopback,0);
        public readonly ConcurrentQueue<string> Domains=new ConcurrentQueue<string>();
        public int Port{get{return ((IPEndPoint)listener.LocalEndpoint).Port;}}
        public ProofSocksServer(){listener.Start();_ = Accept();}
        private async Task Accept(){try{while(true){var client=await listener.AcceptTcpClientAsync();_ = Task.Run(()=>Serve(client));}}catch(ObjectDisposedException){}catch(SocketException){}catch(InvalidOperationException){}}
        private static byte[] Read(Stream stream,int count){var result=new byte[count];int at=0;while(at<count){int got=stream.Read(result,at,count-at);if(got==0)throw new EndOfStreamException();at+=got;}return result;}
        private void Serve(TcpClient client)
        {
            using(client)try{
                client.ReceiveTimeout=5000;client.SendTimeout=5000;var stream=client.GetStream();
                var greeting=Read(stream,2);if(greeting[0]!=5)return;Read(stream,greeting[1]);stream.Write(new byte[]{5,0},0,2);
                var request=Read(stream,4);if(request[0]!=5||request[1]!=1)return;
                string host;if(request[3]==3){host=Encoding.ASCII.GetString(Read(stream,Read(stream,1)[0]));Domains.Enqueue(host);}else if(request[3]==1)host=new IPAddress(Read(stream,4)).ToString();else if(request[3]==4)host=new IPAddress(Read(stream,16)).ToString();else return;
                Read(stream,2);
                if(!host.StartsWith("harbor-dns-proof-",StringComparison.Ordinal)||!host.EndsWith(".invalid",StringComparison.Ordinal))return;
                stream.Write(new byte[]{5,0,0,1,127,0,0,1,0,0},0,10);
                var headers=new StringBuilder();while(headers.Length<8192&&!headers.ToString().EndsWith("\r\n\r\n",StringComparison.Ordinal)){int next=stream.ReadByte();if(next<0)return;headers.Append((char)next);}
                byte[] body=Encoding.UTF8.GetBytes("<!doctype html><html><body>Harbor SOCKS proof</body></html>");
                byte[] head=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: "+body.Length+"\r\n\r\n");
                stream.Write(head,0,head.Length);stream.Write(body,0,body.Length);
            }catch(IOException){}catch(SocketException){}
        }
        public void Dispose(){listener.Stop();}
    }
}
