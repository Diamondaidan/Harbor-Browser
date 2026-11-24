using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

internal sealed class UnloadFixtureServer : IDisposable
{
    private readonly TcpListener listener = new TcpListener(IPAddress.Loopback,0);
    private bool stopped;
    public string Url { get; private set; }
    public int Posts;
    public int Requests;
    public UnloadFixtureServer()
    {
        listener.Start(); Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/posted";
        Task.Run(async()=>{while(!stopped){try{var client=await listener.AcceptTcpClientAsync();_ = Serve(client);}catch(ObjectDisposedException){break;}catch(SocketException){if(stopped)break;throw;}}});
    }
    private async Task Serve(TcpClient client)
    {
        using(client)try
        {
            using(var stream=client.GetStream())
            using(var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true))
            {
                string first=await reader.ReadLineAsync();
                System.Threading.Interlocked.Increment(ref Requests);
                if(first!=null&&first.StartsWith("POST "))System.Threading.Interlocked.Increment(ref Posts);
                for(int i=0;i<128;i++){string line=await reader.ReadLineAsync();if(string.IsNullOrEmpty(line))break;}
                byte[] body=Encoding.UTF8.GetBytes("<!doctype html><link rel=icon href='data:,'><title>Owned POST result</title><p>Posted fixture</p><script>window.fixtureToken='posted';</script>");
                byte[] head=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(head,0,head.Length);await stream.WriteAsync(body,0,body.Length);
            }
        }catch(IOException){}catch(ObjectDisposedException){}
    }
    public void Dispose(){stopped=true;listener.Stop();}
}
