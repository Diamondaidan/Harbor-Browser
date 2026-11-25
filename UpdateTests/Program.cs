using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HarborBrowser.Updates;
using HarborBrowser.Views;

internal static class Program
{
    private static int _checks;private static string _root;private static RSACryptoServiceProvider _key;private static UpdateTrust _trust;
    private static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    private static readonly byte[] Bytes=Enumerable.Range(0,8192).Select(i=>(byte)(i%251)).ToArray();
    private static void Check(string name,bool value){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);_checks++;}
    private static void Reject(string name,Action action){try{action();}catch(Exception ex)when(ex is InvalidDataException||ex is IOException||ex is InvalidOperationException||ex is System.Security.Cryptography.CryptographicException){Check(name,true);return;}throw new Exception("Accepted: "+name);}
    private static async Task RejectAsync(string name,Func<Task> action){try{await action();}catch(Exception ex)when(ex is InvalidDataException||ex is IOException||ex is InvalidOperationException||ex is OperationCanceledException){Check(name,true);return;}throw new Exception("Accepted: "+name);}
    private static string Owned(string name){string p=Path.Combine(_root,name);Directory.CreateDirectory(p);return p;}
    private static UpdatePackage Package(string version){return new UpdatePackage{Version=version,Url=_trust.PackageUrl(version),Sha256=UpdateTrust.Hash(Bytes),Bytes=Bytes.Length,DataSchema=9};}
    private static string Signed(Action<UpdateManifest> change=null,RSACryptoServiceProvider signer=null)
    {
        var now=DateTime.UtcNow;var m=new UpdateManifest{Schema=1,Product="HarborBrowser",Channel="stable",Architecture="x64",Sequence=1,IssuedUtc=now.AddMinutes(-1).ToString("o"),ExpiresUtc=now.AddDays(7).ToString("o"),Notes="Owned release fixture",Latest=Package("0.6.1"),Previous=Package("0.6.0"),RollbackFrom="0.6.1"};change?.Invoke(m);
        byte[] data=Encoding.UTF8.GetBytes(Json.Serialize(m));return Json.Serialize(new UpdateEnvelope{KeyId="fixture",Payload=Convert.ToBase64String(data),Signature=Convert.ToBase64String((signer??_key).SignData(data,CryptoConfig.MapNameToOID("SHA256")))});
    }
    private static VerifiedUpdate Verify(string e){return _trust.Verify(e,DateTime.UtcNow);}
    [STAThread]private static int Main(string[] args)
    {
        try
        {
            if(args.Length>=3&&args[0]=="--verify-record")
            {
                var prepared=UpdateTrustRoot.Create().Verify(File.ReadAllText(args[1]),DateTime.UtcNow);
                UpdateService.VerifyPackage(args[2],prepared.Manifest.Latest);Check("prepared release record verifies against the app's production trust root",prepared.Manifest.Latest.Version==HarborBrowser.Services.AppVersion.Number);
                if(args.Length>3){UpdateService.VerifyPackage(args[3],prepared.Manifest.Previous);Check("prepared rollback package matches the same signed record",true);}
                Console.WriteLine("ALL PASSED: "+_checks+" production release-record roundtrip checks.");return 0;
            }
            _root=Path.GetFullPath(args[0]);if(Directory.Exists(_root))throw new IOException("Use a new owned result directory.");Directory.CreateDirectory(_root);
            _key=new RSACryptoServiceProvider(2048){PersistKeyInCsp=false};_trust=new UpdateTrust("fixture-owner","fixture-repo","fixture",_key.ToXmlString(false));
            Metadata();Downloads().GetAwaiter().GetResult();Recovery();Transport().GetAwaiter().GetResult();NativeHandoff().GetAwaiter().GetResult();Layout();
            Console.WriteLine("ALL PASSED: "+_checks+" update signature, download, recovery, transport and layout checks. Synthetic owned profiles only.");return 0;
        }
        catch(Exception ex){Console.WriteLine("FAIL "+ex);return 1;}
        finally{_key?.Dispose();}
    }
    private static void Metadata()
    {
        var v=Verify(Signed());Check("signed x64 stable release accepted",v.Manifest.Latest.Version=="0.6.1");
        var e=Json.Deserialize<UpdateEnvelope>(v.Envelope);e.Signature=Convert.ToBase64String(new byte[256]);Reject("invalid release signature rejected",()=>Verify(Json.Serialize(e)));
        e=Json.Deserialize<UpdateEnvelope>(v.Envelope);e.KeyId="other";Reject("untrusted key identity rejected",()=>Verify(Json.Serialize(e)));
        using(var other=new RSACryptoServiceProvider(2048){PersistKeyInCsp=false})Reject("signature from another key rejected",()=>Verify(Signed(null,other)));
        e=Json.Deserialize<UpdateEnvelope>(v.Envelope);var b=Convert.FromBase64String(e.Payload);b[15]^=1;e.Payload=Convert.ToBase64String(b);Reject("payload tampering rejected",()=>Verify(Json.Serialize(e)));
        Reject("oversized record rejected",()=>Verify(new string(' ',131073)));
        foreach(var bad in new[]{"0.6","0.6.1.0","v0.6.1","0.6.1-beta","00.6.1","-1.6.1"})Reject("version syntax rejected "+bad,()=>UpdateTrust.VersionOf(bad));
        Reject("foreign product rejected",()=>Verify(Signed(m=>m.Product="Other")));
        Reject("prerelease channel rejected",()=>Verify(Signed(m=>m.Channel="beta")));
        Reject("other architecture rejected",()=>Verify(Signed(m=>m.Architecture="arm64")));
        Reject("expired release rejected",()=>Verify(Signed(m=>{m.IssuedUtc=DateTime.UtcNow.AddDays(-2).ToString("o");m.ExpiresUtc=DateTime.UtcNow.AddDays(-1).ToString("o");})));
        Reject("future-issued record rejected",()=>Verify(Signed(m=>m.IssuedUtc=DateTime.UtcNow.AddDays(1).ToString("o"))));
        Reject("excessive validity period rejected",()=>Verify(Signed(m=>m.ExpiresUtc=DateTime.UtcNow.AddDays(90).ToString("o"))));
        Reject("foreign package repository rejected",()=>Verify(Signed(m=>m.Latest.Url="https://github.com/other/repo/file.exe")));
        Reject("HTTP package rejected",()=>Verify(Signed(m=>m.Latest.Url=m.Latest.Url.Replace("https:","http:"))));
        Reject("oversized signed package rejected",()=>Verify(Signed(m=>m.Latest.Bytes=1073741825)));
        Reject("invalid digest rejected",()=>Verify(Signed(m=>m.Latest.Sha256="123")));
        Reject("incompatible data schema rejected",()=>Verify(Signed(m=>m.Previous.DataSchema=8)));
        Reject("unapproved rollback origin rejected",()=>Verify(Signed(m=>m.RollbackFrom="0.5.7")));
        string state=Path.Combine(Owned("history"),"state.json");UpdateState.Accept(state,v,DateTime.UtcNow);UpdateState.Accept(state,v,DateTime.UtcNow);Check("identical signed record can be rechecked",UpdateState.Read(state).Sequence==1);
        var newer=Verify(Signed(m=>m.Sequence=2));UpdateState.Accept(state,newer,DateTime.UtcNow);
        Reject("metadata sequence rollback rejected",()=>UpdateState.Accept(state,v,DateTime.UtcNow));
        Reject("same sequence changed payload rejected",()=>UpdateState.Accept(state,Verify(Signed(m=>{m.Sequence=2;m.Notes="changed";})),DateTime.UtcNow));
        Reject("signed version rollback rejected",()=>UpdateState.Accept(state,Verify(Signed(m=>{m.Sequence=3;m.Latest=Package("0.6.0");m.Previous=null;})),DateTime.UtcNow));
        Reject("backward PC clock rejected",()=>UpdateState.Accept(state,newer,DateTime.UtcNow.AddHours(-1)));
        File.WriteAllText(state,"invalid");Reject("damaged anti-rollback history fails closed",()=>UpdateState.Read(state));
    }
    private sealed class QueueTransport : IUpdateTransport
    {
        internal readonly Queue<Func<long,string,UpdateResponse>> Responses=new Queue<Func<long,string,UpdateResponse>>();internal int Calls;internal long LastOffset;
        public Task<UpdateResponse> Open(Uri u,long offset,string etag,CancellationToken c){c.ThrowIfCancellationRequested();Calls++;LastOffset=offset;return Task.FromResult(Responses.Dequeue()(offset,etag));}
    }
    private sealed class BrokenStream : MemoryStream
    {
        private readonly Action _interrupt;internal BrokenStream(byte[] bytes,Action interrupt=null):base(bytes){_interrupt=interrupt;}
        public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken c)
        {if(Position>=256){_interrupt?.Invoke();c.ThrowIfCancellationRequested();throw new IOException("Owned interrupted stream");}return base.ReadAsync(buffer,offset,Math.Min(count,256-(int)Position),c);}
    }
    private sealed class GateStream : MemoryStream
    {
        private readonly Action _flip;internal int Reads;internal GateStream(Action flip):base(Bytes){_flip=flip;}
        public override async Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken cancel){Reads++;int n=await base.ReadAsync(buffer,offset,Math.Min(256,count),cancel);_flip();return n;}
    }
    private static UpdateResponse Reply(byte[] b,long offset=0,string etag="\"one\"")
    {return new UpdateResponse{Status=offset>0?206:200,ETag=etag,Length=b.Length-offset,ContentRange=offset>0?"bytes "+offset+"-"+(b.Length-1)+"/"+b.Length:null,Body=new MemoryStream(b.Skip((int)offset).ToArray())};}
    private static QueueTransport WithInterruption(string etag="\"one\"")
    {var q=new QueueTransport();q.Responses.Enqueue((o,e)=>new UpdateResponse{Status=200,ETag=etag,Length=Bytes.Length,Body=new BrokenStream(Bytes)});return q;}
    private static async Task Downloads()
    {
        var v=Verify(Signed());var p=v.Manifest.Latest;
        var q=new QueueTransport();q.Responses.Enqueue((o,e)=>Reply(Encoding.UTF8.GetBytes(v.Envelope)));
        string root=Owned("check");var service=new UpdateService(root,()=>true,q,_trust);var got=await service.Check(CancellationToken.None);Check("feed checked before caching",got.PayloadHash==v.PayloadHash);Check("cached record revalidates signed identity",service.Cached().PayloadHash==v.PayloadHash);
        q.Responses.Enqueue((o,e)=>Reply(Encoding.UTF8.GetBytes(Signed(m=>m.Sequence=0))));await RejectAsync("bad feed cannot replace cached good record",async()=>await service.Check(CancellationToken.None));Check("good cached release retained after bad feed",service.Cached().PayloadHash==v.PayloadHash);
        q=new QueueTransport();var blocked=new UpdateService(Owned("blocked"),()=>false,q,_trust);await RejectAsync("protection gate blocks update check",async()=>await blocked.Check(CancellationToken.None));Check("blocked check sends no request",q.Calls==0);
        q=new QueueTransport();q.Responses.Enqueue((o,e)=>Reply(Bytes));service=new UpdateService(Owned("fresh"),()=>true,q,_trust);string file=await service.Download(v,p,null,CancellationToken.None);Check("complete download hash verified",UpdateTrust.HashFile(file)==p.Sha256);Check("content-addressed exe cache filename",Path.GetFileName(file)==p.Sha256+".exe");
        await service.Download(v,p,null,CancellationToken.None);Check("verified cached package reused without network",q.Calls==1);
        File.WriteAllBytes(file,new byte[1024]);await RejectAsync("tampered cached package refused",async()=>await service.Download(v,p,null,CancellationToken.None));Check("tampered cached exe removed for retry",!File.Exists(file));
        q=WithInterruption();root=Owned("resume");service=new UpdateService(root,()=>true,q,_trust);await RejectAsync("interrupted transfer retained as partial",async()=>await service.Download(v,p,null,CancellationToken.None));Check("only partial file exists after interruption",File.Exists(Path.Combine(service.Downloads,p.Sha256+".part"))&&!File.Exists(Path.Combine(service.Downloads,p.Sha256+".exe")));
        q.Responses.Enqueue((o,e)=>{Check("resume sends exact offset",o==256);Check("resume binds strong ETag",e=="\"one\"");return Reply(Bytes,o);});service=new UpdateService(root,()=>true,q,_trust);file=await service.Download(v,p,null,CancellationToken.None);Check("resume survives service recreation",UpdateTrust.HashFile(file)==p.Sha256);
        q=WithInterruption();service=new UpdateService(Owned("range-ignored"),()=>true,q,_trust);await RejectAsync("range-ignore fixture interrupted",async()=>await service.Download(v,p,null,CancellationToken.None));q.Responses.Enqueue((o,e)=>Reply(Bytes));file=await service.Download(v,p,null,CancellationToken.None);Check("HTTP 200 restarts rather than appends",new FileInfo(file).Length==Bytes.Length&&UpdateTrust.HashFile(file)==p.Sha256);
        q=WithInterruption("W/\"weak\"");service=new UpdateService(Owned("weak-etag"),()=>true,q,_trust);await RejectAsync("weak-ETag fixture interrupted",async()=>await service.Download(v,p,null,CancellationToken.None));q.Responses.Enqueue((o,e)=>{Check("weak ETag never resumes",o==0&&e==null);return Reply(Bytes);});await service.Download(v,p,null,CancellationToken.None);
        q=WithInterruption();service=new UpdateService(Owned("bad-range"),()=>true,q,_trust);await RejectAsync("bad-range fixture interrupted",async()=>await service.Download(v,p,null,CancellationToken.None));q.Responses.Enqueue((o,e)=>{var r=Reply(Bytes,o);r.ContentRange="bytes 255-8191/8192";return r;});await RejectAsync("wrong resumed range rejected",async()=>await service.Download(v,p,null,CancellationToken.None));Check("bad range did not append bytes",new FileInfo(Path.Combine(service.Downloads,p.Sha256+".part")).Length==256);
        q=WithInterruption();service=new UpdateService(Owned("changed-etag"),()=>true,q,_trust);await RejectAsync("changed-ETag fixture interrupted",async()=>await service.Download(v,p,null,CancellationToken.None));q.Responses.Enqueue((o,e)=>Reply(Bytes,o,"\"two\""));await RejectAsync("changed resumed identity rejected",async()=>await service.Download(v,p,null,CancellationToken.None));
        var corrupt=Bytes.ToArray();corrupt[300]^=1;q=new QueueTransport();q.Responses.Enqueue((o,e)=>Reply(corrupt));service=new UpdateService(Owned("bad-hash"),()=>true,q,_trust);await RejectAsync("mismatching download digest rejected",async()=>await service.Download(v,p,null,CancellationToken.None));Check("bad digest leaves no executable or partial",!File.Exists(Path.Combine(service.Downloads,p.Sha256+".exe"))&&!File.Exists(Path.Combine(service.Downloads,p.Sha256+".part")));
        q=new QueueTransport();q.Responses.Enqueue((o,e)=>{var r=Reply(Bytes.Concat(new byte[]{1}).ToArray());r.Length=Bytes.Length;return r;});service=new UpdateService(Owned("oversized-body"),()=>true,q,_trust);await RejectAsync("oversized response body bounded",async()=>await service.Download(v,p,null,CancellationToken.None));
        q=new QueueTransport();q.Responses.Enqueue((o,e)=>{var r=Reply(Bytes);r.Encoding="gzip";return r;});service=new UpdateService(Owned("encoding"),()=>true,q,_trust);await RejectAsync("encoded package response refused",async()=>await service.Download(v,p,null,CancellationToken.None));
        using(var cancel=new CancellationTokenSource())
        {
            q=new QueueTransport();q.Responses.Enqueue((o,e)=>new UpdateResponse{Status=200,ETag="\"one\"",Length=Bytes.Length,Body=new BrokenStream(Bytes,()=>cancel.Cancel())});service=new UpdateService(Owned("cancel"),()=>true,q,_trust);await RejectAsync("cancelled transfer stops and keeps partial",async()=>await service.Download(v,p,null,cancel.Token));Check("cancel never produces executable",!File.Exists(Path.Combine(service.Downloads,p.Sha256+".exe")));
        }
        bool network=true;var gated=new GateStream(()=>network=false);q=new QueueTransport();q.Responses.Enqueue((o,e)=>new UpdateResponse{Status=200,ETag="\"one\"",Length=Bytes.Length,Body=gated});service=new UpdateService(Owned("gate-change"),()=>network,q,_trust);await RejectAsync("routing change interrupts update transfer",async()=>await service.Download(v,p,null,CancellationToken.None));Check("routing gate prevents next response read",gated.Reads==1);
        Reject("unsigned file cannot satisfy required Windows signature",()=>UpdateAuthenticode.Verify(typeof(UpdateTrust).Assembly.Location,new string('F',64)));
    }
    private static void Recovery()
    {
        string profile=Owned("recovery");File.WriteAllText(Path.Combine(profile,"data-version.txt"),"9");File.WriteAllText(Path.Combine(profile,"bookmarks.json"),"[{\"Title\":\"Owned\"}]");File.WriteAllText(Path.Combine(profile,"settings.json"),"{\"ThemeMode\":\"Dark\"}");File.WriteAllText(Path.Combine(profile,"Login Data"),"Synthetic account database; never copy");
        string snapshot=UpdateRecovery.Snapshot(profile,"0.6.0","0.6.1");Check("upgrade snapshot preserves data hash",UpdateTrust.HashFile(Path.Combine(snapshot,"bookmarks.json"))==UpdateTrust.HashFile(Path.Combine(profile,"bookmarks.json")));Check("snapshot includes integrity manifest",File.Exists(Path.Combine(snapshot,"snapshot.json")));Check("account database excluded from snapshot",!File.Exists(Path.Combine(snapshot,"Login Data")));
        using(var held=new FileStream(Path.Combine(profile,"profile.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))Reject("busy profile refuses update backup",()=>UpdateRecovery.Snapshot(profile,"0.6.0","0.6.1"));
        using(var held=new FileStream(Path.Combine(profile,"bookmarks.json"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))Reject("backup read failure stops update preparation",()=>UpdateRecovery.Snapshot(profile,"0.6.0","0.6.1"));
        File.WriteAllText(Path.Combine(profile,"data-version.txt"),"10");Reject("newer profile schema refuses rollback",()=>UpdateRecovery.Snapshot(profile,"0.6.0","0.5.7"));
        var v=Verify(Signed());string downloads=Path.Combine(profile,"Updates","Downloads");Directory.CreateDirectory(downloads);string package=Path.Combine(downloads,v.Manifest.Latest.Sha256+".exe");File.WriteAllBytes(package,Bytes);
        var plan=new UpdateInstallPlan{Envelope=v.Envelope,PackagePath=package,CurrentVersion="0.6.0",TargetVersion="0.6.1"};Check("newer install plan accepted",UpdateInstallPlan.Validate(plan,_trust,profile).Version=="0.6.1");
        plan.PackagePath=Path.Combine(_root,"outside.exe");Reject("installer outside managed cache rejected",()=>UpdateInstallPlan.Validate(plan,_trust,profile));plan.PackagePath=package;plan.CurrentVersion="0.6.2";Reject("downgrade cannot masquerade as update",()=>UpdateInstallPlan.Validate(plan,_trust,profile));
        plan.Rollback=true;plan.CurrentVersion="0.6.1";plan.TargetVersion="0.6.0";Check("explicit approved rollback accepted",UpdateInstallPlan.Validate(plan,_trust,profile).Version=="0.6.0");plan.CurrentVersion="0.6.2";Reject("rollback limited to approved source version",()=>UpdateInstallPlan.Validate(plan,_trust,profile));
        plan.Rollback=false;plan.CurrentVersion="0.6.0";plan.TargetVersion="0.6.1";string history=Path.Combine(profile,"Updates","history.json");UpdateState.Accept(history,Verify(Signed(m=>m.Sequence=4)),DateTime.UtcNow);Reject("superseded install plan refused at launch",()=>UpdateInstallPlan.Validate(plan,_trust,profile));
        using(var held=new FileStream(package,FileMode.Open,FileAccess.Read,FileShare.Read)){UpdateService.VerifyStream(held,v.Manifest.Latest);Reject("verified installer read lock prevents replacement",()=>{using(var write=File.OpenWrite(package)){};});}
    }
    private static async Task Transport()
    {
        Check("production transport accepts GitHub HTTPS",GitHubUpdateTransport.Allowed(new Uri("https://github.com/owner/repo")));
        foreach(var url in new[]{"http://github.com/a","https://github.com.evil.invalid/a","https://user@github.com/a","https://127.0.0.1/a","https://github.com:444/a"})Check("production transport rejects "+url,!GitHubUpdateTransport.Allowed(new Uri(url)));
        using(var server=new LocalHttp())
        {
            var transport=new GitHubUpdateTransport(()=>true,u=>u.Scheme=="http"&&u.Host=="127.0.0.1"&&u.Port==server.Port);
            using(var r=await transport.Open(new Uri(server.Url+"redirect"),0,null,CancellationToken.None))using(var reader=new StreamReader(r.Body)){Check("real HTTP redirect follows within policy",await reader.ReadToEndAsync()=="owned");}
            using(var r=await transport.Open(new Uri(server.Url+"range"),3,"\"native\"",CancellationToken.None)){Check("real HTTP Range and If-Range sent",r.Status==206&&r.ContentRange=="bytes 3-4/5");}
            await RejectAsync("real redirect to untrusted host refused",async()=>{using(var r=await transport.Open(new Uri(server.Url+"bad"),0,null,CancellationToken.None)){};});
        }
    }
    private sealed class LocalHttp : IDisposable
    {
        private readonly TcpListener _server=new TcpListener(IPAddress.Loopback,0);internal int Port;internal string Url;
        internal LocalHttp(){_server.Start();Port=((IPEndPoint)_server.LocalEndpoint).Port;Url="http://127.0.0.1:"+Port+"/";Task.Run(async()=>{try{while(true){var client=await _server.AcceptTcpClientAsync();Task.Run(()=>Handle(client));}}catch(ObjectDisposedException){}catch(SocketException){}});}
        private void Handle(TcpClient client)
        {
            using(client){var stream=client.GetStream();var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true);string first=reader.ReadLine(),line;var headers=new List<string>();while(!string.IsNullOrEmpty(line=reader.ReadLine()))headers.Add(line);string response;
            if(first.Contains("/redirect "))response="HTTP/1.1 302 Found\r\nLocation: /body\r\nContent-Length: 0\r\n\r\n";
            else if(first.Contains("/bad "))response="HTTP/1.1 302 Found\r\nLocation: https://refused.invalid/\r\nContent-Length: 0\r\n\r\n";
            else if(first.Contains("/range ")&&headers.Any(h=>h.Equals("Range: bytes=3-",StringComparison.OrdinalIgnoreCase))&&headers.Any(h=>h.Equals("If-Range: \"native\"",StringComparison.OrdinalIgnoreCase)))response="HTTP/1.1 206 Partial Content\r\nContent-Range: bytes 3-4/5\r\nETag: \"native\"\r\nContent-Length: 2\r\n\r\ned";
            else response="HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nowned";byte[] bytes=Encoding.ASCII.GetBytes(response);stream.Write(bytes,0,bytes.Length);}
        }
        public void Dispose(){_server.Stop();}
    }
    private static async Task NativeHandoff()
    {
        foreach(string scenario in new[]{"success","parent-mismatch","backup-failure","expires"})
        {
            string profile=Owned("native-"+scenario),parentPath=Path.Combine(profile,"HarborBrowser.exe");
            File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fixtures","HarborBrowser.exe"),parentPath);
            File.WriteAllText(Path.Combine(profile,"data-version.txt"),"9");File.WriteAllText(Path.Combine(profile,"bookmarks.json"),"[{\"Title\":\"Native fixture\"}]");
            byte[] installer=File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fixtures","Setup.exe"));
            var verified=Verify(Signed(m=>{m.Latest.Sha256=UpdateTrust.Hash(installer);m.Latest.Bytes=installer.Length;m.Previous=null;if(scenario=="expires")m.ExpiresUtc=DateTime.UtcNow.AddSeconds(3).ToString("o");}));
            string cache=Path.Combine(profile,"Updates","Downloads");Directory.CreateDirectory(cache);string package=Path.Combine(cache,verified.Manifest.Latest.Sha256+".exe");File.WriteAllBytes(package,installer);
            string launch=Path.Combine(profile,"Updates","Launch",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(launch);string planPath=Path.Combine(launch,"plan.json");
            using(var parent=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(parentPath,"\""+profile+"\""){UseShellExecute=false,CreateNoWindow=true}))
            {
                FileStream blocked=null;
                try
                {
                    for(int i=0;i<100&&!File.Exists(Path.Combine(profile,"parent-ready"));i++)await Task.Delay(30);
                    Check("native parent holds fixture profile "+scenario,File.Exists(Path.Combine(profile,"parent-ready"))&&!parent.HasExited);
                    var plan=new UpdateInstallPlan{ParentPid=parent.Id,ParentStartedUtcTicks=parent.StartTime.ToUniversalTime().Ticks+(scenario=="parent-mismatch"?1:0),AppPath=parentPath,CurrentVersion="0.6.0",TargetVersion="0.6.1",PackagePath=package,Envelope=verified.Envelope};
                    File.WriteAllText(planPath,Json.Serialize(plan));
                    if(scenario=="backup-failure")blocked=new FileStream(Path.Combine(profile,"bookmarks.json"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                    var run=Task.Run(()=>UpdateInstallRunner.Run(planPath,profile,_trust));
                    if(scenario=="parent-mismatch")
                    {
                        Check("mismatched parent rejected before handoff",await run!=0&&!File.Exists(Path.Combine(launch,"ready.json")));
                        Check("mismatched parent never launches setup",!File.Exists(Path.Combine(cache,"fixture-installed.txt")));continue;
                    }
                    for(int i=0;i<200&&!File.Exists(Path.Combine(launch,"ready.json"))&&!run.IsCompleted;i++)await Task.Delay(30);
                    Check("native launcher ready after verification "+scenario,File.Exists(Path.Combine(launch,"ready.json")));
                    Check("setup waits for parent exit "+scenario,!File.Exists(Path.Combine(cache,"fixture-installed.txt"))&&!parent.HasExited);
                    Reject("native handoff holds installer against writes "+scenario,()=>{using(var write=File.OpenWrite(package)){};});
                    if(scenario=="expires")await Task.Delay(3500);
                    File.WriteAllText(Path.Combine(profile,"exit-parent"),"exit owned fixture");
                    if(await Task.WhenAny(run,Task.Delay(10000))!=run)throw new TimeoutException("Owned handoff did not finish.");
                    if(scenario=="success")
                    {
                        Check("native setup stub launched after backup",await run==0&&File.Exists(Path.Combine(cache,"fixture-installed.txt")));
                        Check("native handoff creates complete integrity snapshot",Directory.GetFiles(Path.Combine(profile,"UpgradeBackups"),"snapshot.json",SearchOption.AllDirectories).Length==1);
                        Check("native handoff result records setup exit",File.ReadAllText(Path.Combine(launch,"result.json")).Contains("\"ExitCode\":0"));
                    }
                    else{Check(scenario+" prevents setup",await run!=0&&!File.Exists(Path.Combine(cache,"fixture-installed.txt")));Check(scenario+" result is visible for recovery",File.ReadAllText(Path.Combine(launch,"result.json")).Contains("\"Started\":false"));}
                }
                finally{blocked?.Dispose();if(!parent.HasExited){File.WriteAllText(Path.Combine(profile,"exit-parent"),"exit owned fixture");if(!parent.WaitForExit(3000))parent.Kill();}}
            }
        }
    }
    private static void Layout()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/HarborBrowser;component/Themes/Controls.xaml")});
        foreach(bool network in new[]{true,false})foreach(string mode in new[]{"Light","Dark"})
        {
            HarborBrowser.Services.AppearanceService.ApplyApplication(new HarborBrowser.Models.BrowserSettings{ThemeMode=mode});
            var service=new UpdateService(Owned("layout-"+network),()=>network,new QueueTransport(),_trust);var window=new UpdatesWindow(service,"0.6.0",CancellationToken.None);
            Check("update check button honors network policy "+network,window.CheckButton.IsEnabled==network);Check("installer button disabled before verified download "+network,!window.InstallButton.IsEnabled);
            var content=(System.Windows.Controls.ScrollViewer)window.Content;content.Background=(Brush)app.Resources["HarborWindow"];content.Measure(new Size(560,580));content.Arrange(new Rect(0,0,560,580));content.UpdateLayout();
            var bitmap=new RenderTargetBitmap(560,580,96,96,PixelFormats.Pbgra32);bitmap.Render(content);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(_root,"updates-"+network+"-"+mode+".png")))png.Save(output);
            Check("compact updater layout renders "+network,content.ActualWidth==560&&content.ActualHeight==580);
            window.Close();
        }
        app.Shutdown();
    }
}
