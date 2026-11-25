using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HarborBrowser.Updates;

internal static partial class Program
{
    private sealed class InlineProgress:IProgress<UpdateProgress>
    {
        internal Action<UpdateProgress> Action;
        public void Report(UpdateProgress value){Action(value);}
    }
    private sealed class ObservedTransport:IUpdateTransport
    {
        private readonly GitHubUpdateTransport _inner=new GitHubUpdateTransport(()=>true);
        internal readonly List<object> Requests=new List<object>();internal long ResumeOffset;internal int ResumeStatus;
        public async Task<UpdateResponse> Open(Uri url,long offset,string etag,CancellationToken cancel)
        {
            var response=await _inner.Open(url,offset,etag,cancel);
            Requests.Add(new{Url=url.GetLeftPart(UriPartial.Path),Offset=offset,Status=response.Status,HasStrongValidator=!string.IsNullOrEmpty(response.ETag)&&!response.ETag.StartsWith("W/")});
            if(offset>0){ResumeOffset=offset;ResumeStatus=response.Status;}
            return response;
        }
    }
    private static async Task<int> PublicUpdater(string output)
    {
        _root=Path.GetFullPath(output);if(Directory.Exists(_root))throw new IOException("Use a new owned result directory.");Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root,".owned-public-updater"),"Public download only; no installer execution or user profile.");
        var transport=new ObservedTransport();var service=new UpdateService(Owned("profile"),()=>true,transport);
        var release=await service.Check(CancellationToken.None);var package=release.Manifest.Latest;
        Check("public GitHub metadata verifies against production release key",release.Manifest.Sequence>0);
        Check("an older stable install can accept the public stable release",BuildIdentity.Parse("0.6.3").CanUpdateTo(package.Version));
        string part=Path.Combine(service.Downloads,package.Sha256.ToUpperInvariant()+".part");
        using(var pause=new CancellationTokenSource())
        {
            var progress=new InlineProgress{Action=p=>{if(p.Done>=16*1024*1024&&p.Done<p.Total)pause.Cancel();}};
            await RejectAsync("real public download pauses without producing an executable",async()=>await service.Download(release,package,progress,pause.Token));
        }
        long partial=new FileInfo(part).Length;Check("public download leaves a bounded partial file",partial>0&&partial<package.Bytes);
        await Task.Delay(500);Check("paused public transfer stays unchanged",new FileInfo(part).Length==partial);
        service=new UpdateService(Path.GetDirectoryName(service.Root),()=>true,transport);
        Check("public release cache survives updater recreation",service.Cached().PayloadHash==release.PayloadHash);
        string file=await service.Download(release,package,null,CancellationToken.None);
        Check("recreated updater requests the exact public resume offset",transport.ResumeOffset==partial);
        Check("public host supports HTTP range resumption",transport.ResumeStatus==206);
        UpdateService.VerifyPackage(file,package);Check("resumed public installer matches signed size and SHA256",new FileInfo(file).Length==package.Bytes&&UpdateTrust.HashFile(file).Equals(package.Sha256,StringComparison.OrdinalIgnoreCase));
        File.WriteAllText(Path.Combine(_root,"PUBLIC-UPDATER-RESULTS.json"),Json.Serialize(new{Passed=true,Version=package.Version,Sequence=release.Manifest.Sequence,InstallerBytes=package.Bytes,InstallerSHA256=package.Sha256,PartialBytes=partial,Requests=transport.Requests,InstallerExecuted=false,RealBrowserProfileAccessed=false,WindowsNetworkSettingsChanged=false,RecordedUtc=DateTime.UtcNow.ToString("o")}));
        Console.WriteLine("ALL PASSED: "+_checks+" public updater checks; download only, installer never executed.");return 0;
    }
}
