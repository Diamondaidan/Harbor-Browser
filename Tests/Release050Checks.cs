using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HarborBrowser.Services;

internal static partial class Program
{
    private static void Release050Checks()
    {
        Check("Windows protocol activation accepts one quoted HTTPS target",ExternalNavigationService.Targets(new[]{"--","https://example.test/path?q=hello%20world#part"}).Single()=="https://example.test/path?q=hello%20world#part");
        bool rejected=false;try{ExternalNavigationService.Targets(new[]{"--","https://safe.test/","--private"});}catch(ArgumentException){rejected=true;}Check("association launch rejects extra option-shaped arguments",rejected);
        foreach(string raw in new[]{"javascript:alert(1)","data:text/html,hello","harbor://downloads",@"\\server\share\page.html","https://test.invalid/\nhello","--private"})Check("external launch rejects "+raw.Replace('\n',' '),ExternalNavigationService.Target(raw)==null);
        string html=Path.Combine(root,"local document.html");File.WriteAllText(html,"<title>Local</title>");string url=ExternalNavigationService.Target(html);
        Check("local HTML path survives spaces and file URI forwarding",url!=null&&url==ExternalNavigationService.Target(url));
        Check("missing HTML files are refused",ExternalNavigationService.Target(Path.Combine(root,"missing.html"))==null);
        Check("non-HTML local files are refused",ExternalNavigationService.Target(FileBytes("launch.exe",1))==null);
        rejected=false;try{ExternalNavigationService.Targets(Enumerable.Repeat("https://example.test/",17));}catch(ArgumentException){rejected=true;}Check("external launch target count is bounded",rejected);
        Equal("Windows 10 opens general Default apps","ms-settings:defaultapps",ExternalNavigationService.DefaultAppsUri(new Version(10,0,19045)));
        Check("Windows 11 deep link identifies installed Harbor",ExternalNavigationService.DefaultAppsUri(new Version(10,0,22631)).Contains("registeredAppMachine=Harbor%20Browser"));
        var snapshot=VpnClientService.Parse("{\"Profiles\":[{\"Name\":\"Office VPN\",\"AllUsers\":false,\"Status\":\"Connected\"},{\"Name\":\"Office VPN\",\"AllUsers\":true,\"Status\":\"Disconnected\"}],\"Errors\":[]}");
        Check("same-name user and all-user VPN profiles remain distinct",snapshot.Profiles.Count==2&&snapshot.Profiles[0].Key!=snapshot.Profiles[1].Key);
        Check("VPN buttons derive from Windows state",snapshot.Profiles[0].CanDisconnect&&!snapshot.Profiles[0].CanConnect&&snapshot.Profiles[1].CanConnect);
        string disconnect=VpnClientService.DialArguments(snapshot.Profiles[0],false);
        Check("disconnect names one profile and its phonebook",disconnect.StartsWith("\"Office VPN\" /disconnect ")&&disconnect.Contains("/PHONEBOOK:")&&disconnect.Contains("AppData"));
        Check("all-user actions use the other phonebook",VpnClientService.DialArguments(snapshot.Profiles[1],false)!=disconnect);
        foreach(string name in new[]{"/disconnect","--option","quote\"name","new\nline"})Check("invalid VPN argument rejected "+name.Replace('\n',' '),!VpnClientService.ValidName(name));
        Check("VPN names with shell punctuation remain literal direct-process arguments",VpnClientService.ValidName("Office & Research $team"));
        var unknown=VpnClientService.Parse("{\"Profiles\":[{\"Name\":\"VPN\",\"Status\":\"Uncertain\"}],\"Errors\":[]}").Profiles.Single();Check("unknown VPN state enables neither action",!unknown.CanConnect&&!unknown.CanDisconnect);
        Check("credential error points to Windows without requesting secrets",VpnClientService.Failure(691).Contains("Windows VPN settings"));
        VpnCommandChecks().GetAwaiter().GetResult();
        string suffix=".unit."+Guid.NewGuid().ToString("N");
        using(var primary=new BrowserInstanceService(suffix))
        {
            var received=new TaskCompletionSource<string[]>();Task listener=Task.Run(()=>primary.Listen(values=>received.TrySetResult(values)));
            bool sent=Task.Run(()=>{using(var client=new BrowserInstanceService(suffix)){return !client.IsPrimary&&client.Forward(new[]{"https://ipc.test/"});}}).GetAwaiter().GetResult();
            Check("second process path forwards to current-user primary pipe",sent&&received.Task.Result.Single()=="https://ipc.test/");
        }
    }
    private static async Task VpnCommandChecks()
    {
        int commands=0;bool connected=false;
        var service=new VpnClientService((exe,args,timeout)=>
        {
            if(exe.EndsWith("rasdial.exe",StringComparison.OrdinalIgnoreCase)){commands++;connected=!args.Contains("/disconnect");return Task.FromResult(new CommandResult());}
            return Task.FromResult(new CommandResult{Output="{\"Profiles\":[{\"Name\":\"Test VPN\",\"Status\":\""+(connected?"Connected":"Disconnected")+"\"}],\"Errors\":[]}"});
        });
        var p=(await service.Snapshot()).Profiles.Single();Check("VPN connect reports observed state",(await service.Change(p,true)).Contains("connected")&&commands==1);
        await service.Change(p,true);Check("already-connected profile does not reconnect",commands==1);
        await service.Change(p,false);Check("targeted disconnect updates observed state",commands==2&&!connected);
        var missing=new VpnProfile{Name="Missing"};await service.Change(missing,true);Check("stale profile never launches a VPN command",commands==2);
        var failed=new VpnClientService((exe,args,time)=>Task.FromResult(new CommandResult{TimedOut=true}));Check("query timeout becomes unavailable status",(await failed.Snapshot()).Errors.Count==1);
    }
}
