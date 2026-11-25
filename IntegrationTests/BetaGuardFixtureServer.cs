using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

// Loopback-only request counter. No provider/account URL or authentication data
// is used. Counts are taken at the server, independently of the host's UI state.
internal sealed class BetaGuardFixtureServer : IDisposable
{
    private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
    private readonly ConcurrentDictionary<string, int> counts = new ConcurrentDictionary<string, int>();
    private bool closed;
    private readonly RSA tlsKey;
    private readonly X509Certificate2 certificate;
    private readonly bool proxyMode;
    internal string AddressReply = "198.51.100.2";
    internal bool AddressAtRoot;
    internal bool RequireProxyAuthentication;
    internal string ProxyPassword = "canary";
    internal bool DiagnosticsVerified;
    internal string AddressContentType="application/json", AddressBody, AddressRedirect;
    internal int AddressStatus=200, AddressDelayMilliseconds;
    internal string BaseUrl { get; private set; }
    internal string CertificateSpkiHash
    {
        get
        {
            // RSA SubjectPublicKeyInfo for a fixture-only Chromium certificate
            // pin. The key/certificate never enters a system trust store.
            byte[] algorithm={0x30,0x0d,0x06,0x09,0x2a,0x86,0x48,0x86,0xf7,0x0d,0x01,0x01,0x01,0x05,0x00};
            byte[] bits=Der(0x03,new byte[]{0}.Concat(certificate.GetPublicKey()).ToArray());
            using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(Der(0x30,algorithm.Concat(bits).ToArray())));
        }
    }
    private static byte[] Der(byte tag,byte[] value)
    {
        byte[] length=value.Length<128?new[]{(byte)value.Length}:value.Length<256?new[]{(byte)0x81,(byte)value.Length}:new[]{(byte)0x82,(byte)(value.Length>>8),(byte)value.Length};
        return new[]{tag}.Concat(length).Concat(value).ToArray();
    }
    internal BetaGuardFixtureServer(bool tls=false,bool proxy=false)
    {
        proxyMode=proxy;
        if(tls)
        {
            tlsKey=RSA.Create(2048);
            var request=new CertificateRequest("CN=Harbor owned loopback fixture",tlsKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            var names=new SubjectAlternativeNameBuilder();names.AddIpAddress(IPAddress.Loopback);request.CertificateExtensions.Add(names.Build());
            // Legacy Framework SChannel needs an imported key container; the
            // temporary key is deleted on Dispose and no trust store is used.
            using(var generated=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1),DateTimeOffset.UtcNow.AddHours(2)))
                certificate=new X509Certificate2(generated.Export(X509ContentType.Pfx),string.Empty,X509KeyStorageFlags.Exportable);
        }
        listener.Start(); BaseUrl = (tls?"https":"http")+"://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/";
        _ = Accept();
    }
    internal string Url(string key) { return BaseUrl + key.TrimStart('/'); }
    internal int Count(string key) { int value; return counts.TryGetValue("/" + key.TrimStart('/'), out value) ? value : 0; }
    internal Dictionary<string, int> Snapshot(string prefix)
    { return counts.Where(x => x.Key.StartsWith("/" + prefix.TrimStart('/'), StringComparison.Ordinal)).ToDictionary(x => x.Key, x => x.Value); }
    private async Task Accept()
    {
        try { while (!closed) { var client = await listener.AcceptTcpClientAsync(); _ = Task.Run(() => Serve(client)); } }
        catch (ObjectDisposedException) { } catch (SocketException) { } catch (InvalidOperationException) { }
    }
    private void Serve(TcpClient client)
    {
        Stream stream=null;
        using (client) try
        {
            client.ReceiveTimeout = 5000; client.SendTimeout = 5000;
            stream = client.GetStream();
            if(certificate!=null){var secure=new SslStream(stream,false);secure.AuthenticateAsServer(certificate,false,SslProtocols.Tls12,false);stream=secure;}
            var header = new StringBuilder();
            while (header.Length < 16384 && !header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            { int next = stream.ReadByte(); if (next < 0) return; header.Append((char)next); }
            var first = header.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ');
            if (first.Length < 2) return;
            if(proxyMode&&first[0]=="CONNECT")
            {
                string destination=first[1];
                counts.AddOrUpdate("/CONNECT/"+destination,1,(key,count)=>count+1);
                if(RequireProxyAuthentication && header.ToString().IndexOf("Proxy-Authorization: Basic "+Convert.ToBase64String(Encoding.ASCII.GetBytes("owned:"+ProxyPassword)),StringComparison.OrdinalIgnoreCase)<0)
                {
                    counts.AddOrUpdate("/proxy-auth-challenge",1,(key,count)=>count+1);
                    byte[] challenge=Encoding.ASCII.GetBytes("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"Harbor owned fixture\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    stream.Write(challenge,0,challenge.Length);return;
                }
                if(destination=="unavailable-ipv6.owned.invalid:443")
                {
                    byte[] failure=Encoding.ASCII.GetBytes("HTTP/1.1 502 Owned target unavailable\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    stream.Write(failure,0,failure.Length);return;
                }
                if(destination!="api.ipify.org:443"&&destination!="ordinary.owned.invalid:443")return;
                byte[] connected=Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");stream.Write(connected,0,connected.Length);
                var inner=new SslStream(stream,false);inner.AuthenticateAsServer(certificate,false,SslProtocols.Tls12,false);stream=inner;
                header.Clear();
                while(header.Length<16384&&!header.ToString().EndsWith("\r\n\r\n",StringComparison.Ordinal))
                {int next=stream.ReadByte();if(next<0)return;header.Append((char)next);}
                first=header.ToString().Split(new[]{"\r\n"},StringSplitOptions.None)[0].Split(' ');
                if(first.Length<2)return;
            }
            string path = first[1].Split('?')[0];
            counts.AddOrUpdate(path, 1, (key, count) => count + 1);
            if(path=="/route-hang")Task.Delay(2000).GetAwaiter().GetResult();
            if(header.ToString().IndexOf("Upgrade: websocket",StringComparison.OrdinalIgnoreCase)>=0)
            {
                string key=header.ToString().Split(new[]{"\r\n"},StringSplitOptions.None).First(l=>l.StartsWith("Sec-WebSocket-Key:",StringComparison.OrdinalIgnoreCase)).Substring(18).Trim();
                string accept;using(var sha=SHA1.Create())accept=Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                byte[] upgrade=Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "+accept+"\r\n\r\n");stream.Write(upgrade,0,upgrade.Length);
                int opcode=stream.ReadByte(),size=stream.ReadByte();if(opcode<0||size<0||(size&127)>125)return;
                int length=size&127;var mask=new byte[4];if((size&128)!=0)for(int i=0;i<4;i++){int b=stream.ReadByte();if(b<0)return;mask[i]=(byte)b;}
                var payload=new byte[length];for(int i=0;i<length;i++){int b=stream.ReadByte();if(b<0)return;payload[i]=(byte)(b^mask[i%4]);}
                if((opcode&15)==1){stream.WriteByte(0x81);stream.WriteByte((byte)length);stream.Write(payload,0,length);}
                return;
            }
            string type = "text/plain; charset=utf-8", extra = "", body = "Harbor beta guard proof";
            if(path=="/route-address"||((proxyMode||AddressAtRoot)&&path=="/")){type="application/json";body="{\"ip\":\""+AddressReply+"\"}";}
            if (path.EndsWith("/page", StringComparison.Ordinal)) { type = "text/html; charset=utf-8"; body = Page; }
            else if (path == "/worker.js") { type = "application/javascript"; body = Worker; }
            else if (path == "/sw.js") { type = "application/javascript"; body = ServiceWorker; extra = "Service-Worker-Allowed: /\r\n"; }
            else if (path.EndsWith("/download", StringComparison.Ordinal))
                extra = "Content-Disposition: attachment; filename=harbor-beta-guard-proof.txt\r\n";
            string status = "200 OK";
            if(path=="/route-address"||((proxyMode||AddressAtRoot)&&path=="/"))
            {
                if(AddressDelayMilliseconds>0)Task.Delay(AddressDelayMilliseconds).GetAwaiter().GetResult();
                type=AddressContentType;body=AddressBody??body;status=AddressStatus+" Owned response";
                if(AddressRedirect!=null){status="302 Found";extra="Location: "+AddressRedirect+"\r\n";}
            }
            if (path == "/diagnostics/page") { type = "text/html"; body = "<!doctype html><title>Owned diagnostics page</title><link rel='stylesheet' href='/diagnostics/style.css?private-token=canary#fragment'><h1 id='owned'>Original page stays visible</h1>"; }
            if (path == "/diagnostics/refused") { type = "text/html"; body = "<!doctype html><title>Owned refusal</title><link rel='stylesheet' href='/diagnostics/refused.css'><h1>Refused resource</h1>"; }
            if (path == "/diagnostics/refused.css") { type = "text/html"; body = "Refused by fixture"; status = "403 Forbidden"; }
            if (path == "/diagnostics/style.css")
            {
                if (DiagnosticsVerified) { type = "text/css"; body = "h1 {color:rgb(20, 80, 140)}"; }
                else { type = "text/html"; status = "403 Forbidden"; extra = "cf-mitigated: challenge\r\n"; body = "<!doctype html><title>Owned verification fixture</title><p>Synthetic site check; no real CAPTCHA.</p>"; }
            }
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            string response = "HTTP/1.1 " + status + "\r\nContent-Type: " + type + "\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *\r\n" + extra + "\r\n";
            byte[] head = Encoding.ASCII.GetBytes(response); stream.Write(head, 0, head.Length); stream.Write(bytes, 0, bytes.Length);
        }
        catch (IOException) { } catch (SocketException) { } catch (ObjectDisposedException) { } catch(AuthenticationException){}
        finally{try{stream?.Dispose();}catch(IOException){}}
    }
    private const string Page = @"<!doctype html><html><head><title>Harbor beta request fixture</title></head><body>
<p>Owned local request fixture</p><script>
window.betaFixture={ready:false,workerReady:false,swReady:false,completed:0};
window.betaWorker=new Worker('/worker.js');
betaWorker.onmessage=e=>{if(e.data.ready)betaFixture.workerReady=true;else betaFixture.completed++;};
(async()=>{try{window.betaRegistration=await navigator.serviceWorker.register('/sw.js',{scope:'/',updateViaCache:'none'});await navigator.serviceWorker.ready;betaFixture.swReady=!!betaRegistration.active;}catch{}betaFixture.ready=true;})();
window.betaRequests=(prefix,delay=0)=>{
  const perform=()=>{
    fetch('/'+prefix+'/fetch',{cache:'no-store'}).catch(()=>{}).finally(()=>betaFixture.completed++);
    const frame=document.createElement('iframe');frame.src='/'+prefix+'/frame';document.body.append(frame);
    const image=new Image();image.src='/'+prefix+'/image';document.body.append(image);
    betaWorker.postMessage({url:'/'+prefix+'/worker',delay:0});
    try{const socket=new WebSocket(location.origin.replace('http:','ws:')+'/'+prefix+'/websocket');socket.onerror=()=>socket.close();}catch{}
  };
  setTimeout(perform,delay);
  if(betaRegistration?.active){const channel=new MessageChannel();channel.port1.onmessage=e=>{if(e.data?.scheduled)betaFixture.swScheduledPrefix=prefix;channel.port1.close();};betaRegistration.active.postMessage({url:'/'+prefix+'/service-worker',delay},[channel.port2]);}
};
window.betaDownload=prefix=>{const a=document.createElement('a');a.href='/'+prefix+'/download';a.download='harbor-beta-guard-proof.txt';document.body.append(a);a.click();a.remove();};
</script></body></html>";
    private const string Worker = @"self.postMessage({ready:true});self.onmessage=e=>{if(!e.data?.url)return;setTimeout(()=>{fetch(e.data.url,{cache:'no-store'}).catch(()=>{}).finally(()=>self.postMessage({done:true}));},Math.min(3000,e.data.delay||0));};";
    private const string ServiceWorker = @"self.addEventListener('install',e=>e.waitUntil(self.skipWaiting()));self.addEventListener('activate',e=>e.waitUntil(self.clients.claim()));self.addEventListener('message',e=>{if(!e.data?.url)return;e.waitUntil((async()=>{if(e.ports[0]){e.ports[0].postMessage({scheduled:true});e.ports[0].close();}await new Promise(r=>setTimeout(r,Math.min(3000,e.data.delay||0)));try{await fetch(e.data.url,{cache:'no-store'});}catch{}})());});";
    public void Dispose() { closed = true; listener.Stop();certificate?.Dispose();tlsKey?.Dispose(); }
}
