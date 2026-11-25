using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

internal sealed class DownloadFixtureServer : IDisposable
{
    private readonly TcpListener listener=new TcpListener(IPAddress.Loopback,0);
    private volatile bool stopped;
    private readonly ConcurrentDictionary<TcpClient,byte> clients=new ConcurrentDictionary<TcpClient,byte>();
    public byte[] Payload {get;private set;}
    public string Url {get;private set;}
    public int RangeRequests;
    public int TransferRequests;
    public DownloadFixtureServer()
    {
        using(var stream=new MemoryStream())
        {
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
            using(var entry=zip.CreateEntry("fixture.bin",CompressionLevel.NoCompression).Open())
            {var bytes=new byte[2*1024*1024];new Random(731).NextBytes(bytes);entry.Write(bytes,0,bytes.Length);}
            Payload=stream.ToArray();
        }
        listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;
        Task.Run((Func<Task>)Accept);
    }
    private async Task Accept()
    {
        while(!stopped)
        {
            try{var client=await listener.AcceptTcpClientAsync().ConfigureAwait(false);clients.TryAdd(client,0);_ = Task.Run(()=>Serve(client));}
            catch(ObjectDisposedException){break;}catch(SocketException){if(stopped)break;throw;}
        }
    }
    private async Task Serve(TcpClient client)
    {
        using(client)
        try
        {
            using(var stream=client.GetStream())
            using(var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true))
            {
                string first=await reader.ReadLineAsync().ConfigureAwait(false);if(first==null)return;
                long start=0,end=Payload.Length-1;bool ranged=false;string line;
                while(!string.IsNullOrEmpty(line=await reader.ReadLineAsync().ConfigureAwait(false)))
                    if(line.StartsWith("Range: bytes=",StringComparison.OrdinalIgnoreCase)){var range=line.Substring(13).Split('-');long.TryParse(range[0],out start);if(range.Length>1&&range[1].Length>0)long.TryParse(range[1],out end);ranged=true;Interlocked.Increment(ref RangeRequests);Console.WriteLine("INFO "+line);}
                if(first.Contains("favicon.ico")){var missing=Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");await stream.WriteAsync(missing,0,missing.Length).ConfigureAwait(false);return;}
                Interlocked.Increment(ref TransferRequests);
                Console.WriteLine("INFO fixture request: "+first+"; start="+start);
                start=Math.Max(0,Math.Min(start,Payload.Length-1));
                end=Math.Max(start,Math.Min(end,Payload.Length-1));
                string header="HTTP/1.1 "+(ranged?"206 Partial Content":"200 OK")+"\r\nContent-Type: application/zip\r\nContent-Disposition: attachment; filename=\"harbor-download\"\r\nContent-Length: "+(end-start+1)+"\r\nAccept-Ranges: bytes\r\nETag: \"harbor-fixture\"\r\nLast-Modified: Thu, 12 Nov 2026 18:00:00 GMT\r\nConnection: close\r\n";
                if(ranged)header+="Content-Range: bytes "+start+"-"+end+"/"+Payload.Length+"\r\n";
                var encoded=Encoding.ASCII.GetBytes(header+"\r\n");await stream.WriteAsync(encoded,0,encoded.Length).ConfigureAwait(false);
                for(int offset=(int)start;offset<=end&&!stopped;offset+=32768)
                {await stream.WriteAsync(Payload,offset,(int)Math.Min(32768,end-offset+1)).ConfigureAwait(false);await Task.Delay(40).ConfigureAwait(false);}
                Console.WriteLine("INFO fixture sent "+(end-start+1)+" bytes");
            }
        }
        catch(IOException ex){Console.WriteLine("INFO fixture connection closed: "+ex.Message);}catch(SocketException){}catch(ObjectDisposedException){}
        finally{byte ignored;clients.TryRemove(client,out ignored);}
    }
    public void DisconnectTransfers(){foreach(var client in clients.Keys)client.Close();}
    public void Dispose(){stopped=true;listener.Stop();DisconnectTransfers();}
}
