using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HarborBrowser.Services
{
    public sealed class VpnProfile
    {
        public string Name {get;set;}
        public bool AllUsers {get;set;}
        public string Status {get;set;}
        public string Tunnel {get;set;}
        public bool SplitTunnel {get;set;}
        public string Key {get{return (AllUsers?"all:":"user:")+Name;}}
        public string Display {get{return Name+"  ·  "+Status+"  ·  "+(AllUsers?"All users":"Current user");}}
        public bool CanConnect {get{return Status=="Disconnected";}}
        public bool CanDisconnect {get{return Status=="Connected"||Status=="Connecting";}}
    }
    public sealed class VpnSnapshot
    {
        public List<VpnProfile> Profiles {get;set;}=new List<VpnProfile>();
        public List<string> Errors {get;set;}=new List<string>();
    }
    public sealed class CommandResult
    {
        public int ExitCode {get;set;}
        public bool TimedOut {get;set;}
        public string Output {get;set;}
        public string Error {get;set;}
    }
    public static class WindowsCommandRunner
    {
        public static string Quote(string value)
        {
            var text=new StringBuilder("\"");int slashes=0;
            foreach(char c in value){if(c=='\\'){slashes++;continue;}text.Append('\\',c=='"'?slashes*2+1:slashes);text.Append(c);slashes=0;}
            text.Append('\\',slashes*2);return text.Append('"').ToString();
        }
        public static async Task<CommandResult> Run(string executable,string arguments,int milliseconds)
        {
            using(var process=new Process{StartInfo=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8},EnableRaisingEvents=true})
            {
                var exited=new TaskCompletionSource<bool>();process.Exited+=(s,e)=>exited.TrySetResult(true);
                if(!process.Start())throw new InvalidOperationException("Windows could not start the requested helper.");
                var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
                if(await Task.WhenAny(exited.Task,Task.Delay(milliseconds))!=exited.Task)
                {try{process.Kill();}catch{}return new CommandResult{TimedOut=true,ExitCode=-1,Output="",Error="The Windows command timed out."};}
                await exited.Task;return new CommandResult{ExitCode=process.ExitCode,Output=await output,Error=await error};
            }
        }
    }
    public sealed class VpnClientService
    {
        private readonly Func<string,string,int,Task<CommandResult>> _run;
        public VpnClientService(Func<string,string,int,Task<CommandResult>> runner=null){_run=runner??WindowsCommandRunner.Run;}
        private const string Query=@"
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false)
$profiles=@();$problems=@()
foreach($all in @($false,$true)){
 try{
  foreach($p in @(Get-VpnConnection -AllUserConnection:$all -ErrorAction Stop)){
   $profiles+=@{Name=[string]$p.Name;AllUsers=$all;Status=[string]$p.ConnectionStatus;Tunnel=[string]$p.TunnelType;SplitTunnel=[bool]$p.SplitTunneling}
  }
 }catch{$problems+=('Could not read '+$(if($all){'all-user'}else{'current-user'})+' VPN profiles. '+$_.Exception.Message)}
}
@{Profiles=@($profiles);Errors=@($problems)}|ConvertTo-Json -Depth 4 -Compress
";
        public async Task<VpnSnapshot> Snapshot()
        {
            try
            {
                string executable=Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe");
                var result=await _run(executable,"-NoLogo -NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(Query)),20000);
                if(result.TimedOut||result.ExitCode!=0)return new VpnSnapshot{Errors=new List<string>{"Windows VPN status is unavailable. Open Windows VPN settings or try Refresh."}};
                return Parse(result.Output);
            }
            catch(Exception ex){return new VpnSnapshot{Errors=new List<string>{"Could not read Windows VPN profiles. "+ex.Message}};}
        }
        public static VpnSnapshot Parse(string text)
        {
            if(text==null||text.Length>1024*1024)throw new InvalidDataException("The Windows VPN profile response is too large or missing.");
            var value=new JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<VpnSnapshot>(text);
            if(value==null||value.Profiles==null||value.Errors==null)throw new InvalidDataException("Windows returned an invalid VPN status response.");
            foreach(var p in value.Profiles){if(!ValidName(p.Name))throw new InvalidDataException("Windows returned an unsupported VPN profile name.");if(p.Status!="Connected"&&p.Status!="Disconnected"&&p.Status!="Connecting")p.Status="Unknown";}
            value.Profiles=value.Profiles.GroupBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(p=>p.AllUsers).ToList();return value;
        }
        public static bool ValidName(string name){return !string.IsNullOrWhiteSpace(name)&&name.Length<=256&&name[0]!='/'&&name[0]!='-'&&!name.Any(c=>char.IsControl(c)||c=='"');}
        public static string DialArguments(VpnProfile profile,bool connect)
        {
            if(profile==null||!ValidName(profile.Name))throw new ArgumentException("Choose a valid Windows VPN profile.");
            string root=Environment.GetFolderPath(profile.AllUsers?Environment.SpecialFolder.CommonApplicationData:Environment.SpecialFolder.ApplicationData);
            string phonebook=Path.Combine(root,@"Microsoft\Network\Connections\Pbk\rasphone.pbk");
            return WindowsCommandRunner.Quote(profile.Name)+" "+(connect?"":"/disconnect ")+"/PHONEBOOK:"+WindowsCommandRunner.Quote(phonebook);
        }
        public async Task<string> Change(VpnProfile selected,bool connect)
        {
            if(selected==null)throw new ArgumentException("Choose a Windows VPN profile first.");
            var latest=await Snapshot();var profile=latest.Profiles.FirstOrDefault(p=>p.Key.Equals(selected.Key,StringComparison.OrdinalIgnoreCase));
            if(profile==null)return "This profile is no longer available, or Windows could not read its status. Refresh the list or open Windows VPN settings.";
            if(connect&&!profile.CanConnect||!connect&&!profile.CanDisconnect)return "The selected profile is "+profile.Status.ToLowerInvariant()+". Refresh its Windows status before changing it.";
            CommandResult result;
            try{result=await _run(Path.Combine(Environment.SystemDirectory,"rasdial.exe"),DialArguments(profile,connect),45000);}
            catch(Exception ex){return "Windows could not change this VPN connection. "+ex.Message;}
            if(result.TimedOut)return "The command timed out. Windows may still be connecting; refresh its status or open Windows VPN settings.";
            if(result.ExitCode!=0)return Failure(result.ExitCode);
            var after=await Snapshot();var state=after.Profiles.FirstOrDefault(p=>p.Key.Equals(profile.Key,StringComparison.OrdinalIgnoreCase));
            return state==null?"Windows completed the command, but the current connection status is unavailable. Refresh to check.":"Windows reports "+profile.Name+" is "+state.Status.ToLowerInvariant()+".";
        }
        public static string Failure(int code)
        {
            string help;
            switch(code){case 691:help="Windows rejected the sign-in details. Update the saved credentials in Windows VPN settings.";break;case 623:help="The selected Windows VPN profile was not found. Refresh or recreate it in Windows settings.";break;case 800:case 809:help="The VPN server could not be reached. Check the network, server and VPN settings.";break;case 868:help="Windows could not resolve the VPN server name. Check the server address and network.";break;case 720:help="Windows could not establish the VPN protocol. Check the connection's Windows settings.";break;default:help="Windows could not complete the VPN command. Open Windows VPN settings for details.";break;}
            return help+" (Windows error "+code+".)";
        }
    }
    public static class VpnService
    {
        public static void OpenWindowsVpnSettings(){Process.Start(new ProcessStartInfo("ms-settings:network-vpn"){UseShellExecute=true});}
    }
}
