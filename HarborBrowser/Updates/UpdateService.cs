using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HarborBrowser.Updates
{
    internal sealed class PartialUpdate { public string Url {get;set;} public string Sha256 {get;set;} public long Bytes {get;set;} public string ETag {get;set;} }
    internal sealed class UpdateProgress { internal long Done,Total; }
    internal sealed class UpdateService
    {
        internal readonly UpdateTrust Trust; internal readonly string Root,Downloads;
        private readonly IUpdateTransport _transport;private readonly Func<bool> _network;
        private readonly SemaphoreSlim _gate=new SemaphoreSlim(1);
        internal UpdateService(string profile,Func<bool> network,IUpdateTransport transport=null,UpdateTrust trust=null)
        {
            Trust=trust??UpdateTrustRoot.Create();Root=Path.Combine(profile,"Updates");Downloads=Path.Combine(Root,"Downloads");
            _network=network;_transport=transport??new GitHubUpdateTransport(network);
        }
        internal bool NetworkAllowed {get{return _network();}}
        internal VerifiedUpdate Cached()
        {
            string path=Path.Combine(Root,"release.json");if(!File.Exists(path))return null;
            if(new FileInfo(path).Length>131072)throw new InvalidDataException("Cached release details are too large.");
            var value=Trust.Verify(File.ReadAllText(path),DateTime.UtcNow);var state=UpdateState.Read(Path.Combine(Root,"history.json"));
            if(state.Sequence!=value.Manifest.Sequence||state.PayloadHash!=value.PayloadHash)throw new InvalidDataException("Cached release details do not match the last verified update.");
            return value;
        }
        internal async Task<VerifiedUpdate> Check(CancellationToken cancel)
        {
            await _gate.WaitAsync(cancel);
            try
            {
                RequireNetwork(cancel);UpdateState.SafeDirectory(Root);
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel))
                {
                    timeout.CancelAfter(20000);
                    using(var response=await _transport.Open(Trust.Feed,0,null,timeout.Token))
                    {
                        if(response.Status!=200||response.Length>131072||!Identity(response.Encoding))throw new InvalidDataException("The update record response is invalid.");
                        byte[] bytes;using(var memory=new MemoryStream()){await CopyBounded(response.Body,memory,131072,timeout.Token,null);bytes=memory.ToArray();}
                        var now=DateTime.UtcNow;var update=Trust.Verify(new UTF8Encoding(false,true).GetString(bytes),now);
                        RequireNetwork(cancel);UpdateState.Accept(Path.Combine(Root,"history.json"),update,now);UpdateState.Atomic(Path.Combine(Root,"release.json"),update.Envelope);return update;
                    }
                }
            }
            finally{_gate.Release();}
        }
        internal async Task<string> Download(VerifiedUpdate verified,UpdatePackage package,IProgress<UpdateProgress> progress,CancellationToken cancel)
        {
            await _gate.WaitAsync(cancel);
            try
            {
                var current=Trust.Verify(verified.Envelope,DateTime.UtcNow);
                var p=Select(current,package.Version);UpdateState.SafeDirectory(Downloads);
                string name=p.Sha256.ToUpperInvariant(),final=Path.Combine(Downloads,name+".exe"),part=Path.Combine(Downloads,name+".part"),meta=part+".json";
                foreach(string file in new[]{final,part,meta})if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("A redirected update file was refused.");
                if(File.Exists(final)){try{VerifyPackage(final,p);return final;}catch(InvalidDataException){File.Delete(final);throw;}}
                RequireNetwork(cancel);PartialUpdate saved=null;
                try{if(File.Exists(meta)&&new FileInfo(meta).Length<4096)saved=new JavaScriptSerializer().Deserialize<PartialUpdate>(File.ReadAllText(meta));}catch{}
                long offset=File.Exists(part)?new FileInfo(part).Length:0;
                if(saved==null||saved.Url!=p.Url||saved.Sha256!=p.Sha256||saved.Bytes!=p.Bytes||!Strong(saved.ETag)||offset<0||offset>p.Bytes){offset=0;}
                if(offset==p.Bytes)return Finish(part,final,meta,p);
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel))
                {
                    timeout.CancelAfter(TimeSpan.FromMinutes(10));
                    using(var response=await _transport.Open(new Uri(p.Url),offset,offset>0?saved.ETag:null,timeout.Token))
                    {
                        if(!Identity(response.Encoding))throw new InvalidDataException("Encoded update packages are refused.");
                        if(response.Status==200)offset=0;
                        else if(response.Status==206&&offset>0)
                        {
                            var range=Regex.Match(response.ContentRange??"","^bytes ([0-9]+)-([0-9]+)/([0-9]+)$");long start,end,total;
                            if(!range.Success||!long.TryParse(range.Groups[1].Value,out start)||!long.TryParse(range.Groups[2].Value,out end)||!long.TryParse(range.Groups[3].Value,out total)||start!=offset||end!=p.Bytes-1||total!=p.Bytes||response.ETag!=saved.ETag)throw new InvalidDataException("The resumed package range or identity did not match.");
                        }
                        else throw new InvalidDataException("Unexpected update package response.");
                        if(response.Length>=0&&response.Length!=p.Bytes-offset)throw new InvalidDataException("The package length did not match its signed release record.");
                        UpdateState.Atomic(meta,new JavaScriptSerializer().Serialize(new PartialUpdate{Url=p.Url,Sha256=p.Sha256,Bytes=p.Bytes,ETag=Strong(response.ETag)?response.ETag:null}));
                        using(var file=new FileStream(part,offset>0?FileMode.Open:FileMode.Create,FileAccess.Write,FileShare.None))
                        {
                            file.Position=offset;long baseOffset=offset;
                            try{await CopyBounded(response.Body,file,p.Bytes-offset,timeout.Token,n=>progress?.Report(new UpdateProgress{Done=baseOffset+n,Total=p.Bytes}));}
                            finally{file.Flush(true);}
                            if(file.Length!=p.Bytes)throw new IOException("The update download was interrupted. Retry to resume it.");
                        }
                    }
                }
                return Finish(part,final,meta,p);
            }
            finally{_gate.Release();}
        }
        internal static UpdatePackage Select(VerifiedUpdate v,string version)
        {
            if(v.Manifest.Latest.Version==version)return v.Manifest.Latest;
            if(v.Manifest.Previous!=null&&v.Manifest.Previous.Version==version)return v.Manifest.Previous;
            throw new InvalidDataException("This package is not in the signed release record.");
        }
        private async Task CopyBounded(Stream input,Stream output,long limit,CancellationToken cancel,Action<long> progress)
        {
            byte[] buffer=new byte[65536];long count=0;var clock=System.Diagnostics.Stopwatch.StartNew();long last=0;
            while(true){RequireNetwork(cancel);int read=await input.ReadAsync(buffer,0,buffer.Length,cancel);if(read==0)break;if(read>limit-count)throw new InvalidDataException("The update response exceeds its expected size.");await output.WriteAsync(buffer,0,read,cancel);count+=read;if(clock.ElapsedMilliseconds-last>=150){progress?.Invoke(count);last=clock.ElapsedMilliseconds;}}
            progress?.Invoke(count);
        }
        private void RequireNetwork(CancellationToken cancel){cancel.ThrowIfCancellationRequested();if(!_network())throw new InvalidOperationException("Updates are paused while a browser connection is enabled.");}
        private static bool Strong(string etag){return etag!=null&&Regex.IsMatch(etag,"^\"[\\x21\\x23-\\x7e]{0,254}\"$");}
        private static bool Identity(string coding){return string.IsNullOrEmpty(coding)||coding.Equals("identity",StringComparison.OrdinalIgnoreCase);}
        internal static void VerifyPackage(string path,UpdatePackage p)
        {
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))VerifyStream(file,p);
            if(!string.IsNullOrEmpty(p.CertificateSha256))UpdateAuthenticode.Verify(path,p.CertificateSha256);
        }
        private static string Finish(string part,string final,string metadata,UpdatePackage package)
        {
            try{VerifyPackage(part,package);}catch(InvalidDataException){File.Delete(part);File.Delete(metadata);throw;}
            File.Move(part,final);return final;
        }
        internal string LastAttempt()
        {
            string launch=Path.Combine(Root,"Launch");if(!Directory.Exists(launch))return null;
            var folders=new DirectoryInfo(launch).GetDirectories();Array.Sort(folders,(a,b)=>b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            foreach(var folder in folders)
            {
                string result=Path.Combine(folder.FullName,"result.json");if(!File.Exists(result))continue;UpdateState.SafeDirectory(folder.FullName);
                if((File.GetAttributes(result)&FileAttributes.ReparsePoint)!=0||new FileInfo(result).Length>4096)continue;
                try{var values=new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(result));object started,code;if(values.TryGetValue("Started",out started)&&Equals(started,false))return "The previous update attempt did not open setup. Your installed browser was kept.";if(values.TryGetValue("ExitCode",out code))return "Previous setup returned code "+Convert.ToInt32(code)+". The installed version is listed above.";}catch{}
            }
            return null;
        }
        internal static void VerifyStream(Stream file,UpdatePackage p){if(file.Length!=p.Bytes||!UpdateTrust.Hash(file).Equals(p.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The installer hash did not match its signed release record. It was not opened.");}
    }
}
