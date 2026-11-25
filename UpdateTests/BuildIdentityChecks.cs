using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Updates;
using HarborBrowser.Views;

internal static partial class Program
{
    private static void BuildIdentityChecks()
    {
        var stable=BuildIdentity.Parse("0.7.1");var rc=BuildIdentity.Parse("0.7.1-rc.1");
        Check("stable build has a plain version and release channel",!stable.IsCandidate&&stable.Channel=="release"&&stable.DisplayVersion=="0.7.1");
        Check("candidate build retains its RC identity",rc.IsCandidate&&rc.Candidate==1&&rc.DisplayVersion=="0.7.1 RC1"&&rc.InformationalVersion=="0.7.1-rc.1");
        Check("later RC identity roundtrips",BuildIdentity.Parse("0.7.1-rc.12").Channel=="rc12");
        foreach(var bad in new[]{null,"","v0.7.1","00.7.1","0.7.1.0","0.7.1-beta","0.7.1-rc.0","0.7.1-rc.01","0.7.1-rc.-1","0.7.1-rc.2147483648","0.7.1-rc.1 extra"})Reject("invalid build identity rejected "+bad,()=>BuildIdentity.Parse(bad));
        Check("candidate may update to full release of the same version",rc.CanUpdateTo("0.7.1"));
        Check("stable does not offer same-version reinstall",!stable.CanUpdateTo("0.7.1"));
        Check("candidate never offers an older stable version",!rc.CanUpdateTo("0.7.0"));
        Check("both channels offer a newer stable version",rc.CanUpdateTo("0.7.2")&&stable.CanUpdateTo("0.7.2"));
        foreach(var path in new[]{null,"HarborBrowser.exe",@"C:HarborBrowser.exe",@"\\server\share\HarborBrowser.exe",@"\\?\C:\HarborBrowser.exe"})Reject("nonlocal browser identity path refused "+path,()=>BuildIdentity.FromFile(path));
        string fixtures=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fixtures");
        string candidate=Path.Combine(fixtures,"CandidateIdentity.exe"),full=Path.Combine(fixtures,"StableIdentity.exe");
        Check("RC identity is read from executable metadata",BuildIdentity.FromFile(candidate).IsCandidate);
        Check("stable identity is read from executable metadata",!BuildIdentity.FromFile(full).IsCandidate);
        Check("compiled browser and launcher carry matching build identity",BuildIdentity.FromFile(typeof(BuildIdentity).Assembly.Location).InformationalVersion==BuildIdentity.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"HarborUpdateLauncher.exe")).InformationalVersion);
        string profile=Owned("candidate-plan"),downloads=Path.Combine(profile,"Updates","Downloads");Directory.CreateDirectory(downloads);
        var verified=Verify(Signed(m=>{m.Latest=Package("0.7.1");m.Previous=Package("0.7.0");m.RollbackFrom="0.7.1";}));
        string package=Path.Combine(downloads,verified.Manifest.Latest.Sha256+".exe");File.WriteAllBytes(package,Bytes);
        var plan=new UpdateInstallPlan{Envelope=verified.Envelope,AppPath=candidate,CurrentVersion="0.7.1",TargetVersion="0.7.1",PackagePath=package};
        Check("handoff accepts an actual same-version RC executable",UpdateInstallPlan.Validate(plan,_trust,profile).Version=="0.7.1");
        plan.AppPath=full;Reject("handoff refuses a stable same-version executable",()=>UpdateInstallPlan.Validate(plan,_trust,profile));
        plan.AppPath=candidate;plan.CurrentVersion="0.7.2";plan.TargetVersion="0.7.2";Reject("candidate cannot invent an unsigned target",()=>UpdateInstallPlan.Validate(plan,_trust,profile));
        plan.CurrentVersion="0.7.1";plan.TargetVersion="0.7.0";Reject("candidate cannot silently downgrade",()=>UpdateInstallPlan.Validate(plan,_trust,profile));
        plan.TargetVersion="0.7.1";plan.AppPath=null;Reject("same-version update requires actual executable identity",()=>UpdateInstallPlan.Validate(plan,_trust,profile));
    }
    private static void IdentityLayout()
    {
        foreach(bool candidate in new[]{true,false})foreach(bool readOnly in new[]{true,false})
        {
            var queue=new QueueTransport();queue.Responses.Enqueue((o,e)=>Reply(Encoding.UTF8.GetBytes(Signed(m=>{m.Latest=Package("0.7.1");m.Previous=Package("0.7.0");m.RollbackFrom="0.7.1";}))));
            var service=new UpdateService(Owned("identity-ui-"+candidate+"-"+readOnly),()=>true,queue,_trust);
            var window=new UpdatesWindow(service,"0.7.1",CancellationToken.None,readOnly,candidate?"0.7.1-rc.1":"0.7.1");
            window.CheckButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("same-version full release button respects channel and profile schema "+candidate+"/"+readOnly,window.DownloadButton.IsEnabled==(candidate&&!readOnly));
            Check("same-version release status describes the installed channel "+candidate+"/"+readOnly,window.Status.Text.Contains(candidate?"replace this test build":"latest published Harbor release"));
            var stack=(StackPanel)((ScrollViewer)window.Content).Content;
            Check("updater displays installed RC label "+candidate+"/"+readOnly,stack.Children.OfType<TextBlock>().Any(t=>t.Text=="Installed: Harbor 0.7.1"+(candidate?" RC1":"")));
            window.Close();
        }
        var blocked=new UpdatesWindow(new UpdateService(Owned("candidate-paused"),()=>false,new QueueTransport(),_trust),"0.7.1",CancellationToken.None,false,"0.7.1-rc.1");
        Check("candidate updater retains VPN network gate",!blocked.CheckButton.IsEnabled&&!blocked.DownloadButton.IsEnabled);blocked.Close();
    }
}
